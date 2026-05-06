using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 锁血，但是1血时跳过回合
/// </summary>
public class TakiLockHpPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return amount;
        }

        if (target != base.Owner)
        {
            return amount;
        }

        return Math.Min(GetDamageCap(target), amount);
    }

    public override Task AfterModifyingHpLostAfterOsty()
    {
        Flash();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner)
        {
            return decimal.MaxValue;
        }

        return GetDamageCap(target);
    }

    public override Task AfterModifyingDamageAmount(CardModel? cardSource)
    {
        Flash();
        return Task.CompletedTask;
    }

    private int GetDamageCap(Creature? target)
    {
        return target.CurrentHp > 1 ? target.CurrentHp - 1 : 0;
    }

    //是否判定死亡
    public bool IsDead => Owner.CurrentHp == 1;

    /// <summary>
    /// 如果只剩1血，则跳过回合
    /// </summary>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        Log.Debug($"没跳过回合，但是draw！当前状态：{IsDead}");

        if (IsDead && Owner.Player != null)
        {
            Log.Debug("跳过回合！！！！！！！！");
            PlayerCmd.EndTurn(base.Owner.Player, canBackOut: false);
        }

        return Task.CompletedTask;
    }
}