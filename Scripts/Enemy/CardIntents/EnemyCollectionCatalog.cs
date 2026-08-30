using System.Collections.ObjectModel;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存显式注册且顺序稳定的敌人收藏品定义目录。
/// </summary>
public sealed class EnemyCollectionCatalog
{
    private readonly IReadOnlyDictionary<string, EnemyCollectionDefinition> _definitionsById;

    /// <summary>
    /// 创建仅包含调用方显式给出的收藏品目录。
    /// </summary>
    /// <param name="definitions">按注册顺序排列的定义。</param>
    public EnemyCollectionCatalog(IEnumerable<EnemyCollectionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        EnemyCollectionDefinition[] ordered = definitions.ToArray();
        if (ordered.Any(definition => definition is null))
        {
            throw new ArgumentException("收藏品目录不能包含空定义。", nameof(definitions));
        }

        Dictionary<string, EnemyCollectionDefinition> byId = new(StringComparer.Ordinal);
        foreach (EnemyCollectionDefinition definition in ordered)
        {
            if (!byId.TryAdd(definition.CollectionId, definition))
            {
                throw new ArgumentException(
                    $"收藏品目录包含重复定义标识 {definition.CollectionId}。",
                    nameof(definitions));
            }
        }

        Definitions = Array.AsReadOnly(ordered);
        _definitionsById = new ReadOnlyDictionary<string, EnemyCollectionDefinition>(byId);
    }

    /// <summary>获取按显式注册顺序排列的全部定义。</summary>
    public IReadOnlyList<EnemyCollectionDefinition> Definitions { get; }

    /// <summary>
    /// 尝试按稳定标识取得收藏品定义。
    /// </summary>
    /// <param name="collectionId">待查找的定义标识。</param>
    /// <param name="definition">成功时返回已注册定义。</param>
    /// <returns>目录包含该标识时为 <see langword="true"/>。</returns>
    public bool TryGet(string collectionId, out EnemyCollectionDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            definition = null;
            return false;
        }

        return _definitionsById.TryGetValue(collectionId, out definition);
    }

    /// <summary>
    /// 按稳定标识取得必定存在的收藏品定义。
    /// </summary>
    /// <param name="collectionId">待查找的定义标识。</param>
    /// <returns>已注册定义。</returns>
    /// <exception cref="KeyNotFoundException">目录不包含该标识。</exception>
    public EnemyCollectionDefinition GetRequired(string collectionId)
    {
        if (!TryGet(collectionId, out EnemyCollectionDefinition? definition))
        {
            throw new KeyNotFoundException($"收藏品目录未注册定义 {collectionId}。");
        }

        return definition!;
    }
}
