using MegaCrit.Sts2.Core.Entities.Cards;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 标识素材候选来自准备后的敌人手牌还是收藏品可用队列。
/// </summary>
public enum EnemyMaterialSource
{
    /// <summary>候选来自准备后的 CurrentCards。</summary>
    Hand,

    /// <summary>候选来自收藏品权威可用队列。</summary>
    Collection
}

/// <summary>
/// 提供冻结素材规划所需的卡牌或收藏品实例身份、类型和关键词视图。
/// </summary>
public sealed class EnemyMaterialCandidate
{
    /// <summary>
    /// 创建已经完成区域一致性校验的素材候选。
    /// </summary>
    /// <param name="source">候选来源区域。</param>
    /// <param name="cardInstanceKey">手牌候选的稳定实例键。</param>
    /// <param name="collection">收藏品候选实例。</param>
    /// <param name="cardType">素材资格使用的牌类型。</param>
    /// <param name="isInspiration">是否具有灵感。</param>
    /// <param name="isEpiphany">是否具有灵光。</param>
    private EnemyMaterialCandidate(
        EnemyMaterialSource source,
        EnemyCardInstanceKey? cardInstanceKey,
        EnemyCollectionInstance? collection,
        CardType cardType,
        bool isInspiration,
        bool isEpiphany)
    {
        Source = source;
        CardInstanceKey = cardInstanceKey;
        Collection = collection;
        CardType = cardType;
        IsInspiration = isInspiration;
        IsEpiphany = isEpiphany;
        CandidateId = source == EnemyMaterialSource.Hand
            ? $"CARD:{cardInstanceKey!.Value}"
            : $"COLLECTION:{collection!.CollectionInstanceId}";
    }

    /// <summary>获取候选所在素材区域。</summary>
    public EnemyMaterialSource Source { get; }

    /// <summary>获取手牌候选的稳定卡牌实例键；收藏品候选为空。</summary>
    public EnemyCardInstanceKey? CardInstanceKey { get; }

    /// <summary>获取收藏品候选实例；手牌候选为空。</summary>
    public EnemyCollectionInstance? Collection { get; }

    /// <summary>获取收藏品候选的稳定实例标识；手牌候选为空。</summary>
    public string? CollectionInstanceId => Collection?.CollectionInstanceId;

    /// <summary>获取候选参与普通资格过滤的牌类型。</summary>
    public CardType CardType { get; }

    /// <summary>获取手牌候选是否具有灵感；收藏品始终为否。</summary>
    public bool IsInspiration { get; }

    /// <summary>获取候选是否具有灵光通配资格。</summary>
    public bool IsEpiphany { get; }

    /// <summary>获取统一去重使用的稳定候选标识。</summary>
    public string CandidateId { get; }

    /// <summary>
    /// 从准备后的 CurrentCards 创建手牌素材候选。
    /// </summary>
    /// <param name="cardInstanceKey">稳定卡牌实例键。</param>
    /// <param name="cardType">卡牌类型。</param>
    /// <param name="isInspiration">是否具有灵感。</param>
    /// <param name="isEpiphany">是否具有灵光。</param>
    /// <returns>手牌素材候选。</returns>
    public static EnemyMaterialCandidate FromHand(
        EnemyCardInstanceKey cardInstanceKey,
        CardType cardType,
        bool isInspiration,
        bool isEpiphany)
    {
        ArgumentNullException.ThrowIfNull(cardInstanceKey);
        return new EnemyMaterialCandidate(
            EnemyMaterialSource.Hand,
            cardInstanceKey,
            null,
            cardType,
            isInspiration,
            isEpiphany);
    }

    /// <summary>
    /// 从已经绑定实例身份的敌人卡牌创建手牌素材候选。
    /// </summary>
    /// <param name="card">准备后 CurrentCards 中的权威卡牌实例。</param>
    /// <param name="isInspiration">是否具有灵感。</param>
    /// <param name="isEpiphany">是否具有灵光。</param>
    /// <returns>只冻结稳定身份和素材资格的手牌候选。</returns>
    public static EnemyMaterialCandidate FromHand(
        BaseEnemyCard card,
        bool isInspiration,
        bool isEpiphany)
    {
        ArgumentNullException.ThrowIfNull(card);
        return FromHand(card.InstanceKey, card.CardModel.Type, isInspiration, isEpiphany);
    }

    /// <summary>
    /// 从冻结的收藏品可用队列创建素材候选。
    /// </summary>
    /// <param name="collection">具有稳定实例标识的收藏品。</param>
    /// <returns>收藏品素材候选。</returns>
    public static EnemyMaterialCandidate FromCollection(EnemyCollectionInstance collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        return new EnemyMaterialCandidate(
            EnemyMaterialSource.Collection,
            null,
            collection,
            collection.Definition.MaterialCardType,
            false,
            collection.Definition.IsEpiphany);
    }
}

/// <summary>
/// 保存一次冻结素材支付中一个需求槽位与一个稳定实例的有序绑定。
/// </summary>
public sealed class EnemyMaterialBinding
{
    /// <summary>
    /// 创建一项有序素材绑定。
    /// </summary>
    /// <param name="requirementIndex">需求在请求中的稳定索引。</param>
    /// <param name="requirement">本绑定满足的素材需求。</param>
    /// <param name="candidate">被绑定的卡牌或收藏品实例。</param>
    public EnemyMaterialBinding(
        int requirementIndex,
        EnemyMaterialRequirement requirement,
        EnemyMaterialCandidate candidate)
    {
        if (requirementIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requirementIndex));
        }

        RequirementIndex = requirementIndex;
        Requirement = requirement ?? throw new ArgumentNullException(nameof(requirement));
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    /// <summary>获取需求在请求中的稳定索引。</summary>
    public int RequirementIndex { get; }

    /// <summary>获取本绑定满足的素材需求。</summary>
    public EnemyMaterialRequirement Requirement { get; }

    /// <summary>获取被绑定的稳定卡牌或收藏品实例。</summary>
    public EnemyMaterialCandidate Candidate { get; }

    /// <summary>获取本绑定来自手牌还是收藏品队列。</summary>
    public EnemyMaterialSource Source => Candidate.Source;

    /// <summary>获取手牌绑定的稳定卡牌实例键；收藏品绑定为空。</summary>
    public EnemyCardInstanceKey? CardInstanceKey => Candidate.CardInstanceKey;

    /// <summary>获取收藏品绑定的稳定实例标识；手牌绑定为空。</summary>
    public string? CollectionInstanceId => Candidate.CollectionInstanceId;

    /// <summary>获取手牌绑定是否具有灵感。</summary>
    public bool IsInspiration => Candidate.IsInspiration;

    /// <summary>获取绑定是否具有灵光。</summary>
    public bool IsEpiphany => Candidate.IsEpiphany;
}

/// <summary>
/// 表示一次只读、完整且有序的素材预留；提交消费由执行引擎另行完成。
/// </summary>
public sealed class EnemyMaterialReservation
{
    private static readonly EnemyMaterialReservation Incomplete = new(false, []);

    /// <summary>
    /// 创建完整或失败的不可变素材预留。
    /// </summary>
    /// <param name="isComplete">是否完整覆盖请求。</param>
    /// <param name="bindings">冻结的有序素材绑定。</param>
    private EnemyMaterialReservation(bool isComplete, IEnumerable<EnemyMaterialBinding> bindings)
    {
        IsComplete = isComplete;
        Bindings = Array.AsReadOnly(bindings.ToArray());
    }

    /// <summary>获取请求是否已完整绑定全部素材。</summary>
    public bool IsComplete { get; }

    /// <summary>获取按需求和优先级冻结的只读绑定顺序。</summary>
    public IReadOnlyList<EnemyMaterialBinding> Bindings { get; }

    /// <summary>
    /// 创建不携带任何部分绑定的失败预留。
    /// </summary>
    /// <returns>共享的不可变失败预留。</returns>
    internal static EnemyMaterialReservation CreateIncomplete() => Incomplete;

    /// <summary>
    /// 从已经全量验证的有序绑定创建完整预留。
    /// </summary>
    /// <param name="bindings">覆盖请求全部槽位的有序绑定。</param>
    /// <returns>不可变完整预留。</returns>
    internal static EnemyMaterialReservation CreateComplete(IEnumerable<EnemyMaterialBinding> bindings) =>
        new(true, bindings);
}
