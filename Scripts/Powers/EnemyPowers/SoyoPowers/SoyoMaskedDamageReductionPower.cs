using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Soyo's false mask damage window. While present, Soyo loses only 25% HP from incoming HP loss.
/// </summary>
public class SoyoMaskedDamageReductionPower : BasePowerModel
{
    public override string CustomPackedIconPath => "res://STS2_Tomorin_Mod/images/powers/SoyoEstrangementPower.png";
    public override string? CustomBigIconPath => "res://STS2_Tomorin_Mod/images/powers/big/SoyoEstrangementPower.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHpLostBeforeOstyLate(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return amount;
        if (Amount <= 0 || amount <= 0m) return amount;

        return amount * 0.25m;
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;

        Flash();
        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null);
    }

    public override Task AfterModifyingHpLostBeforeOsty()
    {
        Flash();
        return Task.CompletedTask;
    }
}
