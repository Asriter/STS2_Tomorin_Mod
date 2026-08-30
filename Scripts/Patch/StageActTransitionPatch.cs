using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 在原版多人同步完成后进入下一章节的入口决定 Glory 后是否进入 Stage，并保证 Stage 始终按最终层结束。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterNextAct))]
internal static class StageActTransitionPatch
{
    /// <summary>
    /// 仅拦截带有相邻 Stage 候选的 Glory：符合完整资格时继续原版转层，否则复用原版 Architect 终局步骤。
    /// </summary>
    /// <param name="__instance">已经通过多人统一准备状态的 Run 管理器。</param>
    /// <param name="__result">不进入 Stage 时返回的原版终局异步任务。</param>
    /// <returns>允许原始转层时返回 <see langword="true"/>；进入原版终局事件时返回 <see langword="false"/>。</returns>
    [HarmonyPrefix]
    private static bool Prefix(RunManager __instance, ref Task __result)
    {
        var runState = __instance.DebugOnlyGetState();
        if (runState?.Act is STS2_Tomorin_Mod.Acts.Stage)
        {
            __result = EnterArchitectEnding(__instance);
            return false;
        }

        if (runState?.Act is not MegaCrit.Sts2.Core.Models.Acts.Glory)
        {
            return true;
        }

        if (!StageRegistrationPolicy.ContainsStage(runState.Acts))
        {
            // 旧存档没有候选 Stage 时必须保持原版 Glory 结束流程。
            return true;
        }

        if (StageEligibility.IsEligible(runState))
        {
            return true;
        }

        if (!StageEligibility.HasAdjacentUniqueStageCandidate(runState))
        {
            Log.Error("[Stage] 检测到重复或顺序异常的 Stage 候选章节，已安全结束本局。");
        }

        __result = EnterArchitectEnding(__instance);
        return false;
    }

    /// <summary>
    /// 执行当前版本 <see cref="RunManager.EnterNextAct"/> 在最终章节使用的 Architect 终局步骤。
    /// </summary>
    /// <param name="runManager">当前 Run 管理器。</param>
    /// <returns>覆盖淡出、清屏、进入 Architect 事件和淡入的异步任务。</returns>
    private static async Task EnterArchitectEnding(RunManager runManager)
    {
        await runManager.FadeOut();
        AccessTools.Method(typeof(RunManager), "ClearScreens").Invoke(runManager, null);
        await runManager.EnterRoom(new EventRoom(ModelDb.Event<TheArchitect>()));
        await runManager.FadeIn();
    }
}
