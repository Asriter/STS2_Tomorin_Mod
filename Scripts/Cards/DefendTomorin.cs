using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

[Pool(typeof(TomorinCardPool))]
public class DefendTomorin() : BaseCardModel(
    canonicalEnergyCost: 1,
    type: CardType.Skill,
    rarity: CardRarity.Basic,
    targetType: TargetType.Self)
{
	public override bool GainsBlock => true;
    
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(5m, ValueProp.Move)];

    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag>() { CardTag.Defend };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Block.UpgradeValueBy(3m);
    }
}