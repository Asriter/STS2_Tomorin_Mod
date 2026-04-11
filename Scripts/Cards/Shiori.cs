using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.Localization.DynamicVars;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 栞
/// 0费 白卡 技能 作词 状态卡*1 获得1->2层人工制品
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class Shiori : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new ComposeVar(new Dictionary<CardType, int>() { { CardType.Status, 1 } }, ModelDb.Card<OrdinaryAndNaturally>()),
            new PowerVar<ArtifactPower>(1m),
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<ArtifactPower>());
            list.Add(HoverTipFactory.FromCard<OrdinaryAndNaturally>(base.IsUpgraded));
            return list;
        }
    }

    public Shiori() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 从手牌中选择并消耗一张状态卡（或灵感卡）
        await ComposeCmd.Compose<OrdinaryAndNaturally>(choiceContext, Owner, ComposeCost, this);

        await PowerCmd.Apply<ArtifactPower>(Owner.Creature, base.DynamicVars["ArtifactPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars["ArtifactPower"].UpgradeValueBy(1m);
    }
}
