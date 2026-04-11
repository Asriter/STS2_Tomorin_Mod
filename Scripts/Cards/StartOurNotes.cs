using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// O神，启动！
/// 金卡 0费 技能 升级后固有；获得1费；获得2层心之壁；从牌组选1张牌加入手牌；将一张星石加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class StartOurNotes : BaseCardModel
{
    public StartOurNotes() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            list.Add(HoverTipFactory.FromCard<StarStone>());
            return list;
        }
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            var list = base.CanonicalKeywords.ToList();
            list.Add(CardKeyword.Exhaust);
            return list;
        }
    }


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(1m, base.Owner);
        await PowerCmd.Apply<AtFieldPower>(base.Owner.Creature, 2m, base.Owner.Creature, this);

        // Select 1 card from draw pile
        var drawPile = base.Owner.PlayerCombatState?.DrawPile;
        if (drawPile != null && drawPile.Cards.Count > 0)
        {
            var selected = await CommonActions.SelectCards(
                this,
                CardSelectorPrefs.ExhaustSelectionPrompt,
                choiceContext,
                PileType.Draw,
                0, 1);
            foreach (var card in selected)
            {
                await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, this);
            }
        }

        var starStone = base.CombatState!.CreateCard<StarStone>(base.Owner);
        await CardPileCmd.AddGeneratedCardToCombat(starStone, PileType.Hand, addedByPlayer: true);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
