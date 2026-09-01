using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 防止仅用于舞台持久化的内部 Modifier 令 Neow 误入自定义规则选项分支。
/// </summary>
[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
internal static class StageNeowCompatibilityPatch
{
    /// <summary>
    /// 在原版同步生成选项期间临时隐藏舞台进度状态；其他 Modifier 保持原顺序和行为。
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(Neow __instance, out ModifierViewState? __state)
    {
        IRunState? runState = __instance.Owner?.RunState;
        IReadOnlyList<ModifierModel>? original = StageRunModifierVisibilityScope.Hide(runState);
        __state = runState != null && original != null ? new ModifierViewState(runState, original) : null;
    }

    /// <summary>
    /// 无论原版选项生成成功或抛错，都恢复 Run 的完整 Modifier 集合。
    /// </summary>
    [HarmonyFinalizer]
    private static Exception? Finalizer(ModifierViewState? __state, Exception? __exception)
    {
        if (__state != null)
        {
            StageRunModifierVisibilityScope.Restore(__state.RunState, __state.Modifiers);
        }

        return __exception;
    }

    private sealed record ModifierViewState(IRunState RunState, IReadOnlyList<ModifierModel> Modifiers);
}
