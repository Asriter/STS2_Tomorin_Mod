namespace STS2_Tomorin_Mod.Enemy.CardIntents.Test;

/// <summary>
/// 显式注册测试敌人允许生成、消费和回收的六种收藏品。
/// </summary>
public static class CardIntentTestCollectionCatalog
{
    /// <summary>获取满是划痕的笔记本定义标识。</summary>
    public const string BrokenNoteId = TomorinEnemyCollectionCatalogFactory.BrokenNoteId;

    /// <summary>获取冰冷红茶定义标识。</summary>
    public const string ColdRedTeaId = TomorinEnemyCollectionCatalogFactory.ColdRedTeaId;

    /// <summary>获取压皱残页定义标识。</summary>
    public const string CrumpledPaperId = TomorinEnemyCollectionCatalogFactory.CrumpledPaperId;

    /// <summary>获取剩余自助餐定义标识。</summary>
    public const string LeftoverBuffetId = TomorinEnemyCollectionCatalogFactory.LeftoverBuffetId;

    /// <summary>获取深夜罐装咖啡定义标识。</summary>
    public const string MidnightCoffeeId = TomorinEnemyCollectionCatalogFactory.MidnightCoffeeId;

    /// <summary>获取星石定义标识。</summary>
    public const string StarStoneId = TomorinEnemyCollectionCatalogFactory.StarStoneId;

    /// <summary>获取测试战斗唯一收藏品目录。</summary>
    public static EnemyCollectionCatalog Catalog { get; } =
        TomorinEnemyCollectionCatalogFactory.Create();
}
