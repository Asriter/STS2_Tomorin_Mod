using BaseLib.Utils;
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
/// 人类颂歌
/// 1费 金卡 技能 常驻滞留 创作 技能*1 抽3->4张牌，将一张Haruhikage加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class SongOfBeHuman : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new ComposeVar(new Dictionary<CardType, int>() { { CardType.Skill, 2 } }, ModelDb.Card<Haruhikage>()),
            new CardsVar(2),
        };

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromCard<Haruhikage>(base.IsUpgraded));
            return list;
        }
    }

    public SongOfBeHuman() :
        base(1, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ComposeCmd.Compose<Haruhikage>(choiceContext, Owner, ComposeCost, this);

        await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
