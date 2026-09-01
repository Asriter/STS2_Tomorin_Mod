using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 防止仅用于第四层判定的内部 Modifier 被顶部栏当成可见规则图标。
/// </summary>
[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
internal static class StageTopBarCompatibilityPatch
{
    [HarmonyPrefix]
    private static void Prefix(IRunState runState, out IReadOnlyList<ModifierModel>? __state)
    {
        __state = StageRunModifierVisibilityScope.Hide(runState);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(
        IRunState runState,
        IReadOnlyList<ModifierModel>? __state,
        Exception? __exception)
    {
        StageRunModifierVisibilityScope.Restore(runState, __state);
        return __exception;
    }
}
