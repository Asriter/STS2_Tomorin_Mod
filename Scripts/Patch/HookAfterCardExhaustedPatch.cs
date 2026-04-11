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
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Patch;

[HarmonyPatch(typeof(Hook), "AfterCardExhausted")]
internal class HookAfterCardExhaustedPatch
{
    // ==========================================
    // 第一步：定义反向补丁 (Reverse Patch)
    // 作用：提供一个原方法的“克隆体”，供我们稍后手动调用
    // ==========================================
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(Hook), "AfterCardExhausted")]
    public static Task Original_AfterCardExhausted(CombatState combatState, PlayerChoiceContext choiceContext,
        CardModel card, bool causedByEthereal)
    {
        // 这里的代码会在运行时被 Harmony 替换为底层的 IL 指令，所以写什么都会被忽略
        // 抛出异常可以防止 Harmony 应用失败时发生意外的静默错误
        throw new NotImplementedException("这是反向补丁的占位符，如果抛出此异常说明 Harmony 未能成功应用该反向补丁。");
    }

    // ==========================================
    // 第二步：Prefix 完全拦截
    // ==========================================
    [HarmonyPrefix]
    public static bool Prefix(ref Task __result, CombatState combatState, PlayerChoiceContext choiceContext,
        CardModel card, bool causedByEthereal)
    {
        // 将原方法应当返回的 Task 替换为我们自己构建的 Task
        __result = AsyncWrapper(combatState, choiceContext, card, causedByEthereal);

        // 返回 false，告诉 Harmony 立即终止原方法的执行
        return false;
    }

    // ==========================================
    // 第三步：自定义包裹方法 (处理前置逻辑)
    // ==========================================
    private static async Task AsyncWrapper(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card,
        bool causedByEthereal)
    {
        // ------------------------------------------
        // 【A】前置 await 逻辑
        // ------------------------------------------
        foreach (AbstractModel model in combatState.IterateHookListeners())
        {
            if (model is CardModel cardModel && card == model &&
                (cardModel.Keywords.Contains(CustomKeyWord.Inspiration) || cardModel.Keywords.Contains(CustomKeyWord.SingleTurnInspiration)))
            {
                await AutoPlayWhenInspiration(choiceContext, card);
            }
        }

        // ------------------------------------------
        // 【B】手动调用原方法克隆体
        // ------------------------------------------
        // 通过反向补丁执行原本的 AfterCardExhausted 逻辑
        Task originalLogicTask = Original_AfterCardExhausted(combatState, choiceContext, card, causedByEthereal);

        // 等待原方法的逻辑执行完毕
        await originalLogicTask;

        // ------------------------------------------
        // 【C】后置逻辑 (可选)
        // ------------------------------------------
        // 如果原方法执行完之后你还需要做点什么，可以写在这里
    }


    /// <summary>
    /// 灵感自动生效 
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="card"></param>
    private static async Task AutoPlayWhenInspiration(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        CombatState combatState = card.CombatState ?? card.Owner.Creature.CombatState;

        Creature target = null;
        //索敌
        if (card.TargetType == TargetType.AnyEnemy)
        {
            if (target == null)
            {
                target = card.Owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
            }
        }

        if (card.TargetType == TargetType.AnyAlly)
        {
            IEnumerable<Creature> items = combatState.Allies.Where((Creature c) =>
                c != null && c.IsAlive && c.IsPlayer && c != card.Owner.Creature);
            if (target == null)
            {
                target = card.Owner.RunState.Rng.CombatTargets.NextItem(items);
            }

            if (target == null)
            {
                return;
            }
        }

        // 只有需要单体目标的类型才在找不到目标时 return
        if (target == null && (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyAlly))
        {
            return;
        }

        //如果并非重放，则处理x费消耗和星星消耗
        if (!card.IsDupe)
        {
            PlayerCombatState playerCombatState = card.Owner.PlayerCombatState;
            if (card.EnergyCost.CostsX)
            {
                card.EnergyCost.CapturedXValue = playerCombatState.Energy;
            }

            if (card.HasStarCostX)
            {
                card.LastStarsSpent = playerCombatState.Stars;
            }
            else
            {
                card.LastStarsSpent = Math.Max(0, card.GetStarCostWithModifiers());
            }
        }

        //打出次数
        int playCount = card.GetEnchantedReplayCount() + 1;
        playCount = Hook.ModifyCardPlayCount(combatState, card, playCount, target,
            out List<AbstractModel> modifyingModels);

        //资源管理相关
        ResourceInfo resources = new ResourceInfo
        {
            EnergySpent = 0,
            EnergyValue = card.EnergyCost.GetAmountToSpend(),
            StarsSpent = 0,
            StarValue = Math.Max(0, card.GetStarCostWithModifiers())
        };

        //TODO 处理卡牌飞出来的效果

        //根据重放效果处理攻击
        for (int i = 0; i < playCount; i++)
        {
            if (card.Type == CardType.Power)
            {
                await PlayPowerCardFlyVfxOverride(card);
            }
            else if (i > 0)
            {
                NCard nCard = NCard.FindOnTable(card);
                if (nCard != null)
                {
                    await nCard.AnimMultiCardPlay();
                }
            }

            //创建CardPlay
            CardPlay cardPlay = new CardPlay
            {
                Card = card,
                Target = target,
                ResultPile = PileType.Exhaust,
                Resources = resources,
                IsAutoPlay = true,
                PlayIndex = i,
                PlayCount = playCount
            };
            await InvokeOnPlay(card, choiceContext, cardPlay);

            //不知道干嘛的，先放这里
            card.InvokeExecutionFinished();
            //附魔等效果
            if (card.Enchantment != null)
            {
                await card.Enchantment.OnPlay(choiceContext, cardPlay);
                card.Enchantment.InvokeExecutionFinished();
            }

            if (card.Affliction != null)
            {
                AfflictionModel affliction = card.Affliction;
                await affliction.OnPlay(choiceContext, target);
                affliction.InvokeExecutionFinished();
            }
        }


        //TODO 处理卡片消耗的表现效果
    }

    /// <summary>
    /// 打出能力卡附带的动画，通过Harmony库的反射的方式调用base.TaskPlayPowerCardFlyVfx方法，同时保证其await生效
    /// </summary>
    private static async Task PlayPowerCardFlyVfxOverride(CardModel card)
    {
        // 使用缓存的MethodInfo，避免每次反射
        var taskPlayPowerCardFlyVfxMethod = AccessTools.Method(typeof(CardModel), "PlayPowerCardFlyVfx");
        if (taskPlayPowerCardFlyVfxMethod == null)
        {
            Log.Error("[TomorinMod] TaskPlayPowerCardFlyVfx method not found in CardModel.");
            return;
        }

        try
        {
            // 调用基类方法并等待其完成
            var result = taskPlayPowerCardFlyVfxMethod.Invoke(card, null);
            if (result is Task task)
            {
                await task;
            }
            // 如果方法返回void，result将为null，无需等待
        }
        catch (Exception ex)
        {
            Log.Error($"[TomorinMod] Error invoking TaskPlayPowerCardFlyVfx: {ex}");
        }
    }

    private static async Task InvokeOnPlay(CardModel card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        MethodInfo? onPlay = typeof(CardModel).GetMethod(
            "OnPlay", 
            BindingFlags.NonPublic | BindingFlags.Instance, // 关键：非公开 + 实例方法
            null,
            [typeof(PlayerChoiceContext), typeof(CardPlay)], // 严格匹配参数类型
            null
        );
        
        if (onPlay == null)
        {
            Log.Error("[TomorinMod] OnPlay method not found in CardModel.");
            return;
        }
        
        try
        {
            // 调用基类方法并等待其完成
            var result = onPlay.Invoke(card, [choiceContext, cardPlay]);
            if (result is Task task)
            {
                await task;
            }
            // 如果方法返回void，result将为null，无需等待
        }
        catch (Exception ex)
        {
            Log.Error($"[TomorinMod] Error invoking OnPlay: {ex}");
        }
    }
}