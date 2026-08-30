using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 维护 FPO 首领战的原版奖励资格生命周期，不自行创建或配置奖励内容。
/// </summary>
[HarmonyPatch(typeof(CombatRoom), "OfferRoomEndRewards")]
internal static class StageBossRewardLifecyclePatch
{
    /// <summary>
    /// 同一场符合条件的 FPO 首领战仅允许首次进入原版奖励生成入口；其他战斗保持原版行为。
    /// </summary>
    /// <param name="__instance">当前战斗房间。</param>
    /// <param name="__result">重复入口被跳过时返回的已完成任务。</param>
    /// <returns>首次生成或无关战斗时继续原版流程；重复生成时跳过原版入口。</returns>
    [HarmonyPrefix]
    private static bool Prefix(CombatRoom __instance, ref Task __result)
    {
        var combatState = __instance.CombatState;
        var runState = combatState?.RunState;
        var encounter = combatState?.Encounter;
        if (runState == null || encounter == null)
        {
            return true;
        }

        var progress = StageRunProgressModifier.Find(runState);
        if (progress == null)
        {
            return true;
        }

        var room = runState.CurrentRoom;
        if (room == null)
        {
            return true;
        }

        var mapCoord = runState.CurrentMapPoint?.coord;
        progress.ClearStaleBossRewardEligibility(encounter.Id, runState.CurrentActIndex, mapCoord);

        if (progress.BossRewardState == StageBossRewardState.Generated &&
            progress.MatchesBossRewardBattle(encounter.Id, runState.CurrentActIndex, mapCoord))
        {
            Log.Info("[Stage] 已跳过同一场 FPO 首领战的重复奖励生成入口。");
            __result = Task.CompletedTask;
            return false;
        }

        if (progress.BossRewardState != StageBossRewardState.Eligible)
        {
            return true;
        }

        if (progress.MarkBossRewardsGenerated(encounter.Id, runState.CurrentActIndex, mapCoord))
        {
            Log.Info("[Stage] FPO 首领战奖励交由原版 RewardsSet 流程生成。");
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>
/// 在 Stage 候选使 Glory 不再是物理末章时，恢复“仅当前 Boss 战真实击败 FPO 才生成标准奖励”的语义。
/// </summary>
[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class StageBossRewardEligibilityPatch
{
    /// <summary>
    /// 不具备当前 FPO 首领战资格时返回原版空房间奖励集；具备资格时完整放行原版奖励生成。
    /// </summary>
    /// <param name="__instance">正在填充的玩家奖励集合。</param>
    /// <param name="room">奖励所关联的战斗房间。</param>
    /// <param name="__result">无资格时仍用于显示终端继续按钮的空奖励集合。</param>
    /// <returns>需要由原版生成奖励时返回 <see langword="true"/>。</returns>
    [HarmonyPrefix]
    private static bool Prefix(RewardsSet __instance, AbstractRoom room, ref RewardsSet __result)
    {
        var runState = __instance.Player.RunState;
        if (room.RoomType != RoomType.Boss || runState.Act is not Glory ||
            !StageEligibility.HasAdjacentUniqueStageCandidate(runState))
        {
            return true;
        }

        var progress = StageRunProgressModifier.Find(runState);
        var combatState = (room as CombatRoom)?.CombatState;
        var encounter = combatState?.Encounter;
        var isEligibleBattle = progress != null && encounter != null &&
                               progress.MatchesBossRewardBattle(
                                   encounter.Id,
                                   runState.CurrentActIndex,
                                   runState.CurrentMapPoint?.coord) &&
                               progress.BossRewardState is StageBossRewardState.Eligible or StageBossRewardState.Generated;
        if (isEligibleBattle)
        {
            return true;
        }

        __result = __instance.EmptyForRoom(room);
        return false;
    }
}
