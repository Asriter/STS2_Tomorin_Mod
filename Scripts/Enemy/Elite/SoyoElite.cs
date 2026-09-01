using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Encounters;

namespace STS2_Tomorin_Mod.Enemy.Elite;

/// <summary>
/// 素世的精英敌人变体，保留原首领任务和阶段行为并缩放基础属性。
/// </summary>
public sealed class SoyoElite : Soyo
{
    private const decimal StatMultiplier = 1.5m;

    /// <summary>
    /// 获取按精英倍率缩放后的最低初始生命。
    /// </summary>
    public override int MinInitialHp => EliteStatScaler.ScaleDown(base.MinInitialHp, StatMultiplier);

    /// <summary>
    /// 获取与最低初始生命一致的最高初始生命。
    /// </summary>
    public override int MaxInitialHp => MinInitialHp;

    /// <summary>
    /// 继续显示原始素世首领的名称。
    /// </summary>
    public override LocString Title => ModelDb.Monster<Soyo>().Title;

    protected override int MaskMultiAttack => EliteStatScaler.ScaleDown(base.MaskMultiAttack, StatMultiplier);
    protected override int TrueAttack => EliteStatScaler.ScaleDown(base.TrueAttack, StatMultiplier);
    protected override int TrueMultiAttack => EliteStatScaler.ScaleDown(base.TrueMultiAttack, StatMultiplier);
    protected override bool ShouldGrantBossReward => false;

    /// <summary>
    /// 保留原 Boss 入场逻辑后，按 Encounter 槽位初始化原生夹击 Power。
    /// </summary>
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await BandSurroundedCoordinator.InitializeFor(Creature);
    }
}
