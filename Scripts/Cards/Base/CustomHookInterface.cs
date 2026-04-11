using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Cards.Base;

public interface CustomHookInterface
{
    public Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source);

}