namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存一次标准攻击命中的规范基础伤害与纯模拟后伤害。
/// </summary>
public sealed record EnemyDamageHitProjection(
    decimal BaseDamage,
    decimal ProjectedDamage);

/// <summary>
/// 表示单个玩家目标在一次敌人牌执行单元中的预计变化。
/// </summary>
public sealed record EnemyTargetProjection(
    string TargetId,
    IReadOnlyList<EnemyDamageHitProjection> DamageHits,
    IReadOnlyDictionary<string, decimal> PowerDeltas)
{
    /// <summary>获取该目标预计受到的总伤害。</summary>
    public decimal TotalDamage => DamageHits.Sum(hit => hit.ProjectedDamage);
}

/// <summary>
/// 指定收藏品实例在一次冻结执行单元中的结构变化。
/// </summary>
public enum EnemyCollectionProjectionKind
{
    /// <summary>可用收藏品被消费。</summary>
    Consumed,

    /// <summary>生成一件新收藏品。</summary>
    Generated,

    /// <summary>已消耗收藏品被恢复。</summary>
    Recovered
}

/// <summary>
/// 保存收藏品定义、实例和区域变化的诊断投影。
/// </summary>
public sealed record EnemyCollectionProjection(
    string CollectionInstanceId,
    string CollectionId,
    EnemyCollectionProjectionKind Kind);

/// <summary>
/// 保存作词结果牌的稳定身份、进入时机和实例复用方式。
/// </summary>
public sealed record EnemyGeneratedCardProjection(
    EnemyCardInstanceKey CardInstanceKey,
    EnemyCardId CardId,
    EnemyCardTokenTiming Timing,
    bool IncreasesExistingReplay);

/// <summary>
/// 表示一张来源牌某次重放的顺序化预计结果。
/// </summary>
public sealed record EnemyCardReplayProjection(
    EnemyCardInstanceKey RootSourceKey,
    EnemyCardInstanceKey ExecutingCardKey,
    EnemyCardId ExecutingCardId,
    int ReplayIndex,
    IReadOnlyList<EnemyTargetProjection> Targets,
    decimal EnemyBlockDelta,
    IReadOnlyDictionary<string, decimal> EnemyPowerDeltas,
    IReadOnlyList<EnemyCollectionProjection> CollectionDeltas,
    IReadOnlyList<EnemyGeneratedCardProjection> GeneratedCards);

public sealed record EnemyIntentEffectProjection(
    Presentation.EnemyIntentDisplayKey DisplayKey,
    EnemyCardInstanceKey RootSourceKey,
    EnemyCardInstanceKey? ExecutingCardKey,
    EnemyCardId? ExecutingCardId,
    int ReplayIndex,
    IReadOnlyList<EnemyTargetProjection> Targets,
    decimal EnemyBlockDelta,
    IReadOnlyDictionary<string, decimal> EnemyPowerDeltas);

/// <summary>
/// 表示当前冻结行动基于实时 Power 输入计算出的完整只读投影。
/// </summary>
public sealed class LiveActionProjection
{
    /// <summary>
    /// 创建一次实时行动投影。
    /// </summary>
    /// <param name="units">按实际执行顺序排列的逐重放结果。</param>
    /// <param name="isComplete">已知适配器是否覆盖全部数值修正。</param>
    /// <param name="diagnostics">投影不完整或被截断时的诊断。</param>
    public LiveActionProjection(
        IEnumerable<EnemyCardReplayProjection> units,
        bool isComplete,
        IEnumerable<string>? diagnostics = null,
        IEnumerable<EnemyIntentEffectProjection>? timelineEffects = null,
        IEnumerable<EnemyFrozenEffectiveCardState>? effectiveCardStates = null,
        EnemyProjectionEndState? endState = null,
        EnemyActionRiskScore? riskScore = null,
        IEnumerable<EnemyCardInstanceKey>? unavailableCardKeys = null)
    {
        Units = Array.AsReadOnly((units ?? throw new ArgumentNullException(nameof(units))).ToArray());
        IsComplete = isComplete;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
        TimelineEffects = Array.AsReadOnly((timelineEffects ?? Units.Select(unit =>
            new EnemyIntentEffectProjection(
                Presentation.EnemyIntentDisplayKey.ForCard(unit.ExecutingCardKey),
                unit.RootSourceKey,
                unit.ExecutingCardKey,
                unit.ExecutingCardId,
                unit.ReplayIndex,
                unit.Targets,
                unit.EnemyBlockDelta,
                unit.EnemyPowerDeltas))).ToArray());
        EnemyCardInstanceKey[] unavailable = (unavailableCardKeys ?? []).ToArray();
        if (unavailable.Any(key => key is null) || unavailable.Distinct().Count() != unavailable.Length)
        {
            throw new ArgumentException("不可用卡牌集合不能包含空值或重复实例键。", nameof(unavailableCardKeys));
        }

        UnavailableCardKeys = new HashSet<EnemyCardInstanceKey>(unavailable);
        EnemyFrozenEffectiveCardState[] states = (effectiveCardStates ?? []).ToArray();
        if (states.Any(state => state is null) ||
            states.Select(state => state.ExecutingCardInstanceKey).Distinct().Count() != states.Length)
        {
            throw new ArgumentException("投影有效牌元数据不能包含空值或重复实例键。", nameof(effectiveCardStates));
        }

        EffectiveCardStates = new System.Collections.ObjectModel.ReadOnlyDictionary<
            EnemyCardInstanceKey,
            EnemyFrozenEffectiveCardState>(states.ToDictionary(state => state.ExecutingCardInstanceKey));
        EndState = endState ?? EnemyProjectionEndState.Empty;
        RiskScore = riskScore;
    }

    /// <summary>获取按深度优先执行顺序排列的逐牌逐重放结果。</summary>
    public IReadOnlyList<EnemyCardReplayProjection> Units { get; }

    /// <summary>获取投影是否已由全部已知且纯净的修正器完整计算。</summary>
    public bool IsComplete { get; }

    /// <summary>获取投影不完整、未知修改器或有限步骤截断的诊断集合。</summary>
    public IReadOnlyList<string> Diagnostics { get; }

    public IReadOnlyList<EnemyIntentEffectProjection> TimelineEffects { get; }

    /// <summary>获取因正常出牌条件不满足而置灰、但不会使整项投影失败的卡牌实例。</summary>
    public IReadOnlySet<EnemyCardInstanceKey> UnavailableCardKeys { get; }

    /// <summary>获取与冻结行动共享的逐实际实例 N/X 元数据。</summary>
    public IReadOnlyDictionary<EnemyCardInstanceKey, EnemyFrozenEffectiveCardState> EffectiveCardStates { get; }

    /// <summary>获取行动全部递归效果和生命周期结束后的总存量快照。</summary>
    public EnemyProjectionEndState EndState { get; }

    /// <summary>获取可选的四部分完整风险分；未提供评分上下文时为空。</summary>
    public EnemyActionRiskScore? RiskScore { get; }

    /// <summary>复用同一不可变投影数据并附加风险分。</summary>
    public LiveActionProjection WithRiskScore(EnemyActionRiskScore riskScore) =>
        new(
            Units,
            IsComplete,
            Diagnostics,
            TimelineEffects,
            EffectiveCardStates.Values,
            EndState,
            riskScore ?? throw new ArgumentNullException(nameof(riskScore)),
            UnavailableCardKeys);
}
