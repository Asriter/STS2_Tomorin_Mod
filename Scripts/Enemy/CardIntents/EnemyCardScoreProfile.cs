namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存一张敌人牌本体执行一次时参与软锁的直接贡献。
/// </summary>
public sealed record EnemyCardScoreProfile
{
    /// <summary>
    /// 创建不可变评分档案。
    /// </summary>
    /// <param name="attack">准备时修正前的直接攻击总伤害。</param>
    /// <param name="block">准备时修正前的自身格挡。</param>
    /// <param name="buffPowerStacks">力量、敏捷和心之壁以外的普通 Power 层数。</param>
    /// <param name="strength">自身力量层数。</param>
    /// <param name="dexterity">自身敏捷层数。</param>
    /// <param name="atField">自身心之壁层数。</param>
    /// <param name="vulnerable">施加给目标的易伤层数。</param>
    /// <param name="otherDebuff">施加给目标的其他 Debuff 层数。</param>
    /// <param name="normalCollection">获得的普通收藏品价值。</param>
    /// <param name="starStone">获得的星石收藏品价值。</param>
    /// <param name="abilityHint">本次打出会激活的能力静态提示值。</param>
    /// <param name="deferredTokenHint">第一个非即时 Token 的静态提示值。</param>
    public EnemyCardScoreProfile(
        decimal attack = 0m,
        decimal block = 0m,
        decimal buffPowerStacks = 0m,
        decimal strength = 0m,
        decimal dexterity = 0m,
        decimal atField = 0m,
        decimal vulnerable = 0m,
        decimal otherDebuff = 0m,
        decimal normalCollection = 0m,
        decimal starStone = 0m,
        decimal abilityHint = 0m,
        decimal deferredTokenHint = 0m)
    {
        ValidateNonNegative(attack, nameof(attack));
        ValidateNonNegative(block, nameof(block));
        ValidateNonNegative(buffPowerStacks, nameof(buffPowerStacks));
        ValidateNonNegative(strength, nameof(strength));
        ValidateNonNegative(dexterity, nameof(dexterity));
        ValidateNonNegative(atField, nameof(atField));
        ValidateNonNegative(vulnerable, nameof(vulnerable));
        ValidateNonNegative(otherDebuff, nameof(otherDebuff));
        ValidateNonNegative(normalCollection, nameof(normalCollection));
        ValidateNonNegative(starStone, nameof(starStone));
        ValidateNonNegative(abilityHint, nameof(abilityHint));
        ValidateNonNegative(deferredTokenHint, nameof(deferredTokenHint));
        Attack = attack;
        Block = block;
        OtherPersistentPower = buffPowerStacks;
        Strength = strength;
        Dexterity = dexterity;
        AtField = atField;
        Vulnerable = vulnerable;
        OtherDebuff = otherDebuff;
        NormalCollection = normalCollection;
        StarStone = starStone;
        AbilityHint = abilityHint;
        DeferredTokenHint = deferredTokenHint;
    }

    /// <summary>获取所有贡献均为零的共享不可变档案。</summary>
    public static EnemyCardScoreProfile Zero { get; } = new();

    /// <summary>获取直接攻击总伤害。</summary>
    public decimal Attack { get; }

    /// <summary>获取自身格挡贡献。</summary>
    public decimal Block { get; }

    /// <summary>获取力量、敏捷和心之壁以外的普通持续 Power 层数。</summary>
    public decimal OtherPersistentPower { get; }

    /// <summary>兼容旧测试目录的普通 Buff Power 名称。</summary>
    public decimal BuffPowerStacks => OtherPersistentPower;

    /// <summary>获取力量层数。</summary>
    public decimal Strength { get; }

    /// <summary>获取敏捷层数。</summary>
    public decimal Dexterity { get; }

    /// <summary>获取心之壁层数。</summary>
    public decimal AtField { get; }

    /// <summary>兼容旧调用方的心之壁属性名称。</summary>
    public decimal atField => AtField;

    /// <summary>获取施加给目标的易伤层数。</summary>
    public decimal Vulnerable { get; }

    /// <summary>获取施加给目标的其他 Debuff 层数。</summary>
    public decimal OtherDebuff { get; }

    /// <summary>获取普通收藏品静态价值。</summary>
    public decimal NormalCollection { get; }

    /// <summary>获取星石收藏品静态价值。</summary>
    public decimal StarStone { get; }

    /// <summary>获取本次能力激活的静态提示值。</summary>
    public decimal AbilityHint { get; }

    /// <summary>获取第一个延迟 Token 的静态提示值。</summary>
    public decimal DeferredTokenHint { get; }

    /// <summary>
    /// 校验评分贡献不为负数。
    /// </summary>
    /// <param name="value">待校验贡献。</param>
    /// <param name="parameterName">用于异常诊断的参数名。</param>
    private static void ValidateNonNegative(decimal value, string parameterName)
    {
        if (value < decimal.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, "敌人卡牌评分贡献不能为负数。");
        }
    }
}
