using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 击杀Mortis后玩家获得的永久Buff：+5荆棘，每回合开始+10格挡
/// </summary>
public class MortisKillBuffPower : BasePowerModel
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<MortisKillBuffPower>()];
    
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private bool _thornsApplied;

    protected override object InitInternalData()
    {
        _thornsApplied = false;
        return null!;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != base.Owner.Side) return;

        var player = base.Owner.Player;
        if (player == null) return;

        // 每回合+10格挡
        Flash();
        await CreatureCmd.GainBlock(base.Owner, 10m, ValueProp.Move, null);
    }

    // 荆棘效果通过施加ThornsPower实现，在CenterPositionManagerPower中施加此Buff时同时施加5层Thorns
}
