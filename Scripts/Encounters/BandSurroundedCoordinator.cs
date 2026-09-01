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
    /// 按敌人的 Encounter 槽位幂等初始化其原生夹击 Power。
    /// </summary>
    /// <param name="enemy">刚加入战斗房间的乐队精英敌人。</param>
    public static async Task InitializeFor(Creature enemy)
    {
        var context = new ThrowingPlayerChoiceContext();
        var combatState = enemy.CombatState ??
                          throw new InvalidOperationException("乐队夹击初始化时敌人尚未绑定战斗状态。");

        if (enemy.SlotName == BandMemberEncounter.LeftMember)
        {
            if (!enemy.HasPower<BackAttackLeftPower>())
            {
                await PowerCmd.Apply<BackAttackLeftPower>(context, enemy, 1m, enemy, null);
            }

            return;
        }

        if (enemy.SlotName != BandMemberEncounter.RightMember)
        {
            throw new InvalidOperationException(
                $"乐队夹击初始化收到未知敌人槽位：{enemy.SlotName ?? "<null>"}。");
        }

        foreach (var opponent in combatState.GetOpponentsOf(enemy))
        {
            if (!opponent.HasPower<SurroundedPower>())
            {
                await PowerCmd.Apply<SurroundedPower>(context, opponent, 1m, enemy, null);
            }
        }

        if (!enemy.HasPower<BackAttackRightPower>())
        {
            await PowerCmd.Apply<BackAttackRightPower>(context, enemy, 1m, enemy, null);
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
