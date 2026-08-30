using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2_Tomorin_Mod.Enemy.CardIntents.Intents;
using STS2_Tomorin_Mod.Enemy.CardIntents.View;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 将 CardListIntent 标记桥接到动态逐牌视图，并在普通 Intent、故障、空列或视图异常时恢复原版 Unknown。
/// </summary>
[HarmonyPatch(typeof(NIntent), nameof(NIntent.UpdateIntent),
    [typeof(AbstractIntent), typeof(IEnumerable<Creature>), typeof(Creature)])]
internal static class NIntentCardListPatch
{
    private const string IntentHolderFieldName = "_intentHolder";
    private static readonly FieldInfo? IntentHolderField = ResolveIntentHolderField();
    private static readonly ConditionalWeakTable<ICombatState, UiFailureMarker> LoggedCombatFailures = new();
    private static int _loggedFailureWithoutCombat;

    /// <summary>
    /// 在原版完成 Unknown 根更新后幂等启用复合视图，普通 Intent 则恢复原版 Holder。
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(
        NIntent __instance,
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (intent is CardListIntent cardListIntent)
        {
            UpdateCardListIntentView(__instance, cardListIntent, targets, owner);
            return;
        }

        RestoreVanillaAndHideCustomView(__instance);
    }

    /// <summary>
    /// 只为 CardListIntent 启用自定义视图，故障或绑定失败时立即恢复原版 Unknown。
    /// </summary>
    private static void UpdateCardListIntentView(
        NIntent intentNode,
        CardListIntent cardListIntent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (!TryGetIntentHolder(intentNode, out Control holder))
        {
            RestoreVanillaAndHideCustomView(intentNode);
            LogUiFailureOnce(owner, "无法精确读取 NIntent._intentHolder；已安全回退 Unknown。", null);
            return;
        }

        if (cardListIntent.IsFaulted)
        {
            holder.Visible = true;
            HideAndUnbindCustomViews(intentNode);
            return;
        }

        try
        {
            NCardListIntentView view = GetOrCreateUniqueView(intentNode);
            view.Bind(cardListIntent, owner, targets);
            // 投影不完整由视图保留已知卡列并追加全局 Unknown，不属于整视图回退条件。
            bool showComposite = view.HasDisplayableCards && !cardListIntent.IsFaulted;
            view.Visible = showComposite;
            holder.Visible = !showComposite;
        }
        catch (Exception exception)
        {
            holder.Visible = true;
            HideAndUnbindCustomViews(intentNode);
            LogUiFailureOnce(owner, "创建或绑定 NCardListIntentView 失败；已安全回退 Unknown。", exception);
        }
    }

    /// <summary>
    /// 仅接受当前版本精确名称、精确声明类型和精确 Control 字段类型的私有 Holder。
    /// </summary>
    private static FieldInfo? ResolveIntentHolderField()
    {
        FieldInfo? field = typeof(NIntent).GetField(
            IntentHolderFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        return field?.FieldType == typeof(Control) ? field : null;
    }

    /// <summary>
    /// 使用缓存 FieldInfo 安全读取有效 Holder；字段缺失、类型变化或节点失效均返回失败。
    /// </summary>
    private static bool TryGetIntentHolder(NIntent intentNode, out Control holder)
    {
        holder = null!;
        if (IntentHolderField is null)
        {
            return false;
        }

        try
        {
            holder = IntentHolderField.GetValue(intentNode) as Control ?? null!;
            return holder is not null && GodotObject.IsInstanceValid(holder);
        }
        catch (Exception)
        {
            holder = null!;
            return false;
        }
    }

    /// <summary>
    /// 查找根节点内唯一自定义视图，缺失时只创建一个，多实例视为不兼容并触发安全回退。
    /// </summary>
    private static NCardListIntentView GetOrCreateUniqueView(NIntent intentNode)
    {
        NCardListIntentView[] existingViews = intentNode.GetChildren()
            .OfType<NCardListIntentView>()
            .ToArray();
        if (existingViews.Length > 1)
        {
            throw new InvalidOperationException("同一 NIntent 根中存在多个 NCardListIntentView。");
        }

        if (existingViews.Length == 1)
        {
            return existingViews[0];
        }

        NCardListIntentView view = new();
        intentNode.AddChild(view);
        return view;
    }

    /// <summary>
    /// 普通 Intent 更新时恢复原版 Holder，并隐藏、解绑可能随池化根节点残留的自定义视图。
    /// </summary>
    private static void RestoreVanillaAndHideCustomView(NIntent intentNode)
    {
        if (TryGetIntentHolder(intentNode, out Control holder))
        {
            holder.Visible = true;
        }

        HideAndUnbindCustomViews(intentNode);
    }

    /// <summary>
    /// 处理 CardListChanged 期间的延迟渲染错误，吞掉 UI 异常并恢复 Unknown，绝不污染牌堆逻辑。
    /// </summary>
    internal static void HandleDeferredViewFailure(NIntent intentNode, Creature owner, Exception exception)
    {
        try
        {
            RestoreVanillaAndHideCustomView(intentNode);
        }
        catch (Exception fallbackException)
        {
            exception = new AggregateException(exception, fallbackException);
        }

        LogUiFailureOnce(owner, "刷新 NCardListIntentView 失败；已安全回退 Unknown。", exception);
    }

    /// <summary>
    /// 响应已订阅视图的空手牌或首次非空变化，不额外调用 RefreshIntents 即可切换根 Holder。
    /// </summary>
    internal static void UpdateDeferredViewVisibility(
        NIntent intentNode,
        NCardListIntentView view,
        Creature owner,
        bool showComposite)
    {
        if (!TryGetIntentHolder(intentNode, out Control holder))
        {
            view.Unbind();
            view.Visible = false;
            LogUiFailureOnce(owner, "延迟刷新时无法读取 NIntent._intentHolder；已安全回退 Unknown。", null);
            return;
        }

        holder.Visible = !showComposite;
        view.Visible = showComposite;
    }

    /// <summary>
    /// 对根节点内所有自定义视图执行幂等解绑和隐藏，避免旧状态事件订阅泄漏。
    /// </summary>
    private static void HideAndUnbindCustomViews(NIntent intentNode)
    {
        foreach (NCardListIntentView view in intentNode.GetChildren().OfType<NCardListIntentView>())
        {
            view.Unbind();
            view.Visible = false;
        }
    }

    /// <summary>
    /// 同一战斗只记录首个 UI 兼容错误；缺少战斗上下文时整次进程只记录一次。
    /// </summary>
    private static void LogUiFailureOnce(Creature owner, string message, Exception? exception)
    {
        bool shouldLog;
        ICombatState? combatState = owner.CombatState;
        if (combatState is null)
        {
            shouldLog = Interlocked.Exchange(ref _loggedFailureWithoutCombat, 1) == 0;
        }
        else
        {
            UiFailureMarker marker = LoggedCombatFailures.GetValue(combatState, _ => new UiFailureMarker());
            shouldLog = marker.TryMark();
        }

        if (!shouldLog)
        {
            return;
        }

        string detail = exception is null ? string.Empty : $" {exception}";
        Log.Error($"[TomorinMod][CardListIntentUI] {message}{detail}");
    }

    /// <summary>
    /// 保存单场战斗是否已经记录过 UI 兼容错误的线程安全弱引用标记。
    /// </summary>
    private sealed class UiFailureMarker
    {
        private int _isMarked;

        /// <summary>
        /// 原子标记首次错误，只有第一次调用返回 true。
        /// </summary>
        public bool TryMark() => Interlocked.Exchange(ref _isMarked, 1) == 0;
    }
}
