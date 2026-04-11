using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
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
            list.Add(new DamageVar(9, ValueProp.Move));
            list.Add(new IntVar(ExtraDamageKey, 3));
            list.Add(new CalculationBaseVar(0m));
            list.Add(new CalculationExtraVar(1m));
            list.Add(new CalculatedVar("CalculatedHits").WithMultiplier(GetDamageCount));
            return list;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        //先打一下
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        int count = (int)GetDamageCount(this, Owner.Creature);
        if (count > 0)
        {
            await PowerCmd.Remove<AtFieldPower>(Owner.Creature);
            //额外打
            await DamageCmd.Attack(base.DynamicVars[ExtraDamageKey].IntValue)
                .WithHitCount(count)
                .FromCard(this)
                .Targeting(cardPlay.Target)
                .Execute(choiceContext);
            
        }
    }

    public static decimal GetDamageCount(CardModel card, Creature? creature)
    {
        if (card.Owner.Creature.HasPower<AtFieldPower>())
        {
            var atFieldPower = card.Owner.Creature.GetPower<AtFieldPower>();
            return atFieldPower.Amount;
        }
        return 0;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars[ExtraDamageKey].UpgradeValueBy(1);
    }
}