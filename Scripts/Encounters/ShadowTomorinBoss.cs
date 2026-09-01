using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Encounters;

/// <summary>Stage 第四层路线专用的影灯首领遭遇。</summary>
public sealed class ShadowTomorinBoss : CustomEncounterModel
{
    public ShadowTomorinBoss() : base(RoomType.Boss, true)
    {
    }

    public override string CustomBgm => "ShadowTomorinBgm";
    
    protected override bool HasCustomBackground => true;

    public override float GetCameraScaling() => 0.9f;

    public override string BossNodePath =>
        "res://STS2_Tomorin_Mod/images/boss_icon/Shadow_Tomori_Boss_Icon";

    public override string? CustomRunHistoryIconPath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/tomorin_boss_headIcon.png";

    public override string? CustomRunHistoryIconOutlinePath => CustomRunHistoryIconPath;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<ShadowTomorin>().ToMutable(), null)];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<ShadowTomorin>().ToMutable()];

    /// <summary>只由 Stage 的固定路线显式选择，不进入其他章节的随机 Boss 池。</summary>
    public override bool IsValidForAct(ActModel act) => false;
}
