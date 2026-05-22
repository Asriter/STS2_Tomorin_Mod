using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Monsters;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 一阶段buff
/// 复活，切换状态
/// </summary>

public class CrychicPhantomPower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    
    private class Data
    {
        public bool isReviving;
    }
    
    private bool IsReviving => GetInternalData<Data>().isReviving;

    protected override object InitInternalData()
    {
        return new Data();
    }

    public void DoRevive()
    {
        GetInternalData<Data>().isReviving = false;
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == base.Owner && creature.Monster is CrychicPhatom crychicPhatom)
        {
            GetInternalData<Data>().isReviving = true;
            await crychicPhatom.TriggerDeadState();
        }
    }

    public override bool ShouldAllowHitting(Creature creature)
    {
        if (creature != base.Owner)
        {
            return true;
        }
        return !IsReviving;
    }

    public override bool ShouldStopCombatFromEnding()
    {
        return true;
    }

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature)
    {
        if (creature != base.Owner)
        {
            return true;
        }
        return false;
    }

    public override bool ShouldPowerBeRemovedAfterOwnerDeath()
    {
        return false;
    }
}