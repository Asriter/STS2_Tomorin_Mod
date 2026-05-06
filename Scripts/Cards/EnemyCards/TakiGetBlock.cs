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
/// Taki状态卡：每回合开始获得5点格挡
/// </summary>
[Pool(typeof(TokenCardPool))]
public class TakiGetBlock() : BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, false), Taki.IChoosable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public async Task OnChosen()
    {
        await PowerCmd.Apply<TakiGetBlockPower>(Owner.Creature, 1m, Owner.Creature, this);
    }
}
