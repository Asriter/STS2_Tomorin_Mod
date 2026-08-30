using MegaCrit.Sts2.Core.Entities.Cards;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 区分作词支付与不触发作词的普通素材支付。
/// </summary>
public enum EnemyMaterialPaymentKind
{
    /// <summary>按作词素材资格和作词优先级支付。</summary>
    Compose,

    /// <summary>按非作词素材资格和非作词优先级支付。</summary>
    NonCompose
}

/// <summary>
/// 描述一次素材支付中的一种牌类型和所需数量。
/// </summary>
public sealed class EnemyMaterialRequirement
{
    /// <summary>
    /// 创建一种素材需求。
    /// </summary>
    /// <param name="cardType">所需牌类型；空值表示允许任意类型。</param>
    /// <param name="count">必须完整预留的数量。</param>
    public EnemyMaterialRequirement(CardType? cardType, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "素材需求数量必须为正数。");
        }

        CardType = cardType;
        Count = count;
    }

    /// <summary>获取所需牌类型；空值表示允许任意类型。</summary>
    public CardType? CardType { get; }

    /// <summary>获取必须完整预留的数量。</summary>
    public int Count { get; }
}

/// <summary>
/// 描述一次必须完整满足的敌人素材支付请求。
/// </summary>
public sealed class EnemyMaterialRequest
{
    /// <summary>
    /// 创建包含一个或多个有序需求的素材请求。
    /// </summary>
    /// <param name="paymentKind">作词或非作词支付种类。</param>
    /// <param name="requirements">按绑定顺序排列的素材需求。</param>
    public EnemyMaterialRequest(
        EnemyMaterialPaymentKind paymentKind,
        IEnumerable<EnemyMaterialRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        EnemyMaterialRequirement[] ordered = requirements.ToArray();
        if (ordered.Length == 0 || ordered.Any(requirement => requirement is null))
        {
            throw new ArgumentException("素材请求必须包含至少一个非空需求。", nameof(requirements));
        }

        if (paymentKind == EnemyMaterialPaymentKind.Compose &&
            ordered.Any(requirement => requirement.CardType is null))
        {
            throw new ArgumentException("作词素材需求必须指定牌类型。", nameof(requirements));
        }

        if (paymentKind == EnemyMaterialPaymentKind.Compose &&
            ordered.Any(requirement => requirement.CardType is not (
                CardType.Attack or CardType.Skill or CardType.Status)))
        {
            throw new ArgumentException("作词素材只支持攻击、技能或状态牌类型。", nameof(requirements));
        }

        PaymentKind = paymentKind;
        Requirements = Array.AsReadOnly(ordered);
        ProgramId = BuildProgramId();
    }

    /// <summary>获取支付采用的作词或非作词规则。</summary>
    public EnemyMaterialPaymentKind PaymentKind { get; }

    /// <summary>获取按绑定顺序排列的只读素材需求。</summary>
    public IReadOnlyList<EnemyMaterialRequirement> Requirements { get; }

    /// <summary>获取可直接参与卡牌定义指纹的稳定素材程序标识。</summary>
    public string ProgramId { get; }

    /// <summary>
    /// 创建单一牌类型的作词素材请求。
    /// </summary>
    /// <param name="cardType">攻击、技能或状态牌类型。</param>
    /// <param name="count">必须完整预留的数量。</param>
    /// <returns>作词素材请求。</returns>
    public static EnemyMaterialRequest Compose(CardType cardType, int count) =>
        new(EnemyMaterialPaymentKind.Compose, [new EnemyMaterialRequirement(cardType, count)]);

    /// <summary>
    /// 创建包含多个有序牌类型需求的作词素材请求。
    /// </summary>
    /// <param name="requirements">按绑定顺序排列的素材需求。</param>
    /// <returns>作词素材请求。</returns>
    public static EnemyMaterialRequest Compose(IEnumerable<EnemyMaterialRequirement> requirements) =>
        new(EnemyMaterialPaymentKind.Compose, requirements);

    /// <summary>
    /// 创建单一牌类型的非作词素材请求。
    /// </summary>
    /// <param name="cardType">普通资格过滤所需的牌类型。</param>
    /// <param name="count">必须完整预留的数量。</param>
    /// <returns>非作词素材请求。</returns>
    public static EnemyMaterialRequest NonCompose(CardType cardType, int count) =>
        new(EnemyMaterialPaymentKind.NonCompose, [new EnemyMaterialRequirement(cardType, count)]);

    /// <summary>
    /// 创建允许任意其他手牌或任意收藏品的非作词素材请求。
    /// </summary>
    /// <param name="count">必须完整预留的数量。</param>
    /// <returns>无类型限制的非作词素材请求。</returns>
    public static EnemyMaterialRequest NonComposeAny(int count) =>
        new(EnemyMaterialPaymentKind.NonCompose, [new EnemyMaterialRequirement(null, count)]);

    /// <summary>
    /// 从支付种类和有序需求构造不依赖对象地址的稳定程序标识。
    /// </summary>
    /// <returns>可用于定义指纹的素材程序标识。</returns>
    private string BuildProgramId() =>
        $"MATERIAL:{PaymentKind}:{string.Join(",", Requirements.Select(requirement =>
            $"{requirement.CardType?.ToString() ?? "ANY"}x{requirement.Count}"))}";
}
