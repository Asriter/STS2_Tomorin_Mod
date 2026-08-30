namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 指定敌人卡牌在至少成功执行一次后的最终去向。
/// </summary>
public enum EnemyCardLifecycle
{
    /// <summary>成功后进入弃牌堆。</summary>
    Discard,

    /// <summary>成功后进入消耗堆。</summary>
    Exhaust
}

/// <summary>
/// 指定敌人卡牌一次都未成功执行时的处理方式。
/// </summary>
public enum EnemyCardFailureDisposition
{
    /// <summary>不可执行时进入弃牌堆。</summary>
    Discard,

    /// <summary>不可执行时继续保留到下一回合。</summary>
    Retain
}

/// <summary>
/// 指定作词结果加入敌人结算流程的时机。
/// </summary>
public enum EnemyCardTokenTiming
{
    /// <summary>卡牌不生成作词结果。</summary>
    None,

    /// <summary>结果在当前来源牌之后立即深度优先执行。</summary>
    Immediate,

    /// <summary>结果进入保留区并在下一回合作为强制前缀。</summary>
    RetainedNextTurn
}
