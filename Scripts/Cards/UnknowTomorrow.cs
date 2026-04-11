using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards.Base;

namespace STS2_Tomorin_Mod.Cards;

/// <summary>
/// 尚未可知的明日
/// 蓝卡 0费 攻击 当回合每消耗一张卡，对随机敌人造成4->6点伤害
/// </summary>
[Pool(typeof(TomorinCardPool))]
public class UnknowTomorrow : BaseCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var list = base.CanonicalVars.ToList();
            list.Add(new DamageVar(4m, ValueProp.Move));
            list.Add(new CalculationBaseVar(0m));
            list.Add(new CalculationExtraVar(1m));
            list.Add(new CalculatedVar("CalculatedHits").WithMultiplier((CardModel card, Creature? _) => 
                (from e in CombatManager.Instance.History.Entries.OfType<CardExhaustedEntry>()
                where e.HappenedThisTurn(card.CombatState) && e.Card.Owner == card.Owner && e.Card != card 
                select e).Count()));
            return list;
        }
    }

    public UnknowTomorrow() :
        base(0, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .WithHitCount((int)((CalculatedVar)base.DynamicVars["CalculatedHits"]).Calculate(cardPlay.Target))
            .FromCard(this)
            .TargetingRandomOpponents(base.CombatState!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
