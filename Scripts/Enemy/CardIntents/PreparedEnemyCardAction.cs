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
    public EnemyCandidatePlanningException(
        string message,
        IEnumerable<EnemyCandidateRejection>? rejections = null,
        Exception? innerException = null) : base(message, innerException)
    {
        Rejections = Array.AsReadOnly((rejections ?? []).ToArray());
    }

    public IReadOnlyList<EnemyCandidateRejection> Rejections { get; }
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
/// 表示最终完整候选的提交方式。
/// </summary>
public enum EnemyCandidateCommitMode
{
    WithinLocks,
    ForcedOverLock
}

/// <summary>描述一个未写入权威状态的候选为何被拒绝。</summary>
public enum EnemyCandidateRejectionReason
{
    StaticOverLock,
    FullOverLock,
    IncompleteProjection,
    PlanningFault
}

/// <summary>保存一次候选拒绝的顺序、分类和可同步诊断。</summary>
public sealed record EnemyCandidateRejection
{
    public EnemyCandidateRejection(
        int attempt,
        EnemyCandidateRejectionReason reason,
        string diagnostic)
    {
        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }

        if (!Enum.IsDefined(reason) || string.IsNullOrWhiteSpace(diagnostic))
        {
            throw new ArgumentException("候选拒绝必须具有有效分类和非空诊断。", nameof(diagnostic));
        }

        Attempt = attempt;
        Reason = reason;
        Diagnostic = diagnostic;
    }

    public int Attempt { get; }
    public EnemyCandidateRejectionReason Reason { get; }
    public string Diagnostic { get; }
}

/// <summary>
/// 保存最终候选的静态分、完整风险、两层锁、拒绝历史与投影完整性。
/// </summary>
public sealed record EnemySoftLockDiagnostic
{
    public EnemySoftLockDiagnostic(
        EnemyCardScore staticScore,
        EnemyActionRiskScore fullScore,
        EnemySoftLockLimits staticLocks,
        EnemySoftLockLimits fullLocks,
        int candidateAttemptCount,
        IEnumerable<EnemyCandidateRejection>? rejections,
        EnemyCandidateCommitMode commitMode,
        bool projectionIsComplete,
        IEnumerable<string>? projectionDiagnostics = null)
    {
        StaticScore = staticScore ?? throw new ArgumentNullException(nameof(staticScore));
        FullScore = fullScore ?? throw new ArgumentNullException(nameof(fullScore));
        StaticLocks = staticLocks ?? throw new ArgumentNullException(nameof(staticLocks));
        FullLocks = fullLocks ?? throw new ArgumentNullException(nameof(fullLocks));
        if (candidateAttemptCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateAttemptCount));
        }

        if (!Enum.IsDefined(commitMode))
        {
            throw new ArgumentOutOfRangeException(nameof(commitMode));
        }

        EnemyCandidateRejection[] copiedRejections = (rejections ?? []).ToArray();
        if (copiedRejections.Any(rejection => rejection is null || rejection.Attempt >= candidateAttemptCount))
        {
            throw new ArgumentException("拒绝历史只能包含最终提交尝试之前的有效候选。", nameof(rejections));
        }

        string[] copiedProjectionDiagnostics = (projectionDiagnostics ?? []).ToArray();
        if (copiedProjectionDiagnostics.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("投影诊断不能包含空文本。", nameof(projectionDiagnostics));
        }

        if (commitMode == EnemyCandidateCommitMode.ForcedOverLock && !projectionIsComplete)
        {
            throw new ArgumentException("投影不完整的候选不能使用 ForcedOverLock。", nameof(projectionIsComplete));
        }

        CandidateAttemptCount = candidateAttemptCount;
        Rejections = Array.AsReadOnly(copiedRejections);
        CommitMode = commitMode;
        ProjectionIsComplete = projectionIsComplete;
        ProjectionDiagnostics = Array.AsReadOnly(copiedProjectionDiagnostics);
    }

    /// <summary>兼容旧测试与 schema v2 恢复；Task 8 会迁移为完整 DTO。</summary>
    public EnemySoftLockDiagnostic(
        EnemyCardScore score,
        decimal attackLock,
        decimal totalScoreLock,
        int candidateAttemptCount,
        int rejectedCandidateCount,
        bool wasForcedByAttemptLimit)
        : this(
            score,
            new EnemyActionRiskScore(
                score.Attack,
                decimal.Zero,
                decimal.Zero,
                Math.Max(decimal.Zero, score.Total - score.Attack)),
            new EnemySoftLockLimits(attackLock, totalScoreLock),
            new EnemySoftLockLimits(attackLock, totalScoreLock),
            candidateAttemptCount,
            Enumerable.Range(1, Math.Max(0, rejectedCandidateCount))
                .Select(attempt => new EnemyCandidateRejection(
                    attempt,
                    EnemyCandidateRejectionReason.PlanningFault,
                    "由旧版软锁诊断恢复的候选拒绝。")),
            wasForcedByAttemptLimit
                ? EnemyCandidateCommitMode.ForcedOverLock
                : EnemyCandidateCommitMode.WithinLocks,
            projectionIsComplete: true)
    {
    }

    public EnemyCardScore StaticScore { get; }
    public EnemyActionRiskScore FullScore { get; }
    public EnemySoftLockLimits StaticLocks { get; }
    public EnemySoftLockLimits FullLocks { get; }
    public int CandidateAttemptCount { get; }
    public IReadOnlyList<EnemyCandidateRejection> Rejections { get; }
    public EnemyCandidateCommitMode CommitMode { get; }
    public bool ProjectionIsComplete { get; }
    public IReadOnlyList<string> ProjectionDiagnostics { get; }

    public EnemyCardScore Score => StaticScore;
    public decimal AttackLock => StaticLocks.Attack;
    public decimal TotalScoreLock => StaticLocks.Total;
    public int RejectedCandidateCount => Rejections.Count;
    public bool WasForcedByAttemptLimit => CommitMode == EnemyCandidateCommitMode.ForcedOverLock;
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
        IEnumerable<EnemyFrozenEffectiveCardState>? effectiveCardStates = null,
        EnemyCardPhase phase = EnemyCardPhase.None)
    {
        ArgumentNullException.ThrowIfNull(retainedPrefix);
        ArgumentNullException.ThrowIfNull(metricCards);
        ArgumentNullException.ThrowIfNull(sources);
        Metric = metric;
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        Phase = phase;
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

    /// <summary>获取冻结时的权威阶段；评分与提交都不得读取 PendingPhase。</summary>
    public EnemyCardPhase Phase { get; }

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
