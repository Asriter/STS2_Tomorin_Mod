namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 表示收藏品权威队列中具有稳定单调序号的一个战斗实例。
/// </summary>
public sealed class EnemyCollectionInstance
{
    /// <summary>
    /// 创建收藏品战斗实例。
    /// </summary>
    /// <param name="definition">实例引用的不可变定义。</param>
    /// <param name="sequence">本场战斗内唯一且单调递增的序号。</param>
    public EnemyCollectionInstance(EnemyCollectionDefinition definition, long sequence)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "收藏品实例序号不能为负数。");
        }

        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Sequence = sequence;
        CollectionInstanceId = $"{definition.CollectionId}@{sequence}";
    }

    /// <summary>获取本实例引用的不可变定义。</summary>
    public EnemyCollectionDefinition Definition { get; }

    /// <summary>获取本场战斗内唯一且单调递增的序号。</summary>
    public long Sequence { get; }

    /// <summary>获取由定义标识和序号构造的稳定实例标识。</summary>
    public string CollectionInstanceId { get; }
}
