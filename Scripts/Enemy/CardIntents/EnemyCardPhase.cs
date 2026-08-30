namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 表示一副阶段化敌人牌组的稳定内容阶段。
/// </summary>
public enum EnemyCardPhase
{
    /// <summary>旧两参注册牌组的单阶段。</summary>
    None = 0,

    /// <summary>第一阶段。</summary>
    Phase1 = 1,

    /// <summary>第二阶段。</summary>
    Phase2 = 2,

    /// <summary>第三阶段。</summary>
    Phase3 = 3
}

/// <summary>
/// 冻结单个阶段的有序来源实例工厂和规划规则。
/// </summary>
public sealed record EnemyCardPhaseTemplate
{
    /// <summary>
    /// 创建一项不可变阶段模板。
    /// </summary>
    public EnemyCardPhaseTemplate(
        EnemyCardPhase phase,
        IReadOnlyList<Func<BaseEnemyCard>> sourceFactories,
        EnemyCardPlanningRules planningRules,
        int initialSourceInstanceCount)
    {
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "未知敌人卡牌阶段。");
        }

        ArgumentNullException.ThrowIfNull(sourceFactories);
        Func<BaseEnemyCard>[] copied = sourceFactories.ToArray();
        if (copied.Length == 0 || copied.Any(factory => factory is null))
        {
            throw new ArgumentException("阶段模板必须包含至少一个非空来源工厂。", nameof(sourceFactories));
        }

        if (initialSourceInstanceCount != copied.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialSourceInstanceCount),
                "初始来源实例数必须与显式有序工厂数一致。");
        }

        Phase = phase;
        SourceFactories = Array.AsReadOnly(copied);
        PlanningRules = planningRules ?? throw new ArgumentNullException(nameof(planningRules));
        InitialSourceInstanceCount = initialSourceInstanceCount;
    }

    /// <summary>获取本模板所属阶段。</summary>
    public EnemyCardPhase Phase { get; }

    /// <summary>获取包含重复副本且顺序稳定的来源工厂。</summary>
    public IReadOnlyList<Func<BaseEnemyCard>> SourceFactories { get; }

    /// <summary>获取本阶段的不可变规划规则。</summary>
    public EnemyCardPlanningRules PlanningRules { get; }

    /// <summary>获取本阶段应创建的普通来源实例数。</summary>
    public int InitialSourceInstanceCount { get; }
}

/// <summary>
/// 保存从某阶段修订到下一阶段的完整候选状态。
/// </summary>
public sealed record EnemyCardPhaseTransitionCandidate(
    EnemyCardPhase From,
    EnemyCardPhase To,
    long NextRevision,
    EnemyCardCombatState CandidateState);
