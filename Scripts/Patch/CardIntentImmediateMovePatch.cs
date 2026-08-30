using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2_Tomorin_Mod.Enemy.CardIntents;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 协调玩家方回合中原版立即换招与卡牌手牌的取消、补抽生命周期。
/// </summary>
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.SetMoveImmediate),
    [typeof(MoveState), typeof(bool)])]
internal static class CardIntentImmediateMovePatch
{
    /// <summary>
    /// 在原版换招前精确保存旧的下一招引用，供后置逻辑比较实际换招结果。
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(MonsterModel __instance, out MoveState? __state)
    {
        __state = __instance.NextMove;
    }

    /// <summary>
    /// 仅在玩家方协调已真正发生的状态切换，敌人方执行期间不取消或提前抽牌。
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(MonsterModel __instance, MoveState? __state)
    {
        if (__instance is not BaseCardIntentMonsterModel ||
            __instance.Creature?.CombatState?.CurrentSide != CombatSide.Player)
        {
            return;
        }

        MoveState newState = __instance.NextMove;
        if (ReferenceEquals(__state, newState))
        {
            return;
        }

        if (__state is CardIntentMoveState oldCardState)
        {
            oldCardState.CancelPreparedHand();
        }

        if (newState is CardIntentMoveState newCardState)
        {
            newCardState.PrepareCards();
        }
    }
}
