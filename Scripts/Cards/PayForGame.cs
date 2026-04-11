using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 爆大米
/// 金卡 1费 技能 花费20金币；从3张随机稀有Tomorin牌中选1张，本回合费用为0；将一张星石加入手牌
/// 升级后：所展示的牌已升级
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class PayForGame : BaseCardModel
{
    protected override bool IsPlayable => base.Owner.Gold >= 20;

    public PayForGame() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.LoseGold(20m, base.Owner, GoldLossType.Spent);

        var rareCards = ModelDb.CardPool<TomorinCardPool>().AllCards
            .Where(c => c.Rarity == CardRarity.Rare)
            .ToList();

        if (rareCards.Count > 0)
        {
            var models = rareCards.TakeRandom(3, base.Owner.RunState.Rng.CombatCardGeneration).ToList();
            var runtimeCards = new List<CardModel>();
            foreach (var model in models)
            {
                var card = base.CombatState!.CreateCard(model, base.Owner);
                if (IsUpgraded) CardCmd.Upgrade(card);
                runtimeCards.Add(card);
            }

            var prefs = new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1);
            var selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, runtimeCards, base.Owner, prefs)).FirstOrDefault();
            if (selected != null)
            {
                selected.EnergyCost.SetThisTurn(0);
                await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, addedByPlayer: true);
            }
        }

        var starStone = base.CombatState!.CreateCard<StarStone>(base.Owner);
        await CardPileCmd.AddGeneratedCardToCombat(starStone, PileType.Hand, addedByPlayer: true);
    }

    protected override void OnUpgrade()
    {
        // 升级效果通过 IsUpgraded 在 OnPlay 中处理
    }
}
