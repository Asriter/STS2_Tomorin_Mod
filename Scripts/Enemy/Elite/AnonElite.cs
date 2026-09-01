using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Encounters;

namespace STS2_Tomorin_Mod.Enemy.Elite;

/// <summary>
/// 爱音的精英敌人变体，复用原首领状态机并在基础属性层执行倍率缩放。
/// </summary>
public sealed class AnonElite : Anon
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
    /// 继续显示原始爱音首领的名称。
    /// </summary>
    public override LocString Title => ModelDb.Monster<Anon>().Title;

    protected override int NormalSingleAtk => EliteStatScaler.ScaleDown(base.NormalSingleAtk, StatMultiplier);
    protected override int NormalMultiAtk => EliteStatScaler.ScaleDown(base.NormalMultiAtk, StatMultiplier);
    protected override bool ShouldGrantBossReward => false;

    /// <summary>
    /// 保留原 Boss 入场逻辑后，按 Encounter 槽位初始化原生夹击 Power。
    /// </summary>
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await BandSurroundedCoordinator.InitializeFor(Creature);
    }

    /// <summary>
    /// 爱音逃跑后刷新所有玩家的夹击方向。
    /// </summary>
    /// <param name="room">当前战斗房间。</param>
    protected override Task AfterEscapeCompleted(CombatRoom room)
    {
        return BandSurroundedCoordinator.RefreshAfterEscape(room.CombatState, Creature);
    }
}
