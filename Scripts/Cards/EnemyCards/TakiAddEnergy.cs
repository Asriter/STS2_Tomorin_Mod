using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Enemy.Ememies;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// Taki状态卡：能量最大值+1
/// </summary>
[Pool(typeof(TokenCardPool))]
public class TakiAddEnergy() : BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, false), Taki.IChoosable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public async Task OnChosen()
    {
        await PowerCmd.Apply<TakiAddEnergyPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 1m, Owner.Creature, this);
    }

}
