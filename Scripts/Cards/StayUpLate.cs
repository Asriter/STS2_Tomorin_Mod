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
/// 蓝卡 1费 技能 升级后先获得2层心之壁；抽等于心之壁层数的牌；将一张深夜的罐装咖啡加入手牌
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class StayUpLate : BaseCardModel
{
    private const string _isUpgradeKey = "IsUpgrade";
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            list.Add(HoverTipFactory.FromCard<MidnightCoffee>(base.IsUpgraded));
            return list;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar(_isUpgradeKey, 0),
    ];

    public StayUpLate() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await PowerCmd.Apply<AtFieldPower>(base.Owner.Creature, 2m, base.Owner.Creature, this);
        }

        int atFieldStacks = Owner.Creature.HasPower<AtFieldPower>()
            ? (int)Owner.Creature.GetPower<AtFieldPower>().Amount
            : 0;

        if (atFieldStacks > 0)
        {
            await CardPileCmd.Draw(choiceContext, (decimal)atFieldStacks, base.Owner);
        }

        var midnightCoffee = base.CombatState!.CreateCard<MidnightCoffee>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(midnightCoffee, PileType.Hand, addedByPlayer: true);
    }
    
    protected override void OnUpgrade()
    {
        // 升级效果通过 IsUpgraded 在 OnPlay 中处理
        base.DynamicVars[_isUpgradeKey].UpgradeValueBy(1);
    }
}
