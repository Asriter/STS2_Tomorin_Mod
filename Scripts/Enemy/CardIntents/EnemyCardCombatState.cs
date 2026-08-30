namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 指定敌人卡牌实例当前所属的唯一权威牌区。
/// </summary>
public enum EnemyCardZone
{
    /// <summary>抽牌堆。</summary>
    Draw,

    /// <summary>当前公开并待结算的指标牌区。</summary>
    Current,

    /// <summary>下一回合作为强制前缀的保留区。</summary>
    Retained,

    /// <summary>弃牌堆。</summary>
    Discard,

    /// <summary>消耗堆。</summary>
    Exhaust
}

/// <summary>
/// 表示战斗级敌人卡牌逻辑当前所处的高层运行阶段。
/// </summary>
public enum EnemyCardRuntimePhase
{
    /// <summary>尚未准备公开行动。</summary>
    Idle,

    /// <summary>行动结构已经冻结。</summary>
    Prepared,

    /// <summary>正在结算冻结行动。</summary>
    Executing,

    /// <summary>结构故障后停止继续结算。</summary>
    Faulted
}

/// <summary>
/// 唯一拥有五牌区、收藏品、即时栈、准备行动和同步游标的战斗级权威状态。
/// </summary>
public sealed class EnemyCardCombatState
{
    private readonly List<BaseEnemyCard> _drawPile;
    private readonly List<BaseEnemyCard> _currentCards = [];
    private readonly List<BaseEnemyCard> _retainedCards = [];
    private readonly List<BaseEnemyCard> _discardPile = [];
    private readonly List<BaseEnemyCard> _exhaustPile = [];
    private readonly List<BaseEnemyCard> _immediateResolutionStack = [];
    private readonly IReadOnlyList<BaseEnemyCard> _drawView;
    private readonly IReadOnlyList<BaseEnemyCard> _currentView;
    private readonly IReadOnlyList<BaseEnemyCard> _retainedView;
    private readonly IReadOnlyList<BaseEnemyCard> _discardView;
    private readonly IReadOnlyList<BaseEnemyCard> _exhaustView;
    private readonly IReadOnlyList<BaseEnemyCard> _immediateView;

    /// <summary>
    /// 从已经分配唯一模板槽位的初始牌创建权威状态。
    /// </summary>
    /// <param name="deckId">已注册牌组稳定标识。</param>
    /// <param name="initialCards">按模板槽位顺序排列的独立实例。</param>
    internal EnemyCardCombatState(
        EnemyCardDeckId deckId,
        IEnumerable<BaseEnemyCard> initialCards,
        EnemyCardPhase activePhase = EnemyCardPhase.None)
    {
        if (!deckId.IsValid)
        {
            throw new ArgumentException("战斗状态必须绑定有效牌组标识。", nameof(deckId));
        }

        ArgumentNullException.ThrowIfNull(initialCards);
        _drawPile = initialCards.ToList();
        if (_drawPile.Count == 0 || _drawPile.Any(card => card is null || card.TemplateSlot is null))
        {
            throw new ArgumentException("战斗状态初始牌必须非空且全部具有模板槽位。", nameof(initialCards));
        }

        if (_drawPile.Select(card => card.InstanceKey).Distinct().Count() != _drawPile.Count)
        {
            throw new ArgumentException("战斗状态初始牌实例键必须唯一。", nameof(initialCards));
        }

        if (!Enum.IsDefined(activePhase))
        {
            throw new ArgumentOutOfRangeException(nameof(activePhase), activePhase, "未知初始卡牌阶段。");
        }

        DeckId = deckId;
        ActivePhase = activePhase;
        TemplateSlots = Array.AsReadOnly(_drawPile.Select(card => card.TemplateSlot!.Value).ToArray());
        _drawView = _drawPile.AsReadOnly();
        _currentView = _currentCards.AsReadOnly();
        _retainedView = _retainedCards.AsReadOnly();
        _discardView = _discardPile.AsReadOnly();
        _exhaustView = _exhaustPile.AsReadOnly();
        _immediateView = _immediateResolutionStack.AsReadOnly();
        CollectionInventory = new EnemyCollectionInventory();
    }

    /// <summary>获取创建本状态的稳定牌组标识。</summary>
    public EnemyCardDeckId DeckId { get; }

    /// <summary>获取保留模板顺序的初始槽位集合。</summary>
    public IReadOnlyList<int> TemplateSlots { get; private set; }

    /// <summary>获取不可修改的抽牌堆视图。</summary>
    public IReadOnlyList<BaseEnemyCard> DrawPile => _drawView;

    /// <summary>获取不可修改的当前指标牌区视图。</summary>
    public IReadOnlyList<BaseEnemyCard> CurrentCards => _currentView;

    /// <summary>获取不可修改的保留前缀牌区视图。</summary>
    public IReadOnlyList<BaseEnemyCard> RetainedCards => _retainedView;

    /// <summary>获取不可修改的弃牌堆视图。</summary>
    public IReadOnlyList<BaseEnemyCard> DiscardPile => _discardView;

    /// <summary>获取不可修改的消耗堆视图。</summary>
    public IReadOnlyList<BaseEnemyCard> ExhaustPile => _exhaustView;

    /// <summary>获取有序可用收藏品队列的只读视图。</summary>
    public IReadOnlyList<EnemyCollectionInstance> CollectionQueue => CollectionInventory.Available;

    /// <summary>获取已消费收藏品区的只读视图。</summary>
    public IReadOnlyList<EnemyCollectionInstance> ConsumedCollections => CollectionInventory.Consumed;

    /// <summary>获取收藏品可用队列和已消耗区的唯一写入口。</summary>
    public EnemyCollectionInventory CollectionInventory { get; private set; }

    /// <summary>获取深度优先即时结算栈的只读视图。</summary>
    public IReadOnlyList<BaseEnemyCard> ImmediateResolutionStack => _immediateView;

    /// <summary>获取上次成功提交的行动指标。</summary>
    public EnemyActionMetric? LastMetric { get; private set; }

    /// <summary>获取当前冻结的准备行动。</summary>
    public PreparedEnemyCardAction? PreparedAction { get; private set; }

    /// <summary>获取当前运行阶段。</summary>
    public EnemyCardRuntimePhase RuntimePhase { get; private set; } = EnemyCardRuntimePhase.Idle;

    /// <summary>获取下一张战斗生成牌将使用的单调序号。</summary>
    public long NextGeneratedCardSequence { get; private set; }

    /// <summary>获取下一项生成收藏品将使用的单调序号。</summary>
    public long NextCollectionSequence => CollectionInventory.NextSequence;

    /// <summary>获取结构故障诊断；正常素材不足不会写入。</summary>
    public string? FaultDiagnostic { get; private set; }

    /// <summary>获取当前准备周期只选择一次的冻结收藏品。</summary>
    public EnemyCollectionInstance? FrozenPreparationCollection { get; private set; }

    /// <summary>获取当前准备周期共享的同一库存增量。</summary>
    public EnemyPreparedPreActionInventoryDelta? FrozenPreparationDelta { get; private set; }

    /// <summary>获取当前使用的内容阶段。</summary>
    public EnemyCardPhase ActivePhase { get; private set; }

    /// <summary>获取已请求但尚未在安全点应用的下一阶段。</summary>
    public EnemyCardPhase PendingPhase { get; private set; } = EnemyCardPhase.None;

    /// <summary>获取已成功原子应用的阶段迁移修订号。</summary>
    public long PhaseRevision { get; private set; }

    /// <summary>在一次完整阶段候选成功应用后发布一次变更通知。</summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// 记录一个只能在后续 Idle 安全点原子应用的更高阶段请求。
    /// </summary>
    public void RequestPhase(EnemyCardPhase phase)
    {
        if (!Enum.IsDefined(phase) || phase == EnemyCardPhase.None || ActivePhase == EnemyCardPhase.None)
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "只有显式阶段化牌组可请求 P1-P3 迁移。");
        }

        if (phase <= ActivePhase)
        {
            throw new InvalidOperationException($"阶段请求 {phase} 必须高于当前阶段 {ActivePhase}。");
        }

        if (PendingPhase != EnemyCardPhase.None && PendingPhase != phase)
        {
            throw new InvalidOperationException($"已存在待处理阶段 {PendingPhase}，禁止覆盖为 {phase}。");
        }

        PendingPhase = phase;
    }

    /// <summary>
    /// 在不修改当前五区、收藏品或阶段字段的前提下构建完整迁移候选。
    /// </summary>
    public EnemyCardPhaseTransitionCandidate BuildPhaseTransitionCandidate(
        EnemyCardPhaseTemplate nextPhase,
        IEnemyCardRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(nextPhase);
        ArgumentNullException.ThrowIfNull(random);
        if (RuntimePhase != EnemyCardRuntimePhase.Idle ||
            PreparedAction is not null ||
            _immediateResolutionStack.Count != 0)
        {
            throw new InvalidOperationException(
                "阶段迁移候选只能在 Idle、无冻结行动且无即时结算栈的安全点构建。");
        }

        if (PendingPhase == EnemyCardPhase.None || PendingPhase != nextPhase.Phase)
        {
            throw new InvalidOperationException(
                $"阶段模板 {nextPhase.Phase} 与待处理阶段 {PendingPhase} 不一致。");
        }

        EnemyCardContentDirectory directory = EnemyCardDeckRegistry.GetContentDirectory(DeckId);
        if (!ReferenceEquals(directory.GetPhase(nextPhase.Phase), nextPhase))
        {
            throw new InvalidOperationException(
                $"阶段 {nextPhase.Phase} 必须使用牌组 {DeckId} 已注册的权威模板。");
        }

        List<BaseEnemyCard> newSources = EnemyCardDeckRegistry.CreatePhaseDeck(DeckId, nextPhase.Phase);
        Shuffle(newSources, random);
        EnemyCardCombatState candidate = new(DeckId, newSources, nextPhase.Phase);
        candidate._drawPile.Clear();
        candidate._drawPile.AddRange(_drawPile.Where(card => card.CarryAcrossPhase));
        candidate._drawPile.AddRange(newSources);
        CopyCarryCards(_currentCards, candidate._currentCards);
        CopyCarryCards(_retainedCards, candidate._retainedCards);
        CopyCarryCards(_discardPile, candidate._discardPile);
        CopyCarryCards(_exhaustPile, candidate._exhaustPile);
        candidate.TemplateSlots = Array.AsReadOnly(candidate.EnumerateMutableZones()
            .SelectMany(zone => zone)
            .Where(card => card.TemplateSlot.HasValue)
            .Select(card => card.TemplateSlot!.Value)
            .Order()
            .ToArray());
        candidate.CollectionInventory = CollectionInventory.CreateTransactionalClone();
        candidate.NextGeneratedCardSequence = NextGeneratedCardSequence;
        candidate.LastMetric = null;
        candidate.PreparedAction = null;
        candidate.RuntimePhase = EnemyCardRuntimePhase.Idle;
        candidate.FaultDiagnostic = null;
        candidate.FrozenPreparationCollection = null;
        candidate.FrozenPreparationDelta = null;
        candidate.ActivePhase = nextPhase.Phase;
        candidate.PendingPhase = EnemyCardPhase.None;
        candidate.PhaseRevision = checked(PhaseRevision + 1);
        candidate.AssertUniqueOwnership();
        return new EnemyCardPhaseTransitionCandidate(
            ActivePhase,
            nextPhase.Phase,
            candidate.PhaseRevision,
            candidate);
    }

    /// <summary>
    /// 校验候选修订与阶段身份后一次性替换全部权威字段。
    /// </summary>
    public void ApplyPhaseTransition(EnemyCardPhaseTransitionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        EnemyCardCombatState source = candidate.CandidateState ?? throw new ArgumentException(
            "阶段迁移候选缺少完整状态。",
            nameof(candidate));
        long expectedRevision = checked(PhaseRevision + 1);
        if (source.DeckId != DeckId ||
            candidate.From != ActivePhase ||
            candidate.To != PendingPhase ||
            candidate.NextRevision != expectedRevision ||
            source.ActivePhase != candidate.To ||
            source.PendingPhase != EnemyCardPhase.None ||
            source.PhaseRevision != candidate.NextRevision ||
            source.RuntimePhase != EnemyCardRuntimePhase.Idle ||
            source.PreparedAction is not null ||
            source.ImmediateResolutionStack.Count != 0)
        {
            throw new InvalidOperationException("阶段迁移候选已过期、修订不连续或不是完整安全点状态。");
        }

        source.AssertUniqueOwnership();
        EnemyCollectionInventory collectionInventory = source.CollectionInventory.CreateTransactionalClone();
        TemplateSlots = Array.AsReadOnly(source.TemplateSlots.ToArray());
        ReplaceZone(_drawPile, source._drawPile);
        ReplaceZone(_currentCards, source._currentCards);
        ReplaceZone(_retainedCards, source._retainedCards);
        ReplaceZone(_discardPile, source._discardPile);
        ReplaceZone(_exhaustPile, source._exhaustPile);
        _immediateResolutionStack.Clear();
        CollectionInventory = collectionInventory;
        NextGeneratedCardSequence = source.NextGeneratedCardSequence;
        LastMetric = source.LastMetric;
        PreparedAction = source.PreparedAction;
        RuntimePhase = source.RuntimePhase;
        FaultDiagnostic = source.FaultDiagnostic;
        FrozenPreparationCollection = source.FrozenPreparationCollection;
        FrozenPreparationDelta = source.FrozenPreparationDelta;
        ActivePhase = source.ActivePhase;
        PendingPhase = source.PendingPhase;
        PhaseRevision = source.PhaseRevision;
        AssertUniqueOwnership();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 把已有实例原子移动到另一个权威牌区并保持实例身份不变。
    /// </summary>
    /// <param name="instanceKey">待移动实例唯一键。</param>
    /// <param name="destination">目标牌区。</param>
    public void MoveCard(EnemyCardInstanceKey instanceKey, EnemyCardZone destination)
    {
        ArgumentNullException.ThrowIfNull(instanceKey);
        (List<BaseEnemyCard> source, BaseEnemyCard card) = FindOwnedCard(instanceKey);
        List<BaseEnemyCard> target = GetMutableZone(destination);
        if (ReferenceEquals(source, target))
        {
            return;
        }

        source.Remove(card);
        target.Add(card);
        AssertUniqueOwnership();
    }

    /// <summary>
    /// 为新生成牌分配运行时身份并加入指定牌区。
    /// </summary>
    /// <param name="card">尚未绑定任何实例身份的新对象。</param>
    /// <param name="destination">新实例的初始权威牌区。</param>
    public void AddGeneratedCard(BaseEnemyCard card, EnemyCardZone destination)
    {
        ArgumentNullException.ThrowIfNull(card);
        card.AssignSourcePhase(ActivePhase);
        card.AssignRuntimeInstanceId(NextGeneratedCardSequence++);
        GetMutableZone(destination).Add(card);
        AssertUniqueOwnership();
    }

    /// <summary>在真实即时子牌开始执行前把当前区权威实例压入 LIFO 结算栈。</summary>
    internal void PushImmediateResolution(BaseEnemyCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (RuntimePhase != EnemyCardRuntimePhase.Executing ||
            !_currentCards.Contains(card) ||
            _immediateResolutionStack.Contains(card))
        {
            throw new InvalidOperationException(
                "即时结算只能压入 Executing 状态当前牌区中尚未入栈的权威实例。");
        }

        _immediateResolutionStack.Add(card);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>在真实即时子牌及其全部重放结束后弹出同一实例，严格保持 LIFO。</summary>
    internal void PopImmediateResolution(BaseEnemyCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (_immediateResolutionStack.Count == 0 ||
            !ReferenceEquals(_immediateResolutionStack[^1], card))
        {
            throw new InvalidOperationException("即时结算栈弹出顺序与当前权威实例不一致。");
        }

        _immediateResolutionStack.RemoveAt(_immediateResolutionStack.Count - 1);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 清除已消费完毕或测试显式撤销的准备行动，但保留 LastMetric。
    /// </summary>
    public void ClearPreparedAction()
    {
        PreparedAction = null;
        if (RuntimePhase != EnemyCardRuntimePhase.Faulted)
        {
            RuntimePhase = EnemyCardRuntimePhase.Idle;
            FrozenPreparationCollection = null;
            FrozenPreparationDelta = null;
        }
    }

    /// <summary>
    /// 在冻结行动存在时进入执行阶段，禁止重复开始或从故障态继续。
    /// </summary>
    public void BeginExecution()
    {
        if (RuntimePhase != EnemyCardRuntimePhase.Prepared || PreparedAction is null)
        {
            throw new InvalidOperationException("只有具有冻结行动的 Prepared 状态才能开始执行。");
        }

        RuntimePhase = EnemyCardRuntimePhase.Executing;
    }

    /// <summary>
    /// 在行动正常结算或因敌人离场正常中止后清除冻结结构并回到空闲阶段。
    /// </summary>
    public void CompleteExecution()
    {
        if (RuntimePhase != EnemyCardRuntimePhase.Executing)
        {
            throw new InvalidOperationException("只有 Executing 状态才能正常完成行动。");
        }

        if (_immediateResolutionStack.Count != 0)
        {
            throw new InvalidOperationException("即时结算栈非空时不能完成敌人卡牌行动。");
        }

        PreparedAction = null;
        RuntimePhase = EnemyCardRuntimePhase.Idle;
        FrozenPreparationCollection = null;
        FrozenPreparationDelta = null;
    }

    /// <summary>
    /// 记录不可继续的结构故障并保留已提交状态供诊断与重连同步。
    /// </summary>
    /// <param name="diagnostic">非空结构故障说明。</param>
    public void MarkFault(string diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            throw new ArgumentException("结构故障诊断不能为空。", nameof(diagnostic));
        }

        FaultDiagnostic = diagnostic;
        RuntimePhase = EnemyCardRuntimePhase.Faulted;
    }

    /// <summary>
    /// 在候选循环开始前记录唯一准备周期；这里只冻结诊断，不写入权威库存。
    /// </summary>
    internal void StorePreparationCycle(EnemyPreparationCycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        if (PreparedAction is not null || RuntimePhase != EnemyCardRuntimePhase.Idle)
        {
            throw new InvalidOperationException("只有 Idle 且不存在冻结行动时才能开始准备周期。");
        }

        if (FrozenPreparationCollection is not null || FrozenPreparationDelta is not null)
        {
            throw new InvalidOperationException("当前状态已经冻结准备周期，不能覆盖或再次推进收藏品随机源。");
        }

        if (cycle.FrozenPreparationCollection is not null &&
            !cycle.Delta.AddedAvailable.Any(instance =>
                ReferenceEquals(instance, cycle.FrozenPreparationCollection)))
        {
            throw new ArgumentException("冻结准备收藏品必须由同一准备周期增量携带。", nameof(cycle));
        }

        // 在覆盖任何 frozen 诊断前，先以 live available/consumed/next-sequence 完整验证 delta。
        _ = CreateInventoryWithDelta(cycle.Delta);

        FrozenPreparationCollection = cycle.FrozenPreparationCollection;
        FrozenPreparationDelta = cycle.Delta;
        FaultDiagnostic = null;
    }

    /// <summary>
    /// 从相同权威牌区创建候选事务副本。
    /// </summary>
    /// <returns>列表可变但卡牌实例引用保持一致的规划快照。</returns>
    internal EnemyCardPlanningStateSnapshot CreatePlanningSnapshot() =>
        new(_drawPile, _currentCards, _discardPile);

    /// <summary>
    /// 从最终候选槽位结果和其余权威区域创建完整递归准备事务。
    /// </summary>
    /// <param name="snapshot">已经完成配方抽取的候选牌区副本。</param>
    /// <returns>后续素材、即时和回收选择均不会写回权威状态的独立事务。</returns>
    internal EnemyPreparedPlanningState CreatePreparedPlanningState(
        EnemyCardPlanningStateSnapshot snapshot,
        EnemyPreparedPreActionInventoryDelta preActionInventoryDelta)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(preActionInventoryDelta);
        EnemyCollectionInventory planningInventory = CreateInventoryWithDelta(preActionInventoryDelta);
        return new EnemyPreparedPlanningState(
            snapshot.DrawPile,
            snapshot.CurrentCards,
            _retainedCards,
            snapshot.DiscardPile,
            _exhaustPile,
            planningInventory,
            NextGeneratedCardSequence);
    }

    /// <summary>
    /// 原子提交一个候选的牌区结果、指标与冻结行动。
    /// </summary>
    /// <param name="snapshot">从当前状态派生且已完成槽位抽取的事务副本。</param>
    /// <param name="action">与事务副本当前牌序一致的冻结行动。</param>
    internal void CommitPreparedAction(EnemyCardPlanningStateSnapshot snapshot, PreparedEnemyCardAction action)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(action);
        if (PreparedAction is not null || RuntimePhase != EnemyCardRuntimePhase.Idle)
        {
            throw new InvalidOperationException("已有冻结行动时不能再次提交候选。");
        }

        if (!ReferenceEquals(FrozenPreparationDelta, action.PreActionInventoryDelta))
        {
            throw new InvalidOperationException("冻结行动必须提交当前准备周期的同一库存增量对象。");
        }

        ValidatePreparedCommit(snapshot, action);
        EnemyCollectionInventorySnapshot inventorySnapshot =
            CreateInventoryWithDelta(action.PreActionInventoryDelta).CaptureSnapshot();
        PreparedOwnerStateSnapshot ownerBefore = CapturePreparedOwnerState();
        bool committed = CollectionInventory.TryCommitSnapshotAtomically(
            inventorySnapshot,
            () =>
            {
                ReplaceZone(_drawPile, snapshot.DrawPile);
                ReplaceZone(_currentCards, snapshot.CurrentCards);
                ReplaceZone(_discardPile, snapshot.DiscardPile);
                PreparedAction = action;
                LastMetric = action.Metric;
                RuntimePhase = EnemyCardRuntimePhase.Prepared;
            },
            rollbackOwner: () => RestorePreparedOwnerState(ownerBefore),
            notify: action.PreActionInventoryDelta.AddedAvailable.Count > 0,
            out string reason);
        if (!committed)
        {
            throw new InvalidOperationException($"准备库存增量事务预验证失败：{reason}");
        }
    }

    /// <summary>捕获 CommitPreparedAction 会修改的全部 owner 字段及其完整顺序。</summary>
    private PreparedOwnerStateSnapshot CapturePreparedOwnerState() =>
        new(
            _drawPile,
            _currentCards,
            _discardPile,
            PreparedAction,
            LastMetric,
            RuntimePhase);

    /// <summary>在 compound commit callback 失败时恢复全部 owner 字段，不触发通知。</summary>
    private void RestorePreparedOwnerState(PreparedOwnerStateSnapshot before)
    {
        ReplaceZone(_drawPile, before.DrawPile);
        ReplaceZone(_currentCards, before.CurrentCards);
        ReplaceZone(_discardPile, before.DiscardPile);
        PreparedAction = before.PreparedAction;
        LastMetric = before.LastMetric;
        RuntimePhase = before.RuntimePhase;
    }

    /// <summary>在 compound transaction 之前验证候选五区唯一性及行动/Current 的一致性。</summary>
    private void ValidatePreparedCommit(
        EnemyCardPlanningStateSnapshot snapshot,
        PreparedEnemyCardAction action)
    {
        BaseEnemyCard[] all = snapshot.DrawPile
            .Concat(snapshot.CurrentCards)
            .Concat(_retainedCards)
            .Concat(snapshot.DiscardPile)
            .Concat(_exhaustPile)
            .ToArray();
        if (all.Any(card => card is null) ||
            all.Distinct(ReferenceEqualityComparer.Instance).Count() != all.Length ||
            all.Select(card => card.InstanceKey).Distinct().Count() != all.Length)
        {
            throw new InvalidOperationException("准备候选五牌区违反唯一实例所有权不变量。");
        }

        if (!snapshot.CurrentCards.SequenceEqual(action.MetricCards, ReferenceEqualityComparer.Instance))
        {
            throw new InvalidOperationException("准备候选 Current 区与冻结行动指标牌顺序不一致。");
        }
    }

    /// <summary>
    /// 在独立库存克隆上验证并追加冻结增量，供候选解析和最终提交复用。
    /// </summary>
    private EnemyCollectionInventory CreateInventoryWithDelta(
        EnemyPreparedPreActionInventoryDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        EnemyCollectionInventorySnapshot current = CollectionInventory.CaptureSnapshot();
        List<EnemyCollectionInstance> available = current.Available.ToList();
        long nextSequence = current.NextSequence;
        foreach (EnemyCollectionInstance instance in delta.AddedAvailable)
        {
            if (instance.Sequence != nextSequence)
            {
                throw new InvalidOperationException(
                    $"准备收藏品实例 {instance.CollectionInstanceId} 的序号必须等于下一权威序号 {nextSequence}。");
            }

            available.Add(instance);
            nextSequence = checked(nextSequence + 1);
        }

        EnemyCollectionInventory clone = CollectionInventory.CreateTransactionalClone();
        EnemyCollectionInventorySnapshot updated = new(available, current.Consumed, nextSequence);
        if (!clone.TryApplySnapshot(updated, out string reason))
        {
            throw new InvalidOperationException($"准备库存增量无效：{reason}");
        }

        return clone;
    }

    /// <summary>
    /// 只在临时对象已经通过重连全量校验后恢复全部权威区域与运行阶段。
    /// </summary>
    /// <param name="zones">按五个规范牌区提供的完整实例顺序。</param>
    /// <param name="collectionSnapshot">已经重建定义引用的收藏品库存快照。</param>
    /// <param name="nextGeneratedCardSequence">下一张生成牌使用的单调序号。</param>
    /// <param name="lastMetric">上次成功提交的行动指标。</param>
    /// <param name="preparedAction">可选冻结行动。</param>
    /// <param name="runtimePhase">恢复后的高层运行阶段。</param>
    /// <param name="faultDiagnostic">故障态诊断；非故障态必须为空。</param>
    internal void RestoreValidatedRuntime(
        IReadOnlyDictionary<EnemyCardZone, IReadOnlyList<BaseEnemyCard>> zones,
        EnemyCollectionInventorySnapshot collectionSnapshot,
        long nextGeneratedCardSequence,
        EnemyActionMetric? lastMetric,
        PreparedEnemyCardAction? preparedAction,
        EnemyCardRuntimePhase runtimePhase,
        string? faultDiagnostic)
    {
        ArgumentNullException.ThrowIfNull(zones);
        foreach (EnemyCardZone zone in Enum.GetValues<EnemyCardZone>())
        {
            if (!zones.TryGetValue(zone, out IReadOnlyList<BaseEnemyCard>? cards) || cards is null)
            {
                throw new ArgumentException($"重连恢复缺少规范牌区 {zone}。", nameof(zones));
            }
        }

        if (nextGeneratedCardSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextGeneratedCardSequence));
        }

        BaseEnemyCard[] all = zones.Values.SelectMany(cards => cards).ToArray();
        if (all.Any(card => card is null) ||
            all.Select(card => card.InstanceKey).Distinct().Count() != all.Length ||
            all.Distinct(ReferenceEqualityComparer.Instance).Count() != all.Length)
        {
            throw new ArgumentException("重连恢复的五牌区包含空值或重复实例。", nameof(zones));
        }

        long maximumRuntimeId = all.Where(card => card.RuntimeInstanceId.HasValue)
            .Select(card => card.RuntimeInstanceId!.Value)
            .DefaultIfEmpty(-1)
            .Max();
        if (nextGeneratedCardSequence <= maximumRuntimeId)
        {
            throw new ArgumentException("下一生成牌序号没有越过现有运行时实例。", nameof(nextGeneratedCardSequence));
        }

        if (!CollectionInventory.TryApplySnapshot(collectionSnapshot, out string collectionReason))
        {
            throw new ArgumentException($"收藏品恢复快照无效：{collectionReason}", nameof(collectionSnapshot));
        }

        if (runtimePhase is EnemyCardRuntimePhase.Prepared or EnemyCardRuntimePhase.Executing && preparedAction is null)
        {
            throw new ArgumentException("准备或执行阶段必须携带冻结行动。", nameof(preparedAction));
        }

        if (runtimePhase == EnemyCardRuntimePhase.Faulted && string.IsNullOrWhiteSpace(faultDiagnostic))
        {
            throw new ArgumentException("故障阶段必须携带非空诊断。", nameof(faultDiagnostic));
        }

        if (runtimePhase != EnemyCardRuntimePhase.Faulted && !string.IsNullOrEmpty(faultDiagnostic))
        {
            throw new ArgumentException("非故障阶段不能携带故障诊断。", nameof(faultDiagnostic));
        }

        foreach (EnemyCardZone zone in Enum.GetValues<EnemyCardZone>())
        {
            List<BaseEnemyCard> target = GetMutableZone(zone);
            target.Clear();
            target.AddRange(zones[zone]);
        }

        NextGeneratedCardSequence = nextGeneratedCardSequence;
        LastMetric = lastMetric;
        PreparedAction = preparedAction;
        RuntimePhase = runtimePhase;
        FaultDiagnostic = faultDiagnostic;
        _immediateResolutionStack.Clear();
        AssertUniqueOwnership();
    }

    /// <summary>
    /// 通过唯一实例键定位所属牌区和实例对象。
    /// </summary>
    /// <param name="instanceKey">待查找实例键。</param>
    /// <returns>唯一来源牌区与实例。</returns>
    private (List<BaseEnemyCard> Zone, BaseEnemyCard Card) FindOwnedCard(EnemyCardInstanceKey instanceKey)
    {
        foreach (List<BaseEnemyCard> zone in EnumerateMutableZones())
        {
            BaseEnemyCard? card = zone.SingleOrDefault(candidate => candidate.InstanceKey == instanceKey);
            if (card is not null)
            {
                return (zone, card);
            }
        }

        throw new KeyNotFoundException($"当前战斗状态不拥有敌人卡牌实例 {instanceKey}。");
    }

    /// <summary>保留来源区中 Carry 实例的原对象与相对顺序。</summary>
    private static void CopyCarryCards(
        IEnumerable<BaseEnemyCard> source,
        ICollection<BaseEnemyCard> destination)
    {
        foreach (BaseEnemyCard card in source)
        {
            if (card.CarryAcrossPhase)
            {
                destination.Add(card);
            }
        }
    }

    /// <summary>只用权威随机源洗牌新阶段来源，不改变其他牌区或既有 Carry 顺序。</summary>
    private static void Shuffle(List<BaseEnemyCard> cards, IEnemyCardRandomSource random)
    {
        for (int index = cards.Count - 1; index > 0; index--)
        {
            int swapIndex = random.NextIndex(index + 1);
            (cards[index], cards[swapIndex]) = (cards[swapIndex], cards[index]);
        }
    }

    /// <summary>以候选区域的完整顺序替换权威后备列表，不触发卡牌生命周期。</summary>
    private static void ReplaceZone(List<BaseEnemyCard> destination, IEnumerable<BaseEnemyCard> source)
    {
        destination.Clear();
        destination.AddRange(source);
    }

    /// <summary>
    /// 取得指定牌区的唯一可变后备列表。
    /// </summary>
    /// <param name="zone">目标牌区。</param>
    /// <returns>仅供状态内部修改的列表。</returns>
    private List<BaseEnemyCard> GetMutableZone(EnemyCardZone zone) => zone switch
    {
        EnemyCardZone.Draw => _drawPile,
        EnemyCardZone.Current => _currentCards,
        EnemyCardZone.Retained => _retainedCards,
        EnemyCardZone.Discard => _discardPile,
        EnemyCardZone.Exhaust => _exhaustPile,
        _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, "未知敌人卡牌牌区。")
    };

    /// <summary>
    /// 按规范牌区顺序枚举全部可变后备列表。
    /// </summary>
    /// <returns>五个且仅五个权威牌区。</returns>
    private IEnumerable<List<BaseEnemyCard>> EnumerateMutableZones()
    {
        yield return _drawPile;
        yield return _currentCards;
        yield return _retainedCards;
        yield return _discardPile;
        yield return _exhaustPile;
    }

    /// <summary>
    /// 验证五牌区没有重复引用或重复实例键。
    /// </summary>
    private void AssertUniqueOwnership()
    {
        BaseEnemyCard[] all = EnumerateMutableZones().SelectMany(zone => zone).ToArray();
        if (all.Distinct(ReferenceEqualityComparer.Instance).Count() != all.Length ||
            all.Select(card => card.InstanceKey).Distinct().Count() != all.Length)
        {
            throw new InvalidOperationException("敌人卡牌五牌区违反唯一实例所有权不变量。");
        }
    }

    /// <summary>冻结 CommitPreparedAction owner 侧的完整 before-state，供异常 rollback 使用。</summary>
    private sealed class PreparedOwnerStateSnapshot
    {
        public PreparedOwnerStateSnapshot(
            IEnumerable<BaseEnemyCard> drawPile,
            IEnumerable<BaseEnemyCard> currentCards,
            IEnumerable<BaseEnemyCard> discardPile,
            PreparedEnemyCardAction? preparedAction,
            EnemyActionMetric? lastMetric,
            EnemyCardRuntimePhase runtimePhase)
        {
            DrawPile = Array.AsReadOnly(drawPile.ToArray());
            CurrentCards = Array.AsReadOnly(currentCards.ToArray());
            DiscardPile = Array.AsReadOnly(discardPile.ToArray());
            PreparedAction = preparedAction;
            LastMetric = lastMetric;
            RuntimePhase = runtimePhase;
        }

        public IReadOnlyList<BaseEnemyCard> DrawPile { get; }
        public IReadOnlyList<BaseEnemyCard> CurrentCards { get; }
        public IReadOnlyList<BaseEnemyCard> DiscardPile { get; }
        public PreparedEnemyCardAction? PreparedAction { get; }
        public EnemyActionMetric? LastMetric { get; }
        public EnemyCardRuntimePhase RuntimePhase { get; }
    }
}

/// <summary>
/// 保存一次候选评估专用的抽牌、当前和弃牌事务副本。
/// </summary>
internal sealed class EnemyCardPlanningStateSnapshot
{
    /// <summary>
    /// 从权威牌区复制候选起点。
    /// </summary>
    /// <param name="drawPile">当前抽牌堆。</param>
    /// <param name="currentCards">当前指标牌区。</param>
    /// <param name="discardPile">当前弃牌堆。</param>
    public EnemyCardPlanningStateSnapshot(
        IEnumerable<BaseEnemyCard> drawPile,
        IEnumerable<BaseEnemyCard> currentCards,
        IEnumerable<BaseEnemyCard> discardPile)
    {
        DrawPile = drawPile.ToList();
        CurrentCards = currentCards.ToList();
        DiscardPile = discardPile.ToList();
    }

    /// <summary>获取候选事务的可变抽牌堆。</summary>
    public List<BaseEnemyCard> DrawPile { get; }

    /// <summary>获取候选事务的可变当前牌区。</summary>
    public List<BaseEnemyCard> CurrentCards { get; }

    /// <summary>获取候选事务的可变弃牌堆。</summary>
    public List<BaseEnemyCard> DiscardPile { get; }
}
