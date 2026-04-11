using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 诗超绊状态 额外回合相关 保留能量，回合开始时设置然后移除该状态
/// </summary>
public class UtakotobaPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public int LastTurnEnergy = 0;
    public Player Player;

    // public override Task AfterTakingExtraTurn(Player player)
    // {
    //     if (player == base.Owner.Player)
    //     {
    //         PlayerCmd.GainEnergy(LastTurnEnergy, Player);
    //         PowerCmd.Remove(this);
    //     }
    //     
    //     return Task.CompletedTask;
    // }

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player == Player;
    }
    
    public override async Task AfterEnergyReset(Player player)
    {
        if (player == base.Owner.Player)
        {
            await PlayerCmd.GainEnergy(LastTurnEnergy, player);
            await PowerCmd.Remove(this);
        }
    }
}