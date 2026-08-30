using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

public class EnemyMaxDamageReceivedPower : BasePowerModel
{
    private class Data
    {
        public decimal damageReceivedThisPhase;
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldScaleInMultiplayer => true;

    public override int DisplayAmount => (int)RemainingAllowance;

    /// <summary>伤害额度耗尽后是否保持 Taki 既有的敌方回合开始自动移除行为。</summary>
    public bool RemoveAtEnemyTurnStartWhenDepleted { get; set; } = true;

    /// <summary>当前阶段尚可承受的非负伤害额度。</summary>
    public decimal RemainingAllowance =>
        Math.Max(0m, Amount - GetInternalData<Data>().damageReceivedThisPhase);
    
    //转阶段的回调
    public Action? DamageCallBack { get; set; }

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override decimal ModifyHpLostBeforeOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner)
        {
            return amount;
        }
        if (amount == 0m)
        {
            return amount;
        }
        return LimitDamageToRemainingAllowance(amount, RemainingAllowance);
    }

    public override Task AfterModifyingHpLostBeforeOsty()
    {
        Flash();
        return Task.CompletedTask;
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner)
        {
            return Task.CompletedTask;
        }
        if (result.WasFullyBlocked)
        {
            return Task.CompletedTask;
        }
        GetInternalData<Data>().damageReceivedThisPhase += (decimal)result.UnblockedDamage;
        InvokeDisplayAmountChanged();
        
        //如果伤害归零，则触发回调
        if (GetInternalData<Data>().damageReceivedThisPhase >= Amount) 
        {
            DamageCallBack?.Invoke();
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// 敌人回合开始时，如果到达伤害上限则移除该buff
    /// </summary>
    /// <param name="choiceContext"></param>
    /// <param name="side"></param>
    /// <param name="combatState"></param>
    /// <returns></returns>
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (RemoveAtEnemyTurnStartWhenDepleted &&
            side != CombatSide.Player &&
            RemainingAllowance <= 0m)
        {
            PowerCmd.Remove(this);
        }
        return Task.CompletedTask;
    }

    /// <summary>把一次非负入站伤害截断到当前非负额度，供伤害钩子与领域测试共享。</summary>
    public static decimal LimitDamageToRemainingAllowance(decimal amount, decimal remainingAllowance)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return Math.Min(amount, Math.Max(0m, remainingAllowance));
    }
}
