using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace STS2_Tomorin_Mod.Encounters;

/// <summary>
/// 负责乐队精英房的原生夹击 Power 初始化和离场方向刷新。
/// </summary>
internal static class BandSurroundedCoordinator
{
    /// <summary>
    /// 幂等初始化玩家与左右敌人的原生夹击 Power。
    /// </summary>
    /// <param name="leftEnemy">位于玩家左侧的敌人。</param>
    /// <param name="rightEnemy">位于玩家右侧的敌人。</param>
    public static async Task Initialize(Creature leftEnemy, Creature rightEnemy)
    {
        var context = new ThrowingPlayerChoiceContext();
        var combatState = rightEnemy.CombatState ??
                          throw new InvalidOperationException("乐队夹击初始化时右侧敌人尚未绑定战斗状态。");
        foreach (var player in combatState.Players)
        {
            var surrounded = player.Creature.GetPower<SurroundedPower>();
            if (surrounded == null)
            {
                await PowerCmd.Apply<SurroundedPower>(context, player.Creature, 1m, rightEnemy, null);
            }
        }

        if (!leftEnemy.HasPower<BackAttackLeftPower>())
        {
            await PowerCmd.Apply<BackAttackLeftPower>(context, leftEnemy, 1m, leftEnemy, null);
        }

        if (!rightEnemy.HasPower<BackAttackRightPower>())
        {
            await PowerCmd.Apply<BackAttackRightPower>(context, rightEnemy, 1m, rightEnemy, null);
        }
    }

    /// <summary>
    /// 敌人逃跑后令每名玩家的夹击方向指向仍可攻击的敌人。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="escapedEnemy">刚刚完成逃跑的敌人。</param>
    public static async Task RefreshAfterEscape(ICombatState combatState, Creature escapedEnemy)
    {
        var remainingEnemy = combatState.HittableEnemies.FirstOrDefault();
        if (remainingEnemy == null)
        {
            return;
        }

        foreach (var player in combatState.Players)
        {
            var surrounded = player.Creature.GetPower<SurroundedPower>();
            if (surrounded != null)
            {
                await surrounded.AfterDeath(new ThrowingPlayerChoiceContext(), escapedEnemy, false, 0f);
            }
        }
    }
}
