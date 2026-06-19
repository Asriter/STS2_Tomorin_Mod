using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Timoris被动：受到的非直接伤害减半；每回合最多受到 3 x 玩家数 次伤害，超出次数的伤害归零。
/// 在 BeforeDamageReceived 中递增命中计数，在 ModifyHpLostBeforeOstyLate 中截断超出上限的伤害。
/// </summary>
public class TimorisPassivePower : BasePowerModel
{
    // protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<TimorisKillBuffPower>()];

    private const string _canHitName = "CanHitNum";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar(_canHitName, 3)];
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount =>
        (int)DynamicVars[_canHitName].BaseValue * base.Owner.CombatState.Players.Count - HitCountThisTurn;

    private int _hitCountThisTurn;

    private int HitCountThisTurn
    {
        get => _hitCountThisTurn;
        set { _hitCountThisTurn = value; }
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == base.Owner.Side)
        {
            HitCountThisTurn = 0;
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == base.Owner)
        {
            HitCountThisTurn++;
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostBeforeOstyLate(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return amount;

        if (props.HasFlag(ValueProp.Unpowered))
            amount /= 2m;

        int maxHits = (int)DynamicVars[_canHitName].BaseValue * base.Owner.CombatState.Players.Count;
        if (HitCountThisTurn > maxHits)
            return 0m;

        return amount;
    }
}
