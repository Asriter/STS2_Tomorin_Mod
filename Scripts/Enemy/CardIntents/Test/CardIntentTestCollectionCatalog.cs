using MegaCrit.Sts2.Core.Entities.Cards;
using STS2_Tomorin_Mod.Cards.Collections;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.Test;

/// <summary>
/// 显式注册测试敌人允许生成、消费和回收的六种收藏品。
/// </summary>
public static class CardIntentTestCollectionCatalog
{
    /// <summary>获取满是划痕的笔记本定义标识。</summary>
    public const string BrokenNoteId = "STS2_TOMORIN_MOD:COLLECTION_BROKEN_NOTE";

    /// <summary>获取冰冷红茶定义标识。</summary>
    public const string ColdRedTeaId = "STS2_TOMORIN_MOD:COLLECTION_COLD_RED_TEA";

    /// <summary>获取压皱残页定义标识。</summary>
    public const string CrumpledPaperId = "STS2_TOMORIN_MOD:COLLECTION_CRUMPLED_PAPER";

    /// <summary>获取剩余自助餐定义标识。</summary>
    public const string LeftoverBuffetId = "STS2_TOMORIN_MOD:COLLECTION_LEFTOVER_BUFFET";

    /// <summary>获取深夜罐装咖啡定义标识。</summary>
    public const string MidnightCoffeeId = "STS2_TOMORIN_MOD:COLLECTION_MIDNIGHT_COFFEE";

    /// <summary>获取星石定义标识。</summary>
    public const string StarStoneId = "STS2_TOMORIN_MOD:COLLECTION_STAR_STONE";

    /// <summary>获取测试战斗唯一收藏品目录。</summary>
    public static EnemyCollectionCatalog Catalog { get; } = new(
    [
        Definition(BrokenNoteId, typeof(BrokenNote), false, "COLLECTION:BROKEN_NOTE"),
        Definition(ColdRedTeaId, typeof(ColdRedTea), false, "COLLECTION:COLD_RED_TEA"),
        Definition(CrumpledPaperId, typeof(CrumpledPaper), false, "COLLECTION:CRUMPLED_PAPER"),
        Definition(LeftoverBuffetId, typeof(LeftoverBuffet), false, "COLLECTION:LEFTOVER_BUFFET"),
        Definition(MidnightCoffeeId, typeof(MidnightCoffee), false, "COLLECTION:MIDNIGHT_COFFEE"),
        Definition(StarStoneId, typeof(StarStone), true, "COLLECTION:STAR_STONE")
    ]);

    /// <summary>
    /// 创建一项状态牌素材收藏品定义。
    /// </summary>
    /// <param name="id">稳定定义标识。</param>
    /// <param name="displayType">复用卡图和本地化的玩家牌类型。</param>
    /// <param name="isEpiphany">是否为作词通配素材。</param>
    /// <param name="effectProgramId">敌人适配效果程序标识。</param>
    /// <returns>不可变收藏品定义。</returns>
    private static EnemyCollectionDefinition Definition(
        string id,
        Type displayType,
        bool isEpiphany,
        string effectProgramId) =>
        new(id, displayType, CardType.Status, isEpiphany, effectProgramId);
}
