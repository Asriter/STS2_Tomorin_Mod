namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存一次候选评分得到的攻击分量与总评分。
/// </summary>
public sealed record EnemyCardScore
{
    /// <summary>
    /// 创建不可变候选评分。
    /// </summary>
    /// <param name="attack">候选直接攻击总量。</param>
    /// <param name="total">按规则权重计算的总评分。</param>
    public EnemyCardScore(decimal attack, decimal total)
    {
        Attack = attack;
        Total = total;
    }

    /// <summary>获取候选直接攻击总量。</summary>
    public decimal Attack { get; }

    /// <summary>获取候选总评分。</summary>
    public decimal Total { get; }
}

/// <summary>
/// 提供准备时当前战斗修正后的逐牌评分投影，同时禁止候选内部状态回灌。
/// </summary>
public sealed class EnemyCardScoreContext
{
    private readonly Func<BaseEnemyCard, EnemyCardScoreProfile> _projectProfile;

    /// <summary>
    /// 创建准备时评分上下文。
    /// </summary>
    /// <param name="projectProfile">根据当前战斗状态投影单张牌本体一次贡献的纯函数。</param>
    public EnemyCardScoreContext(Func<BaseEnemyCard, EnemyCardScoreProfile> projectProfile)
    {
        _projectProfile = projectProfile ?? throw new ArgumentNullException(nameof(projectProfile));
    }

    /// <summary>获取直接读取不可变定义档案的恒等评分上下文。</summary>
    public static EnemyCardScoreContext Identity { get; } = new(card => card.Definition.ScoreProfile);

    /// <summary>
    /// 取得单张牌在准备时当前战斗修正下的一次本体贡献。
    /// </summary>
    /// <param name="card">待评分实例；重放计数不会传入公式。</param>
    /// <returns>不包含保留、即时、Token 或能力收益的档案。</returns>
    public EnemyCardScoreProfile Project(BaseEnemyCard card) =>
        _projectProfile(card) ?? throw new InvalidOperationException($"卡牌 {card.InstanceKey} 的评分投影返回了空档案。");
}

/// <summary>
/// 按规则权重计算指标候选的一次本体直接收益。
/// </summary>
public sealed class EnemyCardScoreCalculator
{
    /// <summary>
    /// 计算候选攻击分量和总评分，明确忽略每张实例的 ReplayCount。
    /// </summary>
    /// <param name="cards">仅包含当前指标槽位选中的来源牌。</param>
    /// <param name="context">准备时战斗修正投影。</param>
    /// <returns>可用于双软锁判断的不可变评分。</returns>
    public EnemyCardScore Calculate(
        IEnumerable<BaseEnemyCard> cards,
        EnemyCardScoreContext context)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(context);
        return CalculateProfiles(cards.Select(context.Project));
    }

    /// <summary>
    /// 计算一组来源牌静态档案的攻击分量和完整静态总分。
    /// </summary>
    /// <param name="profiles">每张来源牌本体一次执行的不可变评分档案。</param>
    /// <returns>可用于第一层软锁判断的不可变评分。</returns>
    public EnemyCardScore CalculateProfiles(IEnumerable<EnemyCardScoreProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        decimal attack = decimal.Zero;
        decimal total = decimal.Zero;
        foreach (EnemyCardScoreProfile profile in profiles)
        {
            ArgumentNullException.ThrowIfNull(profile);
            attack += profile.Attack;
            total += profile.Attack +
                     profile.Block * EnemyCardScoreWeights.Block +
                     profile.Strength * EnemyCardScoreWeights.Strength +
                     profile.Dexterity * EnemyCardScoreWeights.Dexterity +
                     profile.AtField * EnemyCardScoreWeights.HeartWall +
                     profile.OtherPersistentPower * EnemyCardScoreWeights.OtherPersistentPower +
                     profile.Vulnerable * EnemyCardScoreWeights.Vulnerable +
                     profile.OtherDebuff * EnemyCardScoreWeights.OtherDebuff +
                     profile.NormalCollection * EnemyCardScoreWeights.NormalCollection +
                     profile.StarStone * EnemyCardScoreWeights.StarStone +
                     profile.AbilityHint +
                     profile.DeferredTokenHint * EnemyCardScoreWeights.DeferredTokenHint;
        }

        return new EnemyCardScore(attack, total);
    }
}
