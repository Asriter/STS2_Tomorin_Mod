using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Potions;

public abstract class BasePotionModel : CustomPotionModel, CustomHookInterface
{
    public override string? CustomPackedImagePath => $"res://STS2_Tomorin_Mod/images/potions/{this.GetType().Name}.png";
    public override string? CustomPackedOutlinePath => $"res://STS2_Tomorin_Mod/images/potions/{this.GetType().Name}.png";

    public virtual Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        return Task.CompletedTask;
    }
}