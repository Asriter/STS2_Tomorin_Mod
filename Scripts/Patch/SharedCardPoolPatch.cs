using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.CardPools;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(ModelDb), "AllSharedCardPools", MethodType.Getter)]
internal class SharedCardPoolPatch
{
    [HarmonyPostfix]
    private static void Custom(ref IEnumerable<CardPoolModel>? __result)
    {
        if (__result != null)
        {
            var list = __result.ToList();
            list.Add(ModelDb.CardPool<CollectionsCardPool>());
        }
    }
}