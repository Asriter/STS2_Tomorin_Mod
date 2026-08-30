using MegaCrit.Sts2.Core.Entities.Powers;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>正常卡牌生命周期进入 Exhaust 时获得格挡的影灯能力标记。</summary>
public sealed class CardIntentHeartBeatPower : BasePowerModel
{
    public const decimal BlockPerExhaust = 2m;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => false;
    public override string CustomPackedIconPath =>
        "res://STS2_Tomorin_Mod/images/powers/HeartBeatPower.png";
    public override string? CustomBigIconPath =>
        "res://STS2_Tomorin_Mod/images/powers/big/HeartBeatPower.png";
}
