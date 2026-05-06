using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Enemy;
using STS2_Tomorin_Mod.Enemy.Ememies;

namespace STS2_Tomorin_Mod.Encounters;

public class TakiBoss : CustomEncounterModel
{
    public TakiBoss() : base(RoomType.Boss, true)
    {
    }
    
    protected override bool HasCustomBackground => true;
    public override string CustomBgm => "TakiBgm";
    
    public override float GetCameraScaling() => 0.9f;
    // public override Vector2 GetCameraOffset() => Vector2.Down * 60f;

    public override string BossNodePath => "res://STS2_Tomorin_Mod/images/boss_icon/Taki_Boss_Icon";

    public override string? CustomRunHistoryIconPath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/taki_headIcon.png";

    public override string? CustomRunHistoryIconOutlinePath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/taki_headIcon.png";

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Taki>().ToMutable(), null)
    ];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Taki>().ToMutable()
    ];
    
    public override bool IsValidForAct(ActModel act)
    {
        return false;
    }
}