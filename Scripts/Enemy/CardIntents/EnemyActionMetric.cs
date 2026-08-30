namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 表示敌人每回合从规则配方中选择的一类行动指标。
/// </summary>
public enum EnemyActionMetric
{
    /// <summary>能力、增益与防御组合。</summary>
    Gain,

    /// <summary>双攻击与双随机组合。</summary>
    Attack,

    /// <summary>收藏品、防御与作词组合。</summary>
    ComposeTest,

    /// <summary>阶段化防御与资源积累。</summary>
    Fortify,

    /// <summary>阶段化攻击压力。</summary>
    Pressure,

    /// <summary>阶段化作词组合。</summary>
    Compose,

    /// <summary>高密度攻击组合。</summary>
    Burst,

    /// <summary>资源转化与成长组合。</summary>
    Growth
}
