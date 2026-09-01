using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Tomorin_Mod.Events;

/// <summary>
/// 第四幕第一个固定问号房：依次发放金币、稀有卡牌奖励和随机遗物。
/// </summary>
public sealed class StageSupplyEvent : CustomEventModel
{
    private const string GoldPage = "GOLD";
    private const string RareCardPage = "RARE_CARD";
    private const string RelicPage = "RELIC";

    /// <summary>暂时复用原占位事件的肖像，等待舞台补给专属美术。</summary>
    public override string CustomInitialPortraitPath =>
        "res://STS2_Tomorin_Mod/images/events/StageSupply.png";

    /// <summary>多人游戏中每名玩家独立领取三层奖励。</summary>
    public override bool IsShared => false;

    /// <summary>本事件仅由第四幕固定路线直接指定，不加入任何随机事件池。</summary>
    public override bool IsAllowed(IRunState runState) => false;

    /// <summary>从发放 100 金币的第一页开始。</summary>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [CreateOption(ClaimGold, GoldPage, nameof(ClaimGold))];

    private async Task ClaimGold()
    {
        if (Owner == null)
        {
            return;
        }

        await PlayerCmd.GainGold(100m, Owner, false);
        SetEventState(
            PageDescription(RareCardPage),
            [CreateOption(ClaimRareCard, RareCardPage, nameof(ClaimRareCard))]);
    }

    private async Task ClaimRareCard()
    {
        if (Owner == null)
        {
            return;
        }

        var options = new CardCreationOptions(
            [Owner.Character.CardPool],
            CardCreationSource.Other,
            CardRarityOddsType.Uniform,
            card => card.Rarity == CardRarity.Rare);

        List<CardCreationResult> cards = CardFactory.CreateForReward(Owner, 3, options).ToList();
        if (cards.Count > 0)
        {
            var prompt = new LocString(
                LocTable,
                $"{Id.Entry}.pages.{RareCardPage}.cardSelectionPrompt");
            var prefs = new CardSelectorPrefs(prompt, 0, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true,
            };
            await SelectCardsToAddToDeckFromGrid(cards, prefs);
        }

        SetEventState(
            PageDescription(RelicPage),
            [CreateOption(ClaimRelic, RelicPage, nameof(ClaimRelic))]);
    }

    private async Task ClaimRelic()
    {
        if (Owner == null)
        {
            return;
        }

        RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner).ToMutable();
        await RelicCmd.Obtain(relic, Owner);
        SetEventFinished(PageDescription(RelicPage));
    }

    private EventOption CreateOption(Func<Task> onChosen, string pageKey, string optionKey) =>
        new(this, onChosen, $"{Id.Entry}.pages.{pageKey}.options.{optionKey}");
}
