using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Taki状态效果：有卡被消耗时，下一张卡的伤害变成两倍
/// </summary>
public class TakiAddDamagePower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _nextCardDoubleHits;

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner.Creature == base.Owner)
        {
            Flash();
            _nextCardDoubleHits = 999;
        }
    }


    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (_nextCardDoubleHits > 0 && dealer == base.Owner)
        {
            _nextCardDoubleHits--;
            return 2m;
        }
        return 1m;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_nextCardDoubleHits > 0 && cardPlay.Card.Owner.Creature == base.Owner)
        {
            _nextCardDoubleHits = 0;
        }
        
        return Task.CompletedTask;
    }
}
