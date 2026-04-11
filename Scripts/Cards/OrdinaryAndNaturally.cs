using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 普通和理所当然
/// 1费 白卡 技能 选择一张收集品加入手牌。如果此卡被消耗，重新将此卡加入手牌。
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class OrdinaryAndNaturally() : BaseCardModel(1, CardType.Skill, CardRarity.Token, TargetType.None)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获取CollectionsCardPool中所有卡的规范模型
        var allCollectionCards = ModelDb.CardPool<CollectionsCardPool>().AllCards.ToList();
        if (allCollectionCards.Count == 0) return;

        // 展示选择界面，选择一张收集品
        var prefs = new CardSelectorPrefs(base.SelectionScreenPrompt, 1);
        var selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, allCollectionCards, Owner, prefs)).FirstOrDefault();

        if (selected != null)
        {
            // 创建一张新的收集品副本并加入手牌
            var newCard = base.CombatState!.CreateCard(selected, Owner);
            await CardPileCmd.AddGeneratedCardToCombat(newCard, PileType.Hand, addedByPlayer: true);
        }
    }

    // 如果此卡被消耗，重新将此卡加入手牌
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        await base.AfterCardExhausted(choiceContext, card, causedByEthereal);
        if (card == this)
        {
            await CardPileCmd.Add(this, PileType.Hand, CardPilePosition.Top, this);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Exhaust);
    }
}
