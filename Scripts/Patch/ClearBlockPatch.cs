using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(Creature), "ClearBlock")]
public class ClearBlockPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    private static bool Custom(Creature __instance, ref Task __result)
    {
        __result = CustomClearBlock(__instance);
        return false;
    }

    private static async Task CustomClearBlock(Creature creature)
    {
        var combatState = creature.CombatState;
        bool shouldClearBlock = false;
        int block = 0;
        int maxBlock = creature.Block;

        foreach (AbstractModel combatHookListener in combatState.IterateHookListeners())
        {
            //遍历，找到需要最多能保留的block
            if (!combatHookListener.ShouldClearBlock(creature))
            {
                shouldClearBlock = true;
                await combatHookListener.AfterPreventingBlockClear(combatHookListener, creature);
                var newBlock = creature.Block;

                //多个保留生效的情况下，保留最多的格挡,已经扣了的得给他加回去
                if (newBlock > block)
                {
                    block = newBlock;
                }

                //重置格挡
                await CreatureCmd.GainBlock(creature, maxBlock - newBlock, ValueProp.Unpowered, null, true);
            }
        }

        await CreatureCmd.LoseBlock(new BlockingPlayerChoiceContext(), creature, creature.Block - block, null);
    }
}