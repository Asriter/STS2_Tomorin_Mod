using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 击杀Doloris后玩家获得的永久Buff：回合开始时额外抽1张牌
/// </summary>
public class DolorisKillBuffPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != base.Owner.Player) return count;
        return count + 1m;
    }
}
