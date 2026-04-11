using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2_Tomorin_Mod.Cards;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(ArchaicTooth), "TranscendenceUpgrades", MethodType.Getter)]
internal class ArchaicToothPatch
{
    [HarmonyPostfix]
    private static void Custom(ref Dictionary<ModelId, CardModel> __result)
    {
        __result.Add(ModelDb.Card<Hitoshizuku>().Id, (CardModel) ModelDb.Card<Mayoiuta>());
    }
}