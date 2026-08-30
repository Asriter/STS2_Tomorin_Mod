namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 表示模板副本或战斗生成副本在全部牌区和同步数据中的唯一身份。
/// </summary>
public sealed record EnemyCardInstanceKey : IComparable<EnemyCardInstanceKey>
{
    /// <summary>
    /// 从规范化身份字符串创建实例键。
    /// </summary>
    /// <param name="value">非空且区分大小写的实例身份。</param>
    public EnemyCardInstanceKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("敌人卡牌实例键不能为空。", nameof(value));
        }

        Value = value;
    }

    /// <summary>获取规范化实例身份字符串。</summary>
    public string Value { get; }

    /// <summary>
    /// 为初始模板槽位创建实例键。
    /// </summary>
    /// <param name="templateSlot">从零开始的模板槽位。</param>
    /// <returns>带模板命名空间的稳定实例键。</returns>
    public static EnemyCardInstanceKey FromTemplateSlot(int templateSlot)
    {
        if (templateSlot < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(templateSlot), "模板槽位不能为负数。");
        }

        return new EnemyCardInstanceKey($"TEMPLATE:{templateSlot}");
    }

    /// <summary>
    /// 为战斗生成序号创建实例键。
    /// </summary>
    /// <param name="runtimeInstanceId">从零开始的战斗内单调序号。</param>
    /// <returns>带运行时命名空间的稳定实例键。</returns>
    public static EnemyCardInstanceKey FromRuntimeInstanceId(long runtimeInstanceId)
    {
        if (runtimeInstanceId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runtimeInstanceId), "运行时实例序号不能为负数。");
        }

        return new EnemyCardInstanceKey($"RUNTIME:{runtimeInstanceId}");
    }

    /// <summary>
    /// 按规范字符串比较实例键，供确定性排序使用。
    /// </summary>
    /// <param name="other">另一实例键。</param>
    /// <returns>序号比较结果。</returns>
    public int CompareTo(EnemyCardInstanceKey? other) =>
        other is null ? 1 : string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// 返回规范化身份字符串。
    /// </summary>
    /// <returns>实例身份字符串。</returns>
    public override string ToString() => Value;
}
