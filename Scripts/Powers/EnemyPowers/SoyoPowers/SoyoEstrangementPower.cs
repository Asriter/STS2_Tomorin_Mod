using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Soyo's Estrangement counter. All modifications are clamped to a minimum of 0.
/// </summary>
public class SoyoEstrangementPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => false;

    public static int GetAmount(Creature owner)
    {
        if (!owner.HasPower<SoyoEstrangementPower>())
        {
            return 0;
        }

        var power = owner.GetPower<SoyoEstrangementPower>();
        return power == null ? 0 : Math.Max(0, power.Amount);
    }

    public static async Task Modify(PlayerChoiceContext choiceContext, Creature owner, int delta,
        AbstractModel? source)
    {
        var power = owner.HasPower<SoyoEstrangementPower>()
            ? owner.GetPower<SoyoEstrangementPower>()
            : null;
        int current = Math.Max(0, power?.Amount ?? 0);
        int next = Math.Max(0, current + delta);
        int actualDelta = next - current;
        if (actualDelta != 0)
        {
            if (power == null)
            {
                await PowerCmd.Apply<SoyoEstrangementPower>(choiceContext, owner, next, owner, source as CardModel);
                return;
            }

            await PowerCmd.ModifyAmount(choiceContext, power, actualDelta, owner, source as CardModel);
        }
    }
}
