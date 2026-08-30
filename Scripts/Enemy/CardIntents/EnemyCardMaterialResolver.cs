using MegaCrit.Sts2.Core.Entities.Cards;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 冻结准备后手牌与收藏品队列的素材解析输入，不拥有任何写入口。
/// </summary>
public sealed class EnemyMaterialContext
{
    /// <summary>
    /// 从准备后的 CurrentCards 与当前可用收藏品创建输入快照。
    /// </summary>
    /// <param name="hand">按手牌稳定顺序排列的卡牌素材候选。</param>
    /// <param name="inventory">收藏品权威库存。</param>
    /// <param name="sourceCardInstanceKey">必须排除的来源卡牌实例键。</param>
    public EnemyMaterialContext(
        IEnumerable<EnemyMaterialCandidate> hand,
        EnemyCollectionInventory inventory,
        EnemyCardInstanceKey sourceCardInstanceKey)
    {
        ArgumentNullException.ThrowIfNull(hand);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(sourceCardInstanceKey);
        EnemyMaterialCandidate[] handSnapshot = hand.ToArray();
        if (handSnapshot.Any(candidate => candidate is null || candidate.Source != EnemyMaterialSource.Hand))
        {
            throw new ArgumentException("素材手牌只能包含非空的手牌候选。", nameof(hand));
        }

        if (handSnapshot.Select(candidate => candidate.CandidateId)
            .Distinct(StringComparer.Ordinal).Count() != handSnapshot.Length)
        {
            throw new ArgumentException("素材手牌包含重复卡牌实例键。", nameof(hand));
        }

        Hand = Array.AsReadOnly(handSnapshot);
        Collections = Array.AsReadOnly(
            inventory.Available.Select(EnemyMaterialCandidate.FromCollection).ToArray());
        SourceCardInstanceKey = sourceCardInstanceKey;
    }

    /// <summary>获取按准备后 CurrentCards 顺序冻结的手牌候选。</summary>
    public IReadOnlyList<EnemyMaterialCandidate> Hand { get; }

    /// <summary>获取按权威可用队列顺序冻结的收藏品候选。</summary>
    public IReadOnlyList<EnemyMaterialCandidate> Collections { get; }

    /// <summary>获取永远不得被消费的来源卡牌实例键。</summary>
    public EnemyCardInstanceKey SourceCardInstanceKey { get; }
}

/// <summary>
/// 按三套显式优先级解析素材，并且只返回完整且不修改状态的有序预留。
/// </summary>
public sealed class EnemyCardMaterialResolver
{
    /// <summary>
    /// 尝试为当前一次支付完整绑定全部素材。
    /// </summary>
    /// <param name="request">包含有序需求的素材请求。</param>
    /// <param name="context">准备后手牌、收藏品和来源身份的冻结输入。</param>
    /// <param name="reservation">成功时返回完整绑定；失败时返回无部分绑定的空预留。</param>
    /// <returns>全部需求都能由不同合法实例满足时为 <see langword="true"/>。</returns>
    public bool TryReserve(
        EnemyMaterialRequest request,
        EnemyMaterialContext context,
        out EnemyMaterialReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        List<EnemyMaterialBinding> bindings = [];
        HashSet<string> selectedIds = new(StringComparer.Ordinal);

        for (int requirementIndex = 0; requirementIndex < request.Requirements.Count; requirementIndex++)
        {
            EnemyMaterialRequirement requirement = request.Requirements[requirementIndex];
            for (int slot = 0; slot < requirement.Count; slot++)
            {
                EnemyMaterialCandidate? candidate = BuildPriorityOrder(request, requirement, context)
                    .FirstOrDefault(item => selectedIds.Add(item.CandidateId));
                if (candidate is null)
                {
                    reservation = EnemyMaterialReservation.CreateIncomplete();
                    return false;
                }

                bindings.Add(new EnemyMaterialBinding(requirementIndex, requirement, candidate));
            }
        }

        reservation = EnemyMaterialReservation.CreateComplete(bindings);
        return true;
    }

    /// <summary>
    /// 根据支付种类和需求牌类型构造互斥层级的稳定候选顺序。
    /// </summary>
    /// <param name="request">当前支付请求。</param>
    /// <param name="requirement">当前需求槽位。</param>
    /// <param name="context">冻结素材输入。</param>
    /// <returns>资格先于优先级且同一实例只出现一次的候选序列。</returns>
    private static IReadOnlyList<EnemyMaterialCandidate> BuildPriorityOrder(
        EnemyMaterialRequest request,
        EnemyMaterialRequirement requirement,
        EnemyMaterialContext context)
    {
        IEnumerable<EnemyMaterialCandidate> hand = context.Hand.Where(candidate =>
            candidate.CardInstanceKey != context.SourceCardInstanceKey);
        IEnumerable<EnemyMaterialCandidate> collections = context.Collections;

        if (request.PaymentKind == EnemyMaterialPaymentKind.NonCompose)
        {
            return ConcatenateDistinct(
                hand.Where(candidate => candidate.IsInspiration && MatchesOrdinaryType(candidate, requirement)),
                collections.Where(candidate => MatchesOrdinaryType(candidate, requirement)),
                hand.Where(candidate =>
                    !candidate.IsInspiration &&
                    candidate.IsEpiphany &&
                    MatchesOrdinaryType(candidate, requirement)),
                hand.Where(candidate =>
                    !candidate.IsInspiration &&
                    !candidate.IsEpiphany &&
                    MatchesOrdinaryType(candidate, requirement)));
        }

        if (requirement.CardType == CardType.Status)
        {
            return ConcatenateDistinct(
                collections.Where(candidate => MatchesOrdinaryType(candidate, requirement)),
                hand.Where(candidate => candidate.IsEpiphany),
                hand.Where(candidate =>
                    !candidate.IsInspiration &&
                    !candidate.IsEpiphany &&
                    MatchesOrdinaryType(candidate, requirement)));
        }

        return ConcatenateDistinct(
            collections.Where(candidate => candidate.IsEpiphany),
            hand.Where(candidate =>
                candidate.IsInspiration && MatchesOrdinaryType(candidate, requirement)),
            hand.Where(candidate =>
                !candidate.IsInspiration && candidate.IsEpiphany),
            hand.Where(candidate =>
                !candidate.IsInspiration &&
                !candidate.IsEpiphany &&
                MatchesOrdinaryType(candidate, requirement)));
    }

    /// <summary>
    /// 判断普通资格是否满足请求牌类型；无类型非作词需求接受任意候选。
    /// </summary>
    /// <param name="candidate">待判断候选。</param>
    /// <param name="requirement">当前素材需求。</param>
    /// <returns>候选类型符合需求时为 <see langword="true"/>。</returns>
    private static bool MatchesOrdinaryType(
        EnemyMaterialCandidate candidate,
        EnemyMaterialRequirement requirement) =>
        requirement.CardType is null || candidate.CardType == requirement.CardType;

    /// <summary>
    /// 合并互斥优先层，并以稳定实例标识防御性去重。
    /// </summary>
    /// <param name="tiers">从最高到最低排列的候选层级。</param>
    /// <returns>保持各层和层内输入顺序的只读候选列表。</returns>
    private static IReadOnlyList<EnemyMaterialCandidate> ConcatenateDistinct(
        params IEnumerable<EnemyMaterialCandidate>[] tiers)
    {
        List<EnemyMaterialCandidate> ordered = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (IEnumerable<EnemyMaterialCandidate> tier in tiers)
        {
            foreach (EnemyMaterialCandidate candidate in tier)
            {
                if (seen.Add(candidate.CandidateId))
                {
                    ordered.Add(candidate);
                }
            }
        }

        return ordered.AsReadOnly();
    }
}
