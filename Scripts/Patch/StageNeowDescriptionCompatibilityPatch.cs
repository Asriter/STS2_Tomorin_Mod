using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 防止内部舞台进度令 Neow 显示“存在自定义 Modifier”的提示文案。
/// </summary>
[HarmonyPatch(typeof(Neow), "InitialDescription", MethodType.Getter)]
internal static class StageNeowDescriptionCompatibilityPatch
{
    [HarmonyPrefix]
    private static void Prefix(Neow __instance, out IReadOnlyList<ModifierModel>? __state)
    {
        __state = StageRunModifierVisibilityScope.Hide(__instance.Owner?.RunState);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(
        Neow __instance,
        IReadOnlyList<ModifierModel>? __state,
        Exception? __exception)
    {
        StageRunModifierVisibilityScope.Restore(__instance.Owner?.RunState, __state);
        return __exception;
    }
}
