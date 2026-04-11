using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.CardPools;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 光武形态效果
/// 每回合开始时，从3张随机收集品中选择1张加入手牌，然后消耗1张手牌
/// </summary>

public class TomoriFormPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (player == base.Owner.Player && base.AmountOnTurnStart >= 1)
        {
            Flash();
        
            // Show 3 random collectibles to choose from
            List<CardModel> randomModels = CardFactory.GetDistinctForCombat(player, 
                ModelDb.CardPool<CollectionsCardPool>().AllCards, 
                3, 
                player.RunState.Rng.CombatCardGeneration).ToList();
        
            var	cardModel = await CardSelectCmd.FromChooseACardScreen(choiceContext, randomModels, player, canSkip: true);
            if (cardModel != null)
            {
                cardModel.EnergyCost.SetThisTurnOrUntilPlayed(0);
                await CardPileCmd.AddGeneratedCardToCombat(cardModel, PileType.Hand, addedByPlayer: true);
            }

            // Exhaust 1 card from hand
            var exhaust = await CardSelectCmd.FromHand(choiceContext, base.Owner.Player,
                new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
                null, this);
            foreach (var card in exhaust)
            {
                await CardCmd.Exhaust(choiceContext, card);
            }
        }
    }

    
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != base.Owner.Player)
        {
            return amount;
        }
        return amount + Amount;
    }
}
