using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Taki状态效果：每回合开始时，获得心之壁层数的临时力量，且心之壁不会减半
/// </summary>
public class TakiAtFieldPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _appliedStrength;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;

        if (base.Owner.HasPower<AtFieldPower>())
        {
            int atFieldCount = (int)base.Owner.GetPower<AtFieldPower>().Amount;
            int strengthGain = atFieldCount * Amount;
            if (strengthGain > 0)
            {
                Flash();
                await PowerCmd.Apply<StrengthPower>(base.Owner, strengthGain, base.Owner, null);
                _appliedStrength = strengthGain;
            }
        }
    }

    public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (side != base.Owner.Side) return;

        if (_appliedStrength > 0 && base.Owner.HasPower<StrengthPower>())
        {
            int toRemove = Math.Min(_appliedStrength, (int)base.Owner.GetPower<StrengthPower>().Amount);
            if (toRemove > 0)
            {
                await PowerCmd.ModifyAmount(base.Owner.GetPower<StrengthPower>(), -toRemove, null, null);
            }
            _appliedStrength = 0;
        }
    }
}
