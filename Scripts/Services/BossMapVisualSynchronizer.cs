using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Tomorin_Mod.Services;

/// <summary>
/// 将当前 Act 的权威 Boss 状态投影到已经创建的第一、第二 Boss 地图节点。
/// </summary>
internal static class BossMapVisualSynchronizer
{
    private static readonly Lazy<ReflectionContract> Contract = new(CreateReflectionContract);
    private static readonly object LoggedFailureSync = new();
    private static readonly HashSet<string> LoggedFailures = new(StringComparer.Ordinal);

    /// <summary>
    /// 尝试刷新当前地图的第一、第二 Boss 表现；UI 尚未创建时安全返回，UI 错误只记录日志。
    /// </summary>
    /// <param name="runState">提供当前 Act、Map 与 Boss 身份的权威运行状态。</param>
    internal static void RefreshCurrentBossVisuals(IRunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);

        try
        {
            RefreshCurrentBossVisualsCore(runState, Contract.Value);
        }
        catch (Exception exception)
        {
            LogRefreshFailureOnce(runState, exception);
        }
    }

    /// <summary>
    /// 在反射契约已经验证后执行实际地图节点重新绑定。
    /// </summary>
    /// <param name="runState">权威运行状态。</param>
    /// <param name="contract">当前游戏版本验证通过的反射字段集合。</param>
    private static void RefreshCurrentBossVisualsCore(IRunState runState, ReflectionContract contract)
    {
        NMapScreen? mapScreen = NMapScreen.Instance;
        if (mapScreen == null)
        {
            return;
        }

        RunState? screenRunState = ReadReference<RunState>(contract.MapRunStateField, mapScreen);
        ActMap? screenMap = ReadReference<ActMap>(contract.MapField, mapScreen);
        if (!ReferenceEquals(screenRunState, runState) || !ReferenceEquals(screenMap, runState.Map))
        {
            return;
        }

        NBossMapPoint? primaryNode = ReadReference<NBossMapPoint>(contract.BossPointField, mapScreen);
        if (primaryNode == null || !ReferenceEquals(primaryNode.Point, runState.Map.BossMapPoint))
        {
            return;
        }

        EncounterModel primaryBoss = runState.Act.BossEncounter ?? throw new InvalidOperationException(
            $"章节 {runState.Act.Id} 缺少第一 Boss，无法刷新地图节点。");
        RebindBossPoint(primaryNode, runState.Act, primaryBoss, contract);

        NBossMapPoint? secondNode = ReadReference<NBossMapPoint>(contract.SecondBossPointField, mapScreen);
        EncounterModel? secondBoss = runState.Act.SecondBossEncounter;
        MapPoint? secondMapPoint = runState.Map.SecondBossMapPoint;
        if (secondNode != null &&
            secondBoss != null &&
            secondMapPoint != null &&
            ReferenceEquals(secondNode.Point, secondMapPoint))
        {
            RebindBossPoint(secondNode, runState.Act, secondBoss, contract);
        }
    }

    /// <summary>
    /// 将一个 Boss Encounter 重新绑定到指定 Boss 地图节点，并复用原生即时视觉刷新。
    /// </summary>
    /// <param name="node">准备更新的第一或第二 Boss 节点。</param>
    /// <param name="act">节点应当引用的当前章节。</param>
    /// <param name="encounter">节点应当显示的权威 Boss Encounter。</param>
    /// <param name="contract">反射字段集合。</param>
    private static void RebindBossPoint(
        NBossMapPoint node,
        ActModel act,
        EncounterModel encounter,
        ReflectionContract contract)
    {
        contract.BossPointActField.SetValue(node, act);

        Node2D? spriteContainer = ReadReference<Node2D>(contract.SpriteContainerField, node);
        if (spriteContainer != null)
        {
            spriteContainer.Visible = true;
        }

        MegaSkeletonDataResource? spineResource = encounter.BossNodeSpineResource;
        if (spineResource == null)
        {
            BindPng(node, encounter, contract);
        }
        else
        {
            BindSpine(node, spineResource, contract);
        }

        node.RefreshVisualsInstantly();
    }

    /// <summary>
    /// 绑定普通 PNG 正文和描边，并隐藏可能残留的 Spine 表现。
    /// </summary>
    /// <param name="node">准备绑定普通贴图的 Boss 节点。</param>
    /// <param name="encounter">提供 BossNodePath 的 Encounter。</param>
    /// <param name="contract">反射字段集合。</param>
    private static void BindPng(
        NBossMapPoint node,
        EncounterModel encounter,
        ReflectionContract contract)
    {
        string? bossNodePath = encounter.BossNodePath;
        if (string.IsNullOrWhiteSpace(bossNodePath))
        {
            throw new InvalidOperationException($"Boss Encounter {encounter.Id} 没有 BossNodePath。");
        }

        contract.UsesSpineField.SetValue(node, false);
        Node2D? spineSprite = ReadReference<Node2D>(contract.SpineSpriteField, node);
        if (spineSprite != null)
        {
            spineSprite.Visible = false;
        }

        TextureRect image = ReadReference<TextureRect>(contract.PlaceholderImageField, node)
            ?? node.GetNode<TextureRect>("%PlaceholderImage");
        TextureRect outline = ReadReference<TextureRect>(contract.PlaceholderOutlineField, node)
            ?? node.GetNode<TextureRect>("%PlaceholderOutline");
        contract.PlaceholderImageField.SetValue(node, image);
        contract.PlaceholderOutlineField.SetValue(node, outline);

        image.Texture = PreloadManager.Cache.GetTexture2D(bossNodePath + ".png");
        outline.Texture = PreloadManager.Cache.GetTexture2D(bossNodePath + "_outline.png");
        image.Visible = true;
        outline.Visible = true;
    }

    /// <summary>
    /// 绑定 Spine 骨骼和原版默认动画，并隐藏可能残留的 PNG 表现。
    /// </summary>
    /// <param name="node">准备绑定 Spine 的 Boss 节点。</param>
    /// <param name="spineResource">目标 Boss 的 Spine 骨骼资源。</param>
    /// <param name="contract">反射字段集合。</param>
    private static void BindSpine(
        NBossMapPoint node,
        MegaSkeletonDataResource spineResource,
        ReflectionContract contract)
    {
        contract.UsesSpineField.SetValue(node, true);

        TextureRect? image = ReadReference<TextureRect>(contract.PlaceholderImageField, node);
        TextureRect? outline = ReadReference<TextureRect>(contract.PlaceholderOutlineField, node);
        if (image != null)
        {
            image.Visible = false;
        }

        if (outline != null)
        {
            outline.Visible = false;
        }

        Node2D spineSprite = ReadReference<Node2D>(contract.SpineSpriteField, node)
            ?? throw new InvalidOperationException("NBossMapPoint 尚未完成初始化：_spineSprite 为空。");
        MegaSprite animationController = ReadReference<MegaSprite>(contract.AnimationControllerField, node)
            ?? throw new InvalidOperationException("NBossMapPoint 尚未完成初始化：_animController 为空。");

        spineSprite.Visible = true;
        animationController.SetSkeletonDataRes(spineResource);
        animationController.GetAnimationState().SetAnimation("animation", true, 0);

        ShaderMaterial material = animationController.GetNormalMaterial() as ShaderMaterial
            ?? throw new InvalidOperationException("Boss Spine 的普通材质不是 ShaderMaterial。");
        contract.MaterialField.SetValue(node, material);
    }

    /// <summary>
    /// 创建并验证当前游戏版本所需的全部私有字段反射契约。
    /// </summary>
    /// <returns>字段名称和类型均匹配的不可变契约。</returns>
    private static ReflectionContract CreateReflectionContract()
    {
        return new ReflectionContract(
            RequireField(typeof(NMapScreen), "_runState", typeof(RunState)),
            RequireField(typeof(NMapScreen), "_map", typeof(ActMap)),
            RequireField(typeof(NMapScreen), "_bossPointNode", typeof(NBossMapPoint)),
            RequireField(typeof(NMapScreen), "_secondBossPointNode", typeof(NBossMapPoint)),
            RequireField(typeof(NBossMapPoint), "_act", typeof(ActModel)),
            RequireField(typeof(NBossMapPoint), "_usesSpine", typeof(bool)),
            RequireField(typeof(NBossMapPoint), "_spriteContainer", typeof(Node2D)),
            RequireField(typeof(NBossMapPoint), "_spineSprite", typeof(Node2D)),
            RequireField(typeof(NBossMapPoint), "_animController", typeof(MegaSprite)),
            RequireField(typeof(NBossMapPoint), "_material", typeof(ShaderMaterial)),
            RequireField(typeof(NBossMapPoint), "_placeholderImage", typeof(TextureRect)),
            RequireField(typeof(NBossMapPoint), "_placeholderOutline", typeof(TextureRect)));
    }

    /// <summary>
    /// 查找一个私有字段并验证其运行时类型，避免游戏升级后静默写入错误对象。
    /// </summary>
    /// <param name="declaringType">声明字段的游戏类型。</param>
    /// <param name="fieldName">版本敏感的私有字段名。</param>
    /// <param name="expectedFieldType">当前版本预期的精确字段类型。</param>
    /// <returns>通过名称和类型验证的字段。</returns>
    private static FieldInfo RequireField(Type declaringType, string fieldName, Type expectedFieldType)
    {
        FieldInfo? field = AccessTools.Field(declaringType, fieldName);
        if (field == null)
        {
            throw new MissingFieldException(declaringType.FullName, fieldName);
        }

        if (field.FieldType != expectedFieldType)
        {
            throw new InvalidOperationException(
                $"字段 {declaringType.FullName}.{fieldName} 类型发生变化：" +
                $"预期 {expectedFieldType.FullName}，实际 {field.FieldType.FullName}。");
        }

        return field;
    }

    /// <summary>
    /// 从反射字段读取指定引用类型，字段为空时返回空值。
    /// </summary>
    /// <typeparam name="T">预期的引用类型。</typeparam>
    /// <param name="field">已经验证的字段。</param>
    /// <param name="instance">字段所属对象。</param>
    /// <returns>转换成功的引用或空值。</returns>
    private static T? ReadReference<T>(FieldInfo field, object instance) where T : class
    {
        return field.GetValue(instance) as T;
    }

    /// <summary>
    /// 按错误与 Boss 身份去重记录 UI 失败，避免地图反复打开时刷屏。
    /// </summary>
    /// <param name="runState">提供章节和 Boss 上下文的运行状态。</param>
    /// <param name="exception">视觉刷新期间捕获的异常。</param>
    private static void LogRefreshFailureOnce(IRunState runState, Exception exception)
    {
        string bossId = runState.Act.BossEncounter?.Id.ToString() ?? "<missing>";
        string failureKey = $"{bossId}|{exception.GetType().FullName}|{exception.Message}";
        lock (LoggedFailureSync)
        {
            if (!LoggedFailures.Add(failureKey))
            {
                return;
            }
        }

        Log.Error(
            $"[FateGuidance] Boss 地图视觉刷新失败；Act={runState.Act.Id}，Boss={bossId}，" +
            $"阶段=共享事件结算，错误={exception}");
    }

    /// <summary>
    /// 保存当前游戏版本中地图屏幕和 Boss 节点的私有字段契约。
    /// </summary>
    private sealed record ReflectionContract(
        FieldInfo MapRunStateField,
        FieldInfo MapField,
        FieldInfo BossPointField,
        FieldInfo SecondBossPointField,
        FieldInfo BossPointActField,
        FieldInfo UsesSpineField,
        FieldInfo SpriteContainerField,
        FieldInfo SpineSpriteField,
        FieldInfo AnimationControllerField,
        FieldInfo MaterialField,
        FieldInfo PlaceholderImageField,
        FieldInfo PlaceholderOutlineField);
}
