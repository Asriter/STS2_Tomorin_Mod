using System.Collections.ObjectModel;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 为卡牌 Intent 怪物集中注册行动状态，并暴露全新战斗初始化与主机权威同步入口。
/// </summary>
public abstract class BaseCardIntentMonsterModel : CustomMonsterModel
{
    private Dictionary<string, CardIntentMoveState> _cardIntentStates = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, CardIntentMoveState>? _cardIntentStatesView;

    /// <summary>获取按稳定 StateId 索引的已注册卡牌行动状态。</summary>
    public IReadOnlyDictionary<string, CardIntentMoveState> CardIntentStates =>
        _cardIntentStatesView ??= new ReadOnlyDictionary<string, CardIntentMoveState>(_cardIntentStates);

    /// <summary>在任一权威卡牌行动状态变化后通知同步层重新捕获 DTO。</summary>
    public event Action<CardIntentMoveState>? CardIntentRuntimeChanged;

    /// <summary>
    /// 把运行时错误写入原版日志；领域 Harness 可覆写为空操作。
    /// </summary>
    /// <param name="message">包含怪物、状态或实例身份的诊断。</param>
    protected virtual void LogCardIntentError(string message) => Log.Error(message);

    /// <summary>
    /// 供状态、引擎和运行时适配器转发统一错误日志。
    /// </summary>
    /// <param name="message">待记录诊断。</param>
    internal void ReportCardIntentError(string message) => LogCardIntentError(message);

    /// <summary>
    /// 冻结行动已经完成全部来源生命周期、清空公开行动并回到 Idle 后触发。
    /// 派生首领只能在此安全点提交阶段迁移，不得替换刚刚执行完的行动。
    /// </summary>
    protected internal virtual Task AfterCardIntentActionSettledAsync(
        CardIntentMoveState state,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// 注册派生怪物创建的卡牌行动状态；同 StateId 重建时先解除旧订阅。
    /// </summary>
    /// <param name="state">Owner 必须为当前怪物的状态。</param>
    /// <returns>传入状态，便于状态机构造内联使用。</returns>
    protected CardIntentMoveState RegisterCardIntentState(CardIntentMoveState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ReferenceEquals(state.Owner, this))
        {
            throw new InvalidOperationException($"状态 {state.StateId} 的 Owner 与注册怪物不一致。 ");
        }

        if (_cardIntentStates.TryGetValue(state.StateId, out CardIntentMoveState? previous))
        {
            previous.StateChanged -= OnCardIntentStateChanged;
        }

        _cardIntentStates[state.StateId] = state;
        state.StateChanged += OnCardIntentStateChanged;
        return state;
    }

    /// <summary>
    /// 幂等重建当前怪物全部卡牌战斗状态，从第一回合开始且不恢复普通存档进度。
    /// </summary>
    public void InitializeFreshCardCombat()
    {
        foreach (CardIntentMoveState state in _cardIntentStates.Values)
        {
            state.InitializeFreshCardCombat();
        }
    }

    /// <summary>
    /// 捕获指定行动状态的主机权威重连 DTO；实际网络传输由游戏联机层负责。
    /// </summary>
    /// <param name="stateId">已注册原版 MoveState 稳定标识。</param>
    /// <param name="cursor">下一个尚未执行的安全原子步骤游标。</param>
    /// <returns>当前且唯一支持的重连结构版本。</returns>
    public EnemyCardRuntimeSyncState CaptureReconnectState(
        string stateId,
        EnemyCardExecutionCursor? cursor = null)
    {
        if (!_cardIntentStates.TryGetValue(stateId, out CardIntentMoveState? state))
        {
            throw new KeyNotFoundException($"怪物没有注册卡牌行动状态 {stateId}。");
        }

        return EnemyCardRuntimeSynchronizer.Capture(stateId, state.CombatState, cursor);
    }

    /// <summary>
    /// 将客户端收到的主机 DTO 在临时状态中完整校验后原子替换；失败时保持当前状态并要求主机重发。
    /// </summary>
    /// <param name="syncState">主机发送的当前版本权威 DTO。</param>
    /// <param name="restoredCursor">成功时返回下一个尚未执行的安全边界游标。</param>
    /// <param name="reason">失败时返回可用于请求主机重发的诊断。</param>
    /// <returns>状态和游标全部通过校验并已一次性应用时为真。</returns>
    public bool TryApplyReconnectState(
        EnemyCardRuntimeSyncState? syncState,
        out EnemyCardExecutionCursor? restoredCursor,
        out string reason)
    {
        restoredCursor = null;
        reason = string.Empty;
        if (syncState is null ||
            !_cardIntentStates.TryGetValue(syncState.StateId, out CardIntentMoveState? state))
        {
            reason = "重连 DTO 缺失，或引用了当前怪物未注册的 StateId。";
            return false;
        }

        EnemyCardContentDirectory directory = EnemyCardDeckRegistry.GetContentDirectory(state.DeckId);
        IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> definitions =
            directory.DefinitionFactories.Keys.ToDictionary(
                cardId => cardId,
                cardId => EnemyCardDeckRegistry.ResolveDefinition(state.DeckId, cardId).Definition);

        if (!EnemyCardRuntimeSynchronizer.TryRestore(
                syncState,
                state.DeckId,
                definitions,
                EnemyCardDeckRegistry.GetCollectionCatalog(state.DeckId),
                out EnemyCardCombatState? restoredState,
                out restoredCursor,
                out reason))
        {
            return false;
        }

        state.ApplyValidatedCombatState(restoredState!);
        if (Creature.HasPower<EnemyCollectionInventoryPower>())
        {
            Creature.GetPower<EnemyCollectionInventoryPower>().UpdateProjection(
                restoredState!.CollectionQueue.Select(item => item.Definition.CollectionId).ToArray());
        }

        return true;
    }

    /// <summary>
    /// 在新战斗初始化或重连原子应用后，把指定权威收藏品队列同步到可见 Power。
    /// </summary>
    /// <param name="stateId">已注册卡牌行动状态标识。</param>
    /// <param name="choiceContext">原版多人命令上下文。</param>
    /// <returns>收藏品 Power 创建或刷新完成任务。</returns>
    public Task SynchronizeCollectionPowerAsync(string stateId, PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        if (!_cardIntentStates.TryGetValue(stateId, out CardIntentMoveState? state))
        {
            throw new KeyNotFoundException($"怪物没有注册卡牌行动状态 {stateId}。");
        }

        return EnemyCollectionInventoryPower.SynchronizeAsync(
            choiceContext,
            Creature,
            state.CombatState.CollectionQueue.Select(item => item.Definition.CollectionId).ToArray(),
            this);
    }

    /// <summary>
    /// 在怪物离开房间前解除状态与外部同步订阅，避免克隆和旧房间保留引用。
    /// </summary>
    public override void BeforeRemovedFromRoom()
    {
        foreach (CardIntentMoveState state in _cardIntentStates.Values)
        {
            state.StateChanged -= OnCardIntentStateChanged;
        }

        CardIntentRuntimeChanged = null;
        base.BeforeRemovedFromRoom();
    }

    /// <summary>
    /// 原版浅克隆后为战斗实例创建独立状态注册表。
    /// </summary>
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _cardIntentStates = new Dictionary<string, CardIntentMoveState>(StringComparer.Ordinal);
        _cardIntentStatesView = null;
    }

    /// <summary>
    /// 清除原版 MemberwiseClone 复制的怪物级事件订阅。
    /// </summary>
    protected override void AfterCloned()
    {
        base.AfterCloned();
        CardIntentRuntimeChanged = null;
    }

    /// <summary>
    /// 把权威状态变化转发给主机同步与投影缓存层。
    /// </summary>
    /// <param name="state">发生变化的状态。</param>
    private void OnCardIntentStateChanged(CardIntentMoveState state)
    {
        if (CardIntentRuntimeChanged is null)
        {
            return;
        }

        foreach (Action<CardIntentMoveState> subscriber in
                 CardIntentRuntimeChanged.GetInvocationList().Cast<Action<CardIntentMoveState>>())
        {
            try
            {
                subscriber(state);
            }
            catch (Exception exception)
            {
                LogCardIntentError($"卡牌运行时同步订阅者失败：{exception}");
            }
        }
    }
}
