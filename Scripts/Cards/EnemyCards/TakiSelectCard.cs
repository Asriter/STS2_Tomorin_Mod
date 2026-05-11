using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Enemy.Ememies;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// Taki状态卡：从三张随机稀有本职业卡中选择一张加入手牌，这回合可以免费打出
/// </summary>
[Pool(typeof(TokenCardPool))]
public class TakiSelectCard() : BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, false), Taki.IChoosable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public async Task OnChosen()
    {
        var player = Owner;
        var pool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => c is { Rarity: CardRarity.Rare, Type: not CardType.Status and not CardType.Curse })
            .ToList();

        if (pool.Count == 0) return;

        List<CardModel> cards = CardFactory.GetDistinctForCombat(player, pool, 3, player.RunState.Rng.CombatCardGeneration).ToList();
        var card = await CardSelectCmd.FromChooseACardScreen(new BlockingPlayerChoiceContext(), cards, base.Owner, canSkip: true);
        
        if (card != null)
        {
            card.EnergyCost.SetUntilPlayed(0);
            card.SetStarCostUntilPlayed(0);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
    }
}
