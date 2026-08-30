using MegaCrit.Sts2.Core.Entities.Powers;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 表示卡牌意图测试敌人的“悲伤之雨”能力层数。
/// </summary>
public sealed class CardIntentSorrowfulRainPower : BasePowerModel
{
    /// <inheritdoc />
    public override PowerType Type => PowerType.Buff;

    /// <inheritdoc />
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <inheritdoc />
    public override bool AllowNegative => false;

    /// <inheritdoc />
    public override string CustomPackedIconPath =>
        "res://STS2_Tomorin_Mod/images/powers/SorrowfulRainPower.png";

    /// <inheritdoc />
    public override string? CustomBigIconPath =>
        "res://STS2_Tomorin_Mod/images/powers/big/SorrowfulRainPower.png";
}
