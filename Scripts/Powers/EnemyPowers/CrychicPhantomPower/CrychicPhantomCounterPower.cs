using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Powers;
public class CrychicPhantomCounterPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private const int MaxCount = 6;
    
    public override int DisplayAmount => Amount-1;
    

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<CrychicRememberPower>()];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner)
        {
            await PowerCmd.ModifyAmount(choiceContext, this, 1, null, null, false);
        }
    }

    /// <summary>
    /// 每打出五张卡进一层状态
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="power"></param>
    /// <param name="amount"></param>
    /// <param name="applier"></param>
    /// <param name="cardSource"></param>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier,
        CardModel? cardSource)
    {
        if (power == this)
        {
            if (Amount == MaxCount)
            {
                Flash();
                if (Owner.HasPower<CrychicRememberPower>())
                {
                    var rememberPower = Owner.GetPower<CrychicRememberPower>();
                    await PowerCmd.ModifyAmount(choiceContext, rememberPower, 1, null, null, false);
                }
                else
                {
                    await PowerCmd.Apply<CrychicRememberPower>(choiceContext, Owner, 1, null, null, false);
                }
                
                await PowerCmd.ModifyAmount(choiceContext, this, 1-Amount, null, null, false);
            }
        }
    }
    
    
}