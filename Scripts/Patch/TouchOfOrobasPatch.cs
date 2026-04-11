using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(TouchOfOrobas), "RefinementUpgrades", MethodType.Getter)]
internal class TouchOfOrobasPatch
{
    [HarmonyPostfix]
    private static void Custom(ref Dictionary<ModelId, RelicModel> __result)
    {
        __result.Add(ModelDb.Relic<NormalPencil>().Id, ModelDb.Relic<ShoutOfSoul>());
    }
}