using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Powers;


public class BrokenNotePower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<AtFieldPower>()];


    public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (side == base.Owner.Side)
        {
            AtFieldPower.ShouldDeduceAtFieldPower = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合开始移除状态
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="side"></param>
    /// <param name="combatState"></param>
    /// <returns></returns>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        CombatState combatState)
    {
        if (side == base.Owner.Side)
        {
            await PowerCmd.ModifyAmount(this, -1, null, null);
        }
    }
}