using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 不灭的残响
/// 蓝卡 1费 技能 抽2->3 这回合保留你的手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class Ourai : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new PowerVar<RetainHandPower>(1m)
    ];

    public Ourai() :
        base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 抽牌
        await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, Owner);

        // 获得保留手牌Power
        await PowerCmd.Apply<RetainHandPower>(base.Owner.Creature, base.DynamicVars["RetainHandPower"].BaseValue, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
