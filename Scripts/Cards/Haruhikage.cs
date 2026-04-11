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
/// 春日影
/// 2费 金卡 技能 常驻滞留 创作 状态*2 获得2->3能量，将一张PrideManSaki加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class Haruhikage : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new ComposeVar(new Dictionary<CardType, int>() { { CardType.Status, 2 } }, ModelDb.Card<PrideManSaki>()),
            new EnergyVar(2),
        };

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromCard<PrideManSaki>(base.IsUpgraded));
            return list;
        }
    }

    public Haruhikage() :
        base(2, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ComposeCmd.Compose<PrideManSaki>(choiceContext, Owner, ComposeCost, this);

        await PlayerCmd.GainEnergy(base.DynamicVars.Energy.BaseValue, base.Owner);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Energy.UpgradeValueBy(1m);
    }
}
