namespace STS2_Tomorin_Mod.Enemy.CardIntents.Test;

/// <summary>
/// 定义并幂等注册卡牌 Intent 开发测试所用的固定牌组模板。
/// </summary>
public static class CardIntentTestDeck
{
    private static readonly object RegistrationSync = new();

    /// <summary>
    /// 获取测试牌组的稳定标识。
    /// </summary>
    public static EnemyCardDeckId DeckId { get; } =
        new("STS2_TOMORIN_MOD:CARD_INTENT_TOMORIN_TEST");

    /// <summary>
    /// 获取测试状态一次冻结手牌所需的容量。
    /// </summary>
    public static int HandCapacity => CardIntentTestRules.Default.Recipes.Values.Max(recipe => recipe.Slots.Count);

    /// <summary>
    /// 在第一次显式使用测试怪物前注册牌组；后续调用不改变注册表。
    /// </summary>
    public static void EnsureRegistered()
    {
        lock (RegistrationSync)
        {
            if (EnemyCardDeckRegistry.IsRegistered(DeckId))
            {
                return;
            }

            EnemyCardDeckRegistry.Register(DeckId, CardIntentTestCardCatalog.CreateInitialDeckFactories());
        }
    }
}
