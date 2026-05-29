using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 猛独侵袭
/// 白卡 技能 1费 作词：技能卡*1，从弃牌堆中选择一张卡加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class LonelyLot : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new ComposeVar(new Dictionary<CardType, int>() { { CardType.Skill, 1 } }, ModelDb.Card<LonelyLotToken>())
        };

    public LonelyLot() :
        base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromCard<LonelyLotToken>(base.IsUpgraded));
            return list;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ComposeCmd.Compose<LonelyLotToken>(choiceContext, Owner, ComposeCost, this);

        var discardPile = base.Owner.PlayerCombatState?.DiscardPile;
        if (discardPile == null || discardPile.Cards.Count == 0)
            return;

        var selected = await CommonActions.SelectCards(
            this,
            CardSelectorPrefs.ExhaustSelectionPrompt,
            choiceContext,
            PileType.Discard,
            0, 1);

        foreach (var card in selected)
        {
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, this);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
