namespace STS2_Tomorin_Mod.Enemy.CardIntents.View;

/// <summary>
/// 把敌人卡牌的可选可信富文本描述转换为专用卡面能够直接应用的居中文本。
/// </summary>
public static class EnemyCardDescriptionPresenter
{
    /// <summary>
    /// 为非空描述增加统一居中包装；空描述返回空引用，表示保留原版卡牌描述。
    /// </summary>
    /// <param name="descriptionOverride">敌人卡牌定义提供的可信富文本描述。</param>
    /// <returns>需要写入描述标签的居中文本，或表示不覆写的空引用。</returns>
    public static string? BuildOverrideText(string descriptionOverride)
    {
        ArgumentNullException.ThrowIfNull(descriptionOverride);
        return descriptionOverride.Length == 0
            ? null
            : $"[center]{descriptionOverride}[/center]";
    }
}
