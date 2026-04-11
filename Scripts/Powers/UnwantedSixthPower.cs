using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.Cards.Collections;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 不被需要的第六人效果
/// 本回合每次获得格挡时，额外获得N层心之壁，并将一张压皱的残页加入手牌
/// </summary>

public class UnwantedSixthPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (creature == base.Owner && amount >= 1m)
        {
            Flash();
            await PowerCmd.Apply<AtFieldPower>(base.Owner, base.Amount, base.Owner, null);
        }
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
    {
        if (side == base.Owner.Side)
        {
            await PowerCmd.ModifyAmount(this, -base.Amount, null, null);
        }
    }
}
