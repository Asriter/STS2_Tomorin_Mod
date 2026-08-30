namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>注册影灯三阶段完整目录，并保证同进程内幂等。</summary>
public static class ShadowTomorinDeck
{
    private static readonly object Sync = new();

    public static EnemyCardDeckId DeckId { get; } =
        new("STS2_TOMORIN_MOD:SHADOW_TOMORIN");

    public static void EnsureRegistered()
    {
        if (EnemyCardDeckRegistry.IsRegistered(DeckId))
        {
            return;
        }

        lock (Sync)
        {
            if (EnemyCardDeckRegistry.IsRegistered(DeckId))
            {
                return;
            }

            Dictionary<EnemyCardId, Func<BaseEnemyCard>> definitionFactories =
                ShadowTomorinCardCatalog.AllDefinitions.Keys.ToDictionary(
                    id => id,
                    id => (Func<BaseEnemyCard>)(() => ShadowTomorinCardCatalog.Create(id)));
            EnemyCardPhaseTemplate[] phases =
            [
                CreatePhase(EnemyCardPhase.Phase1),
                CreatePhase(EnemyCardPhase.Phase2),
                CreatePhase(EnemyCardPhase.Phase3)
            ];
            EnemyCardDeckRegistry.Register(new EnemyCardContentDirectory(
                DeckId,
                EnemyCardPhase.Phase1,
                phases,
                definitionFactories,
                ShadowTomorinCollectionCatalog.Catalog));
        }
    }

    private static EnemyCardPhaseTemplate CreatePhase(EnemyCardPhase phase)
    {
        Func<BaseEnemyCard>[] factories = ShadowTomorinCardCatalog.GetPhaseDefinitions(phase)
            .Select(definition => (Func<BaseEnemyCard>)(() => ShadowTomorinCardCatalog.Create(definition.CardId)))
            .ToArray();
        return new EnemyCardPhaseTemplate(
            phase,
            factories,
            ShadowTomorinRules.ForPhase(phase),
            factories.Length);
    }
}
