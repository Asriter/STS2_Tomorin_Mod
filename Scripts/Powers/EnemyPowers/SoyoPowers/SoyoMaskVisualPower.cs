using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Powers;

public class SoyoMaskVisualPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<SoyoMaskedDamageReductionPower>(), HoverTipFactory.FromPower<SoyoEstrangementPower>()];
}