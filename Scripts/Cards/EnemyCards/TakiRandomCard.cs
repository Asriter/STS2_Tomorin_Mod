using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Enemy.Ememies;

namespace STS2_Tomorin_Mod.Cards.EnemyCards;

/// <summary>
/// Taki状态卡：随机将三张无色牌加入手牌，这回合可以免费打出
/// </summary>
[Pool(typeof(TokenCardPool))]
public class TakiRandomCard() : BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, false), Taki.IChoosable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public async Task OnChosen()
    {
        var player = Owner;
        var colorlessCards = ModelDb.CardPool<ColorlessCardPool>().AllCards
            .Where(c => c.Rarity != CardRarity.Token && c.Rarity != CardRarity.Ancient && c.Rarity != CardRarity.Status && c.Rarity != CardRarity.Curse)
            .ToList();

        if (colorlessCards.Count == 0) return;

        var cards = CardFactory.GetDistinctForCombat(player, colorlessCards, Math.Min(3, colorlessCards.Count), player.RunState.Rng.CombatCardGeneration);
        foreach (var card in cards)
        {
            card.EnergyCost.SetUntilPlayed(0);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
    }
}
