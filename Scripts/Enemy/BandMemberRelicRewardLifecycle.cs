using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Encounters;

namespace STS2_Tomorin_Mod.Enemy;

/// <summary>
/// 复用乐队 Boss 原本的奖励触发点：原始 Boss 直接发奖，精英变体为当前 Encounter 记录奖励资格。
/// </summary>
internal static class BandMemberRelicRewardLifecycle
{
    /// <summary>
    /// 在对应 Boss 本体确认满足奖励条件时记录或发放专属遗物。
    /// </summary>
    public static void RecordEarnedAndGrantBossReward<TRelic>(
        Creature creature,
        BandMemberKind member,
        bool shouldGrantBossReward)
        where TRelic : RelicModel
    {
        ArgumentNullException.ThrowIfNull(creature);

        var combatState = creature.CombatState ??
                          throw new InvalidOperationException($"{member} 的奖励触发点缺少 CombatState。");
        if (combatState.RunState.CurrentRoom is not CombatRoom room)
        {
            throw new InvalidOperationException($"{member} 的奖励触发点不在 CombatRoom 中。");
        }

        if (combatState.Encounter is BandMemberEncounter encounter)
        {
            encounter.MarkRelicRewardEarned(member, creature.SlotName);
        }

        if (shouldGrantBossReward)
        {
            BandBossRelicReward.Add<TRelic>(room);
        }
    }
}
