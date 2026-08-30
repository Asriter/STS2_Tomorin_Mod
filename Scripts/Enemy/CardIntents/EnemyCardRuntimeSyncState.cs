using MegaCrit.Sts2.Core.Entities.Cards;
using STS2_Tomorin_Mod.Enemy.CardIntents.Test;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存一张敌人卡牌在当前版本重连协议中的稳定身份和可变重放状态。
/// </summary>
public sealed record EnemyCardRuntimeCardState
{
    /// <summary>获取或初始化稳定卡牌定义标识。</summary>
    public string CardId { get; init; } = string.Empty;

    /// <summary>获取或初始化跨五牌区唯一的实例键。</summary>
    public string InstanceKey { get; init; } = string.Empty;

    /// <summary>获取或初始化初始模板槽位；生成牌为空。</summary>
    public int? TemplateSlot { get; init; }

    /// <summary>获取或初始化生成牌序号；初始牌为空。</summary>
    public long? RuntimeInstanceId { get; init; }

    /// <summary>获取或初始化已持久化的额外重放次数。</summary>
    public int ReplayCount { get; init; }

    /// <summary>获取或初始化实例最初创建时的内容阶段。</summary>
    public EnemyCardPhase SourcePhase { get; init; }

    /// <summary>获取或初始化定义是否允许跨阶段保留；恢复时必须与目录定义一致。</summary>
    public bool CarryAcrossPhase { get; init; }
}

/// <summary>
/// 保存一件收藏品在可用区或已消耗区中的稳定定义与实例身份。
/// </summary>
public sealed record EnemyCollectionRuntimeState
{
    /// <summary>获取或初始化收藏品稳定定义标识。</summary>
    public string CollectionId { get; init; } = string.Empty;

    /// <summary>获取或初始化战斗内单调实例序号。</summary>
    public long Sequence { get; init; }

    /// <summary>获取或初始化由定义与序号派生的稳定实例标识。</summary>
    public string CollectionInstanceId { get; init; } = string.Empty;
}

/// <summary>保存尚未或已经随冻结行动提交的准备前收藏品增量。</summary>
public sealed record EnemyPreparedPreActionInventoryDeltaSyncState
{
    public IReadOnlyList<EnemyCollectionRuntimeState> AddedAvailable { get; init; } = [];
}

/// <summary>保存一个实际执行实例冻结的 N、X、倍率与唯一计数状态。</summary>
public sealed record EnemyFrozenEffectiveCardSyncState
{
    public string ExecutingCardInstanceKey { get; init; } = string.Empty;
    public int FrozenN { get; init; }
    public int? FrozenX { get; init; }
    public int Multiplier { get; init; }
    public bool WasCounted { get; init; }
}

/// <summary>保存静态候选分与对应门槛。</summary>
public sealed record EnemyStaticScoreSyncState
{
    public decimal Attack { get; init; }
    public decimal Total { get; init; }
    public decimal AttackLock { get; init; }
    public decimal TotalLock { get; init; }
}

/// <summary>保存完整行动风险四分量与对应门槛。</summary>
public sealed record EnemyActionRiskScoreSyncState
{
    public decimal AttackRisk { get; init; }
    public decimal SurvivalRisk { get; init; }
    public decimal EngineRisk { get; init; }
    public decimal DeferredRisk { get; init; }
    public decimal AttackLock { get; init; }
    public decimal TotalLock { get; init; }
}

/// <summary>保存一次候选拒绝的稳定分类与诊断。</summary>
public sealed record EnemyCandidateRejectionSyncState
{
    public int Attempt { get; init; }
    public EnemyCandidateRejectionReason Reason { get; init; }
    public string Diagnostic { get; init; } = string.Empty;
}

/// <summary>
/// 保存冻结素材绑定，传输时只引用卡牌或收藏品的稳定实例身份。
/// </summary>
public sealed record EnemyMaterialBindingSyncState
{
    /// <summary>获取或初始化需求在请求中的有序索引。</summary>
    public int RequirementIndex { get; init; }

    /// <summary>获取或初始化需求牌类型；任意类型需求为空。</summary>
    public CardType? RequiredCardType { get; init; }

    /// <summary>获取或初始化本项需求必须满足的数量。</summary>
    public int RequiredCount { get; init; }

    /// <summary>获取或初始化素材来源区域。</summary>
    public EnemyMaterialSource Source { get; init; }

    /// <summary>获取或初始化手牌素材实例键。</summary>
    public string? CardInstanceKey { get; init; }

    /// <summary>获取或初始化收藏品素材实例标识。</summary>
    public string? CollectionInstanceId { get; init; }

    /// <summary>获取或初始化候选参与资格判断的牌类型。</summary>
    public CardType CandidateCardType { get; init; }

    /// <summary>获取或初始化手牌素材是否具有灵感。</summary>
    public bool IsInspiration { get; init; }

    /// <summary>获取或初始化素材是否具有灵光。</summary>
    public bool IsEpiphany { get; init; }
}

/// <summary>
/// 保存一次完整或失败的冻结素材预留。
/// </summary>
public sealed record EnemyMaterialReservationSyncState
{
    /// <summary>获取或初始化预留是否完整覆盖请求。</summary>
    public bool IsComplete { get; init; }

    /// <summary>获取或初始化按需求顺序排列的冻结绑定。</summary>
    public IReadOnlyList<EnemyMaterialBindingSyncState> Bindings { get; init; } = [];
}

/// <summary>
/// 指定递归冻结步骤在传输结构中的显式种类。
/// </summary>
public enum PreparedEnemyResolutionStepSyncKind
{
    /// <summary>直接效果程序列表。</summary>
    DirectEffects,
    /// <summary>卡牌素材消费。</summary>
    ConsumedCard,
    /// <summary>收藏品素材消费。</summary>
    ConsumedCollection,
    /// <summary>收藏品生成。</summary>
    GeneratedCollection,
    /// <summary>作词结果。</summary>
    ComposeResult,
    /// <summary>即时抽牌。</summary>
    ImmediateCard,
    /// <summary>消耗区回收。</summary>
    Recovery
}

/// <summary>保存直接效果步骤的稳定程序标识。</summary>
public sealed record PreparedDirectEffectsStepSyncState
{
    /// <summary>获取或初始化按定义顺序排列的程序标识。</summary>
    public IReadOnlyList<string> EffectProgramIds { get; init; } = [];
}

/// <summary>保存卡牌素材消费及其可选灵感子单元。</summary>
public sealed record PreparedConsumedCardStepSyncState
{
    /// <summary>获取或初始化素材稳定实例键。</summary>
    public string MaterialInstanceKey { get; init; } = string.Empty;
    /// <summary>获取或初始化受控直接子单元。</summary>
    public PreparedEnemyCardUnitPlanSyncState? ControlledChild { get; init; }
}

/// <summary>保存收藏品素材消费及其有序效果子步骤。</summary>
public sealed record PreparedConsumedCollectionStepSyncState
{
    /// <summary>获取或初始化收藏品实例标识。</summary>
    public string CollectionInstanceId { get; init; } = string.Empty;
    /// <summary>获取或初始化收藏品定义标识。</summary>
    public string CollectionId { get; init; } = string.Empty;
    /// <summary>获取或初始化收藏品效果子步骤。</summary>
    public IReadOnlyList<PreparedEnemyResolutionStepSyncState> Children { get; init; } = [];
}

/// <summary>保存收藏品生成定义与预计序号。</summary>
public sealed record PreparedGeneratedCollectionStepSyncState
{
    /// <summary>获取或初始化收藏品定义标识。</summary>
    public string CollectionId { get; init; } = string.Empty;
    /// <summary>获取或初始化预计下一收藏品序号。</summary>
    public long ExpectedSequence { get; init; }
}

/// <summary>保存作词结果身份、时机与可选即时子单元。</summary>
public sealed record PreparedComposeResultStepSyncState
{
    /// <summary>获取或初始化结果卡牌定义标识。</summary>
    public string ResultCardId { get; init; } = string.Empty;
    /// <summary>获取或初始化结果卡牌实例键。</summary>
    public string ResultInstanceKey { get; init; } = string.Empty;
    /// <summary>获取或初始化结果加入时机。</summary>
    public EnemyCardTokenTiming Timing { get; init; }
    /// <summary>获取或初始化是否增加现有实例重放。</summary>
    public bool IncreasesExistingReplay { get; init; }
    /// <summary>获取或初始化新生成即时结果子单元。</summary>
    public PreparedEnemyCardUnitPlanSyncState? ImmediateChild { get; init; }
    /// <summary>获取或初始化首单元后的连续附加重放单元。</summary>
    public IReadOnlyList<PreparedEnemyCardUnitPlanSyncState> AdditionalReplayUnits { get; init; } = [];
}

/// <summary>保存准备阶段选中的即时抽牌及其递归子单元。</summary>
public sealed record PreparedImmediateCardStepSyncState
{
    /// <summary>获取或初始化被选卡牌实例键。</summary>
    public string SelectedCardKey { get; init; } = string.Empty;
    /// <summary>获取或初始化被选卡牌递归子单元。</summary>
    public PreparedEnemyCardUnitPlanSyncState? Child { get; init; }
    /// <summary>获取或初始化首单元后的连续附加重放单元。</summary>
    public IReadOnlyList<PreparedEnemyCardUnitPlanSyncState> AdditionalReplayUnits { get; init; } = [];
}

/// <summary>保存准备阶段选中的回收对象及可选卡牌子单元。</summary>
public sealed record PreparedRecoveryStepSyncState
{
    /// <summary>获取或初始化回收对象种类。</summary>
    public EnemyPreparedRecoveryKind Kind { get; init; }
    /// <summary>获取或初始化稳定实例标识。</summary>
    public string SelectedInstanceId { get; init; } = string.Empty;
    /// <summary>获取或初始化回收卡牌即时子单元。</summary>
    public PreparedEnemyCardUnitPlanSyncState? ImmediateCardChild { get; init; }
    /// <summary>获取或初始化首单元后的连续附加重放单元。</summary>
    public IReadOnlyList<PreparedEnemyCardUnitPlanSyncState> AdditionalReplayUnits { get; init; } = [];
}

/// <summary>
/// 保存一个显式种类及恰好一个对应步骤载荷。
/// </summary>
public sealed record PreparedEnemyResolutionStepSyncState
{
    /// <summary>获取或初始化显式步骤种类。</summary>
    public PreparedEnemyResolutionStepSyncKind Kind { get; init; }
    /// <summary>获取或初始化直接效果载荷。</summary>
    public PreparedDirectEffectsStepSyncState? DirectEffects { get; init; }
    /// <summary>获取或初始化卡牌素材载荷。</summary>
    public PreparedConsumedCardStepSyncState? ConsumedCard { get; init; }
    /// <summary>获取或初始化收藏品素材载荷。</summary>
    public PreparedConsumedCollectionStepSyncState? ConsumedCollection { get; init; }
    /// <summary>获取或初始化收藏品生成载荷。</summary>
    public PreparedGeneratedCollectionStepSyncState? GeneratedCollection { get; init; }
    /// <summary>获取或初始化作词结果载荷。</summary>
    public PreparedComposeResultStepSyncState? ComposeResult { get; init; }
    /// <summary>获取或初始化即时抽牌载荷。</summary>
    public PreparedImmediateCardStepSyncState? ImmediateCard { get; init; }
    /// <summary>获取或初始化回收载荷。</summary>
    public PreparedRecoveryStepSyncState? Recovery { get; init; }
}

/// <summary>
/// 保存一个成功重放递归单元的全部稳定身份与有序步骤。
/// </summary>
public sealed record PreparedEnemyCardUnitPlanSyncState
{
    /// <summary>获取或初始化公开根来源实例键。</summary>
    public string RootSourceKey { get; init; } = string.Empty;
    /// <summary>获取或初始化实际执行实例键。</summary>
    public string ExecutingCardKey { get; init; } = string.Empty;
    /// <summary>获取或初始化实际执行定义标识。</summary>
    public string ExecutingCardId { get; init; } = string.Empty;
    /// <summary>获取或初始化实际执行牌重放索引。</summary>
    public int ReplayIndex { get; init; }
    /// <summary>获取或初始化完整或受控直接模式。</summary>
    public EnemyPreparedExecutionMode Mode { get; init; }
    /// <summary>获取或初始化准备时冻结的显式程序指纹。</summary>
    public string ResolutionProgramFingerprint { get; init; } = string.Empty;
    /// <summary>获取或初始化准备时冻结的出牌条件程序标识。</summary>
    public string PlayConditionProgramId { get; init; } = string.Empty;
    /// <summary>获取或初始化本单元完整素材预留。</summary>
    public IReadOnlyList<EnemyMaterialReservationSyncState> MaterialReservations { get; init; } = [];
    /// <summary>获取或初始化严格 DFS 顺序步骤。</summary>
    public IReadOnlyList<PreparedEnemyResolutionStepSyncState> OrderedSteps { get; init; } = [];
}

/// <summary>
/// 保存单张来源牌的冻结重放、成功单元与截断边界。
/// </summary>
public sealed record PreparedEnemyCardSourceSyncState
{
    /// <summary>获取或初始化来源牌稳定实例键。</summary>
    public string SourceInstanceKey { get; init; } = string.Empty;

    /// <summary>获取或初始化准备时冻结的最大尝试次数。</summary>
    public int MaximumAttempts { get; init; }

    /// <summary>获取或初始化逐次成功重放递归单元。</summary>
    public IReadOnlyList<PreparedEnemyCardUnitPlanSyncState> Units { get; init; } = [];

    /// <summary>获取或初始化首个已知素材不足的尝试索引。</summary>
    public int? TruncationAttemptIndex { get; init; }
}

/// <summary>
/// 保存冻结行动的软锁输入、评分与候选事务诊断。
/// </summary>
public sealed record EnemySoftLockDiagnosticSyncState
{
    /// <summary>获取或初始化直接攻击评分。</summary>
    public decimal AttackScore { get; init; }

    /// <summary>获取或初始化总评分。</summary>
    public decimal TotalScore { get; init; }

    /// <summary>获取或初始化攻击软锁。</summary>
    public decimal AttackLock { get; init; }

    /// <summary>获取或初始化总评分软锁。</summary>
    public decimal TotalScoreLock { get; init; }

    /// <summary>获取或初始化实际候选次数。</summary>
    public int CandidateAttemptCount { get; init; }

    /// <summary>获取或初始化被拒绝候选数量。</summary>
    public int RejectedCandidateCount { get; init; }

    /// <summary>获取或初始化最终候选是否由次数上限强制提交。</summary>
    public bool WasForcedByAttemptLimit { get; init; }

    /// <summary>获取或初始化最终提交前的完整候选拒绝历史。</summary>
    public IReadOnlyList<EnemyCandidateRejectionSyncState> Rejections { get; init; } = [];
}

/// <summary>
/// 保存一次已经公开且结构冻结的行动，不包含实时 Power 数值投影。
/// </summary>
public sealed record PreparedEnemyCardActionSyncState
{
    /// <summary>获取或初始化冻结行动所属的权威阶段。</summary>
    public EnemyCardPhase Phase { get; init; }

    /// <summary>获取或初始化本次行动指标。</summary>
    public EnemyActionMetric Metric { get; init; }

    /// <summary>获取或初始化强制执行的保留牌实例键前缀。</summary>
    public IReadOnlyList<string> RetainedPrefixKeys { get; init; } = [];

    /// <summary>获取或初始化参与本次指标的牌实例键。</summary>
    public IReadOnlyList<string> MetricCardKeys { get; init; } = [];

    /// <summary>获取或初始化与公开执行顺序一致的来源计划。</summary>
    public IReadOnlyList<PreparedEnemyCardSourceSyncState> Sources { get; init; } = [];

    /// <summary>获取或初始化准备时冻结的软锁诊断。</summary>
    public EnemySoftLockDiagnosticSyncState SoftLockDiagnostic { get; init; } = new();
}

/// <summary>
/// 表示当前唯一支持的主机权威敌人卡牌重连传输结构。
/// </summary>
public sealed record EnemyCardRuntimeSyncState
{
    /// <summary>获取当前且唯一接受的协议结构版本。</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>获取或初始化协议结构版本。</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>获取或初始化原版 MoveState 的稳定标识。</summary>
    public string StateId { get; init; } = string.Empty;

    /// <summary>获取或初始化牌组稳定标识。</summary>
    public string DeckId { get; init; } = string.Empty;

    /// <summary>获取或初始化抽牌堆实例顺序。</summary>
    public IReadOnlyList<EnemyCardRuntimeCardState> DrawPile { get; init; } = [];

    /// <summary>获取或初始化当前指标牌区实例顺序。</summary>
    public IReadOnlyList<EnemyCardRuntimeCardState> CurrentCards { get; init; } = [];

    /// <summary>获取或初始化保留牌区实例顺序。</summary>
    public IReadOnlyList<EnemyCardRuntimeCardState> RetainedCards { get; init; } = [];

    /// <summary>获取或初始化弃牌堆实例顺序。</summary>
    public IReadOnlyList<EnemyCardRuntimeCardState> DiscardPile { get; init; } = [];

    /// <summary>获取或初始化消耗堆实例顺序。</summary>
    public IReadOnlyList<EnemyCardRuntimeCardState> ExhaustPile { get; init; } = [];

    /// <summary>获取或初始化可用收藏品队列。</summary>
    public IReadOnlyList<EnemyCollectionRuntimeState> AvailableCollections { get; init; } = [];

    /// <summary>获取或初始化已消耗收藏品顺序。</summary>
    public IReadOnlyList<EnemyCollectionRuntimeState> ConsumedCollections { get; init; } = [];

    /// <summary>获取或初始化下一张生成牌使用的单调序号。</summary>
    public long NextGeneratedCardSequence { get; init; }

    /// <summary>获取或初始化下一件收藏品使用的单调序号。</summary>
    public long NextCollectionSequence { get; init; }

    /// <summary>获取或初始化上次成功提交的行动指标。</summary>
    public EnemyActionMetric? LastMetric { get; init; }

    /// <summary>获取或初始化当前高层运行阶段。</summary>
    public EnemyCardRuntimePhase RuntimePhase { get; init; }

    /// <summary>获取或初始化可选冻结行动。</summary>
    public PreparedEnemyCardActionSyncState? PreparedAction { get; init; }

    /// <summary>获取或初始化下一个尚未执行的安全原子步骤游标。</summary>
    public EnemyCardExecutionCursor? Cursor { get; init; }

    /// <summary>获取或初始化结构故障诊断。</summary>
    public string? FaultDiagnostic { get; init; }

    /// <summary>获取或初始化当前生效内容阶段。</summary>
    public EnemyCardPhase ActivePhase { get; init; }

    /// <summary>获取或初始化已经请求但尚未安全迁移的阶段。</summary>
    public EnemyCardPhase PendingPhase { get; init; }

    /// <summary>获取或初始化已成功应用的阶段迁移修订号。</summary>
    public long PhaseRevision { get; init; }

    /// <summary>获取或初始化候选循环唯一选择的准备收藏品。</summary>
    public EnemyCollectionRuntimeState? FrozenPreparationCollection { get; init; }

    /// <summary>获取或初始化候选循环冻结的准备前库存增量。</summary>
    public EnemyPreparedPreActionInventoryDeltaSyncState? FrozenPreparationDelta { get; init; }

    /// <summary>获取或初始化冻结行动已经完成计数的有效牌数量。</summary>
    public int CompletedEffectiveCardCount { get; init; }

    /// <summary>获取或初始化按真实执行实例冻结的 N/X 元数据。</summary>
    public IReadOnlyList<EnemyFrozenEffectiveCardSyncState> EffectiveCardStates { get; init; } = [];

    /// <summary>获取或初始化最终提交候选的静态分与静态锁。</summary>
    public EnemyStaticScoreSyncState? StaticScore { get; init; }

    /// <summary>获取或初始化最终提交候选的完整风险分与完整锁。</summary>
    public EnemyActionRiskScoreSyncState? FullScore { get; init; }

    /// <summary>获取或初始化最终候选的提交方式。</summary>
    public EnemyCandidateCommitMode? CommitMode { get; init; }

    /// <summary>获取或初始化门控使用的投影是否完整。</summary>
    public bool? ProjectionIsComplete { get; init; }

    /// <summary>获取或初始化完整投影的结构诊断。</summary>
    public IReadOnlyList<string> ProjectionDiagnostics { get; init; } = [];
}

/// <summary>
/// 捕获主机权威重连 DTO，并在临时状态中完成全量验证后返回可原子替换的状态。
/// </summary>
public static class EnemyCardRuntimeSynchronizer
{
    /// <summary>
    /// 在一个已提交原子步骤边界捕获当前权威状态，不包含实时数值投影。
    /// </summary>
    /// <param name="stateId">原版 MoveState 稳定标识。</param>
    /// <param name="state">主机权威五牌区与收藏品状态。</param>
    /// <param name="cursor">下一个尚未执行的安全边界游标。</param>
    /// <returns>不依赖对象地址的当前版本传输 DTO。</returns>
    public static EnemyCardRuntimeSyncState Capture(
        string stateId,
        EnemyCardCombatState state,
        EnemyCardExecutionCursor? cursor)
    {
        if (string.IsNullOrWhiteSpace(stateId))
        {
            throw new ArgumentException("重连状态标识不能为空。", nameof(stateId));
        }

        ArgumentNullException.ThrowIfNull(state);
        if (state.ImmediateResolutionStack.Count != 0)
        {
            throw new InvalidOperationException("只能在即时结算栈为空的已提交原子步骤边界捕获重连状态。");
        }

        if (cursor is not null && !cursor.IsValid())
        {
            throw new ArgumentException("重连游标没有停留在合法非负边界。", nameof(cursor));
        }

        PreparedEnemyCardAction? preparedAction = state.PreparedAction;
        EnemySoftLockDiagnostic? diagnostic = preparedAction?.SoftLockDiagnostic;
        return new EnemyCardRuntimeSyncState
        {
            StateId = stateId,
            DeckId = state.DeckId.Value,
            DrawPile = CaptureCards(state.DrawPile),
            CurrentCards = CaptureCards(state.CurrentCards),
            RetainedCards = CaptureCards(state.RetainedCards),
            DiscardPile = CaptureCards(state.DiscardPile),
            ExhaustPile = CaptureCards(state.ExhaustPile),
            AvailableCollections = CaptureCollections(state.CollectionInventory.Available),
            ConsumedCollections = CaptureCollections(state.CollectionInventory.Consumed),
            NextGeneratedCardSequence = state.NextGeneratedCardSequence,
            NextCollectionSequence = state.NextCollectionSequence,
            LastMetric = state.LastMetric,
            RuntimePhase = state.RuntimePhase,
            PreparedAction = CapturePreparedAction(preparedAction),
            Cursor = cursor?.Clone(),
            FaultDiagnostic = state.FaultDiagnostic,
            ActivePhase = state.ActivePhase,
            PendingPhase = state.PendingPhase,
            PhaseRevision = state.PhaseRevision,
            FrozenPreparationCollection = state.FrozenPreparationCollection is null
                ? null
                : CaptureCollection(state.FrozenPreparationCollection),
            FrozenPreparationDelta = state.FrozenPreparationDelta is null
                ? null
                : new EnemyPreparedPreActionInventoryDeltaSyncState
                {
                    AddedAvailable = CaptureCollections(state.FrozenPreparationDelta.AddedAvailable)
                },
            CompletedEffectiveCardCount = preparedAction?.EffectiveCardStates.Values.Count(item => item.WasCounted) ?? 0,
            EffectiveCardStates = preparedAction?.EffectiveCardStates.Values
                .OrderBy(item => item.ExecutingCardInstanceKey.Value, StringComparer.Ordinal)
                .Select(item => new EnemyFrozenEffectiveCardSyncState
                {
                    ExecutingCardInstanceKey = item.ExecutingCardInstanceKey.Value,
                    FrozenN = item.FrozenN,
                    FrozenX = item.FrozenX,
                    Multiplier = item.Multiplier,
                    WasCounted = item.WasCounted
                }).ToArray() ?? [],
            StaticScore = diagnostic is null
                ? null
                : new EnemyStaticScoreSyncState
                {
                    Attack = diagnostic.StaticScore.Attack,
                    Total = diagnostic.StaticScore.Total,
                    AttackLock = diagnostic.StaticLocks.Attack,
                    TotalLock = diagnostic.StaticLocks.Total
                },
            FullScore = diagnostic is null
                ? null
                : new EnemyActionRiskScoreSyncState
                {
                    AttackRisk = diagnostic.FullScore.AttackRisk,
                    SurvivalRisk = diagnostic.FullScore.SurvivalRisk,
                    EngineRisk = diagnostic.FullScore.EngineRisk,
                    DeferredRisk = diagnostic.FullScore.DeferredRisk,
                    AttackLock = diagnostic.FullLocks.Attack,
                    TotalLock = diagnostic.FullLocks.Total
                },
            CommitMode = diagnostic?.CommitMode,
            ProjectionIsComplete = diagnostic?.ProjectionIsComplete,
            ProjectionDiagnostics = diagnostic?.ProjectionDiagnostics.ToArray() ?? []
        };
    }

    /// <summary>
    /// 在临时对象中验证当前版本、定义、身份、区域互斥、素材引用和游标，再返回完整恢复状态。
    /// </summary>
    /// <param name="syncState">客户端收到的主机权威 DTO。</param>
    /// <param name="expectedDeckId">接收状态预期绑定的已注册牌组。</param>
    /// <param name="cardDefinitions">允许恢复的显式卡牌定义目录。</param>
    /// <param name="collectionCatalog">允许恢复的显式收藏品定义目录。</param>
    /// <param name="restoredState">成功时返回可一次性替换的临时状态。</param>
    /// <param name="restoredCursor">成功时返回独立的安全边界游标副本。</param>
    /// <param name="reason">拒绝时返回请求主机重发所需的结构诊断。</param>
    /// <returns>全量验证成功且没有部分应用时为真。</returns>
    public static bool TryRestore(
        EnemyCardRuntimeSyncState? syncState,
        EnemyCardDeckId expectedDeckId,
        IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> cardDefinitions,
        EnemyCollectionCatalog collectionCatalog,
        out EnemyCardCombatState? restoredState,
        out EnemyCardExecutionCursor? restoredCursor,
        out string reason)
    {
        restoredState = null;
        restoredCursor = null;
        reason = string.Empty;
        try
        {
            ArgumentNullException.ThrowIfNull(syncState);
            ArgumentNullException.ThrowIfNull(cardDefinitions);
            ArgumentNullException.ThrowIfNull(collectionCatalog);
            ValidateEnvelope(syncState, expectedDeckId);

            EnemyCardContentDirectory directory = EnemyCardDeckRegistry.GetContentDirectory(expectedDeckId);
            ValidateContentPhase(syncState, directory);
            IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> registeredDefinitions =
                directory.DefinitionFactories.Keys.ToDictionary(
                    cardId => cardId,
                    cardId => directory.CreateDefinition(cardId).Definition);
            if (cardDefinitions.Keys.Except(registeredDefinitions.Keys).Any() ||
                registeredDefinitions.Keys.Except(cardDefinitions.Keys).Any())
            {
                throw new InvalidOperationException("调用方卡牌定义集合与已注册内容目录不一致。");
            }

            EnemyCollectionCatalog registeredCollections = directory.CollectionCatalog;
            string[] suppliedCollectionIds = collectionCatalog.Definitions
                .Select(item => item.CollectionId)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] registeredCollectionIds = registeredCollections.Definitions
                .Select(item => item.CollectionId)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (!suppliedCollectionIds.SequenceEqual(registeredCollectionIds))
            {
                throw new InvalidOperationException("调用方收藏品定义集合与已注册内容目录不一致。");
            }

            EnemyCardCombatState temporary = EnemyCardDeckRegistry.CreateCombatState(expectedDeckId);
            Dictionary<int, BaseEnemyCard> templates = directory.OrderedPhases
                .SelectMany(phase => EnemyCardDeckRegistry.CreatePhaseDeck(expectedDeckId, phase.Phase))
                .ToDictionary(card => card.TemplateSlot!.Value);
            Dictionary<EnemyCardZone, IReadOnlyList<EnemyCardRuntimeCardState>> transferZones = new()
            {
                [EnemyCardZone.Draw] = syncState.DrawPile,
                [EnemyCardZone.Current] = syncState.CurrentCards,
                [EnemyCardZone.Retained] = syncState.RetainedCards,
                [EnemyCardZone.Discard] = syncState.DiscardPile,
                [EnemyCardZone.Exhaust] = syncState.ExhaustPile
            };
            Dictionary<EnemyCardZone, IReadOnlyList<BaseEnemyCard>> restoredZones = [];
            Dictionary<string, BaseEnemyCard> cardsByKey = new(StringComparer.Ordinal);
            foreach ((EnemyCardZone zone, IReadOnlyList<EnemyCardRuntimeCardState> cards) in transferZones)
            {
                if (cards is null)
                {
                    throw new InvalidOperationException($"重连 DTO 的牌区 {zone} 为空引用。");
                }

                List<BaseEnemyCard> restoredCards = [];
                foreach (EnemyCardRuntimeCardState cardState in cards)
                {
                    BaseEnemyCard card = RestoreCard(
                        cardState,
                        templates,
                        registeredDefinitions,
                        directory,
                        syncState.ActivePhase);
                    if (!cardsByKey.TryAdd(card.InstanceKey.Value, card))
                    {
                        throw new InvalidOperationException($"卡牌实例 {card.InstanceKey} 同时出现在多个牌区。");
                    }

                    restoredCards.Add(card);
                }

                restoredZones.Add(zone, restoredCards.AsReadOnly());
            }

            int[] restoredTemplateSlots = cardsByKey.Values.Where(card => card.TemplateSlot.HasValue)
                .Select(card => card.TemplateSlot!.Value)
                .Order()
                .ToArray();
            int[] expectedTemplateSlots = templates.Values
                .Where(card => card.SourcePhase == syncState.ActivePhase ||
                               card.SourcePhase < syncState.ActivePhase && card.CarryAcrossPhase)
                .Select(card => card.TemplateSlot!.Value)
                .Order()
                .ToArray();
            if (!restoredTemplateSlots.SequenceEqual(expectedTemplateSlots))
            {
                throw new InvalidOperationException("重连 DTO 的阶段模板实例集合与活动阶段及 CarryAcrossPhase 规则不一致。");
            }

            (EnemyCollectionInventorySnapshot inventorySnapshot,
                Dictionary<string, EnemyCollectionInstance> collectionsById) =
                RestoreCollections(syncState, registeredCollections);
            (EnemyCollectionInstance? frozenPreparationCollection,
                EnemyPreparedPreActionInventoryDelta? frozenPreparationDelta) = RestorePreparationCycle(
                syncState,
                inventorySnapshot,
                collectionsById,
                registeredCollections);
            IReadOnlyList<EnemyFrozenEffectiveCardState> effectiveCardStates =
                RestoreEffectiveCardStates(syncState);
            PreparedEnemyCardAction? preparedAction = RestorePreparedAction(
                syncState.PreparedAction,
                cardsByKey,
                collectionsById,
                registeredDefinitions,
                registeredCollections,
                syncState.NextGeneratedCardSequence,
                syncState.NextCollectionSequence,
                frozenPreparationDelta,
                effectiveCardStates,
                syncState);
            ValidatePhase(syncState, preparedAction, frozenPreparationCollection, frozenPreparationDelta);
            EnemyCardExecutionCursor? cursor = ValidateAndCloneCursor(syncState.Cursor, preparedAction);
            temporary.RestoreValidatedRuntime(
                restoredZones,
                inventorySnapshot,
                syncState.NextGeneratedCardSequence,
                syncState.LastMetric,
                preparedAction,
                syncState.RuntimePhase,
                syncState.FaultDiagnostic,
                frozenPreparationCollection,
                frozenPreparationDelta,
                syncState.ActivePhase,
                syncState.PendingPhase,
                syncState.PhaseRevision);
            restoredState = temporary;
            restoredCursor = cursor;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            restoredState = null;
            restoredCursor = null;
            reason = exception.Message;
            return false;
        }
    }

    /// <summary>捕获一个牌区的稳定实例顺序。</summary>
    private static IReadOnlyList<EnemyCardRuntimeCardState> CaptureCards(IEnumerable<BaseEnemyCard> cards) =>
        cards.Select(card => new EnemyCardRuntimeCardState
        {
            CardId = card.CardId.Value,
            InstanceKey = card.InstanceKey.Value,
            TemplateSlot = card.TemplateSlot,
            RuntimeInstanceId = card.RuntimeInstanceId,
            ReplayCount = card.ReplayCount,
            SourcePhase = card.SourcePhase,
            CarryAcrossPhase = card.CarryAcrossPhase
        }).ToArray();

    /// <summary>捕获一个收藏品区域的稳定实例顺序。</summary>
    private static IReadOnlyList<EnemyCollectionRuntimeState> CaptureCollections(
        IEnumerable<EnemyCollectionInstance> collections) =>
        collections.Select(CaptureCollection).ToArray();

    private static EnemyCollectionRuntimeState CaptureCollection(EnemyCollectionInstance item) =>
        new()
        {
            CollectionId = item.Definition.CollectionId,
            Sequence = item.Sequence,
            CollectionInstanceId = item.CollectionInstanceId
        };

    /// <summary>捕获可选冻结行动及其素材引用。</summary>
    private static PreparedEnemyCardActionSyncState? CapturePreparedAction(PreparedEnemyCardAction? action)
    {
        if (action is null)
        {
            return null;
        }

        EnemySoftLockDiagnostic diagnostic = action.SoftLockDiagnostic;
        return new PreparedEnemyCardActionSyncState
        {
            Phase = action.Phase,
            Metric = action.Metric,
            RetainedPrefixKeys = action.RetainedPrefix.Select(card => card.InstanceKey.Value).ToArray(),
            MetricCardKeys = action.MetricCards.Select(card => card.InstanceKey.Value).ToArray(),
            Sources = action.Sources.Select(source => new PreparedEnemyCardSourceSyncState
            {
                SourceInstanceKey = source.SourceKey.Value,
                MaximumAttempts = source.MaximumAttempts,
                Units = source.Units.Select(CaptureUnit).ToArray(),
                TruncationAttemptIndex = source.TruncationAttemptIndex
            }).ToArray(),
            SoftLockDiagnostic = new EnemySoftLockDiagnosticSyncState
            {
                AttackScore = diagnostic.Score.Attack,
                TotalScore = diagnostic.Score.Total,
                AttackLock = diagnostic.AttackLock,
                TotalScoreLock = diagnostic.TotalScoreLock,
                CandidateAttemptCount = diagnostic.CandidateAttemptCount,
                RejectedCandidateCount = diagnostic.RejectedCandidateCount,
                WasForcedByAttemptLimit = diagnostic.WasForcedByAttemptLimit,
                Rejections = diagnostic.Rejections.Select(rejection => new EnemyCandidateRejectionSyncState
                {
                    Attempt = rejection.Attempt,
                    Reason = rejection.Reason,
                    Diagnostic = rejection.Diagnostic
                }).ToArray()
            }
        };
    }

    /// <summary>
    /// 递归捕获一个成功重放单元的稳定身份、素材预留和显式步骤。
    /// </summary>
    /// <param name="unit">运行时不可变冻结单元。</param>
    /// <returns>不含对象引用的同步单元。</returns>
    private static PreparedEnemyCardUnitPlanSyncState CaptureUnit(PreparedEnemyCardUnitPlan unit) =>
        new()
        {
            RootSourceKey = unit.RootSourceKey.Value,
            ExecutingCardKey = unit.ExecutingCardKey.Value,
            ExecutingCardId = unit.ExecutingCardId.Value,
            ReplayIndex = unit.ReplayIndex,
            Mode = unit.Mode,
            ResolutionProgramFingerprint = unit.ResolutionProgramFingerprint,
            PlayConditionProgramId = unit.PlayConditionProgramId,
            MaterialReservations = unit.MaterialReservations.Select(CaptureReservation).ToArray(),
            OrderedSteps = unit.OrderedSteps.Select(CaptureStep).ToArray()
        };

    /// <summary>
    /// 把一个运行时步骤转换为显式种类与唯一对应载荷。
    /// </summary>
    /// <param name="step">运行时递归步骤。</param>
    /// <returns>不依赖运行时类型名的同步步骤。</returns>
    private static PreparedEnemyResolutionStepSyncState CaptureStep(PreparedEnemyResolutionStep step) => step switch
    {
        PreparedDirectEffectsStep direct => new PreparedEnemyResolutionStepSyncState
        {
            Kind = PreparedEnemyResolutionStepSyncKind.DirectEffects,
            DirectEffects = new PreparedDirectEffectsStepSyncState
            {
                EffectProgramIds = direct.EffectProgramIds.ToArray()
            }
        },
        PreparedConsumedCardStep consumedCard => new PreparedEnemyResolutionStepSyncState
        {
            Kind = PreparedEnemyResolutionStepSyncKind.ConsumedCard,
            ConsumedCard = new PreparedConsumedCardStepSyncState
            {
                MaterialInstanceKey = consumedCard.MaterialKey.Value,
                ControlledChild = consumedCard.ControlledChild is null
                    ? null
                    : CaptureUnit(consumedCard.ControlledChild)
            }
        },
        PreparedConsumedCollectionStep consumedCollection => new PreparedEnemyResolutionStepSyncState
        {
            Kind = PreparedEnemyResolutionStepSyncKind.ConsumedCollection,
            ConsumedCollection = new PreparedConsumedCollectionStepSyncState
            {
                CollectionInstanceId = consumedCollection.CollectionInstanceId,
                CollectionId = consumedCollection.CollectionId,
                Children = consumedCollection.Children.Select(CaptureStep).ToArray()
            }
        },
        PreparedGeneratedCollectionStep generatedCollection => new PreparedEnemyResolutionStepSyncState
        {
            Kind = PreparedEnemyResolutionStepSyncKind.GeneratedCollection,
            GeneratedCollection = new PreparedGeneratedCollectionStepSyncState
            {
                CollectionId = generatedCollection.CollectionId,
                ExpectedSequence = generatedCollection.ExpectedSequence
            }
        },
        PreparedComposeResultStep compose => new PreparedEnemyResolutionStepSyncState
        {
            Kind = PreparedEnemyResolutionStepSyncKind.ComposeResult,
            ComposeResult = new PreparedComposeResultStepSyncState
            {
                ResultCardId = compose.ResultCardId.Value,
                ResultInstanceKey = compose.ResultInstanceKey.Value,
                Timing = compose.Timing,
                IncreasesExistingReplay = compose.IncreasesExistingReplay,
                ImmediateChild = compose.ImmediateChild is null ? null : CaptureUnit(compose.ImmediateChild),
                AdditionalReplayUnits = compose.AdditionalReplayUnits.Select(CaptureUnit).ToArray()
            }
        },
        PreparedImmediateCardStep immediate => new PreparedEnemyResolutionStepSyncState
        {
            Kind = PreparedEnemyResolutionStepSyncKind.ImmediateCard,
            ImmediateCard = new PreparedImmediateCardStepSyncState
            {
                SelectedCardKey = immediate.SelectedCardKey.Value,
                Child = CaptureUnit(immediate.Child),
                AdditionalReplayUnits = immediate.AdditionalReplayUnits.Select(CaptureUnit).ToArray()
            }
        },
        PreparedRecoveryStep recovery => new PreparedEnemyResolutionStepSyncState
        {
            Kind = PreparedEnemyResolutionStepSyncKind.Recovery,
            Recovery = new PreparedRecoveryStepSyncState
            {
                Kind = recovery.Kind,
                SelectedInstanceId = recovery.SelectedInstanceId,
                ImmediateCardChild = recovery.ImmediateCardChild is null
                    ? null
                    : CaptureUnit(recovery.ImmediateCardChild),
                AdditionalReplayUnits = recovery.AdditionalReplayUnits.Select(CaptureUnit).ToArray()
            }
        },
        _ => throw new InvalidOperationException($"无法捕获未知冻结步骤 {step.GetType().Name}。 ")
    };

    /// <summary>捕获一次冻结素材预留。</summary>
    private static EnemyMaterialReservationSyncState CaptureReservation(EnemyMaterialReservation reservation) =>
        new()
        {
            IsComplete = reservation.IsComplete,
            Bindings = reservation.Bindings.Select(binding => new EnemyMaterialBindingSyncState
            {
                RequirementIndex = binding.RequirementIndex,
                RequiredCardType = binding.Requirement.CardType,
                RequiredCount = binding.Requirement.Count,
                Source = binding.Source,
                CardInstanceKey = binding.CardInstanceKey?.Value,
                CollectionInstanceId = binding.CollectionInstanceId,
                CandidateCardType = binding.Candidate.CardType,
                IsInspiration = binding.IsInspiration,
                IsEpiphany = binding.IsEpiphany
            }).ToArray()
        };

    /// <summary>验证协议版本、牌组和基础字段。</summary>
    private static void ValidateEnvelope(EnemyCardRuntimeSyncState syncState, EnemyCardDeckId expectedDeckId)
    {
        if (syncState.SchemaVersion != EnemyCardRuntimeSyncState.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"不支持敌人卡牌重连结构版本 {syncState.SchemaVersion}，需要主机重发当前版本。");
        }

        if (string.IsNullOrWhiteSpace(syncState.StateId))
        {
            throw new InvalidOperationException("重连 DTO 缺少 StateId。");
        }

        if (!string.Equals(syncState.DeckId, expectedDeckId.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"重连 DTO 牌组 {syncState.DeckId} 与预期 {expectedDeckId} 不一致。");
        }

        if (syncState.NextGeneratedCardSequence < 0 || syncState.NextCollectionSequence < 0 ||
            syncState.PhaseRevision < 0 || syncState.CompletedEffectiveCardCount < 0)
        {
            throw new InvalidOperationException("重连 DTO 的实例序号、阶段修订与有效牌计数不能为负数。");
        }
    }

    /// <summary>验证活动阶段、待迁移阶段与内容目录之间的合法关系。</summary>
    private static void ValidateContentPhase(
        EnemyCardRuntimeSyncState syncState,
        EnemyCardContentDirectory directory)
    {
        if (!Enum.IsDefined(syncState.ActivePhase) || !Enum.IsDefined(syncState.PendingPhase))
        {
            throw new InvalidOperationException("重连 DTO 包含未知内容阶段。");
        }

        _ = directory.GetPhase(syncState.ActivePhase);
        if (syncState.PendingPhase != EnemyCardPhase.None)
        {
            _ = directory.GetPhase(syncState.PendingPhase);
            if (syncState.ActivePhase == EnemyCardPhase.None || syncState.PendingPhase <= syncState.ActivePhase)
            {
                throw new InvalidOperationException("待迁移阶段必须是活动阶段之后的已注册显式阶段。");
            }
        }

        if (syncState.ActivePhase == directory.InitialPhase && syncState.PhaseRevision != 0)
        {
            throw new InvalidOperationException("尚未离开初始阶段的状态不能携带非零阶段修订。");
        }

        if (syncState.ActivePhase != directory.InitialPhase && syncState.PhaseRevision == 0)
        {
            throw new InvalidOperationException("已经迁移到后续阶段的状态必须携带非零阶段修订。");
        }
    }

    /// <summary>验证并重建一张模板牌或生成牌实例。</summary>
    private static BaseEnemyCard RestoreCard(
        EnemyCardRuntimeCardState cardState,
        IReadOnlyDictionary<int, BaseEnemyCard> templates,
        IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> cardDefinitions,
        EnemyCardContentDirectory directory,
        EnemyCardPhase activePhase)
    {
        ArgumentNullException.ThrowIfNull(cardState);
        if (!EnemyCardId.TryParse(cardState.CardId, out EnemyCardId cardId) || cardState.ReplayCount < 0 ||
            !Enum.IsDefined(cardState.SourcePhase))
        {
            throw new InvalidOperationException("重连卡牌定义标识、来源阶段或重放次数无效。");
        }

        _ = directory.GetPhase(cardState.SourcePhase);

        bool isTemplate = cardState.TemplateSlot.HasValue;
        if (isTemplate == cardState.RuntimeInstanceId.HasValue)
        {
            throw new InvalidOperationException("重连卡牌必须且只能具有模板槽位或运行时序号之一。");
        }

        BaseEnemyCard card;
        if (isTemplate)
        {
            int slot = cardState.TemplateSlot!.Value;
            if (!templates.TryGetValue(slot, out card!) || card.CardId != cardId ||
                card.InstanceKey != EnemyCardInstanceKey.FromTemplateSlot(slot))
            {
                throw new InvalidOperationException($"模板槽位 {slot} 与注册牌组定义不一致。");
            }
        }
        else
        {
            long runtimeId = cardState.RuntimeInstanceId!.Value;
            if (runtimeId < 0 || !cardDefinitions.ContainsKey(cardId))
            {
                throw new InvalidOperationException($"生成牌 {cardState.CardId} 的定义或序号无效。");
            }

            card = CreateCatalogCard(cardDefinitions[cardId]);
            card.AssignRuntimeInstanceId(runtimeId);
            card.AssignSourcePhase(cardState.SourcePhase);
        }

        if (!string.Equals(cardState.InstanceKey, card.InstanceKey.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"卡牌实例键 {cardState.InstanceKey} 与其身份字段不一致。");
        }

        if (card.SourcePhase != cardState.SourcePhase ||
            card.CarryAcrossPhase != cardState.CarryAcrossPhase ||
            card.Definition.CarryAcrossPhase != cardState.CarryAcrossPhase)
        {
            throw new InvalidOperationException($"卡牌实例 {cardState.InstanceKey} 的来源阶段或 CarryAcrossPhase 与注册目录不一致。");
        }

        if (card.SourcePhase > activePhase ||
            card.SourcePhase < activePhase && !card.CarryAcrossPhase)
        {
            throw new InvalidOperationException($"卡牌实例 {cardState.InstanceKey} 违反活动阶段保留规则。");
        }

        card.RestoreReplayCount(cardState.ReplayCount);
        return card;
    }

    /// <summary>从显式同步目录定义创建无额外对象状态的生成牌实例。</summary>
    private static BaseEnemyCard CreateCatalogCard(EnemyCardDefinition definition) =>
        new RestoredEnemyCard(definition);

    /// <summary>验证并重建收藏品可用区、已消耗区和实例索引。</summary>
    private static (EnemyCollectionInventorySnapshot Snapshot,
        Dictionary<string, EnemyCollectionInstance> ById) RestoreCollections(
        EnemyCardRuntimeSyncState syncState,
        EnemyCollectionCatalog collectionCatalog)
    {
        Dictionary<string, EnemyCollectionInstance> byId = new(StringComparer.Ordinal);
        IReadOnlyList<EnemyCollectionInstance> available = RestoreCollectionZone(syncState.AvailableCollections);
        IReadOnlyList<EnemyCollectionInstance> consumed = RestoreCollectionZone(syncState.ConsumedCollections);
        EnemyCollectionInventorySnapshot snapshot = new(available, consumed, syncState.NextCollectionSequence);
        EnemyCollectionInventory probe = new();
        if (!probe.TryApplySnapshot(snapshot, out string reason))
        {
            throw new InvalidOperationException(reason);
        }

        return (snapshot, byId);

        IReadOnlyList<EnemyCollectionInstance> RestoreCollectionZone(
            IReadOnlyList<EnemyCollectionRuntimeState> transfer)
        {
            if (transfer is null)
            {
                throw new InvalidOperationException("重连 DTO 的收藏品区域为空引用。");
            }

            List<EnemyCollectionInstance> result = [];
            foreach (EnemyCollectionRuntimeState item in transfer)
            {
                ArgumentNullException.ThrowIfNull(item);
                EnemyCollectionDefinition definition = collectionCatalog.GetRequired(item.CollectionId);
                EnemyCollectionInstance instance = new(definition, item.Sequence);
                if (!string.Equals(item.CollectionInstanceId, instance.CollectionInstanceId, StringComparison.Ordinal) ||
                    !byId.TryAdd(instance.CollectionInstanceId, instance))
                {
                    throw new InvalidOperationException($"收藏品实例 {item.CollectionInstanceId} 重复或身份不一致。");
                }

                result.Add(instance);
            }

            return result.AsReadOnly();
        }
    }

    /// <summary>从传输 DTO 重建唯一准备周期，并复用已经提交到库存的同一收藏品实例。</summary>
    private static (EnemyCollectionInstance? Collection, EnemyPreparedPreActionInventoryDelta? Delta)
        RestorePreparationCycle(
            EnemyCardRuntimeSyncState syncState,
            EnemyCollectionInventorySnapshot inventory,
            IDictionary<string, EnemyCollectionInstance> collectionsById,
            EnemyCollectionCatalog collectionCatalog)
    {
        EnemyPreparedPreActionInventoryDeltaSyncState? transfer = syncState.FrozenPreparationDelta;
        if (transfer is null)
        {
            if (syncState.FrozenPreparationCollection is not null)
            {
                throw new InvalidOperationException("冻结准备收藏品存在但准备库存增量缺失。");
            }

            return (null, null);
        }

        if (transfer.AddedAvailable is null)
        {
            throw new InvalidOperationException("冻结准备库存增量为空引用。");
        }

        HashSet<string> availableIds = inventory.Available
            .Select(item => item.CollectionInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> consumedIds = inventory.Consumed
            .Select(item => item.CollectionInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        List<EnemyCollectionInstance> added = [];
        HashSet<string> deltaIds = new(StringComparer.Ordinal);
        foreach (EnemyCollectionRuntimeState item in transfer.AddedAvailable)
        {
            ArgumentNullException.ThrowIfNull(item);
            EnemyCollectionDefinition definition = collectionCatalog.GetRequired(item.CollectionId);
            EnemyCollectionInstance candidate = new(definition, item.Sequence);
            if (!string.Equals(candidate.CollectionInstanceId, item.CollectionInstanceId, StringComparison.Ordinal) ||
                !deltaIds.Add(item.CollectionInstanceId))
            {
                throw new InvalidOperationException("冻结准备库存增量包含重复或身份不一致的收藏品。");
            }

            if (consumedIds.Contains(item.CollectionInstanceId))
            {
                throw new InvalidOperationException("准备前新增收藏品不能已经位于消耗区。");
            }

            if (collectionsById.TryGetValue(item.CollectionInstanceId, out EnemyCollectionInstance? existing))
            {
                if (!availableIds.Contains(item.CollectionInstanceId) ||
                    existing.Sequence != item.Sequence ||
                    !string.Equals(existing.Definition.CollectionId, item.CollectionId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("冻结准备库存增量与已提交可用区实例不一致。");
                }

                candidate = existing;
            }
            else
            {
                if (syncState.RuntimePhase is EnemyCardRuntimePhase.Prepared or EnemyCardRuntimePhase.Executing)
                {
                    throw new InvalidOperationException("准备或执行阶段的准备库存增量尚未原子提交到可用区。");
                }

                if (item.Sequence < syncState.NextCollectionSequence)
                {
                    throw new InvalidOperationException("未提交的准备收藏品序号落后于当前下一收藏品序号。");
                }

                collectionsById.Add(candidate.CollectionInstanceId, candidate);
            }

            added.Add(candidate);
        }

        EnemyPreparedPreActionInventoryDelta delta = new(added.AsReadOnly());
        if (syncState.FrozenPreparationCollection is null)
        {
            return (null, delta);
        }

        EnemyCollectionRuntimeState frozen = syncState.FrozenPreparationCollection;
        EnemyCollectionInstance collection = added.SingleOrDefault(item =>
            string.Equals(item.CollectionInstanceId, frozen.CollectionInstanceId, StringComparison.Ordinal)) ??
            throw new InvalidOperationException("冻结准备收藏品不属于同一准备库存增量。");
        if (collection.Sequence != frozen.Sequence ||
            !string.Equals(collection.Definition.CollectionId, frozen.CollectionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("冻结准备收藏品与增量中的定义或序号不一致。");
        }

        return (collection, delta);
    }

    /// <summary>验证并重建冻结行动的逐实例 N/X 状态。</summary>
    private static IReadOnlyList<EnemyFrozenEffectiveCardState> RestoreEffectiveCardStates(
        EnemyCardRuntimeSyncState syncState)
    {
        if (syncState.EffectiveCardStates is null)
        {
            throw new InvalidOperationException("冻结有效牌状态为空引用。");
        }

        List<EnemyFrozenEffectiveCardState> result = [];
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (EnemyFrozenEffectiveCardSyncState transfer in syncState.EffectiveCardStates)
        {
            ArgumentNullException.ThrowIfNull(transfer);
            if (string.IsNullOrWhiteSpace(transfer.ExecutingCardInstanceKey) ||
                !keys.Add(transfer.ExecutingCardInstanceKey))
            {
                throw new InvalidOperationException("冻结有效牌状态包含空键或重复键。");
            }

            result.Add(new EnemyFrozenEffectiveCardState(
                new EnemyCardInstanceKey(transfer.ExecutingCardInstanceKey),
                transfer.FrozenN,
                transfer.FrozenX,
                transfer.Multiplier,
                transfer.WasCounted));
        }

        if (syncState.CompletedEffectiveCardCount != result.Count(item => item.WasCounted))
        {
            throw new InvalidOperationException("CompletedEffectiveCardCount 与唯一已计数有效牌状态数量不一致。");
        }

        return result.AsReadOnly();
    }

    /// <summary>验证稳定引用并重建冻结行动。</summary>
    private static PreparedEnemyCardAction? RestorePreparedAction(
        PreparedEnemyCardActionSyncState? transfer,
        IReadOnlyDictionary<string, BaseEnemyCard> cardsByKey,
        IReadOnlyDictionary<string, EnemyCollectionInstance> collectionsById,
        IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> cardDefinitions,
        EnemyCollectionCatalog collectionCatalog,
        long nextGeneratedCardSequence,
        long nextCollectionSequence,
        EnemyPreparedPreActionInventoryDelta? preparationDelta,
        IReadOnlyList<EnemyFrozenEffectiveCardState> effectiveCardStates,
        EnemyCardRuntimeSyncState syncState)
    {
        if (transfer is null)
        {
            if (effectiveCardStates.Count != 0 || syncState.CompletedEffectiveCardCount != 0 ||
                syncState.StaticScore is not null || syncState.FullScore is not null ||
                syncState.CommitMode is not null || syncState.ProjectionIsComplete is not null ||
                syncState.ProjectionDiagnostics is null || syncState.ProjectionDiagnostics.Count != 0)
            {
                throw new InvalidOperationException("没有冻结行动的状态不能携带有效牌或两层门控快照。");
            }

            return null;
        }

        if (preparationDelta is null)
        {
            throw new InvalidOperationException("冻结行动缺少同一准备周期的库存增量。");
        }

        if (transfer.Phase != syncState.ActivePhase)
        {
            throw new InvalidOperationException("冻结行动阶段与当前活动阶段不一致。");
        }

        BaseEnemyCard ResolveCard(string key) => cardsByKey.TryGetValue(key, out BaseEnemyCard? card)
            ? card
            : throw new InvalidOperationException($"冻结行动引用了不存在的卡牌实例 {key}。");

        BaseEnemyCard[] retained = transfer.RetainedPrefixKeys.Select(ResolveCard).ToArray();
        BaseEnemyCard[] metric = transfer.MetricCardKeys.Select(ResolveCard).ToArray();
        PlanRestoreContext planContext = new(
            cardsByKey,
            collectionsById,
            cardDefinitions,
            collectionCatalog,
            nextGeneratedCardSequence,
            nextCollectionSequence,
            CardIntentTestRules.Default.StepLimit);
        PreparedEnemyCardSource[] sources = transfer.Sources.Select(source => new PreparedEnemyCardSource(
            ResolveCard(source.SourceInstanceKey),
            source.MaximumAttempts,
            source.Units.Select(unit => RestoreUnit(
                unit,
                new EnemyCardInstanceKey(source.SourceInstanceKey),
                planContext,
                depth: 0)),
            source.TruncationAttemptIndex)).ToArray();
        EnemySoftLockDiagnosticSyncState diagnostic = transfer.SoftLockDiagnostic ??
                                                       throw new InvalidOperationException("冻结行动缺少软锁诊断。");
        if (diagnostic.CandidateAttemptCount < 1 || diagnostic.RejectedCandidateCount < 0 ||
            diagnostic.Rejections is null || diagnostic.RejectedCandidateCount != diagnostic.Rejections.Count)
        {
            throw new InvalidOperationException("冻结行动的候选次数或拒绝历史无效。");
        }

        EnemyStaticScoreSyncState staticScore = syncState.StaticScore ??
                                                throw new InvalidOperationException("冻结行动缺少静态评分快照。");
        EnemyActionRiskScoreSyncState fullScore = syncState.FullScore ??
                                                  throw new InvalidOperationException("冻结行动缺少完整风险快照。");
        EnemyCandidateCommitMode commitMode = syncState.CommitMode ??
                                              throw new InvalidOperationException("冻结行动缺少提交方式。");
        bool projectionIsComplete = syncState.ProjectionIsComplete ??
                                    throw new InvalidOperationException("冻结行动缺少投影完整性字段。");
        if (syncState.ProjectionDiagnostics is null)
        {
            throw new InvalidOperationException("冻结行动的投影诊断为空引用。");
        }

        if (diagnostic.AttackScore != staticScore.Attack ||
            diagnostic.TotalScore != staticScore.Total ||
            diagnostic.AttackLock != staticScore.AttackLock ||
            diagnostic.TotalScoreLock != staticScore.TotalLock ||
            diagnostic.WasForcedByAttemptLimit != (commitMode == EnemyCandidateCommitMode.ForcedOverLock))
        {
            throw new InvalidOperationException("冻结行动的兼容软锁字段与 schema v3 两层门控快照不闭合。");
        }

        EnemyCandidateRejection[] rejections = diagnostic.Rejections
            .Select(rejection => new EnemyCandidateRejection(
                rejection.Attempt,
                rejection.Reason,
                rejection.Diagnostic))
            .ToArray();
        EnemyCardScore restoredStaticScore = new(staticScore.Attack, staticScore.Total);
        EnemyActionRiskScore restoredFullScore = new(
            fullScore.AttackRisk,
            fullScore.SurvivalRisk,
            fullScore.EngineRisk,
            fullScore.DeferredRisk);
        EnemySoftLockLimits staticLocks = new(staticScore.AttackLock, staticScore.TotalLock);
        EnemySoftLockLimits fullLocks = new(fullScore.AttackLock, fullScore.TotalLock);
        bool staticOver = restoredStaticScore.Attack > staticLocks.Attack ||
                          restoredStaticScore.Total > staticLocks.Total;
        bool fullOver = restoredFullScore.AttackRisk > fullLocks.Attack ||
                        restoredFullScore.TotalRisk > fullLocks.Total;
        if (!projectionIsComplete ||
            commitMode == EnemyCandidateCommitMode.WithinLocks && (staticOver || fullOver) ||
            commitMode == EnemyCandidateCommitMode.ForcedOverLock && !staticOver && !fullOver)
        {
            throw new InvalidOperationException("冻结行动的评分、提交方式与投影完整性不满足门控闭合关系。");
        }

        ValidateEffectiveCardClosure(transfer, effectiveCardStates, cardDefinitions, cardsByKey);

        return new PreparedEnemyCardAction(
            transfer.Metric,
            retained,
            metric,
            sources,
            new EnemySoftLockDiagnostic(
                restoredStaticScore,
                restoredFullScore,
                staticLocks,
                fullLocks,
                diagnostic.CandidateAttemptCount,
                rejections,
                commitMode,
                projectionIsComplete,
                syncState.ProjectionDiagnostics),
            preparationDelta,
            effectiveCardStates,
            transfer.Phase);
    }

    /// <summary>验证同步计划中的每个实际执行实例恰好对应一个冻结有效牌状态。</summary>
    private static void ValidateEffectiveCardClosure(
        PreparedEnemyCardActionSyncState action,
        IReadOnlyList<EnemyFrozenEffectiveCardState> effectiveCardStates,
        IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> cardDefinitions,
        IReadOnlyDictionary<string, BaseEnemyCard> cardsByKey)
    {
        Dictionary<string, EnemyCardId> knownExecutions = action.Sources.ToDictionary(
            source => source.SourceInstanceKey,
            source => cardsByKey.TryGetValue(source.SourceInstanceKey, out BaseEnemyCard? card)
                ? card.CardId
                : throw new InvalidOperationException("冻结来源实例不在恢复牌区中。"),
            StringComparer.Ordinal);
        HashSet<string> successful = new(StringComparer.Ordinal);
        foreach (PreparedEnemyCardUnitPlanSyncState unit in action.Sources
                     .SelectMany(source => source.Units)
                     .SelectMany(EnumerateUnitTree))
        {
            if (!EnemyCardId.TryParse(unit.ExecutingCardId, out EnemyCardId cardId))
            {
                throw new InvalidOperationException("冻结计划包含无效的实际执行定义标识。");
            }

            if (knownExecutions.TryGetValue(unit.ExecutingCardKey, out EnemyCardId existing) && existing != cardId)
            {
                throw new InvalidOperationException("同一实际执行实例键在冻结计划中引用了不同定义。");
            }

            knownExecutions[unit.ExecutingCardKey] = cardId;
            successful.Add(unit.ExecutingCardKey);
        }

        Dictionary<string, EnemyFrozenEffectiveCardState> states = effectiveCardStates.ToDictionary(
            item => item.ExecutingCardInstanceKey.Value,
            StringComparer.Ordinal);
        if (states.Keys.Any(key => !knownExecutions.ContainsKey(key)) ||
            !successful.SetEquals(states.Values
                .Where(state => state.WasCounted)
                .Select(state => state.ExecutingCardInstanceKey.Value)))
        {
            throw new InvalidOperationException("冻结计划的成功单元与 WasCounted 有效牌实例集合不闭合。");
        }

        foreach ((string key, EnemyFrozenEffectiveCardState state) in states)
        {
            EnemyCardId cardId = knownExecutions[key];
            if (!cardDefinitions.TryGetValue(cardId, out EnemyCardDefinition? definition))
            {
                throw new InvalidOperationException($"实际执行定义 {cardId} 不在同步内容目录中。");
            }

            bool requiresFrozenX = definition.Effects.Any(effect => effect is EnemyFrozenXAttackAllEffect);
            if (requiresFrozenX != state.FrozenX.HasValue)
            {
                throw new InvalidOperationException($"实际执行实例 {key} 的冻结 X 元数据与定义不一致。");
            }
        }
    }

    private static IEnumerable<PreparedEnemyCardUnitPlanSyncState> EnumerateUnitTree(
        PreparedEnemyCardUnitPlanSyncState unit)
    {
        yield return unit;
        foreach (PreparedEnemyCardUnitPlanSyncState child in unit.OrderedSteps.SelectMany(EnumerateStepUnits))
        {
            foreach (PreparedEnemyCardUnitPlanSyncState descendant in EnumerateUnitTree(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<PreparedEnemyCardUnitPlanSyncState> EnumerateStepUnits(
        PreparedEnemyResolutionStepSyncState step) => step.Kind switch
    {
        PreparedEnemyResolutionStepSyncKind.ConsumedCard when step.ConsumedCard?.ControlledChild is not null =>
            [step.ConsumedCard.ControlledChild],
        PreparedEnemyResolutionStepSyncKind.ConsumedCollection when step.ConsumedCollection is not null =>
            step.ConsumedCollection.Children.SelectMany(EnumerateStepUnits),
        PreparedEnemyResolutionStepSyncKind.ComposeResult when step.ComposeResult is not null =>
            (step.ComposeResult.ImmediateChild is null
                ? Enumerable.Empty<PreparedEnemyCardUnitPlanSyncState>()
                : [step.ComposeResult.ImmediateChild])
            .Concat(step.ComposeResult.AdditionalReplayUnits),
        PreparedEnemyResolutionStepSyncKind.ImmediateCard when step.ImmediateCard is not null =>
            (step.ImmediateCard.Child is null
                ? Enumerable.Empty<PreparedEnemyCardUnitPlanSyncState>()
                : [step.ImmediateCard.Child])
            .Concat(step.ImmediateCard.AdditionalReplayUnits),
        PreparedEnemyResolutionStepSyncKind.Recovery when step.Recovery is not null =>
            (step.Recovery.ImmediateCardChild is null
                ? Enumerable.Empty<PreparedEnemyCardUnitPlanSyncState>()
                : [step.Recovery.ImmediateCardChild])
            .Concat(step.Recovery.AdditionalReplayUnits),
        _ => []
    };

    /// <summary>
    /// 递归验证同步单元的稳定身份、引用、步骤预算和模式组合。
    /// </summary>
    /// <param name="transfer">同步单元。</param>
    /// <param name="expectedRoot">所属公开来源实例键。</param>
    /// <param name="context">跨整项行动共享的恢复校验上下文。</param>
    /// <param name="depth">当前递归深度。</param>
    /// <returns>完全验证后的不可变运行时单元。</returns>
    private static PreparedEnemyCardUnitPlan RestoreUnit(
        PreparedEnemyCardUnitPlanSyncState transfer,
        EnemyCardInstanceKey expectedRoot,
        PlanRestoreContext context,
        int depth)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        context.EnterNode(depth);
        EnemyCardInstanceKey root = new(transfer.RootSourceKey);
        EnemyCardInstanceKey executingKey = new(transfer.ExecutingCardKey);
        if (root != expectedRoot || !EnemyCardId.TryParse(transfer.ExecutingCardId, out EnemyCardId executingId))
        {
            throw new InvalidOperationException("冻结单元根来源或实际执行定义标识无效。 ");
        }

        context.ValidateExecutingCard(executingKey, executingId);
        if (string.IsNullOrWhiteSpace(transfer.ResolutionProgramFingerprint) ||
            string.IsNullOrWhiteSpace(transfer.PlayConditionProgramId) ||
            !context.CardDefinitions.TryGetValue(executingId, out EnemyCardDefinition? definition))
        {
            throw new InvalidOperationException("冻结单元缺少有效显式程序、条件或执行定义。 ");
        }

        EnemyMaterialReservation[] reservations = transfer.MaterialReservations
            .Select(item => RestoreReservation(item, context.CardsByKey, context.CollectionsById))
            .ToArray();
        PreparedEnemyResolutionStep[] steps = transfer.OrderedSteps
            .Select(item => RestoreStep(item, expectedRoot, context, depth + 1))
            .ToArray();
        PreparedEnemyCardUnitPlan unit = new(
            root,
            executingKey,
            executingId,
            transfer.ReplayIndex,
            transfer.Mode,
            reservations,
            steps,
            transfer.ResolutionProgramFingerprint,
            transfer.PlayConditionProgramId);
        unit.ValidateFrozenDefinition(definition);
        return unit;
    }

    /// <summary>
    /// 验证显式步骤种类只携带唯一对应载荷并递归重建运行时步骤。
    /// </summary>
    /// <param name="transfer">同步步骤包络。</param>
    /// <param name="expectedRoot">所属公开来源实例键。</param>
    /// <param name="context">跨行动恢复校验上下文。</param>
    /// <param name="depth">当前递归深度。</param>
    /// <returns>验证后的运行时步骤。</returns>
    private static PreparedEnemyResolutionStep RestoreStep(
        PreparedEnemyResolutionStepSyncState transfer,
        EnemyCardInstanceKey expectedRoot,
        PlanRestoreContext context,
        int depth)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        context.EnterNode(depth);
        int payloadCount = new object?[]
        {
            transfer.DirectEffects,
            transfer.ConsumedCard,
            transfer.ConsumedCollection,
            transfer.GeneratedCollection,
            transfer.ComposeResult,
            transfer.ImmediateCard,
            transfer.Recovery
        }.Count(payload => payload is not null);
        if (payloadCount != 1)
        {
            throw new InvalidOperationException("冻结同步步骤必须且只能携带一个显式载荷。 ");
        }

        return transfer.Kind switch
        {
            PreparedEnemyResolutionStepSyncKind.DirectEffects when transfer.DirectEffects is not null =>
                new PreparedDirectEffectsStep(transfer.DirectEffects.EffectProgramIds),
            PreparedEnemyResolutionStepSyncKind.ConsumedCard when transfer.ConsumedCard is not null =>
                RestoreConsumedCard(transfer.ConsumedCard),
            PreparedEnemyResolutionStepSyncKind.ConsumedCollection when transfer.ConsumedCollection is not null =>
                RestoreConsumedCollection(transfer.ConsumedCollection),
            PreparedEnemyResolutionStepSyncKind.GeneratedCollection when transfer.GeneratedCollection is not null =>
                RestoreGeneratedCollection(transfer.GeneratedCollection),
            PreparedEnemyResolutionStepSyncKind.ComposeResult when transfer.ComposeResult is not null =>
                RestoreCompose(transfer.ComposeResult),
            PreparedEnemyResolutionStepSyncKind.ImmediateCard when transfer.ImmediateCard is not null =>
                RestoreImmediate(transfer.ImmediateCard),
            PreparedEnemyResolutionStepSyncKind.Recovery when transfer.Recovery is not null =>
                RestoreRecovery(transfer.Recovery),
            _ => throw new InvalidOperationException($"未知或载荷不匹配的冻结步骤种类 {transfer.Kind}。 ")
        };

        PreparedConsumedCardStep RestoreConsumedCard(PreparedConsumedCardStepSyncState payload)
        {
            EnemyCardInstanceKey materialKey = new(payload.MaterialInstanceKey);
            context.ValidateExistingCard(materialKey);
            PreparedEnemyCardUnitPlan? child = payload.ControlledChild is null
                ? null
                : RestoreUnit(payload.ControlledChild, expectedRoot, context, depth);
            return new PreparedConsumedCardStep(materialKey, child);
        }

        PreparedConsumedCollectionStep RestoreConsumedCollection(
            PreparedConsumedCollectionStepSyncState payload)
        {
            if (!context.CollectionsById.TryGetValue(
                    payload.CollectionInstanceId,
                    out EnemyCollectionInstance? collection) ||
                !string.Equals(collection.Definition.CollectionId, payload.CollectionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("冻结收藏品消费引用悬空或定义不匹配。 ");
            }

            return new PreparedConsumedCollectionStep(
                payload.CollectionInstanceId,
                payload.CollectionId,
                payload.Children.Select(child => RestoreStep(child, expectedRoot, context, depth)));
        }

        PreparedGeneratedCollectionStep RestoreGeneratedCollection(
            PreparedGeneratedCollectionStepSyncState payload)
        {
            EnemyCollectionDefinition definition = context.CollectionCatalog.GetRequired(payload.CollectionId);
            context.AcceptGeneratedCollection(definition, payload.ExpectedSequence);
            return new PreparedGeneratedCollectionStep(payload.CollectionId, payload.ExpectedSequence);
        }

        PreparedComposeResultStep RestoreCompose(PreparedComposeResultStepSyncState payload)
        {
            if (!EnemyCardId.TryParse(payload.ResultCardId, out EnemyCardId resultId) ||
                !context.CardDefinitions.ContainsKey(resultId))
            {
                throw new InvalidOperationException("作词结果引用了未知卡牌定义。 ");
            }

            EnemyCardInstanceKey resultKey = new(payload.ResultInstanceKey);
            if (payload.IncreasesExistingReplay)
            {
                context.ValidateExecutingCard(resultKey, resultId);
            }
            else
            {
                context.AcceptGeneratedCard(resultKey, resultId);
            }

            PreparedEnemyCardUnitPlan? child = payload.ImmediateChild is null
                ? null
                : RestoreUnit(payload.ImmediateChild, expectedRoot, context, depth);
            PreparedEnemyCardUnitPlan[] additional = payload.AdditionalReplayUnits
                .Select(unit => RestoreUnit(unit, expectedRoot, context, depth))
                .ToArray();
            return new PreparedComposeResultStep(
                resultId,
                resultKey,
                payload.Timing,
                payload.IncreasesExistingReplay,
                child,
                additional);
        }

        PreparedImmediateCardStep RestoreImmediate(PreparedImmediateCardStepSyncState payload)
        {
            EnemyCardInstanceKey selectedKey = new(payload.SelectedCardKey);
            context.ValidateExistingCard(selectedKey);
            PreparedEnemyCardUnitPlan child = RestoreUnit(
                payload.Child ?? throw new InvalidOperationException("即时抽牌缺少递归子单元。 "),
                expectedRoot,
                context,
                depth);
            PreparedEnemyCardUnitPlan[] additional = payload.AdditionalReplayUnits
                .Select(unit => RestoreUnit(unit, expectedRoot, context, depth))
                .ToArray();
            return new PreparedImmediateCardStep(selectedKey, child, additional);
        }

        PreparedRecoveryStep RestoreRecovery(PreparedRecoveryStepSyncState payload)
        {
            if (payload.Kind == EnemyPreparedRecoveryKind.Card)
            {
                EnemyCardInstanceKey key = new(payload.SelectedInstanceId);
                context.ValidateExistingCard(key);
            }
            else if (payload.Kind == EnemyPreparedRecoveryKind.Collection &&
                     !context.CollectionsById.ContainsKey(payload.SelectedInstanceId))
            {
                throw new InvalidOperationException("回收收藏品引用了不存在的实例。 ");
            }

            PreparedEnemyCardUnitPlan? child = payload.ImmediateCardChild is null
                ? null
                : RestoreUnit(payload.ImmediateCardChild, expectedRoot, context, depth);
            PreparedEnemyCardUnitPlan[] additional = payload.AdditionalReplayUnits
                .Select(unit => RestoreUnit(unit, expectedRoot, context, depth))
                .ToArray();
            return new PreparedRecoveryStep(payload.Kind, payload.SelectedInstanceId, child, additional);
        }
    }

    /// <summary>验证素材实例引用并重建一次冻结预留。</summary>
    private static EnemyMaterialReservation RestoreReservation(
        EnemyMaterialReservationSyncState transfer,
        IReadOnlyDictionary<string, BaseEnemyCard> cardsByKey,
        IReadOnlyDictionary<string, EnemyCollectionInstance> collectionsById)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (!transfer.IsComplete)
        {
            if (transfer.Bindings.Count != 0)
            {
                throw new InvalidOperationException("失败素材预留不能携带部分绑定。");
            }

            return EnemyMaterialReservation.CreateIncomplete();
        }

        List<EnemyMaterialBinding> bindings = [];
        foreach (EnemyMaterialBindingSyncState binding in transfer.Bindings)
        {
            EnemyMaterialRequirement requirement = new(binding.RequiredCardType, binding.RequiredCount);
            EnemyMaterialCandidate candidate;
            if (binding.Source == EnemyMaterialSource.Hand)
            {
                if (binding.CardInstanceKey is null || binding.CollectionInstanceId is not null ||
                    !cardsByKey.TryGetValue(binding.CardInstanceKey, out BaseEnemyCard? card) ||
                    card.CardModel.Type != binding.CandidateCardType)
                {
                    throw new InvalidOperationException("冻结手牌素材引用不存在或资格字段不一致。");
                }

                candidate = EnemyMaterialCandidate.FromHand(
                    card.InstanceKey,
                    binding.CandidateCardType,
                    binding.IsInspiration,
                    binding.IsEpiphany);
            }
            else
            {
                if (binding.CollectionInstanceId is null || binding.CardInstanceKey is not null ||
                    !collectionsById.TryGetValue(binding.CollectionInstanceId, out EnemyCollectionInstance? item) ||
                    item.Definition.MaterialCardType != binding.CandidateCardType ||
                    item.Definition.IsEpiphany != binding.IsEpiphany || binding.IsInspiration)
                {
                    throw new InvalidOperationException("冻结收藏品素材引用不存在或资格字段不一致。");
                }

                candidate = EnemyMaterialCandidate.FromCollection(item);
            }

            bindings.Add(new EnemyMaterialBinding(binding.RequirementIndex, requirement, candidate));
        }

        return EnemyMaterialReservation.CreateComplete(bindings);
    }

    /// <summary>验证运行阶段、冻结行动和故障诊断之间的闭合关系。</summary>
    private static void ValidatePhase(
        EnemyCardRuntimeSyncState syncState,
        PreparedEnemyCardAction? preparedAction,
        EnemyCollectionInstance? frozenPreparationCollection,
        EnemyPreparedPreActionInventoryDelta? frozenPreparationDelta)
    {
        if (!Enum.IsDefined(syncState.RuntimePhase))
        {
            throw new InvalidOperationException("重连 DTO 包含未知运行阶段。");
        }

        if (syncState.RuntimePhase == EnemyCardRuntimePhase.Idle && preparedAction is not null)
        {
            throw new InvalidOperationException("空闲阶段不能携带冻结行动。");
        }

        if (syncState.RuntimePhase is EnemyCardRuntimePhase.Prepared or EnemyCardRuntimePhase.Executing &&
            preparedAction is null)
        {
            throw new InvalidOperationException("准备或执行阶段缺少冻结行动。");
        }

        if (syncState.RuntimePhase == EnemyCardRuntimePhase.Faulted !=
            !string.IsNullOrWhiteSpace(syncState.FaultDiagnostic))
        {
            throw new InvalidOperationException("故障阶段与故障诊断不一致。");
        }

        if (syncState.RuntimePhase == EnemyCardRuntimePhase.Idle &&
            (frozenPreparationCollection is not null || frozenPreparationDelta is not null))
        {
            throw new InvalidOperationException("空闲阶段不能携带尚未清除的准备周期。");
        }

        if (preparedAction is not null &&
            (frozenPreparationDelta is null ||
             !ReferenceEquals(preparedAction.PreActionInventoryDelta, frozenPreparationDelta)))
        {
            throw new InvalidOperationException("冻结行动必须引用当前恢复状态的同一准备库存增量对象。");
        }

        if (frozenPreparationCollection is not null &&
            (frozenPreparationDelta is null ||
             !frozenPreparationDelta.AddedAvailable.Any(item => ReferenceEquals(item, frozenPreparationCollection))))
        {
            throw new InvalidOperationException("冻结准备收藏品必须来自同一准备库存增量。");
        }
    }

    /// <summary>验证安全边界游标并返回不共享可变字段的副本。</summary>
    private static EnemyCardExecutionCursor? ValidateAndCloneCursor(
        EnemyCardExecutionCursor? cursor,
        PreparedEnemyCardAction? action)
    {
        if (cursor is null)
        {
            return null;
        }

        if (!cursor.IsValid())
        {
            throw new InvalidOperationException("重连游标包含负索引，未停留在安全步骤边界。");
        }

        if (action is not null)
        {
            if (cursor.SourceIndex > action.Sources.Count)
            {
                throw new InvalidOperationException("重连游标的来源索引越过冻结行动边界。");
            }

            if (cursor.SourceIndex < action.Sources.Count &&
                cursor.ReplayIndex > action.Sources[cursor.SourceIndex].Units.Count)
            {
                throw new InvalidOperationException("重连游标的重放索引越过来源尝试边界。");
            }

            if (cursor.SourceIndex < action.Sources.Count)
            {
                PreparedEnemyCardSource source = action.Sources[cursor.SourceIndex];
                if (cursor.ReplayIndex == source.Units.Count)
                {
                    if (cursor.StepPath.Count != 0)
                    {
                        throw new InvalidOperationException("重连游标已越过来源全部成功单元，步骤路径必须为空。 ");
                    }
                }
                else
                {
                    ValidateStepPath(source.Units[cursor.ReplayIndex].OrderedSteps, cursor.StepPath, depth: 0);
                }
            }
        }

        return cursor.Clone();
    }

    /// <summary>
    /// 递归验证每个路径分量没有越过当前步骤集合，并且更深路径确实指向子树。
    /// </summary>
    /// <param name="steps">当前递归层有序步骤。</param>
    /// <param name="path">完整下一步骤路径。</param>
    /// <param name="depth">当前路径分量索引。</param>
    private static void ValidateStepPath(
        IReadOnlyList<PreparedEnemyResolutionStep> steps,
        IReadOnlyList<int> path,
        int depth)
    {
        if (depth >= path.Count)
        {
            return;
        }

        int nextIndex = path[depth];
        if (nextIndex > steps.Count)
        {
            throw new InvalidOperationException("重连游标步骤路径分量越过当前递归层边界。 ");
        }

        if (depth == path.Count - 1)
        {
            return;
        }

        if (nextIndex == steps.Count)
        {
            throw new InvalidOperationException("已完成的递归层不能携带更深步骤路径。 ");
        }

        IReadOnlyList<PreparedEnemyResolutionStep>? children = steps[nextIndex] switch
        {
            PreparedConsumedCardStep { ControlledChild: not null } consumed =>
                consumed.ControlledChild.OrderedSteps,
            PreparedConsumedCollectionStep collection => collection.Children,
            PreparedComposeResultStep { ImmediateChild: not null } compose =>
                compose.ImmediateChild.OrderedSteps,
            PreparedImmediateCardStep immediate => immediate.Child.OrderedSteps,
            PreparedRecoveryStep { ImmediateCardChild: not null } recovery =>
                recovery.ImmediateCardChild.OrderedSteps,
            _ => null
        };
        if (children is null)
        {
            throw new InvalidOperationException("重连游标更深步骤路径没有对应递归子树。 ");
        }

        ValidateStepPath(children, path, depth + 1);
    }

    /// <summary>
    /// 跨整个冻结行动共享递归预算、稳定目录和预计生成序号校验。
    /// </summary>
    private sealed class PlanRestoreContext
    {
        private readonly Dictionary<EnemyCardInstanceKey, EnemyCardId> _plannedGeneratedCards = [];
        private readonly Dictionary<string, BaseEnemyCard> _cardsByKey;
        private readonly Dictionary<string, EnemyCollectionInstance> _collectionsById;
        private readonly int _nodeLimit;
        private long _nextGeneratedCardSequence;
        private long _nextCollectionSequence;
        private int _nodeCount;

        /// <summary>
        /// 创建完整计划恢复校验上下文。
        /// </summary>
        /// <param name="cardsByKey">五牌区现有卡牌实例索引。</param>
        /// <param name="collectionsById">可用与已消费收藏品实例索引。</param>
        /// <param name="cardDefinitions">显式卡牌定义目录。</param>
        /// <param name="collectionCatalog">显式收藏品定义目录。</param>
        /// <param name="nextGeneratedCardSequence">权威下一生成牌序号。</param>
        /// <param name="nextCollectionSequence">权威下一收藏品序号。</param>
        /// <param name="nodeLimit">最大递归深度和总节点数。</param>
        public PlanRestoreContext(
            IReadOnlyDictionary<string, BaseEnemyCard> cardsByKey,
            IReadOnlyDictionary<string, EnemyCollectionInstance> collectionsById,
            IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> cardDefinitions,
            EnemyCollectionCatalog collectionCatalog,
            long nextGeneratedCardSequence,
            long nextCollectionSequence,
            int nodeLimit)
        {
            _cardsByKey = new Dictionary<string, BaseEnemyCard>(cardsByKey, StringComparer.Ordinal);
            _collectionsById = new Dictionary<string, EnemyCollectionInstance>(
                collectionsById,
                StringComparer.Ordinal);
            CardDefinitions = cardDefinitions;
            CollectionCatalog = collectionCatalog;
            _nextGeneratedCardSequence = nextGeneratedCardSequence;
            _nextCollectionSequence = nextCollectionSequence;
            _nodeLimit = nodeLimit;
        }

        /// <summary>获取五牌区现有卡牌实例索引。</summary>
        public IReadOnlyDictionary<string, BaseEnemyCard> CardsByKey => _cardsByKey;

        /// <summary>获取全部现有收藏品实例索引。</summary>
        public IReadOnlyDictionary<string, EnemyCollectionInstance> CollectionsById => _collectionsById;

        /// <summary>获取显式卡牌定义目录。</summary>
        public IReadOnlyDictionary<EnemyCardId, EnemyCardDefinition> CardDefinitions { get; }

        /// <summary>获取显式收藏品定义目录。</summary>
        public EnemyCollectionCatalog CollectionCatalog { get; }

        /// <summary>
        /// 同时验证递归深度和整棵树累计节点数未越过执行上限。
        /// </summary>
        /// <param name="depth">当前递归深度。</param>
        public void EnterNode(int depth)
        {
            if (depth > _nodeLimit || ++_nodeCount > _nodeLimit)
            {
                throw new InvalidOperationException("冻结行动递归深度或总步骤数越过执行上限。 ");
            }
        }

        /// <summary>
        /// 验证卡牌实例存在于权威五区。
        /// </summary>
        /// <param name="key">稳定实例键。</param>
        public void ValidateExistingCard(EnemyCardInstanceKey key)
        {
            if (!CardsByKey.ContainsKey(key.Value))
            {
                throw new InvalidOperationException($"冻结计划引用了不存在的卡牌实例 {key}。 ");
            }
        }

        /// <summary>
        /// 验证实际执行身份来自现有权威实例或此前已声明的预计生成实例。
        /// </summary>
        /// <param name="key">实际执行实例键。</param>
        /// <param name="cardId">实际执行定义标识。</param>
        public void ValidateExecutingCard(EnemyCardInstanceKey key, EnemyCardId cardId)
        {
            if (CardsByKey.TryGetValue(key.Value, out BaseEnemyCard? existing))
            {
                if (existing.CardId != cardId)
                {
                    throw new InvalidOperationException("冻结实际执行实例与权威 CardId 不匹配。 ");
                }

                return;
            }

            if (!_plannedGeneratedCards.TryGetValue(key, out EnemyCardId generatedId) || generatedId != cardId)
            {
                throw new InvalidOperationException($"冻结实际执行实例 {key} 没有现有或预计生成来源。 ");
            }
        }

        /// <summary>
        /// 接受严格连续的预计生成牌身份并推进临时序号。
        /// </summary>
        /// <param name="key">预计生成实例键。</param>
        /// <param name="cardId">预计生成定义标识。</param>
        public void AcceptGeneratedCard(EnemyCardInstanceKey key, EnemyCardId cardId)
        {
            EnemyCardInstanceKey expected = EnemyCardInstanceKey.FromRuntimeInstanceId(_nextGeneratedCardSequence);
            if (key != expected || !_plannedGeneratedCards.TryAdd(key, cardId))
            {
                throw new InvalidOperationException("作词预计生成牌序号不连续或实例重复。 ");
            }

            BaseEnemyCard generated = new RestoredEnemyCard(CardDefinitions[cardId]);
            generated.AssignRuntimeInstanceId(_nextGeneratedCardSequence);
            if (!_cardsByKey.TryAdd(key.Value, generated))
            {
                throw new InvalidOperationException("作词预计生成牌与现有卡牌实例键重复。 ");
            }

            checked
            {
                _nextGeneratedCardSequence++;
            }
        }

        /// <summary>
        /// 接受严格连续的预计收藏品定义与序号，注册临时实例并推进序号。
        /// </summary>
        /// <param name="definition">已从显式目录解析的收藏品定义。</param>
        /// <param name="sequence">步骤声明的预计序号。</param>
        public void AcceptGeneratedCollection(EnemyCollectionDefinition definition, long sequence)
        {
            if (sequence != _nextCollectionSequence)
            {
                throw new InvalidOperationException("冻结收藏品预计生成序号不连续。 ");
            }

            EnemyCollectionInstance generated = new(definition, sequence);
            if (!_collectionsById.TryAdd(generated.CollectionInstanceId, generated))
            {
                throw new InvalidOperationException("预计生成收藏品与现有实例标识重复。 ");
            }

            checked
            {
                _nextCollectionSequence++;
            }
        }
    }

    /// <summary>仅承载显式恢复定义且没有额外可变对象状态的生成牌。</summary>
    private sealed class RestoredEnemyCard : BaseEnemyCard
    {
        /// <summary>从已校验定义创建尚未绑定运行时身份的实例。</summary>
        /// <param name="definition">显式目录中的不可变定义。</param>
        public RestoredEnemyCard(EnemyCardDefinition definition) : base(definition)
        {
        }
    }
}
