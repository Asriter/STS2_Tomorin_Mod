using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Mortis被动：攻击卡伤害减半，非攻击卡伤害翻倍。
/// 通过 ModifyDamageAdditive 实现：攻击卡返回 -50% 加值，非攻击卡返回 +100% 加值。
/// </summary>
public class MortisPassivePower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != base.Owner) return 0m;

        if (cardSource?.Type == CardType.Attack)
        {
            // 攻击卡：减去一半伤害 → 最终伤害 = amount - 0.5*amount = 0.5*amount
            return -amount * 0.5m;
        }
        else
        {
            // 非攻击卡：加上一倍伤害 → 最终伤害 = amount + amount = 2*amount
            return amount;
        }
    }
}
