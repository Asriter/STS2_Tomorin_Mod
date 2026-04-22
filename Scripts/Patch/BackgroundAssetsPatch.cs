using HarmonyLib;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2_Tomorin_Mod.Patch;


[HarmonyPatch(typeof(BackgroundAssets), "SelectRandomBackgroundAssetLayers")]
public class BackgroundAssetsPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    private static bool Custom(Rng rng, ref Dictionary<string, List<string>> bgLayers, List<string> __result)
    {
        foreach (var pair in bgLayers)
        {
            var list = pair.Value;
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = list[i].Replace(".remap", "");
            }
        }
        
        return true;
    }
}