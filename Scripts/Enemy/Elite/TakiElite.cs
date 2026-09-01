using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Encounters;
using STS2_Tomorin_Mod.Enemy.Ememies;

namespace STS2_Tomorin_Mod.Enemy.Elite;

/// <summary>
/// 立希的精英敌人变体，复用原首领阶段状态机并关闭首领房专属结算。
/// </summary>
public sealed class TakiElite : Taki
{
    private const decimal StatMultiplier = 2m;

    /// <summary>
    /// 获取按精英倍率缩放后的最低初始生命。
    /// </summary>
    public override int MinInitialHp => EliteStatScaler.ScaleDown(base.MinInitialHp, StatMultiplier);

    /// <summary>
    /// 获取与最低初始生命一致的最高初始生命。
    /// </summary>
    public override int MaxInitialHp => MinInitialHp;

    /// <summary>
    /// 继续显示原始立希首领的名称。
    /// </summary>
    public override LocString Title => ModelDb.Monster<Taki>().Title;

    protected override int PhaseOneStateAtk => EliteStatScaler.ScaleDown(base.PhaseOneStateAtk, StatMultiplier);
    protected override int PhaseOneNormalAtk => EliteStatScaler.ScaleDown(base.PhaseOneNormalAtk, StatMultiplier);
    protected override int PhaseOneBigAtk => EliteStatScaler.ScaleDown(base.PhaseOneBigAtk, StatMultiplier);
    protected override int PhaseTwoCardAtk => EliteStatScaler.ScaleDown(base.PhaseTwoCardAtk, StatMultiplier);
    protected override int PhaseThreeAtk => EliteStatScaler.ScaleDown(base.PhaseThreeAtk, StatMultiplier);
    protected override int PhaseOneHp => EliteStatScaler.ScaleDown(base.PhaseOneHp, StatMultiplier);
    protected override int PhaseTwoHp => EliteStatScaler.ScaleDown(base.PhaseTwoHp, StatMultiplier);
    protected override bool ShouldGrantBossReward => false;
    protected override bool ShouldEndRoomAfterEscape => false;

    /// <summary>
    /// 保留原 Boss 入场逻辑后，按 Encounter 槽位初始化原生夹击 Power。
    /// </summary>
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await BandSurroundedCoordinator.InitializeFor(Creature);
    }

    /// <summary>
    /// 立希逃跑后刷新所有玩家的夹击方向，而不提前结束仍有敌人的房间。
    /// </summary>
    /// <param name="room">当前战斗房间。</param>
    protected override Task AfterEscapeCompleted(CombatRoom room)
    {
        return BandSurroundedCoordinator.RefreshAfterEscape(room.CombatState, Creature);
    }
}
