using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Encounters;
using STS2_Tomorin_Mod.Events;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyNextEvent))]
internal static class FixedFirstEventPatch
{
    [HarmonyPriority(Priority.Last)]
    [HarmonyPostfix]
    private static void Postfix(IRunState runState, EventModel currentEvent, ref EventModel __result)
    {
        if (runState.CurrentActIndex != 1 ||
            runState.Act.BossEncounter?.Id != ModelDb.Encounter<RaanaBoss>().Id)
        {
            // Log.Warn("进入事件但是退出！当前层数id：" + runState.CurrentActIndex);
            return;
        }

        if (runState is not RunState concreteRunState)
        {
            // Log.Warn("进入事件但是退出！状态2，为啥啊？");
            return;
        }

        var fixedEvent = ModelDb.Event<FeedTheCat>();
        if (concreteRunState.VisitedEventIds.Contains(fixedEvent.Id))
        {
            // Log.Warn("进入事件但是退出！当前事件已经跑过");
            return;
        }

        if (HasEnteredEventInCurrentAct(runState))
        {
            // Log.Warn("进入事件但是退出！当前事件已经跑过，但是第二个判断");
            return;
        }

        FeedTheCat.BeginFixedSelectionCheck();
        try
        {
            if (!fixedEvent.IsAllowed(concreteRunState))
            {
                return;
            }
        }
        finally
        {
            FeedTheCat.EndFixedSelectionCheck();
        }

        __result = fixedEvent;
    }

    private static bool HasEnteredEventInCurrentAct(IRunState runState)
    {
        if (runState.MapPointHistory.Count <= runState.CurrentActIndex)
        {
            return false;
        }

        return runState.MapPointHistory[runState.CurrentActIndex]
            .Any(entry => entry.HasRoomOfType(RoomType.Event));
    }
}
