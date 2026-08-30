namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>影灯正式收藏品目录与确定性加权表。</summary>
public static class ShadowTomorinCollectionCatalog
{
    public const string BrokenNoteId = TomorinEnemyCollectionCatalogFactory.BrokenNoteId;
    public const string ColdRedTeaId = TomorinEnemyCollectionCatalogFactory.ColdRedTeaId;
    public const string CrumpledPaperId = TomorinEnemyCollectionCatalogFactory.CrumpledPaperId;
    public const string LeftoverBuffetId = TomorinEnemyCollectionCatalogFactory.LeftoverBuffetId;
    public const string MidnightCoffeeId = TomorinEnemyCollectionCatalogFactory.MidnightCoffeeId;
    public const string StarStoneId = TomorinEnemyCollectionCatalogFactory.StarStoneId;

    public static EnemyCollectionCatalog Catalog { get; } =
        TomorinEnemyCollectionCatalogFactory.Create();

    public static IReadOnlyList<(EnemyCollectionDefinition Definition, int Weight)> WeightedDefinitions { get; } =
        Array.AsReadOnly(new (EnemyCollectionDefinition, int)[]
        {
            (Catalog.GetRequired(BrokenNoteId), ShadowTomorinBalance.BrokenNoteWeight),
            (Catalog.GetRequired(CrumpledPaperId), ShadowTomorinBalance.CrumpledPaperWeight),
            (Catalog.GetRequired(MidnightCoffeeId), ShadowTomorinBalance.MidnightCoffeeWeight),
            (Catalog.GetRequired(ColdRedTeaId), ShadowTomorinBalance.ColdRedTeaWeight),
            (Catalog.GetRequired(LeftoverBuffetId), ShadowTomorinBalance.LeftoverBuffetWeight),
            (Catalog.GetRequired(StarStoneId), ShadowTomorinBalance.StarStoneWeight)
        });
}
