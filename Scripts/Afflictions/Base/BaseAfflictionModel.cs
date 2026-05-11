using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Afflictions.Base;

public class BaseAfflictionModel : AfflictionModel, ILocalizationProvider, ICustomModel
{
    public virtual List<(string, string)>? Localization => (List<(string, string)>) null;
}