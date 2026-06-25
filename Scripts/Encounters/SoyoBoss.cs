using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Encounters;

public class SoyoBoss : CustomEncounterModel
{
    public SoyoBoss() : base(RoomType.Boss, true)
    {
    }

    protected override bool HasCustomBackground => true;
    public override string CustomBgm => "SoyoBgm";
    
    public override float GetCameraScaling() => 0.9f;
    // public override Vector2 GetCameraOffset() => Vector2.Down * 60f;

    public override string BossNodePath => "res://STS2_Tomorin_Mod/images/boss_icon/Soyo_Boss_Icon";

    public override string? CustomRunHistoryIconPath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/soyo_headIcon.png";

    public override string? CustomRunHistoryIconOutlinePath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/soyo_headIcon.png";

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Soyo>().ToMutable(), null)
    ];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Soyo>().ToMutable()
    ];
    
    public override bool IsValidForAct(ActModel act)
    {
        return false;
    }
}