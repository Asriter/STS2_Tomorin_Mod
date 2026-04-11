using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Localization.DynamicVars;

/// <summary>
/// 回收消耗区的卡
/// </summary>
public class DisExhaustVar : DynamicVar
{
    public const string DefauleName = "DisExhaust";
    public DisExhaustVar(decimal baseValue) : base(DefauleName, baseValue)
    {
    }
}