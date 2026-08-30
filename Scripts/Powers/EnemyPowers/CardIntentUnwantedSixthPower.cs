using MegaCrit.Sts2.Core.Entities.Powers;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>仅在当前完整敌方行动内响应独立格挡获得的影灯能力身份。</summary>
public sealed class CardIntentUnwantedSixthPower : BasePowerModel
{
    public const decimal HeartWallPerBlockGrant = 1m;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => false;
    public override string CustomPackedIconPath =>
        "res://STS2_Tomorin_Mod/images/powers/UnwantedSixthPower.png";
    public override string? CustomBigIconPath =>
        "res://STS2_Tomorin_Mod/images/powers/big/UnwantedSixthPower.png";
}
