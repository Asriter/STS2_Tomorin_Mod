using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 筑起心之墙壁
/// 0费 技能 抽1 获得1-2层心之壁
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class BuildAtField : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1),
        new PowerVar<AtFieldPower>(1m)
    ];

    public BuildAtField() :
        base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 抽牌
        await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, Owner);

        // 获得心之壁
        await PowerCmd.Apply<AtFieldPower>(base.Owner.Creature, base.DynamicVars["AtFieldPower"].BaseValue, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["AtFieldPower"].UpgradeValueBy(1);
    }
}
