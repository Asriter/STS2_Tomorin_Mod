using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Stage;
using StageAct = STS2_Tomorin_Mod.Acts.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 防止仅作为条件候选注册的隐藏 Stage 抢走原版 A10 分配给最终正常章节的第二 Boss。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
internal static class StageDoubleBossRoutingPatch
{
    /// <summary>
    /// 原版完成所有房间生成后，把误写到章节列表末尾的第二 Boss 迁回 Stage 前的 Glory。
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(RunManager __instance)
    {
        RunState? runState = __instance.DebugOnlyGetState();
        if (runState == null || !__instance.AscensionManager.HasLevel(AscensionLevel.DoubleBoss))
        {
            return;
        }

        IReadOnlyList<ActModel> acts = runState.Acts;
        if (acts.Count == 0)
        {
            return;
        }

        ModelId stageId = ModelDb.Act<StageAct>().Id;
        bool[] isStageByActIndex = acts.Select(act => act.Id == stageId).ToArray();
        int targetIndex = StageDoubleBossRoutingPolicy.FindDoubleBossTargetIndex(isStageByActIndex);
        if (targetIndex == acts.Count - 1)
        {
            return;
        }

        ActModel targetAct = acts[targetIndex];
        if (targetAct is not Glory)
        {
            Log.Error(
                $"[Stage] 双 Boss 重定向目标不是 Glory；目标索引={targetIndex}，目标章节={targetAct.Id}。已保留原版分配。");
            return;
        }

        ActModel originalTargetAct = acts[^1];
        EncounterModel? misplacedBoss = originalTargetAct.SecondBossEncounter;
        ModelId misplacedBossId = misplacedBoss?.Id ?? targetAct.BossEncounter.Id;
        EncounterModel[] allBosses = targetAct.AllBossEncounters.ToArray();
        StageDoubleBossRoutingPolicy.RelocationPlan<ModelId>? plan =
            StageDoubleBossRoutingPolicy.CreateRelocationPlan(
                __instance.AscensionManager.HasLevel(AscensionLevel.DoubleBoss),
                isStageByActIndex,
                targetAct.BossEncounter.Id,
                allBosses.Select(encounter => encounter.Id).ToArray(),
                misplacedBoss != null,
                misplacedBossId);
        if (plan == null)
        {
            return;
        }

        EncounterModel[] candidates = plan.EligibleBossIds
            .Select(id => allBosses.First(encounter => encounter.Id == id))
            .ToArray();
        if (candidates.Length == 0)
        {
            Log.Error($"[Stage] Glory {targetAct.Id} 没有区别于第一 Boss 的第二 Boss 候选。已保留原版分配。");
            return;
        }

        EncounterModel? secondBoss = plan.PreferredBossIndex >= 0
            ? candidates[plan.PreferredBossIndex]
            : runState.Rng.UpFront.NextItem(candidates);
        if (secondBoss == null)
        {
            Log.Error($"[Stage] Glory {targetAct.Id} 的第二 Boss 选择意外返回空值。已保留原版分配。");
            return;
        }

        StageDoubleBossRoutingPolicy.ApplyRelocation(
            plan,
            secondBoss,
            (index, boss) => acts[index].SetSecondBossEncounter(boss),
            index => acts[index].SetSecondBossEncounter(null));
        Log.Info(
            $"[Stage] 已将 A10 第二 Boss {secondBoss.Id} 从章节索引 {plan.OriginalTargetActIndex} 重定向到 Glory 索引 {plan.TargetActIndex}。");
    }
}
