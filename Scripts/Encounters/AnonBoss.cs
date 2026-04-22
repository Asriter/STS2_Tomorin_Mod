using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Enemy.Ememies;

namespace STS2_Tomorin_Mod.Encounters;

public class AnonBoss : CustomEncounterModel
{
    public AnonBoss() : base(RoomType.Boss, true)
    {
    }
    
    protected override bool HasCustomBackground => true;
    // public override string CustomBgm => "{90e9ca7b-3b88-4cf6-88ab-a5eb9db02d5d}";
    public override string CustomBgm => "AnonBgm";
	// public override string CustomBgm => "event:/music/act1_b_boss_waterfall_giant";
    
    public override float GetCameraScaling() => 0.9f;
    // public override Vector2 GetCameraOffset() => Vector2.Down * 60f;

    public override string BossNodePath => "res://STS2_Tomorin_Mod/images/boss_icon/Anon_Boss_Icon";

    public override string? CustomRunHistoryIconPath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/anon_headIcon.png";

    public override string? CustomRunHistoryIconOutlinePath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/anon_headIcon.png";

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [
            (ModelDb.Monster<Anon>().ToMutable(), null)
        ];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Anon>().ToMutable()
    ];
    
    public override bool IsValidForAct(ActModel act)
    {
        return false;
    }
}