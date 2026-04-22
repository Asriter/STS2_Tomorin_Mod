using BaseLib.Config;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using STS2_Tomorin_Mod.Encounters;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(ActModel), "AllBossEncounters", MethodType.Getter)]
public class ActModelBossPatch
{
    [HarmonyPriority(Priority.Last)]
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref IEnumerable<EncounterModel> __result)
    {
        if (TomorinModConfig.LoadModBoss)
        {
            //根据房间类型判断要给的boss
            if (__instance is Underdocks)
                __result = [ModelDb.Encounter<AnonBoss>()];
        }
    }
}