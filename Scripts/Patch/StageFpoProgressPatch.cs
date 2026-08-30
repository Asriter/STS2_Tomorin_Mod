using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Enemy;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 在实际死亡回调中记录 FPO 本局解锁进度，并建立当前首领战奖励资格。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
internal static class StageFpoProgressPatch
{
    /// <summary>
    /// 仅处理稳定模型标识为 FullPowerOblivionis 的实际死亡；被阻止死亡不会触发本回调。
    /// </summary>
    /// <param name="runState">当前局的同步状态。</param>
    /// <param name="combatState">发生死亡的当前战斗状态。</param>
    /// <param name="creature">死亡生物。</param>
    [HarmonyPostfix]
    private static void Postfix(IRunState runState, ICombatState combatState, Creature creature, bool wasRemovalPrevented)
    {
        if (wasRemovalPrevented || creature.ModelId != ModelDb.Monster<FullPowerOblivionis>().Id)
        {
            return;
        }

        var progress = StageRunProgressModifier.Find(runState);
        if (progress == null)
        {
            return;
        }

        if (progress.MarkFullPowerOblivionisDefeated())
        {
            Log.Info("[Stage] 已记录 FPO 击败进度：FullPowerOblivionis 已在本局中真实死亡。");
        }

        if (runState.CurrentRoom?.RoomType == RoomType.Boss && combatState.Encounter != null)
        {
            progress.MarkBossRewardEligible(combatState.Encounter.Id, runState.CurrentActIndex, runState.CurrentMapPoint?.coord);
        }
    }
}
