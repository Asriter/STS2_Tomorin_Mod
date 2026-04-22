using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 全力歌唱
/// 1费 金卡 造成9-12点伤害。移除自身所有心之壁，每移除一层额外造成3-4点伤害
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class SingFullPower() : BaseCardModel(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    private const string ExtraDamageKey = "ExtraDamage";

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var list = base.CanonicalVars.ToList();
            list.Add(new CalculationBaseVar(9));
            list.Add(new ExtraDamageVar(3));
            list.Add(new CalculatedDamageVar(ValueProp.Move).WithMultiplier(GetDamageCount));
            return list;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var list = base.ExtraHoverTips.ToList();
            list.Add(HoverTipFactory.FromPower<AtFieldPower>());
            return list;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        //先打一下
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    public static decimal GetDamageCount(CardModel card, Creature? creature)
    {
        var amount = 0;
        if (card.Owner.Creature.HasPower<AtFieldPower>())
        {
            var atFieldPower = card.Owner.Creature.GetPower<AtFieldPower>();
            amount = atFieldPower.Amount;
        }

        return amount;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(3);
        DynamicVars.ExtraDamage.UpgradeValueBy(1);
    }
}