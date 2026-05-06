using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Taki状态效果：每回合开始多抽3张卡
/// </summary>
public class TakiDrawCardPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
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
        return count + 2 * Amount;
    }
}
