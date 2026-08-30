namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 表示敌人卡牌可参与行动指标匹配的七类语义标签。
/// </summary>
[Flags]
public enum EnemyCardTag
{
    /// <summary>不参与任何指定标签槽位。</summary>
    None = 0,

    /// <summary>能力牌。</summary>
    Ability = 1 << 0,

    /// <summary>施加普通 Power 的牌。</summary>
    Buff = 1 << 1,

    /// <summary>获得力量、敏捷或心之壁的牌。</summary>
    Gain = 1 << 2,

    /// <summary>生成收藏品的牌。</summary>
    CollectionGenerator = 1 << 3,

    /// <summary>获得格挡的牌。</summary>
    Defense = 1 << 4,

    /// <summary>造成攻击伤害的牌。</summary>
    Attack = 1 << 5,

    /// <summary>执行作词流程的牌。</summary>
    Compose = 1 << 6
}
