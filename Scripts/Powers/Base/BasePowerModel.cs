using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Powers;

public abstract class BasePowerModel : CustomPowerModel, CustomHookInterface
{
    public override string CustomPackedIconPath => $"res://STS2_Tomorin_Mod/images/powers/{this.GetType().Name}.png";
    public override string? CustomBigIconPath => $"res://STS2_Tomorin_Mod/images/powers/big/{this.GetType().Name}.png";

    public virtual Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        return Task.CompletedTask;
    }
    
    
}