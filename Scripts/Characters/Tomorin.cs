using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.CardPools;
using STS2_Tomorin_Mod.Cards;
using STS2_Tomorin_Mod.Commands;
using STS2_Tomorin_Mod.PotionPools;
using STS2_Tomorin_Mod.RelicPools;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Characters;

public sealed class Tomorin : CustomCharacterModel
{
    public static readonly Color DefaultColor = new Color("77BBDD");
    public static readonly Color OutlineColor = new Color("779ce4");

    public const string energyColorName = "tomorin"; // 能量显示的颜色名称

    public override CharacterGender Gender => CharacterGender.Feminine; // 性别

    protected override CharacterModel? UnlocksAfterRunAs => null;

    public override Color NameColor => new Color("#C546EC"); // 名称显示的颜色

    public override int StartingHp => 75; // 角色初始生命值

    public override int StartingGold => 99; // 角色初始拥有的金钱

    public override CardPoolModel CardPool => ModelDb.CardPool<TomorinCardPool>();

    public override PotionPoolModel PotionPool => ModelDb.PotionPool<TomorinPotionPool>();

    public override RelicPoolModel RelicPool => ModelDb.RelicPool<TomorinRelicPool>();

    public override List<CardModel> StartingDeck =>
    [
        ModelDb.Card<StrikeTomorin>(),
        ModelDb.Card<StrikeTomorin>(),
        ModelDb.Card<StrikeTomorin>(),
        ModelDb.Card<StrikeTomorin>(),
        ModelDb.Card<StrikeTomorin>(),
        ModelDb.Card<DefendTomorin>(),
        // ModelDb.Card<DefendTomorin>(),
        ModelDb.Card<DefendTomorin>(),
        ModelDb.Card<DefendTomorin>(),
        ModelDb.Card<DefendTomorin>(),
        ModelDb.Card<Hitoshizuku>()
    ]; // 初始卡组

    public override List<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<NormalPencil>()
    ];

    // 角色播放攻击动作后，到真正出手/命中前的延迟时间
    public override float AttackAnimDelay => 0.15f;

    // 角色播放施法动作后，到效果实际触发前的延迟时间
    public override float CastAnimDelay => 0.25f;

    // 卡牌左上角费用数字的描边颜色
    public override Color EnergyLabelOutlineColor => OutlineColor;

    // 角色相关对白、气泡、事件发言等文本的颜色
    public override Color DialogueColor => DefaultColor;

    // 地图上该角色绘制连线时使用的颜色
    public override Color MapDrawingColor => DefaultColor;

    // 联机状态下，这个角色的指向线主体颜色
    public override Color RemoteTargetingLineColor => DefaultColor;

    // 联机指向线的外描边颜色
    public override Color RemoteTargetingLineOutline => OutlineColor;

    public override List<string> GetArchitectAttackVfx()
    {
        return new List<string>();
    }

    //每回合开始时初始化compose次数
    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            ComposeCmd.ComposeCostCardDict.Clear();
        }

        return Task.CompletedTask;
    }


    //TODO 临时方案 用猎人的语音
    public override string CharacterSelectSfx =>
        ModelDb.Character<Silent>().CharacterSelectSfx;

    public override string CharacterTransitionSfx =>
        "event:/sfx/ui/wipe_silent";

    #region Path相关

    public override string? CustomVisualPath => $"res://{MainFile.ModId}/scenes/creature_visuals/tomorin.tscn";

    //TODO 拖尾逻辑暂时没有patch后的直接赋值的方法，所以先用原版机器人的（反正都是蓝色系
    // public override string? CustomTrailPath => "res://STS2_Tomorin_Mod/scenes/vfx/card_trail_tomorin.tscn";SceneHelper.GetScenePath("vfx/card_trail_" + this.Id.Entry.ToLowerInvariant());
    public override string? CustomTrailPath => SceneHelper.GetScenePath("vfx/card_trail_defect");

    public override string? CustomIconPath => "res://STS2_Tomorin_Mod/scenes/ui/character_icons/tomorin_icon.tscn";

    public override string? CustomIconTexturePath =>
        "res://STS2_Tomorin_Mod/images/ui/top_panel/character_icon_tomorin.png";

    public override string? CustomEnergyCounterPath =>
        "res://STS2_Tomorin_Mod/scenes/combat/energy_counters/tomorin_energy_counter.tscn";

    public override string? CustomRestSiteAnimPath =>
        "res://STS2_Tomorin_Mod/scenes/rest_site/characters/tomorin_rest_site.tscn";

    public override string? CustomMerchantAnimPath =>
        "res://STS2_Tomorin_Mod/scenes/merchant/characters/tomorin_merchant.tscn";

    //四条联机手臂
    public override string? CustomArmPointingTexturePath =>
        "res://STS2_Tomorin_Mod/images/hands/multiplayer_hand_tomorin_point.png";

    public override string? CustomArmRockTexturePath =>
        "res://STS2_Tomorin_Mod/images/hands/multiplayer_hand_tomorin_rock.png";

    public override string? CustomArmPaperTexturePath =>
        "res://STS2_Tomorin_Mod/images/hands/multiplayer_hand_tomorin_paper.png";

    public override string? CustomArmScissorsTexturePath =>
        "res://STS2_Tomorin_Mod/images/hands/multiplayer_hand_tomorin_scissors.png";

    public override string? CustomCharacterSelectBg =>
        "res://STS2_Tomorin_Mod/scenes/screens/char_select/char_select_bg_tomorin.tscn";

    public override string? CustomCharacterSelectIconPath =>
        "res://STS2_Tomorin_Mod/images/charui/char_select_tomorin.png";

    public override string CustomCharacterSelectLockedIconPath =>
        "res://STS2_Tomorin_Mod/images/charui/char_select_tomorin_locked.png";

    public override string? CustomCharacterSelectTransitionPath =>
        "res://STS2_Tomorin_Mod/materials/transitions/tomorin_transition_mat.tres";

    public override string CustomMapMarkerPath =>
        "res://STS2_Tomorin_Mod/images/packed/map/icons/map_marker_tomorin.png";

    //TODO 各种特效，暂时不作处理

    #endregion
}