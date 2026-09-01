using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using STS2_Tomorin_Mod.Enemy.CardIntents.Presentation;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.View;

/// <summary>
/// 显示一张敌人缩略牌及其独立原版 Intent 行，并允许父视图按实例键增量复用。
/// </summary>
public partial class NEnemyIntentCardSlot : VBoxContainer
{
    private const float ThumbnailScale = 0.24f;
    private const float ThumbnailWidth = 96f;
    private const float ThumbnailHeight = 136f;
    private const float CardEffectSpacing = 8f;
    private const float EffectRowHeight = 72f;

    private readonly Control _thumbnailHost;
    private readonly HBoxContainer _effectRow;
    private readonly List<EffectNodeBinding> _effectNodes = [];
    private NCard? _thumbnail;
    private CardModel? _cardModel;
    private string _descriptionOverride = string.Empty;
    private Creature? _owner;
    private Creature[] _targets = [];
    private Action<string, Exception?>? _diagnosticSink;

    /// <summary>
    /// 创建不接收输入、具有固定缩略牌尺寸和逐牌效果行的动态槽位。
    /// </summary>
    public NEnemyIntentCardSlot()
    {
        Name = nameof(NEnemyIntentCardSlot);
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(ThumbnailWidth, ThumbnailHeight + CardEffectSpacing + EffectRowHeight);
        AddThemeConstantOverride("separation", (int)CardEffectSpacing);

        _thumbnailHost = new Control
        {
            Name = "ThumbnailHost",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(ThumbnailWidth, ThumbnailHeight)
        };
        AddChild(_thumbnailHost);

        _effectRow = new HBoxContainer
        {
            Name = "EffectRow",
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            Alignment = AlignmentMode.Center,
            CustomMinimumSize = new Vector2(ThumbnailWidth, EffectRowHeight)
        };
        _effectRow.AddThemeConstantOverride("separation", 4);
        AddChild(_effectRow);
    }

    /// <summary>获取当前槽位绑定的稳定实例键。</summary>
    public EnemyIntentDisplayKey DisplayKey { get; private set; } = null!;

    /// <summary>获取当前槽位绑定的领域卡牌，供共享 Hover 预览换绑。</summary>
    public CardModel CardModel => _cardModel ?? throw new InvalidOperationException("敌人 Intent 卡槽尚未绑定卡面。");

    public string DescriptionOverride => _descriptionOverride;

    /// <summary>
    /// 绑定一张逐牌展示；相同实例键只在效果结构变化时重建该槽的原版 Intent 节点。
    /// </summary>
    /// <param name="presentation">一张公开卡牌的纯展示模型。</param>
    /// <param name="targets">原版 Intent 当前目标。</param>
    /// <param name="owner">拥有行动的怪物实体。</param>
    /// <param name="diagnosticSink">不影响整列显示的描述兼容错误接收器。</param>
    public void Bind(
        EnemyCardIntentPresentation presentation,
        IReadOnlyList<Creature> targets,
        Creature owner,
        Action<string, Exception?> diagnosticSink)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(diagnosticSink);

        if (_cardModel is not null && !Equals(DisplayKey, presentation.DisplayKey))
        {
            throw new InvalidOperationException("按实例键复用的敌人 Intent 卡槽禁止改绑到不同实例。");
        }

        DisplayKey = presentation.DisplayKey;
        _cardModel = presentation.CardModel;
        _descriptionOverride = presentation.DescriptionOverride;
        _owner = owner;
        _targets = targets.ToArray();
        _diagnosticSink = diagnosticSink;
        EnsureThumbnail();
        BindThumbnail();
        _thumbnail!.Modulate = presentation.IsDimmed
            ? new Color(0.42f, 0.42f, 0.42f, 1f)
            : Colors.White;

        if (!EffectsEqual(presentation.Effects))
        {
            RebuildEffectNodes(presentation.Effects);
        }
        else
        {
            RefreshAllEffectIntents();
        }
    }

    /// <summary>
    /// 取得缩略牌当前全局矩形；节点尚未进入树或已释放时返回失败。
    /// </summary>
    public bool TryGetThumbnailGlobalRect(out Rect2 rect)
    {
        rect = default;
        if (!GodotObject.IsInstanceValid(_thumbnail) || !_thumbnail!.IsInsideTree() || !_thumbnail.Visible)
        {
            return false;
        }

        return TryGetCardGlobalRect(_thumbnail, out rect);
    }

    /// <summary>
    /// 仅重新调用攻击节点的原版 UpdateIntent，使本地玩家 Power 修正实时反映到标签。
    /// </summary>
    public void RefreshAttackIntents()
    {
        if (_owner is null)
        {
            return;
        }

        foreach (EffectNodeBinding binding in _effectNodes)
        {
            if (binding.IsAttack && GodotObject.IsInstanceValid(binding.Node))
            {
                binding.Node.UpdateIntent(binding.Intent, _targets, _owner);
            }
        }
    }

    /// <summary>
    /// 解除领域引用并归还池化缩略牌、释放本槽创建的全部非池化 Intent 节点。
    /// </summary>
    public void Release()
    {
        ReleaseThumbnail();
        ReleaseEffectNodes();
        _cardModel = null;
        _descriptionOverride = string.Empty;
        _owner = null;
        _targets = [];
        _diagnosticSink = null;
    }

    /// <summary>
    /// 离开场景树时幂等归还所有原版节点，防止 NodePool 与事件上下文泄漏。
    /// </summary>
    public override void _ExitTree()
    {
        Release();
        base._ExitTree();
    }

    /// <summary>
    /// 首次绑定时从原版池取得一个缩略 NCard，并彻底关闭递归输入和焦点。
    /// </summary>
    private void EnsureThumbnail()
    {
        if (GodotObject.IsInstanceValid(_thumbnail))
        {
            return;
        }

        CardModel cardModel = _cardModel ?? throw new InvalidOperationException("创建缩略牌前必须绑定卡面。");
        NCard thumbnail = NCard.Create(cardModel, ModelVisibility.Visible) ??
                          throw new InvalidOperationException("原版 NCard.Create 未返回缩略牌节点。");
        _thumbnail = thumbnail;
        _thumbnailHost.AddChild(thumbnail);
        thumbnail.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
        thumbnail.PivotOffset = thumbnail.Size / 2f;
        thumbnail.Scale = Vector2.One * ThumbnailScale;
        DisableInteractionRecursive(thumbnail);
    }

    /// <summary>
    /// 先让原版根据 CardModel 完整刷新，再只对非空覆写写入专用描述标签。
    /// </summary>
    private void BindThumbnail()
    {
        if (_thumbnail is null || _cardModel is null)
        {
            throw new InvalidOperationException("缩略牌绑定上下文不完整。");
        }

        _thumbnail.Model = _cardModel;
        _thumbnail.UpdateVisuals(PileType.None, CardPreviewMode.None);
        _thumbnail.KillRarityGlow();
        if (GodotObject.IsInstanceValid(_thumbnail.CardHighlight))
        {
            _thumbnail.CardHighlight.Visible = false;
        }

        ApplyDescriptionOverride(_thumbnail, _descriptionOverride);
    }

    /// <summary>
    /// 比较当前效果记录与新展示记录，避免相同实例键无意义重建其整行节点。
    /// </summary>
    private bool EffectsEqual(IReadOnlyList<EnemyCardEffectIntentPresentation> effects)
    {
        if (_effectNodes.Count != effects.Count)
        {
            return false;
        }

        for (int index = 0; index < effects.Count; index++)
        {
            if (!Equals(_effectNodes[index].Presentation, effects[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 只重建当前卡槽的效果节点，并为每项展示选择对应的原版 Intent 类型。
    /// </summary>
    private void RebuildEffectNodes(IReadOnlyList<EnemyCardEffectIntentPresentation> effects)
    {
        ReleaseEffectNodes();
        for (int index = 0; index < effects.Count; index++)
        {
            EnemyCardEffectIntentPresentation presentation = effects[index];
            (AbstractIntent intent, bool isAttack) = CreateVanillaIntent(presentation);
            NIntent node = NIntent.Create(index * 0.1f);
            node.Name = $"EffectIntent{index + 1}";
            node.MouseFilter = MouseFilterEnum.Ignore;
            node.FocusMode = FocusModeEnum.None;
            _effectRow.AddChild(node);
            DisableInteractionRecursive(node);
            _effectNodes.Add(new EffectNodeBinding(presentation, intent, node, isAttack));
        }

        RefreshAllEffectIntents();
    }

    /// <summary>
    /// 将展示效果转换为原版 Intent；无法由当前程序集表达的多段小数伤害安全降级为 Unknown。
    /// </summary>
    private static (AbstractIntent Intent, bool IsAttack) CreateVanillaIntent(
        EnemyCardEffectIntentPresentation presentation)
    {
        return presentation switch
        {
            EnemyAttackPresentation { HitCount: 1 } attack =>
                (new SingleAttackIntent(() => attack.BaseDamage), true),
            EnemyAttackPresentation attack when attack.HitCount > 1 &&
                                                attack.BaseDamage == decimal.Truncate(attack.BaseDamage) &&
                                                attack.BaseDamage is >= int.MinValue and <= int.MaxValue =>
                (new MultiAttackIntent((int)attack.BaseDamage, attack.HitCount), true),
            EnemyAttackPresentation => (new UnknownIntent(), false),
            EnemyDefendPresentation => (new DefendIntent(), false),
            EnemyBuffPresentation => (new BuffIntent(), false),
            EnemyDebuffPresentation => (new DebuffIntent(false), false),
            EnemyUnknownPresentation => (new UnknownIntent(), false),
            _ => (new UnknownIntent(), false)
        };
    }

    /// <summary>
    /// 使用当前 owner 与 targets 绑定本槽全部原版 Intent，保留原版图标与标签算法。
    /// </summary>
    private void RefreshAllEffectIntents()
    {
        Creature owner = _owner ?? throw new InvalidOperationException("刷新逐牌 Intent 前必须绑定怪物。");
        foreach (EffectNodeBinding binding in _effectNodes)
        {
            binding.Node.UpdateIntent(binding.Intent, _targets, owner);
        }
    }

    /// <summary>
    /// 在原版视觉更新后尝试写入描述覆写；单卡失败仅记录诊断并保留原版描述。
    /// </summary>
    private void ApplyDescriptionOverride(NCard cardNode, string descriptionOverride)
    {
        string? overrideText = EnemyCardDescriptionPresenter.BuildOverrideText(descriptionOverride);
        if (overrideText is null)
        {
            return;
        }

        try
        {
            // 通过 Godot 属性写入以兼容游戏程序集与模组资源中同名 MegaRichTextLabel 的类型边界。
            cardNode.GetNode<Node>("%DescriptionLabel").Set("text", overrideText);
        }
        catch (Exception exception)
        {
            _diagnosticSink?.Invoke($"展示槽 {DisplayKey} 无法应用描述覆写，已保留原版描述。", exception);
        }
    }

    /// <summary>
    /// 递归关闭原版节点的鼠标、焦点和输入处理，保证战斗点击穿透卡列。
    /// </summary>
    internal static void DisableInteractionRecursive(Node node)
    {
        node.SetProcessInput(false);
        node.SetProcessUnhandledInput(false);
        node.SetProcessUnhandledKeyInput(false);
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
            control.FocusMode = FocusModeEnum.None;
        }

        foreach (Node child in node.GetChildren())
        {
            DisableInteractionRecursive(child);
        }
    }

    /// <summary>
    /// 使用原版 NCard 的固有卡面尺寸计算全局命中矩形。
    /// card.tscn 的根 Control 尺寸为零，实际卡面以根原点为中心绘制，因此不能使用 Control.Size。
    /// </summary>
    internal static bool TryGetCardGlobalRect(NCard card, out Rect2 rect)
    {
        rect = default;
        if (!GodotObject.IsInstanceValid(card) || !card.IsInsideTree())
        {
            return false;
        }

        rect = CalculateCardGlobalRect(NCard.defaultSize, card.GetGlobalTransform());
        return rect.Size.X > 0f && rect.Size.Y > 0f;
    }

    /// <summary>
    /// 将以原点为中心的固有卡面四角通过完整全局变换转换为轴对齐矩形。
    /// </summary>
    private static Rect2 CalculateCardGlobalRect(Vector2 intrinsicCardSize, Transform2D globalTransform)
    {
        Vector2 halfSize = intrinsicCardSize / 2f;
        Vector2 topLeft = globalTransform * -halfSize;
        Vector2 topRight = globalTransform * new Vector2(halfSize.X, -halfSize.Y);
        Vector2 bottomLeft = globalTransform * new Vector2(-halfSize.X, halfSize.Y);
        Vector2 bottomRight = globalTransform * halfSize;
        float minimumX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
        float minimumY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
        float maximumX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
        float maximumY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));
        return new Rect2(
            new Vector2(minimumX, minimumY),
            new Vector2(maximumX - minimumX, maximumY - minimumY));
    }

    /// <summary>
    /// 将缩略 NCard 从树中移除后严格归还原版 NodePool。
    /// </summary>
    private void ReleaseThumbnail()
    {
        if (!GodotObject.IsInstanceValid(_thumbnail))
        {
            _thumbnail = null;
            return;
        }

        _thumbnail!.GetParent()?.RemoveChild(_thumbnail);
        NodePool.Free(_thumbnail);
        _thumbnail = null;
    }

    /// <summary>
    /// 释放本槽所有非池化 NIntent 节点并清空展示记录。
    /// </summary>
    private void ReleaseEffectNodes()
    {
        foreach (EffectNodeBinding binding in _effectNodes)
        {
            if (!GodotObject.IsInstanceValid(binding.Node))
            {
                continue;
            }

            binding.Node.GetParent()?.RemoveChild(binding.Node);
            binding.Node.QueueFree();
        }

        _effectNodes.Clear();
    }

    /// <summary>
    /// 保存单项展示、原版模型、节点和攻击类别，供局部 diff 与 Power 刷新使用。
    /// </summary>
    private sealed record EffectNodeBinding(
        EnemyCardEffectIntentPresentation Presentation,
        AbstractIntent Intent,
        NIntent Node,
        bool IsAttack);
}
