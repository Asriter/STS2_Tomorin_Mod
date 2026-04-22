using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 抱头蹲防效果
/// 每回合开始时获得等于心之壁层数的格挡
/// </summary>

public class DuckAndCoverPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (creature == base.Owner && base.Owner.HasPower<AtFieldPower>())
        {
            decimal atFieldAmount = base.Owner.GetPower<AtFieldPower>().Amount;
            if (atFieldAmount > 0)
            {
                Flash();
                await CreatureCmd.GainBlock(base.Owner, atFieldAmount, ValueProp.Unpowered, null);
            }
        }
    }
}
