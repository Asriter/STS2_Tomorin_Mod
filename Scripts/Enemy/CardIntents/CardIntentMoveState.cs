using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2_Tomorin_Mod.Enemy.CardIntents.Test;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 把原版 MoveState 生命周期适配到战斗级五牌区、指标规划器和深度优先结算引擎。
/// </summary>
public sealed class CardIntentMoveState : MoveState
{
    private readonly CardIntentMoveRuntime _runtime;
    private readonly Func<int, int>? _randomIndexSelector;
    private readonly Func<bool>? _shouldStopExecution;
    private readonly Func<decimal, Task>? _attackExecutor;
    private readonly Func<decimal, Task>? _defendExecutor;
    private readonly Func<decimal, int, Task>? _attackAllExecutor;
    private readonly Func<Type, decimal, Task>? _enemyPowerExecutor;
    private readonly Func<Type, decimal, Task>? _targetPowerExecutor;
    private readonly Func<IReadOnlyList<string>, Task>? _collectionPowerExecutor;
    private readonly ICombatState? _combatStateOverride;
    private readonly CardIntentTestRules _rules;
    private readonly EnemyActionMetricPlanner _planner;
    private readonly EnemyCardExecutionEngine _executionEngine;
    private readonly EnemyActionProjectionService _projectionService = new();
    private LiveActionProjection? _liveProjection;

    /// <summary>
    /// 通过两阶段 runtime 创建原版行动状态与全新权威逻辑状态。
    /// </summary>
    private CardIntentMoveState(
        CardIntentMoveRuntime runtime,
        string stateId,
        BaseCardIntentMonsterModel owner,
        EnemyCardDeckId deckId,
        int handCapacity,
        Func<int, int>? randomIndexSelector,
        Func<bool>? shouldStopExecution,
        Func<decimal, Task>? attackExecutor,
        Func<decimal, Task>? defendExecutor,
        Func<decimal, int, Task>? attackAllExecutor,
        Func<Type, decimal, Task>? enemyPowerExecutor,
        Func<Type, decimal, Task>? targetPowerExecutor,
        Func<IReadOnlyList<string>, Task>? collectionPowerExecutor,
        ICombatState? combatStateOverride,
        CardIntentTestRules rules,
        EnemyCardExecutionEngine? executionEngine)
        : base(stateId, runtime.ExecuteCardsAsync, runtime.Intent)
    {
        if (string.IsNullOrWhiteSpace(stateId))
        {
            throw new ArgumentException("CardIntentMoveState 必须具有非空 StateId。", nameof(stateId));
        }

        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (!deckId.IsValid)
        {
            throw new ArgumentException("CardIntentMoveState 必须具有有效 DeckId。", nameof(deckId));
        }

        if (handCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(handCapacity));
        }

        DeckId = deckId;
        HandCapacity = handCapacity;
        _runtime = runtime;
        _randomIndexSelector = randomIndexSelector;
        _shouldStopExecution = shouldStopExecution;
        _attackExecutor = attackExecutor;
        _defendExecutor = defendExecutor;
        _attackAllExecutor = attackAllExecutor;
        _enemyPowerExecutor = enemyPowerExecutor;
        _targetPowerExecutor = targetPowerExecutor;
        _collectionPowerExecutor = collectionPowerExecutor;
        _combatStateOverride = combatStateOverride;
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _planner = new EnemyActionMetricPlanner(_rules, new EnemyCardScoreCalculator());
        _executionEngine = executionEngine ?? new EnemyCardExecutionEngine();
        CombatState = CreateFreshCombatState();
        runtime.Attach(this);
    }

    /// <summary>
    /// 创建固定使用 CardListIntent 的测试敌人行动状态。
    /// </summary>
    /// <param name="stateId">原版状态机内唯一状态标识。</param>
    /// <param name="owner">拥有此状态的卡牌 Intent 怪物。</param>
    /// <param name="deckId">已注册牌组标识。</param>
    /// <param name="handCapacity">兼容现有视图的指标最大槽位数。</param>
    /// <param name="randomIndexSelector">可选确定性测试随机索引 seam。</param>
    /// <param name="shouldStopExecution">可选测试终止 seam。</param>
    /// <param name="attackExecutor">可选测试攻击 seam。</param>
    /// <param name="defendExecutor">可选测试格挡 seam。</param>
    /// <param name="attackAllExecutor">可选测试全体多段攻击 seam。</param>
    /// <param name="enemyPowerExecutor">可选测试敌人自身 Power seam。</param>
    /// <param name="targetPowerExecutor">可选测试全体玩家 Power seam。</param>
    /// <param name="collectionPowerExecutor">可选测试收藏品队列 Power 同步 seam。</param>
    /// <param name="combatStateOverride">可选测试战斗状态。</param>
    /// <param name="rules">可选不可变规划与执行规则。</param>
    /// <param name="executionEngine">可选测试执行引擎；生产环境使用默认深度优先引擎。</param>
    /// <returns>绑定全新权威战斗状态的 MoveState。</returns>
    public static CardIntentMoveState Create(
        string stateId,
        BaseCardIntentMonsterModel owner,
        EnemyCardDeckId deckId,
        int handCapacity,
        Func<int, int>? randomIndexSelector = null,
        Func<bool>? shouldStopExecution = null,
        Func<decimal, Task>? attackExecutor = null,
        Func<decimal, Task>? defendExecutor = null,
        Func<decimal, int, Task>? attackAllExecutor = null,
        Func<Type, decimal, Task>? enemyPowerExecutor = null,
        Func<Type, decimal, Task>? targetPowerExecutor = null,
        Func<IReadOnlyList<string>, Task>? collectionPowerExecutor = null,
        ICombatState? combatStateOverride = null,
        CardIntentTestRules? rules = null,
        EnemyCardExecutionEngine? executionEngine = null)
    {
        CardIntentMoveRuntime runtime = new();
        return new CardIntentMoveState(
            runtime,
            stateId,
            owner,
            deckId,
            handCapacity,
            randomIndexSelector,
            shouldStopExecution,
            attackExecutor,
            defendExecutor,
            attackAllExecutor,
            enemyPowerExecutor,
            targetPowerExecutor,
            collectionPowerExecutor,
            combatStateOverride,
            rules ?? CardIntentTestRules.Default,
            executionEngine);
    }

    /// <summary>获取拥有此状态的怪物模型。</summary>
    public BaseCardIntentMonsterModel Owner { get; }

    /// <summary>获取声明牌组的稳定标识。</summary>
    public EnemyCardDeckId DeckId { get; }

    /// <summary>获取兼容旧视图的指标最大槽位数。</summary>
    public int HandCapacity { get; }

    /// <summary>获取当前战斗五牌区和收藏品的唯一权威状态。</summary>
    public EnemyCardCombatState CombatState { get; private set; }

    /// <summary>获取最近一次基于当前 Power 适配输入计算的非权威实时投影。</summary>
    public LiveActionProjection? LiveProjection => _liveProjection;

    /// <summary>获取抽牌堆实时只读视图。</summary>
    public IReadOnlyList<BaseEnemyCard> DeckList => CombatState.DrawPile;

    /// <summary>获取保留前缀与指标牌组成的公开执行顺序。</summary>
    public IReadOnlyList<BaseEnemyCard> CardList => CombatState.PreparedAction is { } action
        ? action.RetainedPrefix.Concat(action.MetricCards).ToArray()
        : Array.Empty<BaseEnemyCard>();

    /// <summary>获取弃牌堆实时只读视图。</summary>
    public IReadOnlyList<BaseEnemyCard> DiscardList => CombatState.DiscardPile;

    /// <summary>获取保留牌区实时只读视图。</summary>
    public IReadOnlyList<BaseEnemyCard> RetainedList => CombatState.RetainedCards;

    /// <summary>获取消耗牌区实时只读视图。</summary>
    public IReadOnlyList<BaseEnemyCard> ExhaustList => CombatState.ExhaustPile;

    /// <summary>获取当前行动是否已经冻结并公开。</summary>
    public bool IsPrepared => CombatState.RuntimePhase == EnemyCardRuntimePhase.Prepared;

    /// <summary>获取当前行动是否正在执行。</summary>
    public bool IsExecuting => CombatState.RuntimePhase == EnemyCardRuntimePhase.Executing;

    /// <summary>获取状态是否因结构异常停止。</summary>
    public bool IsFaulted => CombatState.RuntimePhase == EnemyCardRuntimePhase.Faulted;

    /// <summary>获取公开行动本体原始攻击总分，仅供诊断与旧视图兼容。</summary>
    public decimal TotalRawAttack => CardList.Sum(card => card.Definition.ScoreProfile.Attack);

    /// <summary>获取公开行动本体原始格挡总分，仅供诊断与旧视图兼容。</summary>
    public decimal TotalRawDefense => CardList.Sum(card => card.Definition.ScoreProfile.Block);

    /// <summary>获取公开行动是否包含攻击标签。</summary>
    public bool HasAttack => CardList.Any(card => card.Definition.Tags.HasFlag(EnemyCardTag.Attack));

    /// <summary>获取公开行动是否包含防御标签。</summary>
    public bool HasDefense => CardList.Any(card => card.Definition.Tags.HasFlag(EnemyCardTag.Defense));

    /// <summary>在权威牌区、行动、收藏品或运行阶段变化后通知怪物与 Intent。</summary>
    internal event Action<CardIntentMoveState>? StateChanged;

    /// <summary>
    /// 以战斗 RNG 评估候选并原子提交一项指标行动；重复准备不会推进 RNG。
    /// </summary>
    /// <returns>本次确实创建新冻结行动时为真。</returns>
    public bool PrepareCards()
    {
        if (CombatState.RuntimePhase != EnemyCardRuntimePhase.Idle || CombatState.PreparedAction is not null)
        {
            return false;
        }

        try
        {
            PreparedEnemyCardAction action =
                _planner.Prepare(CombatState, new EnemyPlanningContext(CreateRandomSource()));
            string cardOrder = string.Join(
                " -> ",
                action.Sources.Select((source, index) =>
                    $"{index + 1}:{source.SourceCard.CardId}[{source.SourceKey}]"));
            Log.Info(
                $"[CardIntentOrder] StateId={StateId}; DeckId={DeckId}; " +
                $"Metric={action.Metric}; Cards={cardOrder}");
            NotifyStateChanged();
            return true;
        }
        catch (Exception exception)
        {
            EnemyCardRuntimePhase phaseBeforeFault = CombatState.RuntimePhase;
            string diagnostic = $"准备敌人卡牌行动失败：{exception.Message}";
            CombatState.MarkFault(diagnostic);
            ReportFaultDiagnostic(
                CombatState,
                "Preparation",
                phaseBeforeFault.ToString(),
                exception);
            NotifyStateChanged();
            return false;
        }
    }

    /// <summary>
    /// 对当前冻结行动执行无副作用实时投影；该结果不参与评分、同步或牌区写入。
    /// </summary>
    /// <param name="input">全部有效玩家和已知 Power 修正的纯数据输入。</param>
    /// <returns>逐来源、逐重放、逐目标投影。</returns>
    public LiveActionProjection RefreshLiveProjection(EnemyActionProjectionInput input)
    {
        PreparedEnemyCardAction action = CombatState.PreparedAction ??
                                         throw new InvalidOperationException("没有冻结行动时不能计算实时投影。");
        _liveProjection = _projectionService.Project(action, input);
        _runtime.RaiseCardListChanged();
        return _liveProjection;
    }

    /// <summary>
    /// 为 Intent 视图取得只包含结构语义的当前投影，不触发卡列事件递归。
    /// </summary>
    /// <param name="targets">原版 Intent 当前绑定的目标顺序。</param>
    /// <returns>从冻结计划派生且不写入战斗状态的结构投影。</returns>
    public LiveActionProjection GetLiveProjectionForDisplay(IReadOnlyList<Creature> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        PreparedEnemyCardAction action = CombatState.PreparedAction ??
                                         throw new InvalidOperationException("没有冻结行动时不能读取显示投影。");
        EnemySimulationTarget[] projectionTargets = targets
            .Select((_, index) => new EnemySimulationTarget(
                $"TARGET:{index}",
                decimal.One,
                decimal.One))
            .ToArray();
        _liveProjection = _projectionService.Project(
            action,
            new EnemyActionProjectionInput(projectionTargets, _rules.StepLimit));
        return _liveProjection;
    }

    /// <summary>
    /// 通过深度优先引擎结算冻结行动，素材不足按正常不可打出处理。
    /// </summary>
    /// <param name="targets">原版怪物行动传入的玩家目标顺序。</param>
    /// <returns>行动完成、正常中断或故障后的任务。</returns>
    public async Task ExecuteCardsAsync(IReadOnlyList<Creature> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (!IsPrepared || IsFaulted)
        {
            return;
        }

        EnemyCardExecutionContext context = new(
            Owner,
            this,
            new ThrowingPlayerChoiceContext(),
            targets,
            _combatStateOverride ?? Owner.CombatState,
            shouldStopExecution: ShouldStopExecution,
            attackExecutor: _attackExecutor,
            defendExecutor: _defendExecutor,
            attackAllExecutor: _attackAllExecutor,
            enemyPowerExecutor: _enemyPowerExecutor,
            targetPowerExecutor: _targetPowerExecutor,
            collectionPowerExecutor: _collectionPowerExecutor);
        await _executionEngine.ExecutePreparedActionAsync(
            CombatState,
            context,
            CreateRandomSource(),
            _rules.StepLimit);
        NotifyStateChanged();
    }

    /// <summary>
    /// 取消尚未执行的公开行动；指标牌进入弃牌堆，保留前缀返回保留区。
    /// </summary>
    /// <returns>本次确实取消冻结行动时为真。</returns>
    public bool CancelPreparedHand()
    {
        if (!IsPrepared || CombatState.PreparedAction is not { } action)
        {
            return false;
        }

        foreach (BaseEnemyCard card in action.MetricCards.ToArray())
        {
            if (CombatState.CurrentCards.Any(current => current.InstanceKey == card.InstanceKey))
            {
                CombatState.MoveCard(card.InstanceKey, EnemyCardZone.Discard);
            }
        }

        CombatState.ClearPreparedAction();
        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// 幂等重建初始牌、清空运行中区域并恢复规则配置的初始收藏品。
    /// </summary>
    public void InitializeFreshCardCombat()
    {
        CombatState = CreateFreshCombatState();
        NotifyStateChanged();
    }

    /// <summary>
    /// 在重连 DTO 已经通过临时全量校验后一次性替换权威状态，拒绝半成品逐字段应用。
    /// </summary>
    /// <param name="validatedState">与当前状态绑定同一牌组的完整临时状态。</param>
    internal void ApplyValidatedCombatState(EnemyCardCombatState validatedState)
    {
        ArgumentNullException.ThrowIfNull(validatedState);
        if (validatedState.DeckId != DeckId)
        {
            throw new InvalidOperationException(
                $"重连状态牌组 {validatedState.DeckId} 与行动状态牌组 {DeckId} 不一致。");
        }

        CombatState = validatedState;
        if (validatedState.RuntimePhase == EnemyCardRuntimePhase.Faulted)
        {
            ReportFaultDiagnostic(
                validatedState,
                "ReconnectRestore",
                "Unavailable(RemoteFault)",
                exception: null);
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// 输出统一的 Faulted 诊断上下文；异常存在时保留完整类型、内部异常和堆栈。
    /// </summary>
    /// <param name="faultedState">已经进入 Faulted 的权威状态。</param>
    /// <param name="stage">产生或恢复故障的生命周期阶段。</param>
    /// <param name="phaseBeforeFault">本地进入 Faulted 前的阶段；远端恢复时明确标记不可用。</param>
    /// <param name="exception">本地产生故障的完整异常；远端恢复没有本机异常对象。</param>
    internal void ReportFaultDiagnostic(
        EnemyCardCombatState faultedState,
        string stage,
        string phaseBeforeFault,
        Exception? exception)
    {
        PreparedEnemyCardAction? preparedAction = faultedState.PreparedAction;
        string preparedActionSummary = preparedAction is null
            ? "None"
            : $"Metric:{preparedAction.Metric},Sources:{preparedAction.Sources.Count}";
        string exceptionDetail = exception is null
            ? "Exception=Unavailable (restored fault diagnostics do not contain a serialized local exception or stack trace)."
            : $"Exception={Environment.NewLine}{exception}";
        Owner.ReportCardIntentError(
            $"[CardIntentFault] Stage={stage}; StateId={StateId}; DeckId={DeckId}; " +
            $"PhaseBeforeFault={phaseBeforeFault}; RuntimePhase={faultedState.RuntimePhase}; " +
            $"Zones=Draw:{faultedState.DrawPile.Count},Current:{faultedState.CurrentCards.Count}," +
            $"Retained:{faultedState.RetainedCards.Count},Discard:{faultedState.DiscardPile.Count}," +
            $"Exhaust:{faultedState.ExhaustPile.Count},Immediate:{faultedState.ImmediateResolutionStack.Count}; " +
            $"Collections=Available:{faultedState.CollectionQueue.Count}," +
            $"Consumed:{faultedState.ConsumedCollections.Count}; PreparedAction={preparedActionSummary}; " +
            $"FailureReason={faultedState.FaultDiagnostic ?? "<missing>"}{Environment.NewLine}" +
            exceptionDetail);
    }

    /// <summary>
    /// 创建已注入战斗 CombatCardSelection RNG 或测试 seam 的边界检查包装。
    /// </summary>
    private IEnemyCardRandomSource CreateRandomSource() =>
        new EnemyCardRandomSource(count =>
            _randomIndexSelector?.Invoke(count) ?? Owner.RunRng.CombatCardSelection.NextInt(count));

    /// <summary>
    /// 创建全新战斗级状态并按规则追加初始星石收藏品。
    /// </summary>
    private EnemyCardCombatState CreateFreshCombatState()
    {
        EnemyCardCombatState state = EnemyCardDeckRegistry.CreateCombatState(DeckId);
        if (DeckId == CardIntentTestDeck.DeckId)
        {
            EnemyCollectionDefinition starStone = CardIntentTestCollectionCatalog.Catalog.GetRequired(
                CardIntentTestCollectionCatalog.StarStoneId);
            for (int index = 0; index < _rules.InitialStarStoneCount; index++)
            {
                state.CollectionInventory.Append(starStone);
            }
        }

        return state;
    }

    /// <summary>
    /// 判断测试 seam、战斗结束、怪物死亡或离场是否要求正常中止。
    /// </summary>
    private bool ShouldStopExecution()
    {
        if (_shouldStopExecution is not null)
        {
            return _shouldStopExecution();
        }

        try
        {
            return (CombatManager.Instance?.IsOverOrEnding ?? true) ||
                   Owner.Creature.IsDead ||
                   !Owner.CombatState.ContainsCreature(Owner.Creature) ||
                   !Owner.CombatState.IsLiveCombat();
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>同步通知现有 Intent 视图和怪物级运行时投影。</summary>
    private void NotifyStateChanged()
    {
        _liveProjection = null;
        _projectionService.Invalidate();
        _runtime.RaiseCardListChanged();
        if (StateChanged is null)
        {
            return;
        }

        foreach (Action<CardIntentMoveState> subscriber in
                 StateChanged.GetInvocationList().Cast<Action<CardIntentMoveState>>())
        {
            try
            {
                subscriber(this);
            }
            catch (Exception exception)
            {
                Owner.ReportCardIntentError($"状态 {StateId} 变更订阅者失败：{exception}");
            }
        }
    }
}
