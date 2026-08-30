using System.Collections.ObjectModel;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 为一个稳定 DeckId 冻结全部阶段、敌人卡牌定义工厂和收藏品目录。
/// </summary>
public sealed class EnemyCardContentDirectory
{
    private readonly IReadOnlyDictionary<EnemyCardPhase, EnemyCardPhaseTemplate> _phases;

    /// <summary>
    /// 创建一个与牌组标识绑定的完整不可变内容目录。
    /// </summary>
    public EnemyCardContentDirectory(
        EnemyCardDeckId deckId,
        EnemyCardPhase initialPhase,
        IEnumerable<EnemyCardPhaseTemplate> phases,
        IReadOnlyDictionary<EnemyCardId, Func<BaseEnemyCard>> definitionFactories,
        EnemyCollectionCatalog collectionCatalog)
    {
        if (!deckId.IsValid)
        {
            throw new ArgumentException("内容目录必须绑定有效牌组标识。", nameof(deckId));
        }

        if (!Enum.IsDefined(initialPhase))
        {
            throw new ArgumentOutOfRangeException(nameof(initialPhase), initialPhase, "未知初始阶段。");
        }

        ArgumentNullException.ThrowIfNull(phases);
        EnemyCardPhaseTemplate[] copiedPhases = phases.ToArray();
        if (copiedPhases.Length == 0 || copiedPhases.Any(phase => phase is null))
        {
            throw new ArgumentException("内容目录必须包含至少一个阶段模板。", nameof(phases));
        }

        Dictionary<EnemyCardPhase, EnemyCardPhaseTemplate> phasesById = [];
        foreach (EnemyCardPhaseTemplate phase in copiedPhases)
        {
            if (!phasesById.TryAdd(phase.Phase, phase))
            {
                throw new ArgumentException($"内容目录包含重复阶段 {phase.Phase}。", nameof(phases));
            }
        }

        if (!phasesById.ContainsKey(initialPhase))
        {
            throw new ArgumentException($"内容目录未注册初始阶段 {initialPhase}。", nameof(initialPhase));
        }

        ArgumentNullException.ThrowIfNull(definitionFactories);
        if (definitionFactories.Count == 0)
        {
            throw new ArgumentException("内容目录必须显式注册至少一个卡牌定义工厂。", nameof(definitionFactories));
        }

        Dictionary<EnemyCardId, Func<BaseEnemyCard>> copiedDefinitions = [];
        foreach ((EnemyCardId cardId, Func<BaseEnemyCard> factory) in definitionFactories)
        {
            if (!cardId.IsValid || factory is null)
            {
                throw new ArgumentException("定义目录不能包含无效标识或空工厂。", nameof(definitionFactories));
            }

            copiedDefinitions.Add(cardId, factory);
        }

        DeckId = deckId;
        InitialPhase = initialPhase;
        _phases = new ReadOnlyDictionary<EnemyCardPhase, EnemyCardPhaseTemplate>(phasesById);
        DefinitionFactories = new ReadOnlyDictionary<EnemyCardId, Func<BaseEnemyCard>>(copiedDefinitions);
        CollectionCatalog = collectionCatalog ?? throw new ArgumentNullException(nameof(collectionCatalog));
    }

    /// <summary>获取目录绑定的稳定牌组标识。</summary>
    public EnemyCardDeckId DeckId { get; }

    /// <summary>获取新战斗使用的初始阶段。</summary>
    public EnemyCardPhase InitialPhase { get; }

    /// <summary>获取初始来源与所有生成链共享的完整定义工厂。</summary>
    public IReadOnlyDictionary<EnemyCardId, Func<BaseEnemyCard>> DefinitionFactories { get; }

    /// <summary>获取与本牌组绑定的收藏品定义目录。</summary>
    public EnemyCollectionCatalog CollectionCatalog { get; }

    /// <summary>
    /// 取得指定阶段的冻结模板。
    /// </summary>
    public EnemyCardPhaseTemplate GetPhase(EnemyCardPhase phase) =>
        _phases.TryGetValue(phase, out EnemyCardPhaseTemplate? template)
            ? template
            : throw new KeyNotFoundException($"牌组 {DeckId} 未注册阶段 {phase}。");

    /// <summary>
    /// 从完整定义目录创建一个尚未绑定战斗身份的新领域实例。
    /// </summary>
    public BaseEnemyCard CreateDefinition(EnemyCardId cardId)
    {
        if (!cardId.IsValid || !DefinitionFactories.TryGetValue(cardId, out Func<BaseEnemyCard>? factory))
        {
            throw new KeyNotFoundException($"牌组 {DeckId} 未注册卡牌定义 {cardId}。");
        }

        BaseEnemyCard card = factory() ?? throw new InvalidOperationException(
            $"牌组 {DeckId} 的定义工厂 {cardId} 返回了空对象。");
        if (card.CardId != cardId || card.TemplateSlot is not null || card.RuntimeInstanceId is not null)
        {
            throw new InvalidOperationException(
                $"牌组 {DeckId} 的定义工厂 {cardId} 改变了标识或返回已绑定实例。");
        }

        return card;
    }

    /// <summary>以稳定阶段顺序供注册表验证全部模板。</summary>
    internal IReadOnlyList<EnemyCardPhaseTemplate> OrderedPhases =>
        _phases.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
}
