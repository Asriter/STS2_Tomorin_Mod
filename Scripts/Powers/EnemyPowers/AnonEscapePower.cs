using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2_Tomorin_Mod.Enemy.Ememies;

namespace STS2_Tomorin_Mod.Powers;

/// <summary>
/// 爱音的buff，死亡后复活并逃跑
/// </summary>
public class AnonEscapePower : BasePowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == base.Owner)
        {
            await ((Anon)creature.Monster).TriggerAnonRun();
        }
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