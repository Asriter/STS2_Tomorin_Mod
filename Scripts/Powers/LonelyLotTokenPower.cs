using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 皲裂的心效果
/// 每当获得心之壁时，额外获得N层心之壁
/// </summary>
public class LonelyLotTokenPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _isProcessing;

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (_isProcessing) return;
        if (power is AtFieldPower && power.Owner == base.Owner && amount > 0)
        {
            _isProcessing = true;
            try
            {
                Flash();
                await PowerCmd.Apply<AtFieldPower>(choiceContext, base.Owner, base.Amount, base.Owner, null);
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }
}
