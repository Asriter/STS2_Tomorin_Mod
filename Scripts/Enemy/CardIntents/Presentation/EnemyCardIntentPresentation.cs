namespace STS2_Tomorin_Mod.Enemy.CardIntents.Presentation;

/// <summary>
/// 表示单张公开敌人卡牌及其按固定类别顺序排列的纯展示效果。
/// </summary>
public sealed record EnemyCardIntentPresentation
{
    /// <summary>
    /// 创建一张公开敌人卡牌的不可变展示模型。
    /// </summary>
    /// <param name="cardInstanceKey">公开卡牌的稳定实例键。</param>
    /// <param name="card">供缩略牌及悬停预览绑定的领域卡牌实例。</param>
    /// <param name="effects">按攻击、格挡、Buff、Debuff、Unknown 排列的效果。</param>
    public EnemyCardIntentPresentation(
        EnemyCardInstanceKey cardInstanceKey,
        BaseEnemyCard card,
        IEnumerable<EnemyCardEffectIntentPresentation> effects)
    {
        CardInstanceKey = cardInstanceKey ?? throw new ArgumentNullException(nameof(cardInstanceKey));
        Card = card ?? throw new ArgumentNullException(nameof(card));
        Effects = Array.AsReadOnly((effects ?? throw new ArgumentNullException(nameof(effects))).ToArray());
    }

    /// <summary>获取公开卡牌的稳定实例键。</summary>
    public EnemyCardInstanceKey CardInstanceKey { get; }

    /// <summary>获取供专用视图绑定的领域卡牌实例。</summary>
    public BaseEnemyCard Card { get; }

    /// <summary>获取按固定类别顺序排列的不可变效果集合。</summary>
    public IReadOnlyList<EnemyCardEffectIntentPresentation> Effects { get; }
}

/// <summary>
/// 表示能够映射为原版 Intent 的单项敌人卡牌效果类别。
/// </summary>
public abstract record EnemyCardEffectIntentPresentation;

/// <summary>
/// 表示一组基础单次伤害相同、可映射为原版单段或多段攻击 Intent 的命中。
/// </summary>
/// <param name="BaseDamage">交给原版攻击 Intent 继续计算本地玩家实时修正的单次基础伤害。</param>
/// <param name="HitCount">跨重放与子步骤归并后的命中次数。</param>
public sealed record EnemyAttackPresentation(decimal BaseDamage, int HitCount)
    : EnemyCardEffectIntentPresentation;

/// <summary>表示至少存在一次正向敌人格挡变化。</summary>
public sealed record EnemyDefendPresentation : EnemyCardEffectIntentPresentation;

/// <summary>表示至少存在一次敌人自身 Power 变化。</summary>
public sealed record EnemyBuffPresentation : EnemyCardEffectIntentPresentation;

/// <summary>表示至少存在一次玩家目标 Power 变化。</summary>
public sealed record EnemyDebuffPresentation : EnemyCardEffectIntentPresentation;

/// <summary>
/// 表示单张公开卡牌存在无法安全映射的投影结构。
/// </summary>
/// <param name="Diagnostic">稳定且可记录的错误诊断。</param>
public sealed record EnemyUnknownPresentation(string Diagnostic)
    : EnemyCardEffectIntentPresentation;

/// <summary>
/// 表示完整卡列的不可变逐牌展示结果及卡列级不完整投影状态。
/// </summary>
public sealed record EnemyCardListPresentation
{
    /// <summary>
    /// 创建保持公开卡列顺序的不可变展示结果。
    /// </summary>
    /// <param name="cards">按公开卡列顺序排列的逐牌展示。</param>
    /// <param name="requiresGlobalUnknown">卡列是否需要额外显示全局 Unknown。</param>
    /// <param name="diagnostics">构建与投影不完整诊断的有序集合。</param>
    public EnemyCardListPresentation(
        IEnumerable<EnemyCardIntentPresentation> cards,
        bool requiresGlobalUnknown,
        IEnumerable<string>? diagnostics = null)
    {
        Cards = Array.AsReadOnly((cards ?? throw new ArgumentNullException(nameof(cards))).ToArray());
        RequiresGlobalUnknown = requiresGlobalUnknown;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    /// <summary>获取严格保持公开卡列顺序的逐牌展示。</summary>
    public IReadOnlyList<EnemyCardIntentPresentation> Cards { get; }

    /// <summary>获取是否需要在卡列级额外显示原版 Unknown Intent。</summary>
    public bool RequiresGlobalUnknown { get; }

    /// <summary>获取投影或映射错误的不可变有序诊断。</summary>
    public IReadOnlyList<string> Diagnostics { get; }
}
