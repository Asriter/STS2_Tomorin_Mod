using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Powers;

public class RaanaStudioPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new EnergyVar(1)];

    public override async Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        if (player == base.Owner.Player)
        { 
            Flash();
            await PlayerCmd.GainEnergy(Amount, player);
        }
    }
}