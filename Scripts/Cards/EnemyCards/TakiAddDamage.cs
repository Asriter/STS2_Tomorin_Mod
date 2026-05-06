using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Enemy.Ememies;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards.EnemyCards;

/// <summary>
/// Taki状态卡：有卡被消耗时，下一张卡的伤害变成两倍
/// </summary>
[Pool(typeof(TokenCardPool))]
public class TakiAddDamage() : BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, false), Taki.IChoosable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public async Task OnChosen()
    {
        await PowerCmd.Apply<TakiAddDamagePower>(Owner.Creature, 1m, Owner.Creature, this);
    }
}
