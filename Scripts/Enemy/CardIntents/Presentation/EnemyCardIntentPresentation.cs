using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.Presentation;

public sealed record EnemyCardIntentPresentation
{
    public EnemyCardIntentPresentation(EnemyIntentTimelineEntry entry, IEnumerable<EnemyCardEffectIntentPresentation> effects)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        Effects = Array.AsReadOnly((effects ?? throw new ArgumentNullException(nameof(effects))).ToArray());
    }

    public EnemyCardIntentPresentation(
        EnemyCardInstanceKey cardInstanceKey,
        BaseEnemyCard card,
        IEnumerable<EnemyCardEffectIntentPresentation> effects,
        bool isDimmed = false)
        : this(new EnemyIntentTimelineEntry(
            EnemyIntentDisplayKey.ForCard(cardInstanceKey),
            (card ?? throw new ArgumentNullException(nameof(card))).CardModel,
            card.DescriptionOverride,
            EnemyIntentTimelineRole.Source,
            isDimmed,
            cardInstanceKey,
            card.CardId), effects)
    {
        Card = card;
    }

    public EnemyIntentTimelineEntry Entry { get; }
    public EnemyIntentDisplayKey DisplayKey => Entry.DisplayKey;
    public EnemyCardInstanceKey? CardInstanceKey => Entry.CardInstanceKey;
    public CardModel CardModel => Entry.CardModel;
    public string DescriptionOverride => Entry.DescriptionOverride;
    public bool IsDimmed => Entry.IsDimmed;
    public BaseEnemyCard? Card { get; }
    public IReadOnlyList<EnemyCardEffectIntentPresentation> Effects { get; }
}

public abstract record EnemyCardEffectIntentPresentation;
public sealed record EnemyAttackPresentation(decimal BaseDamage, int HitCount) : EnemyCardEffectIntentPresentation;
public sealed record EnemyDefendPresentation : EnemyCardEffectIntentPresentation;
public sealed record EnemyBuffPresentation : EnemyCardEffectIntentPresentation;
public sealed record EnemyDebuffPresentation : EnemyCardEffectIntentPresentation;
public sealed record EnemyUnknownPresentation(string Diagnostic) : EnemyCardEffectIntentPresentation;

public sealed record EnemyCardListPresentation
{
    public EnemyCardListPresentation(
        IEnumerable<EnemyCardIntentPresentation> cards,
        bool requiresGlobalUnknown,
        IEnumerable<string>? diagnostics = null)
    {
        Cards = Array.AsReadOnly((cards ?? throw new ArgumentNullException(nameof(cards))).ToArray());
        RequiresGlobalUnknown = requiresGlobalUnknown;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public IReadOnlyList<EnemyCardIntentPresentation> Cards { get; }
    public bool RequiresGlobalUnknown { get; }
    public IReadOnlyList<string> Diagnostics { get; }
}
