namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 列出敌人卡牌逻辑向视图、诊断和同步层发布的确定性事件种类。
/// </summary>
public enum EnemyCardResolutionEventType
{
    /// <summary>行动已经完成冻结并提交。</summary>
    ActionPrepared,
    /// <summary>卡牌因素材不足或已被提前消费而不可执行。</summary>
    CardMarkedUnplayable,
    /// <summary>一组素材已经完成原子预留。</summary>
    MaterialReserved,
    /// <summary>普通卡牌素材已经进入消耗堆。</summary>
    CardConsumed,
    /// <summary>收藏品素材已经进入已消耗区。</summary>
    CollectionConsumed,
    /// <summary>新收藏品已经追加到可用队列。</summary>
    CollectionGenerated,
    /// <summary>即时牌已经压入深度优先步骤栈。</summary>
    ImmediateCardQueued,
    /// <summary>后续重放因无法完整支付素材而截断。</summary>
    ReplayTruncated,
    /// <summary>一次来源牌或受控灵感执行单元已经成功完成。</summary>
    CardResolved,
    /// <summary>敌人死亡、离场或战斗结束导致行动正常中断。</summary>
    ActionInterrupted,
    /// <summary>结构或效果处理异常导致执行故障。</summary>
    ExecutionFaulted,
    /// <summary>行动全部步骤已经完成。</summary>
    ActionCompleted
}

/// <summary>
/// 表示一项不持有 UI 对象的稳定敌人卡牌结算事件。
/// </summary>
/// <param name="Type">事件种类。</param>
/// <param name="StepSequence">行动内单调递增的步骤序号。</param>
/// <param name="CardKey">相关卡牌实例身份；无卡牌时为空。</param>
/// <param name="CollectionInstanceId">相关收藏品实例身份；无收藏品时为空。</param>
/// <param name="ReplayIndex">本体为零、后续重放依次递增的索引。</param>
/// <param name="Diagnostic">可选中文诊断信息。</param>
public sealed record EnemyCardResolutionEvent(
    EnemyCardResolutionEventType Type,
    long StepSequence,
    EnemyCardInstanceKey? CardKey = null,
    string? CollectionInstanceId = null,
    int ReplayIndex = 0,
    string? Diagnostic = null);
