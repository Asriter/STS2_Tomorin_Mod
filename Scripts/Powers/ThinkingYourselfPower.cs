using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 只想着自己呢 - Power
/// 回合结束前，力量和敏捷下降
/// </summary>

public class ThinkingYourselfPower : BasePowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => false;

    public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (side == base.Owner.Side)
        {
            Flash();
            // 降低力量
            await PowerCmd.Apply<StrengthPower>(base.Owner, Amount, base.Owner, null);
            // 降低敏捷
            await PowerCmd.Apply<DexterityPower>(base.Owner, Amount, base.Owner, null);
            // 移除自身
            await PowerCmd.Remove(this);
        }
    }

}
