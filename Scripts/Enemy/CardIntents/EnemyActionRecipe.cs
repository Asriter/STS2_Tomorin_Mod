using System.Collections.Frozen;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 描述一个配方槽位的软 Tag 偏好与不可绕过的稳定资格条件。
/// </summary>
public sealed record EnemyActionSlotRule
{
    public EnemyActionSlotRule(
        EnemyCardTag? RequiredTag,
        IReadOnlySet<EnemyCardId>? AllowedDefinitionIds = null,
        bool MustMatchSelectedComposeMaterial = false)
    {
        if (RequiredTag == EnemyCardTag.None ||
            RequiredTag is not null && !EnemyActionRecipe.IsSingleTag(RequiredTag.Value))
        {
            throw new ArgumentException(
                "指定标签槽位必须恰好包含一个非 None 标签；随机槽位请使用空值。",
                nameof(RequiredTag));
        }

        if (AllowedDefinitionIds is not null &&
            (AllowedDefinitionIds.Count == 0 || AllowedDefinitionIds.Any(id => !id.IsValid)))
        {
            throw new ArgumentException("DefinitionId 资格集合必须非空且全部有效。", nameof(AllowedDefinitionIds));
        }

        this.RequiredTag = RequiredTag;
        this.AllowedDefinitionIds = AllowedDefinitionIds is null
            ? null
            : AllowedDefinitionIds.ToFrozenSet();
        this.MustMatchSelectedComposeMaterial = MustMatchSelectedComposeMaterial;
    }

    public EnemyCardTag? RequiredTag { get; }
    public IReadOnlySet<EnemyCardId>? AllowedDefinitionIds { get; }
    public bool MustMatchSelectedComposeMaterial { get; }
}

/// <summary>
/// 保存一个候选中 Compose 来源及其即时攻击组合的硬上限。
/// </summary>
public sealed record EnemyCandidateConstraints
{
    public EnemyCandidateConstraints(
        int MaxComposeSources,
        int MaxImmediateAttackComposeSources,
        int MaxComposeSourcesProducingImmediateAttack)
    {
        if (MaxComposeSources < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxComposeSources));
        }

        if (MaxImmediateAttackComposeSources < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxImmediateAttackComposeSources));
        }

        if (MaxComposeSourcesProducingImmediateAttack < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxComposeSourcesProducingImmediateAttack));
        }

        this.MaxComposeSources = MaxComposeSources;
        this.MaxImmediateAttackComposeSources = MaxImmediateAttackComposeSources;
        this.MaxComposeSourcesProducingImmediateAttack = MaxComposeSourcesProducingImmediateAttack;
    }

    public static EnemyCandidateConstraints Unrestricted { get; } =
        new(int.MaxValue, int.MaxValue, int.MaxValue);

    public int MaxComposeSources { get; }
    public int MaxImmediateAttackComposeSources { get; }
    public int MaxComposeSourcesProducingImmediateAttack { get; }
}

/// <summary>
/// 保存一个行动指标从左到右的固定槽位配方及候选硬约束。
/// </summary>
public sealed class EnemyActionRecipe
{
    /// <summary>从兼容旧测试牌组的 Tag 槽位创建无额外限制的配方。</summary>
    public EnemyActionRecipe(EnemyActionMetric metric, IEnumerable<EnemyCardTag?> slots)
        : this(
            metric,
            (slots ?? throw new ArgumentNullException(nameof(slots)))
                .Select(tag => new EnemyActionSlotRule(tag)),
            EnemyCandidateConstraints.Unrestricted)
    {
    }

    /// <summary>从稳定槽位资格与候选约束创建不可变行动配方。</summary>
    public EnemyActionRecipe(
        EnemyActionMetric metric,
        IEnumerable<EnemyActionSlotRule> slots,
        EnemyCandidateConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(slots);
        EnemyActionSlotRule[] copied = slots.ToArray();
        if (copied.Length == 0 || copied.Any(slot => slot is null))
        {
            throw new ArgumentException("行动指标配方必须至少包含一个非空槽位。", nameof(slots));
        }

        if (!Enum.IsDefined(metric))
        {
            throw new ArgumentOutOfRangeException(nameof(metric));
        }

        Metric = metric;
        Slots = Array.AsReadOnly(copied);
        Constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
    }

    public EnemyActionMetric Metric { get; }
    public IReadOnlyList<EnemyActionSlotRule> Slots { get; }
    public EnemyCandidateConstraints Constraints { get; }

    internal static bool IsSingleTag(EnemyCardTag tag)
    {
        int value = (int)tag;
        return value > 0 && (value & (value - 1)) == 0 && Enum.IsDefined(tag);
    }
}
