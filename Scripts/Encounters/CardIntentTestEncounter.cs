using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Tomorin_Mod.Enemy.CardIntents.Test;

namespace STS2_Tomorin_Mod.Encounters;

/// <summary>
/// 提供只可按稳定模型 ID 显式启动的卡牌 Intent 开发测试战斗。
/// </summary>
/// <remarks>
/// 开发入口：完成 ModelDb 初始化后，以 <c>ModelDb.Encounter&lt;CardIntentTestEncounter&gt;().Id</c>
/// 取得稳定 ID，并将该 Encounter 的可变副本交给开发控制台或测试房间启动器。
/// 本类型不会被任何 Act、地图或随机遭遇池引用。
/// </remarks>
public sealed class CardIntentTestEncounter : CustomEncounterModel
{
    /// <summary>
    /// 获取供开发控制台或测试房间启动器使用的稳定 Encounter 模型 ID。
    /// </summary>
    public static ModelId ExplicitEncounterId =>
        ModelDb.Encounter<CardIntentTestEncounter>().Id;

    /// <summary>
    /// 创建自动注册到 ModelDb、但不进入任何正常 Act 的测试 Encounter。
    /// </summary>
    public CardIntentTestEncounter() : base(RoomType.Elite, true)
    {
    }

    /// <summary>
    /// 返回测试战斗可能生成的唯一敌人原型。
    /// </summary>
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<CardIntentTestMonster>()
    ];

    /// <summary>
    /// 生成卡牌 Intent 测试敌人的独立可变实例。
    /// </summary>
    /// <returns>仅包含测试敌人的生成列表。</returns>
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<CardIntentTestMonster>().ToMutable(), null)
    ];

    /// <summary>
    /// 明确拒绝加入任何 Act 的正常遭遇候选集合。
    /// </summary>
    /// <param name="act">任意待检查的 Act。</param>
    /// <returns>始终返回 false。</returns>
    public override bool IsValidForAct(ActModel act)
    {
        return false;
    }
}
