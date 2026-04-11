using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Relics;

public abstract class BaseRelicModel : CustomRelicModel, CustomHookInterface
{
    protected override string BigIconPath =>  $"res://STS2_Tomorin_Mod/images/relics/big/{this.GetType().Name}.png";
    public override string PackedIconPath => $"res://STS2_Tomorin_Mod/images/relics/{this.GetType().Name}.png";
    protected override string PackedIconOutlinePath => $"res://STS2_Tomorin_Mod/images/relics/{this.GetType().Name}.png";
    
    public virtual Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        return Task.CompletedTask;
    }
    
    
}