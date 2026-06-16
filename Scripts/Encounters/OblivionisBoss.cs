using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Enemy;

namespace STS2_Tomorin_Mod.Encounters;

public class OblivionisBoss : CustomEncounterModel
{
    public OblivionisBoss() : base(RoomType.Boss, true)
    {
    }

    //站位
    public const string MortisSlot = "Mortis";
    public const string DolrisSlot = "Dolris";
    public const string AmorisSlot = "Amoris";
    public const string TmorisSlot = "Tmoris";
    public const string OblvnsSlot = "Oblvns";

    protected override bool HasCustomBackground => true;
    public override string CustomBgm => "OblivionisBossBgm";

    public override float GetCameraScaling() => 0.85f;

    public override string BossNodePath =>
        "res://STS2_Tomorin_Mod/images/boss_icon/Oblvns_Boss_Icon";

    public override string? CustomRunHistoryIconPath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/oblvns_boss_headIcon.png";

    public override string? CustomRunHistoryIconOutlinePath =>
        "res://STS2_Tomorin_Mod/images/enemy_headIcon/oblvns_boss_headIcon.png";

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    //站位scene相关
    public override bool HasScene => true;
    public override string? CustomScenePath => "res://STS2_Tomorin_Mod/scenes/encounters/oblvns_boss.tscn";

    public override IReadOnlyList<string> Slots
    {
        get
        {
            return new List<string>()
            {
                MortisSlot,
                DolrisSlot,
                AmorisSlot,
                TmorisSlot,
                OblvnsSlot
            };
        }
    }

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Mortis>().ToMutable(), MortisSlot),
        (ModelDb.Monster<Amoris>().ToMutable(), AmorisSlot),
        (ModelDb.Monster<Doloris>().ToMutable(), DolrisSlot),
        (ModelDb.Monster<Timoris>().ToMutable(), TmorisSlot),
        (ModelDb.Monster<Oblivionis>().ToMutable(), OblvnsSlot),
        // (ModelDb.Monster<FullPowerOblivionis>().ToMutable(), _dolrisSlot),
    ];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Oblivionis>().ToMutable(),
        ModelDb.Monster<Doloris>().ToMutable(),
        ModelDb.Monster<Mortis>().ToMutable(),
        ModelDb.Monster<Timoris>().ToMutable(),
        ModelDb.Monster<Amoris>().ToMutable(),
        ModelDb.Monster<FullPowerOblivionis>().ToMutable()
    ];

    public override bool IsValidForAct(ActModel act)
    {
        return false;
    }
}