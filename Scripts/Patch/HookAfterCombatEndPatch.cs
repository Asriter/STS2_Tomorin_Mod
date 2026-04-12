using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(Hook), "AfterCombatEnd")]
internal class HookAfterCombatEndPatch
{
    // ==========================================
    // 第一步：定义反向补丁 (Reverse Patch)
    // 作用：提供一个原方法的“克隆体”，供我们稍后手动调用
    // ==========================================
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(Hook), "AfterCombatEnd")]
    public static Task Original_AfterCardExhausted(IRunState runState, CombatState? combatState, CombatRoom room)
    {
        // 这里的代码会在运行时被 Harmony 替换为底层的 IL 指令，所以写什么都会被忽略
        // 抛出异常可以防止 Harmony 应用失败时发生意外的静默错误
        throw new NotImplementedException("这是反向补丁的占位符，如果抛出此异常说明 Harmony 未能成功应用该反向补丁。");
    }

    // ==========================================
    // 第二步：Prefix 完全拦截
    // ==========================================
    [HarmonyPrefix]
    public static bool Prefix(ref Task __result, IRunState runState, CombatState? combatState, CombatRoom room)
    {
        // 将原方法应当返回的 Task 替换为我们自己构建的 Task
        __result = AsyncWrapper(runState, combatState, room);

        // 返回 false，告诉 Harmony 立即终止原方法的执行
        return false;
    }

    // ==========================================
    // 第三步：自定义包裹方法 (处理前置逻辑)
    // ==========================================
    private static async Task AsyncWrapper(IRunState runState, CombatState? combatState, CombatRoom room)
    {
        // ------------------------------------------
        // 【A】前置 await 逻辑
        // ------------------------------------------
        foreach (AbstractModel model in combatState.IterateHookListeners())
        {
            if (model is CardModel cardModel && cardModel.Keywords.Contains(CustomKeyWord.SingleTurnInspiration))
            {
                cardModel.RemoveKeyword(CustomKeyWord.SingleTurnInspiration);
            }

            // 进入房间前，刷新消耗卡的计数
            ComposeCmd.ComposeCostCardDict.Clear();
        }

        // ------------------------------------------
        // 【B】手动调用原方法克隆体
        // ------------------------------------------
        // 通过反向补丁执行原本的 AfterCardExhausted 逻辑
        Task originalLogicTask = Original_AfterCardExhausted(runState, combatState, room);

        // 等待原方法的逻辑执行完毕
        await originalLogicTask;

        // ------------------------------------------
        // 【C】后置逻辑 (可选)
        // ------------------------------------------
        // 如果原方法执行完之后你还需要做点什么，可以写在这里
    }
}