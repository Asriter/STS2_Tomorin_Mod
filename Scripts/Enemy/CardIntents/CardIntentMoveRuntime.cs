using MegaCrit.Sts2.Core.Entities.Creatures;
using STS2_Tomorin_Mod.Enemy.CardIntents.Intents;
using STS2_Tomorin_Mod.Enemy.CardIntents.Presentation;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 仅解决 MoveState 基类构造顺序：预先提供执行委托和固定 CardListIntent，再只读转发到唯一状态所有者。
/// </summary>
public sealed class CardIntentMoveRuntime
{
    private CardIntentMoveState? _state;

    /// <summary>
    /// 创建尚未绑定状态的两阶段构造适配器。
    /// </summary>
    internal CardIntentMoveRuntime()
    {
        Intent = new CardListIntent(this);
    }

    /// <summary>获取构造阶段创建且终身不替换的复合数据 Intent。</summary>
    internal CardListIntent Intent { get; }

    /// <summary>获取绑定状态声明的稳定牌组标识。</summary>
    internal EnemyCardDeckId DeckId => State.DeckId;

    /// <summary>获取绑定状态的唯一冻结手牌只读视图。</summary>
    internal IReadOnlyList<BaseEnemyCard> CardList => State.CardList;

    /// <summary>获取冻结计划的真实结构展示顺序。</summary>
    internal EnemyIntentTimeline IntentTimeline => State.IntentTimeline;

    /// <summary>获取绑定状态是否已进入安全故障模式。</summary>
    internal bool IsFaulted => State.IsFaulted;

    /// <summary>获取最近一次无副作用实时投影；尚未由适配层计算时为空。</summary>
    internal LiveActionProjection? LiveProjection => State.LiveProjection;

    /// <summary>获取冻结行动的两层门禁诊断；尚未提交行动时为空。</summary>
    internal EnemySoftLockDiagnostic? SoftLockDiagnostic =>
        State.CombatState.PreparedAction?.SoftLockDiagnostic;

    /// <summary>
    /// 为复合 Intent 视图取得冻结行动的只读结构投影，不触发视图事件。
    /// </summary>
    /// <param name="targets">原版 Intent 当前绑定的目标顺序。</param>
    /// <returns>按冻结 DFS 计划派生的显示投影。</returns>
    internal LiveActionProjection GetLiveProjectionForDisplay(IReadOnlyList<Creature> targets) =>
        State.GetLiveProjectionForDisplay(targets);

    /// <summary>获取已完成两阶段构造的唯一状态所有者。</summary>
    internal CardIntentMoveState State =>
        _state ?? throw new InvalidOperationException("CardIntentMoveRuntime 尚未绑定状态。");

    /// <summary>
    /// 当冻结手牌、准备标记或故障标记变化时通知复合 Intent 视图刷新。
    /// </summary>
    internal event Action? CardListChanged;

    /// <summary>
    /// 完成两阶段构造绑定；同一个 runtime 禁止被多个状态共享。
    /// </summary>
    /// <param name="state">唯一拥有五牌区、收藏品和运行阶段的状态。</param>
    internal void Attach(CardIntentMoveState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_state is not null)
        {
            throw new InvalidOperationException("CardIntentMoveRuntime 已绑定状态，不能重复绑定。");
        }

        _state = state;
    }

    /// <summary>
    /// 把原版 MoveState 执行委托转发给绑定状态，不拥有任何牌堆或执行标记。
    /// </summary>
    /// <param name="targets">原版怪物行动目标。</param>
    /// <returns>绑定状态的顺序执行任务。</returns>
    internal Task ExecuteCardsAsync(IReadOnlyList<Creature> targets) =>
        State.ExecuteCardsAndSettleAsync(targets);

    /// <summary>
    /// 由绑定状态在权威数据变化后触发只读视图通知。
    /// </summary>
    internal void RaiseCardListChanged()
    {
        if (CardListChanged is null)
        {
            return;
        }

        foreach (Action subscriber in CardListChanged.GetInvocationList().Cast<Action>())
        {
            try
            {
                subscriber();
            }
            catch (Exception exception)
            {
                State.Owner.ReportCardIntentError(
                    $"卡牌 Intent 视图刷新订阅者抛出异常，运行时牌堆保持不变：{exception}");
            }
        }
    }
}
