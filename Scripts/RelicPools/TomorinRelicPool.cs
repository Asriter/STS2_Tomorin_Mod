using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Characters;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.RelicPools;

public class TomorinRelicPool : CustomRelicPoolModel
{
    // public override string EnergyColorName => Tomorin.energyColorName;

    public override Color LabOutlineColor => Tomorin.DefaultColor;
    
    protected override IEnumerable<RelicModel> GenerateAllRelics()
    {
        return
        [
            ModelDb.Relic<NormalPencil>(),
            ModelDb.Relic<SoyoBase>(),
            ModelDb.Relic<TakiDrum>(),
            ModelDb.Relic<AnonGuitar>(),
            ModelDb.Relic<RaanaGuitar>(),
        ];
    }

    public override string? BigEnergyIconPath => MainFile.BigEnergyIconPath;

    public override string? TextEnergyIconPath => MainFile.TextEnergyIconPath;
}
