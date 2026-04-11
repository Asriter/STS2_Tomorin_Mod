using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace STS2_Tomorin_Mod.Powers;

public class NameOfTearPower : BasePowerModel
{
    public const string Name = "NameOfTearPower";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}