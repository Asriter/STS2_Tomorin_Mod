using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 下回合少抽卡
/// </summary>
public class LessDrawNextTurnPower : BasePowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != base.Owner.Player)
        {
            return count;
        }
        
        if (base.AmountOnTurnStart == 0)
        {
            return count;
        }
        return count - (decimal)base.Amount;
    }

    public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        if (side == base.Owner.Side && base.AmountOnTurnStart != 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}