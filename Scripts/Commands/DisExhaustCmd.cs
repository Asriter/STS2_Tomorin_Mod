using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Commands;

/// <summary>
/// 回收消耗区的卡
/// </summary>
public static class DisExhaustCmd
{
    public static async Task DisExhaust(PlayerChoiceContext choiceContext, Player player, int count, CardModel source)
    {
        var exhaustPile = player.PlayerCombatState?.ExhaustPile;
        if (exhaustPile == null || exhaustPile.Cards.Count == 0)
            return;

        var selected = await CommonActions.SelectCards(
            source,
            CardSelectorPrefs.ExhaustSelectionPrompt,
            choiceContext,
            PileType.Exhaust,
            0, count);

        foreach (var card in selected)
        {
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, source);
        }
    }
    
    /// <summary>
    /// 随机获取消耗牌堆中的牌
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="player"></param>
    /// <param name="count"></param>
    /// <param name="source"></param>
    public static async Task RandomDisExhaust(PlayerChoiceContext choiceContext, Player player, int count, CardModel source)
    {
        var exhaustPile = player.PlayerCombatState?.ExhaustPile;
        if (exhaustPile == null || exhaustPile.Cards.Count == 0)
            return;
        
        // Owner.RunState.Rng.CombatCardGeneration
        var cards = exhaustPile.Cards.TakeRandom(count, player.RunState.Rng.CombatCardGeneration);

        foreach (var card in cards)
        {
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, source);
        }
    }
}