namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存一个行动指标从左到右的固定槽位配方；空 Tag 表示随机槽位。
/// </summary>
public sealed class EnemyActionRecipe
{
    /// <summary>
    /// 创建不可变行动配方。
    /// </summary>
    /// <param name="metric">配方所属指标。</param>
    /// <param name="slots">从左到右的标签槽位；空值为随机槽位。</param>
    public EnemyActionRecipe(EnemyActionMetric metric, IEnumerable<EnemyCardTag?> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        EnemyCardTag?[] copied = slots.ToArray();
        if (copied.Length == 0)
        {
            throw new ArgumentException("行动指标配方必须至少包含一个槽位。", nameof(slots));
        }

        if (copied.Any(tag => tag == EnemyCardTag.None || (tag is not null && !IsSingleTag(tag.Value))))
        {
            throw new ArgumentException("指定标签槽位必须恰好包含一个非 None 标签；随机槽位请使用空值。", nameof(slots));
        }

        Metric = metric;
        Slots = Array.AsReadOnly(copied);
    }

    /// <summary>获取配方所属行动指标。</summary>
    public EnemyActionMetric Metric { get; }

    /// <summary>获取从左到右且不可修改的槽位集合。</summary>
    public IReadOnlyList<EnemyCardTag?> Slots { get; }

    /// <summary>
    /// 判断标签值是否只包含单个已定义位。
    /// </summary>
    /// <param name="tag">待检查标签。</param>
    /// <returns>恰好一个位时为真。</returns>
    private static bool IsSingleTag(EnemyCardTag tag)
    {
        int value = (int)tag;
        return value > 0 && (value & (value - 1)) == 0 && Enum.IsDefined(tag);
    }
}
