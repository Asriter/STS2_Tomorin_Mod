using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Encounters;

namespace STS2_Tomorin_Mod.Enemy.Elite;

/// <summary>
/// 乐奈的精英敌人变体，保留原首领兴趣分支并缩放基础属性。
/// </summary>
public sealed class RaanaElite : Raana
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
    /// 继续显示原始乐奈首领的名称。
    /// </summary>
    public override LocString Title => ModelDb.Monster<Raana>().Title;

    protected override int S1Attack => EliteStatScaler.ScaleDown(base.S1Attack, StatMultiplier);
    protected override int S2Attack => EliteStatScaler.ScaleDown(base.S2Attack, StatMultiplier);
    protected override int S4Attack => EliteStatScaler.ScaleDown(base.S4Attack, StatMultiplier);
    protected override int S4HighAttack => EliteStatScaler.ScaleDown(base.S4HighAttack, StatMultiplier);
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
