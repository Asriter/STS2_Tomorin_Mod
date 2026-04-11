using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Localization.CustomEnums;

namespace STS2_Tomorin_Mod.Commands;

/// <summary>
/// 作词
/// 打出时消耗记述的卡，处理打出后的效果，将一张记述的卡加入手牌，然后自身消失
/// </summary>
public static class ComposeCmd
{
    //每回合消耗了的卡计数
    public static Dictionary<Player, int> ComposeCostCardDict = new Dictionary<Player, int>();

    /// <summary>
    /// 执行作词效果：消耗指定类型和数量的手牌，然后为目标卡牌添加重放或加入手牌，最后消耗源卡牌。
    /// </summary>
    /// <param name="choiceContext">选择上下文对象 PlayerChoiceContext</param>
    /// <param name="player">执行操作的玩家</param>
    /// <param name="costCards">需要消耗的卡牌类型及其数量</param>
    /// <param name="source">打出的作词卡牌（将会被消耗）</param>
    /// <returns>处理后的目标卡牌（如果存在）或新加入手牌的卡牌</returns>
    /// <exception cref="InvalidOperationException">手牌中缺少足够的卡牌用于消耗</exception>
    public static async Task Compose<T>(PlayerChoiceContext choiceContext, Player player,
        Dictionary<CardType, int> costCards, CardModel source) where T : CardModel
    {
        if (player.PlayerCombatState == null)
            throw new InvalidOperationException("Player is not in combat state.");

        // 1. 检查手牌中是否有足够数量的对应牌用作cost,如果不满足条件则直接返回（处理乱战等自动打出的情况）
        if (!CanUseCompose(player, costCards, source))
            return;

        // 2. 从手牌中选择足量的costCard消耗
        var cardsToConsume = new List<CardModel>();
        foreach (var kv in costCards)
        {
            var cardType = kv.Key;
            var requiredCount = kv.Value;

            cardsToConsume.AddRange(await CardSelectCmd.FromHand(
                choiceContext,
                player,
                new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, requiredCount),
                model => model.Type == cardType || model.CanonicalKeywords.Contains(CustomKeyWord.Epiphany),
                source));
        }

        //记录每个角色分别打了多少下
        if (!ComposeCostCardDict.ContainsKey(player))
            ComposeCostCardDict.Add(player, 0);
        for (int i = 0; i < cardsToConsume.Count; i++)
        {
            ComposeCostCardDict[player]++;
            await CardCmd.Exhaust(choiceContext, cardsToConsume[i]);
        }

        // 3. 检测目标卡牌是否在卡组/手牌/弃牌堆中存在（不包括source自身）
        CardModel? targetCard = null;
        var pilesToSearch = new[]
        {
            player.PlayerCombatState.Hand,
            player.PlayerCombatState.DrawPile,
            player.PlayerCombatState.DiscardPile
        };

        foreach (var pile in pilesToSearch)
        {
            targetCard = pile.Cards.FirstOrDefault(card =>
                card != source && card.GetType() == typeof(T) && card.IsUpgraded == source.IsUpgraded);
            if (targetCard != null)
                break;
        }

        // 4. 如果存在，为其加上一层重放；否则将这张卡加入手牌
        if (targetCard != null)
        {
            // 增加重放次数
            targetCard.BaseReplayCount += 1;
            CardCmd.Preview(targetCard);
        }
        else
        {
            // 创建一张新的同类型卡牌加入手牌
            var newCard = source.CombatState.CreateCard<T>(source.Owner);
            if (source.IsUpgraded)
                CardCmd.Upgrade(newCard);

            await CardPileCmd.AddGeneratedCardToCombat(
                newCard,
                PileType.Hand,
                addedByPlayer: true);
        }

        // 5. 消耗source卡牌（自身消失）
        await CardCmd.Exhaust(choiceContext, source);

        //触发所有的”作词后”效果
        IRunState runState = player.RunState;
        foreach (AbstractModel model in runState.IterateHookListeners(null))
        {
            if (model is CustomHookInterface customHook)
            {
                await customHook.AfterCompose(choiceContext, player, source);
            }
        }

        // 触发战斗中所有power的”作词后”效果
        foreach (PowerModel power in player.Creature.Powers)
        {
            if (power is CustomHookInterface powerHook)
            {
                await powerHook.AfterCompose(choiceContext, player, source);
            }
        }
    }

    public static bool CanUseCompose(Player player,
        Dictionary<CardType, int> costCards, CardModel source)
    {
        if (player.PlayerCombatState == null)
            throw new InvalidOperationException("Player is not in combat state.");

        var hand = player.PlayerCombatState.Hand;

        //查找灵光乍现tag的数量
        var epiphanyCount = hand.Cards.Count(card => card.CanonicalKeywords.Contains(CustomKeyWord.Epiphany));

        // 1. 检查手牌中是否有足够数量的对应牌用作cost
        foreach (var kv in costCards)
        {
            var cardType = kv.Key;
            var requiredCount = kv.Value;
            var actualCount = hand.Cards.Count(card => card != source && card.Type == cardType && !card.CanonicalKeywords.Contains(CustomKeyWord.Epiphany));
            if (actualCount + epiphanyCount < requiredCount)
                return false;

            if (actualCount < requiredCount)
                epiphanyCount -= (requiredCount - actualCount);
        }
        
        return true;
    }
}