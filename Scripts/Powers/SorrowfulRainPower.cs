using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 梅雨（雨）效果
/// 每次触发作词时，获得N层心之壁
/// </summary>

public class SorrowfulRainPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCompose(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        if (player == base.Owner.Player)
        {
            Flash();
            await PowerCmd.Apply<AtFieldPower>(base.Owner, base.Amount, base.Owner, null);
        }
    }
}
