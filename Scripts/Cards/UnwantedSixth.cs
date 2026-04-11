using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 不被需要的第六人
/// 蓝卡 0费 技能 本回合每次获得格挡时，额外获得1->2层心之壁，并将一张压皱的残页加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class UnwantedSixth : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new List<DynamicVar>()
        {
            new PowerVar<UnwantedSixthPower>(1m),
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<UnwantedSixthPower>());
            list.Add(HoverTipFactory.FromCard<CrumpledPaper>());
            return list;
        }
    }

    public UnwantedSixth() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<UnwantedSixthPower>(
            base.Owner.Creature,
            base.DynamicVars["UnwantedSixthPower"].BaseValue,
            base.Owner.Creature,
            this);
        
        // 生成一张压皱的残页加入手牌
        var card = base.CombatState!.CreateCard<CrumpledPaper>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars["UnwantedSixthPower"].UpgradeValueBy(1m);
    }
}
