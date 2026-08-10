using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Powers;

public class RaanaInterestPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Amount-1;

    public int LowThreshold => 18 * Math.Max(1, CombatState.Players.Count)+1;
    public int HighThreshold => 30 * Math.Max(1, CombatState.Players.Count)+1;

    public async Task ModifyInterest(PlayerChoiceContext choiceContext, int delta, CardModel? source)
    {
        var nextAmount = Math.Max(0, Amount + delta);
        var actualDelta = nextAmount - Amount;
        if (actualDelta != 0)
        {
            Flash();
            await PowerCmd.ModifyAmount(choiceContext, this, actualDelta, Owner, source, false);
        }

        InvokeDisplayAmountChanged();
        if (Owner.Monster is Raana raana)
        {
            raana.RefreshInterestMoveStateIfNeeded();
        }
        
        InvokeDisplayAmountChanged();
    }

    public async Task ClearInterest(PlayerChoiceContext choiceContext)
    {
        await ModifyInterest(choiceContext, -Amount, null);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature?.Side == CombatSide.Player)
        {
            await ModifyInterest(choiceContext, InterestForPlayedCard(cardPlay.Card), cardPlay.Card);
        }
    }

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner?.Creature?.Side == CombatSide.Player)
        {
            await ModifyInterest(choiceContext, card is LeftoverBuffet ? -2 : 1, card);
        }
    }

    private static int InterestForPlayedCard(CardModel card)
    {
        return card.Rarity switch
        {
            CardRarity.Uncommon => 2,
            CardRarity.Rare => 5,
            _ => 1
        };
    }
}
