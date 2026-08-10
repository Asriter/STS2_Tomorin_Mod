using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2_Tomorin_Mod.RelicPools;

namespace STS2_Tomorin_Mod.Relics;

[Pool(typeof(EventRelicPool))]
public class EmptyParfait : BaseRelicModel
{
    protected override string PackedIconOutlinePath => PackedIconPath;

    public override RelicRarity Rarity => RelicRarity.Event;
}
