namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存准备行动公开前必须追加到可用区的冻结收藏品实例。
/// </summary>
public sealed record EnemyPreparedPreActionInventoryDelta
{
    public EnemyPreparedPreActionInventoryDelta(IReadOnlyList<EnemyCollectionInstance> addedAvailable)
    {
        ArgumentNullException.ThrowIfNull(addedAvailable);
        EnemyCollectionInstance[] copied = addedAvailable.ToArray();
        if (copied.Any(instance => instance is null) ||
            copied.Select(instance => instance.CollectionInstanceId)
                .Distinct(StringComparer.Ordinal).Count() != copied.Length)
        {
            throw new ArgumentException("准备库存增量不能包含空值或重复收藏品实例。", nameof(addedAvailable));
        }

        AddedAvailable = Array.AsReadOnly(copied);
    }

    public static EnemyPreparedPreActionInventoryDelta Empty { get; } = new([]);

    public IReadOnlyList<EnemyCollectionInstance> AddedAvailable { get; }
}

/// <summary>
/// 保存一次候选循环共享的收藏品选择与对应库存增量。
/// </summary>
public sealed class EnemyPreparationCycle
{
    public EnemyPreparationCycle(
        EnemyCollectionInstance? frozenPreparationCollection,
        EnemyPreparedPreActionInventoryDelta delta)
    {
        FrozenPreparationCollection = frozenPreparationCollection;
        Delta = delta ?? throw new ArgumentNullException(nameof(delta));
    }

    public EnemyCollectionInstance? FrozenPreparationCollection { get; }
    public EnemyPreparedPreActionInventoryDelta Delta { get; }
}

/// <summary>
/// 表示候选循环在上限内始终无法构造完整配方的确定性结构故障。
/// </summary>
public sealed class EnemyCandidatePlanningException : InvalidOperationException
{
    public EnemyCandidatePlanningException(string message) : base(message)
    {
    }
}

/// <summary>
/// 冻结单张来源牌的最大尝试次数、成功递归单元和截断边界。
/// </summary>
public sealed class PreparedEnemyCardSource
{
    /// <summary>
    /// 创建不可变来源牌计划。
    /// </summary>
    /// <param name="sourceCard">具有唯一战斗身份的来源实例。</param>
    /// <param name="maximumAttempts">冻结的一加 ReplayCount 最大尝试次数。</param>
    /// <param name="units">逐次成功重放的独立递归冻结单元。</param>
    /// <param name="truncationAttemptIndex">已知素材不足时的首个截断尝试索引；空值表示准备时未截断。</param>
    public PreparedEnemyCardSource(
        BaseEnemyCard sourceCard,
        int maximumAttempts,
        IEnumerable<PreparedEnemyCardUnitPlan>? units = null,
        int? truncationAttemptIndex = null)
    {
        SourceCard = sourceCard ?? throw new ArgumentNullException(nameof(sourceCard));
        if (maximumAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts), "来源牌最大尝试次数必须大于零。");
        }

        if (truncationAttemptIndex is < 0 || truncationAttemptIndex > maximumAttempts)
        {
            throw new ArgumentOutOfRangeException(nameof(truncationAttemptIndex), "截断索引必须落在来源尝试范围内。");
        }

        PreparedEnemyCardUnitPlan[] copiedUnits = (units ?? []).ToArray();
        if (copiedUnits.Any(unit => unit is null ||
                                    unit.RootSourceKey != SourceKey ||
                                    unit.ExecutingCardKey != SourceKey ||
                                    unit.ExecutingCardId != SourceCard.CardId ||
                                    unit.Mode != EnemyPreparedExecutionMode.Normal))
        {
            throw new ArgumentException("公开来源的成功单元必须匹配来源身份并采用完整执行模式。", nameof(units));
        }

        if (!copiedUnits.Select(unit => unit.ReplayIndex).SequenceEqual(Enumerable.Range(0, copiedUnits.Length)))
        {
            throw new ArgumentException("公开来源成功单元的重放索引必须从零连续递增。", nameof(units));
        }

        if (copiedUnits.Length > maximumAttempts ||
            truncationAttemptIndex is int truncation &&
            (copiedUnits.Length != truncation || truncation >= maximumAttempts))
        {
            throw new ArgumentException("成功单元数量必须与最大尝试及首个截断索引一致。", nameof(units));
        }

        if (truncationAttemptIndex is null && copiedUnits.Length != maximumAttempts)
        {
            throw new ArgumentException("未截断来源必须为每次最大尝试携带一个成功单元。", nameof(units));
        }

        MaximumAttempts = maximumAttempts;
        Units = Array.AsReadOnly(copiedUnits);
        TruncationAttemptIndex = truncationAttemptIndex;
    }

    /// <summary>获取来源牌实例。</summary>
    public BaseEnemyCard SourceCard { get; }

    /// <summary>获取来源实例唯一键。</summary>
    public EnemyCardInstanceKey SourceKey => SourceCard.InstanceKey;

    /// <summary>获取准备时冻结的最大尝试次数。</summary>
    public int MaximumAttempts { get; }

    /// <summary>获取逐次成功重放的独立递归冻结单元。</summary>
    public IReadOnlyList<PreparedEnemyCardUnitPlan> Units { get; }

    /// <summary>获取已知首个截断尝试索引。</summary>
    public int? TruncationAttemptIndex { get; }
}

/// <summary>
/// 保存准备时双软锁的输入、尝试次数、拒绝次数与强制提交原因。
/// </summary>
public sealed record EnemySoftLockDiagnostic
{
    /// <summary>
    /// 创建不可变软锁诊断。
    /// </summary>
    /// <param name="score">最终提交候选的一次本体评分。</param>
    /// <param name="attackLock">准备时攻击软锁。</param>
    /// <param name="totalScoreLock">准备时总评分软锁。</param>
    /// <param name="candidateAttemptCount">实际评估候选次数。</param>
    /// <param name="rejectedCandidateCount">未提交候选数量。</param>
    /// <param name="wasForcedByAttemptLimit">最终候选是否因次数上限强制提交。</param>
    public EnemySoftLockDiagnostic(
        EnemyCardScore score,
        decimal attackLock,
        decimal totalScoreLock,
        int candidateAttemptCount,
        int rejectedCandidateCount,
        bool wasForcedByAttemptLimit)
    {
        Score = score ?? throw new ArgumentNullException(nameof(score));
        AttackLock = attackLock;
        TotalScoreLock = totalScoreLock;
        CandidateAttemptCount = candidateAttemptCount;
        RejectedCandidateCount = rejectedCandidateCount;
        WasForcedByAttemptLimit = wasForcedByAttemptLimit;
    }

    /// <summary>获取最终候选准备时评分。</summary>
    public EnemyCardScore Score { get; }

    /// <summary>获取准备时攻击软锁。</summary>
    public decimal AttackLock { get; }

    /// <summary>获取准备时总评分软锁。</summary>
    public decimal TotalScoreLock { get; }

    /// <summary>获取实际评估候选次数。</summary>
    public int CandidateAttemptCount { get; }

    /// <summary>获取未提交候选数量。</summary>
    public int RejectedCandidateCount { get; }

    /// <summary>获取是否因达到尝试上限而强制提交。</summary>
    public bool WasForcedByAttemptLimit { get; }
}

/// <summary>
/// 冻结已公开行动的指标、保留前缀、指标牌、逐来源计划及准备时软锁诊断。
/// </summary>
public sealed class PreparedEnemyCardAction
{
    /// <summary>
    /// 创建不可变准备行动。
    /// </summary>
    /// <param name="metric">本次提交的行动指标。</param>
    /// <param name="retainedPrefix">先于指标牌执行且不参与软锁的保留实例。</param>
    /// <param name="metricCards">按槽位顺序冻结的指标实例。</param>
    /// <param name="sources">按深度优先来源顺序冻结的逐来源计划。</param>
    /// <param name="softLockDiagnostic">准备时评分与候选诊断。</param>
    /// <param name="preActionInventoryDelta">与行动原子提交的准备前库存增量。</param>
    public PreparedEnemyCardAction(
        EnemyActionMetric metric,
        IEnumerable<BaseEnemyCard> retainedPrefix,
        IEnumerable<BaseEnemyCard> metricCards,
        IEnumerable<PreparedEnemyCardSource> sources,
        EnemySoftLockDiagnostic softLockDiagnostic,
        EnemyPreparedPreActionInventoryDelta? preActionInventoryDelta = null,
        IEnumerable<EnemyFrozenEffectiveCardState>? effectiveCardStates = null)
    {
        ArgumentNullException.ThrowIfNull(retainedPrefix);
        ArgumentNullException.ThrowIfNull(metricCards);
        ArgumentNullException.ThrowIfNull(sources);
        Metric = metric;
        RetainedPrefix = Array.AsReadOnly(retainedPrefix.ToArray());
        MetricCards = Array.AsReadOnly(metricCards.ToArray());
        Sources = Array.AsReadOnly(sources.ToArray());
        SoftLockDiagnostic = softLockDiagnostic ?? throw new ArgumentNullException(nameof(softLockDiagnostic));
        PreActionInventoryDelta = preActionInventoryDelta ?? EnemyPreparedPreActionInventoryDelta.Empty;
        EnemyFrozenEffectiveCardState[] effectiveStates = (effectiveCardStates ?? []).ToArray();
        if (effectiveStates.Any(state => state is null) ||
            effectiveStates.Select(state => state.ExecutingCardInstanceKey).Distinct().Count() != effectiveStates.Length)
        {
            throw new ArgumentException("冻结有效牌状态不能包含空值或重复实例键。", nameof(effectiveCardStates));
        }

        EffectiveCardStates = new System.Collections.ObjectModel.ReadOnlyDictionary<
            EnemyCardInstanceKey,
            EnemyFrozenEffectiveCardState>(effectiveStates.ToDictionary(state => state.ExecutingCardInstanceKey));
        BaseEnemyCard[] ordered = RetainedPrefix.Concat(MetricCards).ToArray();
        if (ordered.Select(card => card.InstanceKey).Distinct().Count() != ordered.Length)
        {
            throw new ArgumentException("冻结行动的保留前缀与指标牌不能复用同一实例。");
        }

        if (!Sources.Select(source => source.SourceKey).SequenceEqual(ordered.Select(card => card.InstanceKey)))
        {
            throw new ArgumentException("逐来源计划必须与保留前缀及指标牌的执行顺序完全一致。", nameof(sources));
        }

        if (EffectiveCardStates.Count > 0)
        {
            EnemyCardInstanceKey[] plannedKeys = Sources
                .SelectMany(source => source.Units)
                .SelectMany(EnumerateUnitTree)
                .Select(unit => unit.ExecutingCardKey)
                .Distinct()
                .ToArray();
            if (plannedKeys.Any(key => !EffectiveCardStates.TryGetValue(key, out EnemyFrozenEffectiveCardState? state) ||
                                       state.ExecutingCardInstanceKey != key ||
                                       !state.WasCounted))
            {
                throw new ArgumentException("每个成功冻结单元都必须具有已计数的逐实例有效牌状态。", nameof(effectiveCardStates));
            }
        }
    }

    /// <summary>获取本次提交的行动指标。</summary>
    public EnemyActionMetric Metric { get; }

    /// <summary>获取不参与软锁的冻结保留前缀。</summary>
    public IReadOnlyList<BaseEnemyCard> RetainedPrefix { get; }

    /// <summary>获取按配方槽位顺序冻结的指标牌。</summary>
    public IReadOnlyList<BaseEnemyCard> MetricCards { get; }

    /// <summary>获取按实际来源执行顺序冻结的逐来源计划。</summary>
    public IReadOnlyList<PreparedEnemyCardSource> Sources { get; }

    /// <summary>获取准备时固定且不会随实时投影改变的软锁诊断。</summary>
    public EnemySoftLockDiagnostic SoftLockDiagnostic { get; }

    /// <summary>获取与本行动一起提交且只追加一次的准备前库存增量。</summary>
    public EnemyPreparedPreActionInventoryDelta PreActionInventoryDelta { get; }

    /// <summary>获取按真实执行实例键索引的冻结 N/X 元数据。</summary>
    public IReadOnlyDictionary<EnemyCardInstanceKey, EnemyFrozenEffectiveCardState> EffectiveCardStates { get; }

    /// <summary>获取兼容候选调用方的当前冻结行动自身。</summary>
    public PreparedEnemyCardAction Candidate => this;

    private static IEnumerable<PreparedEnemyCardUnitPlan> EnumerateUnitTree(PreparedEnemyCardUnitPlan unit)
    {
        yield return unit;
        foreach (PreparedEnemyResolutionStep step in unit.OrderedSteps)
        {
            IEnumerable<PreparedEnemyCardUnitPlan> children = step switch
            {
                PreparedConsumedCardStep { ControlledChild: not null } consumed => [consumed.ControlledChild],
                PreparedConsumedCollectionStep collection => collection.Children.SelectMany(EnumerateStepUnits),
                PreparedComposeResultStep compose =>
                    (compose.ImmediateChild is null
                        ? Enumerable.Empty<PreparedEnemyCardUnitPlan>()
                        : new[] { compose.ImmediateChild })
                    .Concat(compose.AdditionalReplayUnits),
                PreparedImmediateCardStep immediate => [immediate.Child, .. immediate.AdditionalReplayUnits],
                PreparedRecoveryStep { ImmediateCardChild: not null } recovery =>
                    [recovery.ImmediateCardChild, .. recovery.AdditionalReplayUnits],
                _ => []
            };
            foreach (PreparedEnemyCardUnitPlan child in children.SelectMany(EnumerateUnitTree))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<PreparedEnemyCardUnitPlan> EnumerateStepUnits(PreparedEnemyResolutionStep step) =>
        step switch
        {
            PreparedConsumedCardStep { ControlledChild: not null } consumed => [consumed.ControlledChild],
            PreparedConsumedCollectionStep collection => collection.Children.SelectMany(EnumerateStepUnits),
            PreparedComposeResultStep compose =>
                (compose.ImmediateChild is null
                    ? Enumerable.Empty<PreparedEnemyCardUnitPlan>()
                    : new[] { compose.ImmediateChild })
                .Concat(compose.AdditionalReplayUnits),
            PreparedImmediateCardStep immediate => [immediate.Child, .. immediate.AdditionalReplayUnits],
            PreparedRecoveryStep { ImmediateCardChild: not null } recovery =>
                [recovery.ImmediateCardChild, .. recovery.AdditionalReplayUnits],
            _ => []
        };
}
