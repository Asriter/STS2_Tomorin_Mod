namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存稳定牌组标识到新敌人卡牌实例工厂的进程级注册关系。
/// </summary>
public static class EnemyCardDeckRegistry
{
    private static readonly object Sync = new();
    private static readonly Dictionary<EnemyCardDeckId, DeckDefinition> Definitions = new();
    private static readonly EnemyCardPlanningRules LegacyDirectoryRules = new(
        new EnemySoftLockLimits(decimal.MaxValue, decimal.MaxValue),
        new EnemySoftLockLimits(decimal.MaxValue, decimal.MaxValue),
        maxCandidateAttempts: 1,
        stepLimit: 64,
        [new EnemyWeightedActionRecipe(
            new EnemyActionRecipe(EnemyActionMetric.Gain, [null]),
            weight: 1)]);

    /// <summary>
    /// 注册一副敌人牌组，并立即验证工厂的非空、实例独立性、稳定身份与显示资源。
    /// </summary>
    /// <param name="deckId">牌组稳定标识。</param>
    /// <param name="cardFactories">按模板顺序排列、每次都必须返回新实例的卡牌工厂。</param>
    /// <exception cref="InvalidOperationException">牌组重复或工厂定义不稳定时抛出。</exception>
    public static void Register(
        EnemyCardDeckId deckId,
        IEnumerable<Func<BaseEnemyCard>> cardFactories)
    {
        if (!deckId.IsValid)
        {
            throw new ArgumentException("敌人牌组必须具有有效稳定标识。", nameof(deckId));
        }

        ArgumentNullException.ThrowIfNull(cardFactories);
        Func<BaseEnemyCard>[] factories = cardFactories.ToArray();
        if (factories.Length == 0 || factories.Any(factory => factory is null))
        {
            throw new ArgumentException("敌人牌组必须包含至少一个非空卡牌工厂。", nameof(cardFactories));
        }

        lock (Sync)
        {
            if (Definitions.ContainsKey(deckId))
            {
                throw new InvalidOperationException($"敌人牌组 {deckId} 已经注册，DeckId 必须唯一。");
            }
        }

        List<BaseEnemyCard> firstProbe = InstantiateAndValidate(deckId, factories, expectedDefinitions: null);
        CardDefinitionFingerprint[] templateDefinitions = firstProbe
            .Select(CardDefinitionFingerprint.FromCard)
            .ToArray();
        List<BaseEnemyCard> secondProbe = InstantiateAndValidate(deckId, factories, templateDefinitions);
        HashSet<BaseEnemyCard> firstReferences = new(firstProbe, ReferenceEqualityComparer.Instance);
        if (secondProbe.Any(firstReferences.Contains))
        {
            throw new InvalidOperationException(
                $"敌人牌组 {deckId} 的工厂跨调用复用了卡牌对象；每个工厂每次必须创建新实例。");
        }

        Dictionary<EnemyCardId, Func<BaseEnemyCard>> definitionFactories = [];
        for (int index = 0; index < firstProbe.Count; index++)
        {
            definitionFactories.TryAdd(firstProbe[index].CardId, factories[index]);
        }

        EnemyCardContentDirectory directory = new(
            deckId,
            EnemyCardPhase.None,
            [new EnemyCardPhaseTemplate(
                EnemyCardPhase.None,
                factories,
                LegacyDirectoryRules,
                factories.Length)],
            definitionFactories,
            new EnemyCollectionCatalog([]));
        Register(directory);
    }

    /// <summary>
    /// 注册一副含完整阶段、生成链定义与收藏品的不可变内容目录。
    /// </summary>
    public static void Register(EnemyCardContentDirectory contentDirectory)
    {
        ArgumentNullException.ThrowIfNull(contentDirectory);
        EnemyCardDeckId deckId = contentDirectory.DeckId;
        lock (Sync)
        {
            if (Definitions.ContainsKey(deckId))
            {
                throw new InvalidOperationException($"敌人牌组 {deckId} 已经注册，DeckId 必须唯一。");
            }
        }

        DeckDefinition definition = BuildDeckDefinition(contentDirectory);
        lock (Sync)
        {
            if (!Definitions.TryAdd(deckId, definition))
            {
                throw new InvalidOperationException($"敌人牌组 {deckId} 已经注册，DeckId 必须唯一。");
            }
        }
    }

    /// <summary>
    /// 获取牌组是否已经完成无半成品的注册。
    /// </summary>
    /// <param name="deckId">待查询牌组标识。</param>
    /// <returns>存在完整注册定义时为 <see langword="true"/>。</returns>
    public static bool IsRegistered(EnemyCardDeckId deckId)
    {
        lock (Sync)
        {
            return Definitions.ContainsKey(deckId);
        }
    }

    /// <summary>
    /// 创建一副全部为新对象的运行时牌组，并校验其容量满足状态配置。
    /// </summary>
    /// <param name="deckId">已注册的牌组标识。</param>
    /// <param name="minimumCardCount">调用状态要求的最小牌组容量。</param>
    /// <returns>保持模板顺序的新卡牌实例列表。</returns>
    public static List<BaseEnemyCard> CreateDeck(EnemyCardDeckId deckId, int minimumCardCount = 1)
    {
        if (minimumCardCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumCardCount), "最小牌组容量必须大于零。");
        }

        DeckDefinition definition = GetDefinition(deckId);
        List<BaseEnemyCard> cards = CreatePhaseDeck(deckId, definition.ContentDirectory.InitialPhase);
        if (cards.Count < minimumCardCount)
        {
            throw new InvalidOperationException(
                $"敌人牌组 {deckId} 的容量 {cards.Count} 小于状态要求的 {minimumCardCount}。");
        }

        return cards;
    }

    /// <summary>
    /// 从指定阶段模板创建全部新来源实例，并绑定跨阶段唯一模板槽位。
    /// </summary>
    public static List<BaseEnemyCard> CreatePhaseDeck(EnemyCardDeckId deckId, EnemyCardPhase phase)
    {
        DeckDefinition definition = GetDefinition(deckId);
        PhaseDefinition phaseDefinition = definition.GetPhase(phase);
        List<BaseEnemyCard> cards = InstantiateAndValidate(
            deckId,
            phaseDefinition.Factories,
            phaseDefinition.TemplateDefinitions);
        for (int index = 0; index < cards.Count; index++)
        {
            cards[index].AssignTemplateSlot(checked(phaseDefinition.TemplateSlotOffset + index));
            cards[index].AssignSourcePhase(phase);
        }

        return cards;
    }

    /// <summary>
    /// 从已注册不可变模板创建具有五牌区唯一所有权的新战斗状态。
    /// </summary>
    /// <param name="deckId">已注册牌组稳定标识。</param>
    /// <returns>全部初始牌位于抽牌堆且实例身份唯一的新状态。</returns>
    public static EnemyCardCombatState CreateCombatState(EnemyCardDeckId deckId)
    {
        EnemyCardContentDirectory directory = GetContentDirectory(deckId);
        return new EnemyCardCombatState(
            deckId,
            CreatePhaseDeck(deckId, directory.InitialPhase),
            directory.InitialPhase);
    }

    /// <summary>
    /// 获取牌组模板中保留重复副本与顺序的稳定卡牌标识。
    /// </summary>
    /// <param name="deckId">已注册牌组标识。</param>
    /// <returns>不可修改的模板卡牌标识视图。</returns>
    public static IReadOnlyList<EnemyCardId> GetTemplateCardIds(EnemyCardDeckId deckId) =>
        GetDefinition(deckId).GetPhase(GetContentDirectory(deckId).InitialPhase).TemplateCardIds;

    /// <summary>获取已注册牌组的完整阶段内容目录。</summary>
    public static EnemyCardContentDirectory GetContentDirectory(EnemyCardDeckId deckId) =>
        GetDefinition(deckId).ContentDirectory;

    /// <summary>从已注册完整目录创建未绑定身份的新定义实例，并持续校验注册时的规范语义。</summary>
    public static BaseEnemyCard ResolveDefinition(EnemyCardDeckId deckId, EnemyCardId cardId)
    {
        DeckDefinition definition = GetDefinition(deckId);
        CardDefinitionFingerprint canonical = definition.GetCanonicalDefinition(cardId);
        BaseEnemyCard first = definition.ContentDirectory.CreateDefinition(cardId);
        BaseEnemyCard second = definition.ContentDirectory.CreateDefinition(cardId);
        if (ReferenceEquals(first, second))
        {
            throw new InvalidOperationException(
                $"敌人牌组 {deckId} 的定义工厂 {cardId} 在解析时复用了卡牌对象。");
        }

        CardDefinitionFingerprint firstFingerprint = CardDefinitionFingerprint.FromCard(first);
        if (!firstFingerprint.Matches(second))
        {
            throw new InvalidOperationException(
                $"敌人牌组 {deckId} 的定义工厂 {cardId} 在同次解析的双探针间改变了完整语义。");
        }

        if (!canonical.Matches(first) || !canonical.Matches(second))
        {
            throw new InvalidOperationException(
                $"敌人牌组 {deckId} 的定义工厂 {cardId} 在注册后偏离了规范语义。");
        }

        return first;
    }

    /// <summary>获取已注册牌组的收藏品目录。</summary>
    public static EnemyCollectionCatalog GetCollectionCatalog(EnemyCardDeckId deckId) =>
        GetContentDirectory(deckId).CollectionCatalog;

    /// <summary>
    /// 获取注册校验阶段缓存并去重后的 CardModel 显示资源路径。
    /// </summary>
    /// <param name="deckId">已注册牌组标识。</param>
    /// <returns>不在 Intent 首次显示时临时实例化卡牌的只读资源路径。</returns>
    public static IReadOnlyList<string> GetAssetPaths(EnemyCardDeckId deckId) =>
        GetDefinition(deckId).AssetPaths;

    /// <summary>枚举任意已知展示 CardModel 的牌面资源，供 Token 与收藏品预加载复用。</summary>
    internal static IEnumerable<string> GetCardModelAssetPaths(MegaCrit.Sts2.Core.Models.CardModel cardModel) =>
        EnumerateCardModelAssetPaths(cardModel);

    /// <summary>
    /// 查询指定牌组模板是否能够解析给定稳定卡牌标识。
    /// </summary>
    /// <param name="deckId">已注册牌组标识。</param>
    /// <param name="cardId">待解析卡牌标识。</param>
    /// <returns>模板中至少存在一个同标识副本时为 <see langword="true"/>。</returns>
    public static bool CanResolveCardId(EnemyCardDeckId deckId, EnemyCardId cardId) =>
        cardId.IsValid && GetContentDirectory(deckId).DefinitionFactories.ContainsKey(cardId);

    /// <summary>
    /// 全量验证完整目录的定义工厂、阶段工厂与预加载资源。
    /// </summary>
    private static DeckDefinition BuildDeckDefinition(EnemyCardContentDirectory directory)
    {
        EnemyCardDeckId deckId = directory.DeckId;
        Dictionary<EnemyCardId, CardDefinitionFingerprint> canonicalDefinitions = [];
        List<BaseEnemyCard> definitionProbes = [];
        foreach (EnemyCardId cardId in directory.DefinitionFactories.Keys)
        {
            BaseEnemyCard first = directory.CreateDefinition(cardId);
            BaseEnemyCard second = directory.CreateDefinition(cardId);
            if (ReferenceEquals(first, second))
            {
                throw new InvalidOperationException(
                    $"敌人牌组 {deckId} 的定义工厂 {cardId} 跨调用复用了卡牌对象。");
            }

            CardDefinitionFingerprint fingerprint = CardDefinitionFingerprint.FromCard(first);
            if (!fingerprint.Matches(second))
            {
                throw new InvalidOperationException(
                    $"敌人牌组 {deckId} 的定义工厂 {cardId} 跨调用改变了完整语义。");
            }

            canonicalDefinitions.Add(cardId, fingerprint);
            definitionProbes.Add(first);
        }

        Dictionary<EnemyCardPhase, PhaseDefinition> phases = [];
        int templateSlotOffset = 0;
        foreach (EnemyCardPhaseTemplate phase in directory.OrderedPhases)
        {
            List<BaseEnemyCard> firstProbe = InstantiateAndValidate(
                deckId,
                phase.SourceFactories,
                expectedDefinitions: null);
            CardDefinitionFingerprint[] templateDefinitions = firstProbe
                .Select(CardDefinitionFingerprint.FromCard)
                .ToArray();
            foreach (BaseEnemyCard card in firstProbe)
            {
                if (!canonicalDefinitions.TryGetValue(card.CardId, out CardDefinitionFingerprint? canonical) ||
                    !canonical.Matches(card))
                {
                    throw new InvalidOperationException(
                        $"敌人牌组 {deckId} 的阶段 {phase.Phase} 引用了未注册或语义不同的定义 {card.CardId}。");
                }
            }

            List<BaseEnemyCard> secondProbe = InstantiateAndValidate(
                deckId,
                phase.SourceFactories,
                templateDefinitions);
            HashSet<BaseEnemyCard> firstReferences = new(firstProbe, ReferenceEqualityComparer.Instance);
            if (secondProbe.Any(firstReferences.Contains))
            {
                throw new InvalidOperationException(
                    $"敌人牌组 {deckId} 的阶段 {phase.Phase} 工厂跨调用复用了卡牌对象。");
            }

            phases.Add(
                phase.Phase,
                new PhaseDefinition(
                    phase.SourceFactories,
                    templateDefinitions,
                    templateSlotOffset));
            templateSlotOffset = checked(templateSlotOffset + phase.InitialSourceInstanceCount);
        }

        string[] assetPaths = definitionProbes
            .SelectMany(card => EnumerateCardModelAssetPaths(card.CardModel))
            .Concat(directory.CollectionCatalog.Definitions
                .SelectMany(collection => EnumerateCardModelAssetPaths(collection.ResolveCardModel())))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new DeckDefinition(directory, phases, canonicalDefinitions, assetPaths);
    }

    /// <summary>
    /// 取得已注册定义；未知牌组属于配置或恢复错误，绝不使用替代牌组兜底。
    /// </summary>
    /// <param name="deckId">待查询牌组标识。</param>
    /// <returns>完整不可变注册定义。</returns>
    private static DeckDefinition GetDefinition(EnemyCardDeckId deckId)
    {
        lock (Sync)
        {
            if (!Definitions.TryGetValue(deckId, out DeckDefinition? definition))
            {
                throw new KeyNotFoundException($"未知敌人牌组 {deckId}。");
            }

            return definition;
        }
    }

    /// <summary>
    /// 调用工厂并验证同次调用无对象复用、身份合法且与已注册模板一致。
    /// </summary>
    /// <param name="deckId">用于错误诊断的牌组标识。</param>
    /// <param name="factories">待调用工厂。</param>
    /// <param name="expectedDefinitions">可选的期望卡牌定义身份顺序。</param>
    /// <returns>经过完整验证的新实例列表。</returns>
    private static List<BaseEnemyCard> InstantiateAndValidate(
        EnemyCardDeckId deckId,
        IReadOnlyList<Func<BaseEnemyCard>> factories,
        IReadOnlyList<CardDefinitionFingerprint>? expectedDefinitions)
    {
        List<BaseEnemyCard> cards = new(factories.Count);
        HashSet<BaseEnemyCard> references = new(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < factories.Count; index++)
        {
            BaseEnemyCard card = factories[index]() ?? throw new InvalidOperationException(
                $"敌人牌组 {deckId} 的第 {index} 个工厂返回了空对象。");
            if (!references.Add(card))
            {
                throw new InvalidOperationException(
                    $"敌人牌组 {deckId} 的多个槽位复用了同一个卡牌对象。");
            }

            if (!card.CardId.IsValid || card.CardModel is null)
            {
                throw new InvalidOperationException(
                    $"敌人牌组 {deckId} 的第 {index} 张牌缺少有效 CardId 或 CardModel。");
            }

            if (expectedDefinitions is not null &&
                (index >= expectedDefinitions.Count || !expectedDefinitions[index].Matches(card)))
            {
                throw new InvalidOperationException(
                    $"敌人牌组 {deckId} 的工厂在不同调用间改变了卡牌类型或完整语义定义指纹。");
            }

            cards.Add(card);
        }

        if (expectedDefinitions is not null && cards.Count != expectedDefinitions.Count)
        {
            throw new InvalidOperationException($"敌人牌组 {deckId} 的工厂数量与已注册模板不一致。");
        }

        return cards;
    }

    /// <summary>
    /// 枚举原版 CardModel 公开声明的牌面与战斗资源，并在存在内建覆盖层时加入其场景。
    /// </summary>
    /// <param name="cardModel">已验证的只读显示模型原型。</param>
    /// <returns>无需创建 NCard 即可取得的稳定资源路径。</returns>
    private static IEnumerable<string> EnumerateCardModelAssetPaths(MegaCrit.Sts2.Core.Models.CardModel cardModel)
    {
        foreach (string path in cardModel.AllPortraitPaths)
        {
            yield return path;
        }

        foreach (string path in cardModel.RunAssetPaths)
        {
            yield return path;
        }

        if (cardModel.HasBuiltInOverlay)
        {
            yield return cardModel.OverlayPath;
        }
    }

    /// <summary>保存注册后不可变的完整内容目录、阶段验证结果与预加载路径。</summary>
    private sealed class DeckDefinition
    {
        public DeckDefinition(
            EnemyCardContentDirectory contentDirectory,
            IReadOnlyDictionary<EnemyCardPhase, PhaseDefinition> phases,
            IReadOnlyDictionary<EnemyCardId, CardDefinitionFingerprint> canonicalDefinitions,
            IReadOnlyList<string> assetPaths)
        {
            ContentDirectory = contentDirectory;
            Phases = new Dictionary<EnemyCardPhase, PhaseDefinition>(phases);
            CanonicalDefinitions = new Dictionary<EnemyCardId, CardDefinitionFingerprint>(canonicalDefinitions);
            AssetPaths = Array.AsReadOnly(assetPaths.ToArray());
        }

        public EnemyCardContentDirectory ContentDirectory { get; }
        public IReadOnlyDictionary<EnemyCardPhase, PhaseDefinition> Phases { get; }
        private IReadOnlyDictionary<EnemyCardId, CardDefinitionFingerprint> CanonicalDefinitions { get; }

        /// <summary>获取已缓存并去重的牌面显示资源路径。</summary>
        public IReadOnlyList<string> AssetPaths { get; }

        public PhaseDefinition GetPhase(EnemyCardPhase phase) =>
            Phases.TryGetValue(phase, out PhaseDefinition? definition)
                ? definition
                : throw new KeyNotFoundException($"牌组 {ContentDirectory.DeckId} 未注册阶段 {phase}。");

        public CardDefinitionFingerprint GetCanonicalDefinition(EnemyCardId cardId) =>
            cardId.IsValid && CanonicalDefinitions.TryGetValue(cardId, out CardDefinitionFingerprint? definition)
                ? definition
                : throw new KeyNotFoundException($"牌组 {ContentDirectory.DeckId} 未注册卡牌定义 {cardId}。");
    }

    /// <summary>保存一个阶段的有序工厂、语义指纹和全局模板槽位起点。</summary>
    private sealed class PhaseDefinition
    {
        public PhaseDefinition(
            IReadOnlyList<Func<BaseEnemyCard>> factories,
            IReadOnlyList<CardDefinitionFingerprint> templateDefinitions,
            int templateSlotOffset)
        {
            Factories = Array.AsReadOnly(factories.ToArray());
            TemplateDefinitions = Array.AsReadOnly(templateDefinitions.ToArray());
            TemplateCardIds = Array.AsReadOnly(templateDefinitions.Select(definition => definition.CardId).ToArray());
            TemplateSlotOffset = templateSlotOffset;
        }

        public IReadOnlyList<Func<BaseEnemyCard>> Factories { get; }
        public IReadOnlyList<CardDefinitionFingerprint> TemplateDefinitions { get; }
        public IReadOnlyList<EnemyCardId> TemplateCardIds { get; }
        public int TemplateSlotOffset { get; }
    }

    /// <summary>
    /// 保存注册兼容所要求的完整卡牌定义身份，不包含运行时对象地址。
    /// </summary>
    private sealed class CardDefinitionFingerprint
    {
        /// <summary>
        /// 创建一项卡牌定义身份指纹。
        /// </summary>
        /// <param name="cardType">敌人卡牌的具体运行时类型。</param>
        /// <param name="semanticFingerprint">覆盖显示模型、Tag、评分、素材、生命周期、Token 与效果程序的指纹。</param>
        private CardDefinitionFingerprint(
            Type cardType,
            EnemyCardId cardId,
            string semanticFingerprint)
        {
            CardType = cardType;
            CardId = cardId;
            SemanticFingerprint = semanticFingerprint;
        }

        /// <summary>获取具体敌人卡牌类型。</summary>
        public Type CardType { get; }

        /// <summary>获取稳定敌人卡牌标识。</summary>
        public EnemyCardId CardId { get; }

        /// <summary>获取覆盖完整执行语义的稳定定义指纹。</summary>
        public string SemanticFingerprint { get; }

        /// <summary>
        /// 从已校验敌人卡牌创建定义身份指纹。
        /// </summary>
        /// <param name="card">已校验卡牌实例。</param>
        /// <returns>不包含实例引用的定义身份。</returns>
        public static CardDefinitionFingerprint FromCard(BaseEnemyCard card) =>
            new(
                card.GetType(),
                card.CardId,
                card.Definition.SemanticFingerprint);

        /// <summary>
        /// 比较新实例是否保持全部可影响执行与显示的定义身份。
        /// </summary>
        /// <param name="card">工厂新创建的卡牌。</param>
        /// <returns>所有定义字段完全一致时为 <see langword="true"/>。</returns>
        public bool Matches(BaseEnemyCard card) =>
            card.GetType() == CardType &&
            card.CardId == CardId &&
            string.Equals(
                card.Definition.SemanticFingerprint,
                SemanticFingerprint,
                StringComparison.Ordinal);
    }
}
