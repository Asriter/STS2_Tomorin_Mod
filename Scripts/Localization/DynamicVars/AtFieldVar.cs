using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Localization.DynamicVars;

public class AtFieldVar : DynamicVar
{
	public const string DefaultName = "AtField";
    
    public AtFieldVar(int baseValue) : base(DefaultName, baseValue)
    {
	    this.WithTooltip();
    }
}