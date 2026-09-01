using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 在原生事件池完成自身访问计数后，把舞台问号房替换为路线规定的固定事件。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEvent))]
internal static class StageEventResolverPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, RunState runState, ref EventModel __result)
    {
        if (__instance is not Acts.Stage)
        {
            return;
        }

        __result = StageRoomResolver.ResolveEventForCurrentProgress(runState);
    }
}
