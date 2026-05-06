using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Enemy.Ememies;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards.EnemyCards;

/// <summary>
/// Taki状态卡：每回合开始时，获得心之壁层数的临时力量，且心之壁不会减半
/// </summary>
[Pool(typeof(TokenCardPool))]
public class TakiAtField() : BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, false), Taki.IChoosable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public async Task OnChosen()
    {
        await PowerCmd.Apply<TakiAtFieldPower>(Owner.Creature, 1m, Owner.Creature, this);
    }
}
