using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2_Tomorin_Mod.Encounters;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// Oblivionis隐藏Boss复活Power（参照DoorRevivalPower）。
/// 当Oblivionis死亡且0子Boss被击杀时触发：
/// 1. 子Boss逃脱
/// 2. Oblivionis进入DeadState
/// 3. 创建FullPowerOblivionis
/// 4. 播放入场动画
/// 5. 移除Oblivionis尸体
/// </summary>
public sealed class OblivionisHiddenRevivalPower : BasePowerModel
{
    private class Data
    {
        public bool isHalfDead;
    }

    public bool IsHalfDead => GetInternalData<Data>().isHalfDead;
    // protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData() => new Data();

    public override Task BeforeDeath(Creature creature)
    {
        if (creature == base.Owner)
        {
            GetInternalData<Data>().isHalfDead = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != base.Owner) return;

        //判断是否能够叫隐藏boss
        var subBossCount = base.CombatState.Enemies.Count(e => e != base.Owner && e.IsAlive);


        if (wasRemovalPrevented)
        {
            GetInternalData<Data>().isHalfDead = false;
            return;
        }

        // 1. 所有存活子Boss逃脱
        var subBosses = base.CombatState.Enemies
            .Where(e => e != base.Owner && e.IsAlive)
            .ToList();
        foreach (var subBoss in subBosses)
        {
            await CreatureCmd.Escape(subBoss);
        }

        if (subBossCount < 4)
            return;


        // 2. Oblivionis进入DeadState
        if (base.Owner.Monster is Oblivionis oblivionis)
        {
            oblivionis.PrepareForHiddenDeath();
            oblivionis.SetMoveImmediate(oblivionis.DeadState);
        }

        // 3. 创建FullPowerOblivionis
        var fullPowerMonster = ModelDb.Monster<FullPowerOblivionis>().ToMutable();
        var newCreature = await CreatureCmd.Add(fullPowerMonster, base.CombatState, CombatSide.Enemy,
            OblivionisBoss.DolrisSlot);

        // 4. 播放入场动画
        if (newCreature.Monster is FullPowerOblivionis fpo)
        {
            await fpo.AnimIn();
        }

        // 5. 移除原Oblivionis尸体
        GetInternalData<Data>().isHalfDead = false;
        NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (nCreature != null)
        {
            NCombatRoom.Instance?.RemoveCreatureNode(nCreature);
        }
    }

    public override bool ShouldAllowHitting(Creature creature)
    {
        if (creature != base.Owner) return true;
        return !IsHalfDead;
    }

    public override bool ShouldStopCombatFromEnding()
    {
        if (!IsHalfDead) return false;
        return true;
    }
    // public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}