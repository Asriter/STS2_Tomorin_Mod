using BaseLib.Utils;
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
/// 熬夜作曲
/// 蓝卡 1费 技能 抽2-3， 获得4-5点心之壁
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class StayUpLate : BaseCardModel
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            return list;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<AtFieldPower>(3),
        new CardsVar(2),
    ];

    public StayUpLate() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
        
        await PowerCmd.Apply<AtFieldPower>(choiceContext, base.Owner.Creature, base.DynamicVars["AtFieldPower"].BaseValue,
            base.Owner.Creature, this);

        // var midnightCoffee = base.CombatState!.CreateCard<MidnightCoffee>(Owner);
        // await CardPileCmd.AddGeneratedCardToCombat(midnightCoffee, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        // 升级效果通过 IsUpgraded 在 OnPlay 中处理
        base.DynamicVars.Cards.UpgradeValueBy(1);
        DynamicVars[AtFieldPower.DefaultName].UpgradeValueBy(1);
    }
}