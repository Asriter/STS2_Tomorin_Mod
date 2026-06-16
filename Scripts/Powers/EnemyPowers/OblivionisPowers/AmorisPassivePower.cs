using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Amoris被动：单次攻击最多造成15点伤害。
/// 通过 ModifyHpLostBeforeOstyLate 将超过15的伤害截断为15。
/// </summary>
public class AmorisPassivePower : BasePowerModel
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<AmorisKillBuffPower>(), HoverTipFactory.ForEnergy(this)];
    
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("MaxDamage", 10), new EnergyVar(1)];

    public override decimal ModifyHpLostBeforeOstyLate(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return amount;
        var maxDamage = DynamicVars["MaxDamage"].BaseValue;
        Log.Warn(DynamicVars.Energy.BaseValue.ToString());
        return amount > maxDamage ? maxDamage : amount;
    }
}
