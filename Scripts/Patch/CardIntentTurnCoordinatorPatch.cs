using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using STS2_Tomorin_Mod.Enemy.CardIntents;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 在原版玩家方整体回合开始流程完成后，为每个卡牌行动敌人的当前招式准备一次冻结手牌。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnStart),
    [typeof(ICombatState), typeof(CombatSide), typeof(IReadOnlyList<Creature>)])]
internal static class CardIntentTurnCoordinatorPatch
{
    /// <summary>
    /// 包装原版异步任务，确保卡牌准备发生在原版 Hook 全部完成之后。
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(ref Task __result, ICombatState combatState, CombatSide side)
    {
        __result = PrepareCurrentCardMovesAfterOriginalAsync(__result, combatState, side);
    }

    /// <summary>
    /// 等待原版任务后，只在玩家方整体回合为存活敌人的当前卡牌招式准备手牌。
    /// </summary>
    private static async Task PrepareCurrentCardMovesAfterOriginalAsync(
        Task originalTask,
        ICombatState combatState,
        CombatSide side)
    {
        await originalTask;
        if (side != CombatSide.Player)
        {
            return;
        }

        foreach (Creature enemy in combatState.Enemies)
        {
            if (!enemy.IsAlive || enemy.Monster is not BaseCardIntentMonsterModel monster)
            {
                continue;
            }

            if (monster.NextMove is CardIntentMoveState cardState)
            {
                cardState.PrepareCards();
            }
        }
    }
}
