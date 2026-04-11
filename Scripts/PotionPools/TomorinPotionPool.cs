using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Characters;

namespace STS2_Tomorin_Mod.PotionPools;

public class TomorinPotionPool : CustomPotionPoolModel
{
    protected override IEnumerable<PotionModel> GenerateAllPotions()
    {
        return new List<PotionModel>();
    }

    // public override string EnergyColorName => Tomorin.energyColorName;
    
    public override string? BigEnergyIconPath => MainFile.BigEnergyIconPath;

    public override string? TextEnergyIconPath => MainFile.TextEnergyIconPath;
}
