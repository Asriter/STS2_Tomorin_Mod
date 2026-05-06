using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Enemy.Ememies;
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Cards.EnemyCards;

/// <summary>
/// Taki状态卡：所有卡牌获得"灵感"
/// </summary>
[Pool(typeof(TokenCardPool))]
public class TakiInspiration() : BaseCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None, false), Taki.IChoosable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public async Task OnChosen()
    {
        var combatState = Owner.PlayerCombatState;

        var allCards = new List<CardModel>();
        allCards.AddRange(combatState.Hand.Cards);
        allCards.AddRange(combatState.DrawPile.Cards);
        allCards.AddRange(combatState.DiscardPile.Cards);
        allCards.AddRange(combatState.ExhaustPile.Cards);

        foreach (var card in allCards)
        {
            if (!card.Keywords.Contains(CustomKeyWord.Inspiration) && !card.Keywords.Contains(CustomKeyWord.SingleTurnInspiration))
            {
                card.AddKeyword(CustomKeyWord.SingleTurnInspiration);
            }
        }
    }
}
