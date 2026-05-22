using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2_Tomorin_Mod.Afflictions.Base;
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Afflictions;

/// <summary>
/// Crychic诅咒: 打出此卡时，玩家需选择丢弃2张手牌
/// 手牌不足2张时，丢弃全部剩余手牌（由玩家逐张选择）
/// 对应需求 3.3.3
/// </summary>
public class CrychicDiscardCurse : BaseAfflictionModel
{
    /// <summary>
    /// 需要丢弃的卡牌数量
    /// </summary>
    private const int DiscardCount = 2;
    
	public override bool HasExtraCardText => true;
    
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card != Card || Card?.Owner == null) return;

        var player = Card.Owner;
        var hand = player.PlayerCombatState.Hand;
        var remainingCards = hand.Cards.Where(c => c != Card).ToList();

        if (remainingCards.Count == 0) return;

        int actualDiscard = DiscardCount;
        if (remainingCards.Count < DiscardCount)
        {
            actualDiscard = remainingCards.Count;
        }

        var cards = await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            player,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, actualDiscard),
            model => true,
            Card);

        foreach (var card in cards)
        {
            await CardPileCmd.Add(card, PileType.Discard, CardPilePosition.Top, this);
        }
    }
    
}
