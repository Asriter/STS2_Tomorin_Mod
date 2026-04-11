using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 我什么都会做的 - Power
/// 战斗结束后获得金币
/// </summary>
  
public class DoEverythingPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => false;

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        room.AddExtraReward(base.Owner.Player, new GoldReward(base.Amount, base.Owner.Player));
        await Task.CompletedTask;
    }
}
