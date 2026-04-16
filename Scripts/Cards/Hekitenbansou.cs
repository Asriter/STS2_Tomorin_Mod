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
/// 碧天伴走 Hekitenbansou
/// 金卡 1费 技能
/// 作词 攻击卡*1
/// 将一张"HekitenbansouToken"加入手牌，将这张卡的复制品加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class Hekitenbansou : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new ComposeVar(new Dictionary<CardType, int>() { { CardType.Attack, 1 } }, ModelDb.Card<HekitenbansouToken>()),
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromCard<HekitenbansouToken>(base.IsUpgraded));
            return list;
        }
    }

    public Hekitenbansou() :
        base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ComposeCmd.Compose<HekitenbansouToken>(choiceContext, Owner, ComposeCost, this);

        // 将这张卡的复制品加入手牌
        var copy = CreateClone();
        if (base.IsUpgraded)
            CardCmd.Upgrade(copy);
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, addedByPlayer: true);
    }

    protected override void OnUpgrade()
    {
    }
}
