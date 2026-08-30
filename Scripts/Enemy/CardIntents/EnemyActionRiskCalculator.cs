using System.Collections.ObjectModel;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Cards.Collections;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>保存一张敌人牌在行动终态中的稳定区域和长线属性。</summary>
public sealed record EnemyProjectedCardZoneState(
    EnemyCardInstanceKey InstanceKey,
    EnemyCardId CardId,
    EnemyCardZone Zone,
    EnemyCardPhase SourcePhase,
    bool CarryAcrossPhase,
    int ReplayCount);

/// <summary>保存投影开始前的纯数据快照；后续模拟不会读取或修改真实战斗状态。</summary>
public sealed class EnemyProjectionInitialState
{
    public EnemyProjectionInitialState(
        EnemyCardPhase activePhase = EnemyCardPhase.None,
        decimal enemyBlock = 0m,
        IReadOnlyDictionary<string, decimal>? enemyPowers = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>? targetPowers = null,
        IEnumerable<EnemyProjectedCardZoneState>? cards = null,
        IEnumerable<EnemyCollectionInstance>? availableCollections = null,
        IEnumerable<EnemyCollectionInstance>? consumedCollections = null)
    {
        if (!Enum.IsDefined(activePhase) || enemyBlock < decimal.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(activePhase));
        }

        ActivePhase = activePhase;
        EnemyBlock = enemyBlock;
        EnemyPowers = CopyPowerMap(enemyPowers);
        TargetPowers = CopyTargetPowerMap(targetPowers);
        EnemyProjectedCardZoneState[] copiedCards = (cards ?? []).ToArray();
        if (copiedCards.Any(card => card is null || card.ReplayCount < 0) ||
            copiedCards.Select(card => card.InstanceKey).Distinct().Count() != copiedCards.Length)
        {
            throw new ArgumentException("投影初始牌区包含空值、负重放或重复实例。", nameof(cards));
        }

        Cards = Array.AsReadOnly(copiedCards);
        AvailableCollections = CopyCollections(availableCollections, nameof(availableCollections));
        ConsumedCollections = CopyCollections(consumedCollections, nameof(consumedCollections));
        string[] collectionIds = AvailableCollections.Concat(ConsumedCollections)
            .Select(item => item.CollectionInstanceId)
            .ToArray();
        if (collectionIds.Distinct(StringComparer.Ordinal).Count() != collectionIds.Length)
        {
            throw new ArgumentException("投影初始收藏品区域包含重复实例。", nameof(availableCollections));
        }
    }

    public EnemyCardPhase ActivePhase { get; }
    public decimal EnemyBlock { get; }
    public IReadOnlyDictionary<string, decimal> EnemyPowers { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> TargetPowers { get; }
    public IReadOnlyList<EnemyProjectedCardZoneState> Cards { get; }
    public IReadOnlyList<EnemyCollectionInstance> AvailableCollections { get; }
    public IReadOnlyList<EnemyCollectionInstance> ConsumedCollections { get; }

    /// <summary>从权威牌区复制一个与后续写入隔离的结构快照。</summary>
    public static EnemyProjectionInitialState FromCombatState(
        EnemyCardCombatState state,
        decimal enemyBlock = 0m,
        IReadOnlyDictionary<string, decimal>? enemyPowers = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>? targetPowers = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        IEnumerable<EnemyProjectedCardZoneState> Map(
            IEnumerable<BaseEnemyCard> cards,
            EnemyCardZone zone) => cards.Select(card => new EnemyProjectedCardZoneState(
                card.InstanceKey,
                card.CardId,
                zone,
                card.SourcePhase,
                card.CarryAcrossPhase,
                card.ReplayCount));

        return new EnemyProjectionInitialState(
            state.ActivePhase,
            enemyBlock,
            enemyPowers,
            targetPowers,
            Map(state.DrawPile, EnemyCardZone.Draw)
                .Concat(Map(state.CurrentCards, EnemyCardZone.Current))
                .Concat(Map(state.RetainedCards, EnemyCardZone.Retained))
                .Concat(Map(state.DiscardPile, EnemyCardZone.Discard))
                .Concat(Map(state.ExhaustPile, EnemyCardZone.Exhaust)),
            state.CollectionInventory.Available,
            state.CollectionInventory.Consumed);
    }

    internal static IReadOnlyDictionary<string, decimal> CopyPowerMap(
        IReadOnlyDictionary<string, decimal>? source) =>
        new ReadOnlyDictionary<string, decimal>(
            new Dictionary<string, decimal>(source ?? new Dictionary<string, decimal>(), StringComparer.Ordinal));

    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> CopyTargetPowerMap(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>? source)
    {
        Dictionary<string, IReadOnlyDictionary<string, decimal>> copied = new(StringComparer.Ordinal);
        foreach ((string targetId, IReadOnlyDictionary<string, decimal> powers) in
                 source ?? new Dictionary<string, IReadOnlyDictionary<string, decimal>>())
        {
            if (string.IsNullOrWhiteSpace(targetId) || powers is null)
            {
                throw new ArgumentException("目标 Power 快照必须具有非空目标和字典。", nameof(source));
            }

            copied.Add(targetId, CopyPowerMap(powers));
        }

        return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>(copied);
    }

    private static IReadOnlyList<EnemyCollectionInstance> CopyCollections(
        IEnumerable<EnemyCollectionInstance>? source,
        string parameterName)
    {
        EnemyCollectionInstance[] copied = (source ?? []).ToArray();
        if (copied.Any(item => item is null))
        {
            throw new ArgumentException("收藏品快照不能包含空实例。", parameterName);
        }

        return Array.AsReadOnly(copied);
    }
}

/// <summary>保存完整冻结行动结算后的总存量，而不是仅保存本行动增量。</summary>
public sealed class EnemyProjectionEndState
{
    public static EnemyProjectionEndState Empty { get; } = new();

    public EnemyProjectionEndState(
        decimal enemyBlock = 0m,
        IReadOnlyDictionary<string, decimal>? enemyPowers = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>? targetPowers = null,
        IEnumerable<EnemyProjectedCardZoneState>? cards = null,
        IEnumerable<EnemyCollectionInstance>? availableCollections = null,
        IEnumerable<EnemyCollectionInstance>? consumedCollections = null)
    {
        EnemyBlock = Math.Max(decimal.Zero, enemyBlock);
        EnemyPowers = EnemyProjectionInitialState.CopyPowerMap(enemyPowers);
        TargetPowers = EnemyProjectionInitialState.CopyTargetPowerMap(targetPowers);
        Cards = Array.AsReadOnly((cards ?? []).ToArray());
        AvailableCollections = Array.AsReadOnly((availableCollections ?? []).ToArray());
        ConsumedCollections = Array.AsReadOnly((consumedCollections ?? []).ToArray());
    }

    public decimal EnemyBlock { get; }
    public IReadOnlyDictionary<string, decimal> EnemyPowers { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> TargetPowers { get; }
    public IReadOnlyList<EnemyProjectedCardZoneState> Cards { get; }
    public IReadOnlyList<EnemyCollectionInstance> AvailableCollections { get; }
    public IReadOnlyList<EnemyCollectionInstance> ConsumedCollections { get; }

    public decimal GetEnemyPower(string powerId) =>
        EnemyPowers.TryGetValue(powerId, out decimal amount) ? Math.Max(decimal.Zero, amount) : decimal.Zero;
}

/// <summary>四部分完整行动风险分；总分不再施加额外整体折扣。</summary>
public sealed record EnemyActionRiskScore(
    decimal AttackRisk,
    decimal SurvivalRisk,
    decimal EngineRisk,
    decimal DeferredRisk)
{
    public decimal TotalRisk => AttackRisk + SurvivalRisk + EngineRisk + DeferredRisk;
}

/// <summary>提供阶段分母、内容解析和未写入终态的延迟增层信息。</summary>
public sealed class EnemyActionRiskContext
{
    public EnemyActionRiskContext(
        EnemyCardPhase phase,
        int phaseInitialTemplateInstanceCount,
        EnemyCardContentDirectory contentDirectory,
        IEnumerable<string>? additionalDefensivePowerIds = null,
        IReadOnlyDictionary<EnemyCardInstanceKey, int>? pendingDeferredReplayIncrements = null)
    {
        if (!Enum.IsDefined(phase) || phaseInitialTemplateInstanceCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        Phase = phase;
        PhaseInitialTemplateInstanceCount = phaseInitialTemplateInstanceCount;
        ContentDirectory = contentDirectory ?? throw new ArgumentNullException(nameof(contentDirectory));
        AdditionalDefensivePowerIds = new HashSet<string>(
            (additionalDefensivePowerIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        PendingDeferredReplayIncrements = new ReadOnlyDictionary<EnemyCardInstanceKey, int>(
            new Dictionary<EnemyCardInstanceKey, int>(pendingDeferredReplayIncrements ??
                new Dictionary<EnemyCardInstanceKey, int>()));
        if (PendingDeferredReplayIncrements.Values.Any(value => value < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(pendingDeferredReplayIncrements));
        }
    }

    public EnemyCardPhase Phase { get; }
    public int PhaseInitialTemplateInstanceCount { get; }
    public EnemyCardContentDirectory ContentDirectory { get; }
    public IReadOnlySet<string> AdditionalDefensivePowerIds { get; }
    public IReadOnlyDictionary<EnemyCardInstanceKey, int> PendingDeferredReplayIncrements { get; }
}

/// <summary>依据完整投影终态计算攻击、生存、运转和延迟四类风险。</summary>
public sealed class EnemyActionRiskCalculator
{
    private static readonly string StrengthId = StablePowerId(typeof(StrengthPower));
    private static readonly string DexterityId = StablePowerId(typeof(DexterityPower));
    private static readonly string HeartWallId = StablePowerId(typeof(AtFieldPower));
    private static readonly string VulnerableId = StablePowerId(typeof(VulnerablePower));
    private static readonly string DuckAndCoverId = StablePowerId(typeof(DuckAndCoverPower));
    private static readonly string HeartBeatId = StablePowerId(typeof(HeartBeatPower));
    private static readonly string SorrowfulRainId = StablePowerId(typeof(SorrowfulRainPower));
    private static readonly string CardIntentSorrowfulRainId = StablePowerId(typeof(CardIntentSorrowfulRainPower));
    private static readonly string AdayumeId = StablePowerId(typeof(AdayumePower));
    private static readonly string CardIntentAdayumeId = StablePowerId(typeof(CardIntentAdayumePower));
    private static readonly string NameOfTearId = StablePowerId(typeof(NameOfTearPower));

    public EnemyActionRiskScore Calculate(
        LiveActionProjection projection,
        EnemyActionRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(context);
        EnemyProjectionEndState end = projection.EndState;

        decimal attack = projection.Units
            .SelectMany(unit => unit.Targets)
            .GroupBy(target => target.TargetId, StringComparer.Ordinal)
            .Select(group => group.Sum(target => target.TotalDamage))
            .DefaultIfEmpty(decimal.Zero)
            .Max();

        decimal dexterity = end.GetEnemyPower(DexterityId);
        decimal heartWall = end.GetEnemyPower(HeartWallId);
        decimal defensive = IsActive(end, DuckAndCoverId) ? 0.65m * heartWall : decimal.Zero;
        defensive += IsActive(end, HeartBeatId) ? 8m : decimal.Zero;
        defensive += context.AdditionalDefensivePowerIds.Sum(id => 5m * end.GetEnemyPower(id));
        decimal survival = 0.65m * end.EnemyBlock + 6m * dexterity + 3m * heartWall + defensive;

        decimal targetVulnerable = SumTargetPower(end, VulnerableId);
        decimal otherTargetDebuff = end.TargetPowers.Values.Sum(powers => powers
            .Where(pair => !string.Equals(pair.Key, VulnerableId, StringComparison.Ordinal))
            .Sum(pair => Math.Max(decimal.Zero, pair.Value)));
        decimal engine = 10m * end.GetEnemyPower(StrengthId)
                         + CalculateAbilityRisk(end, context)
                         + 6m * targetVulnerable
                         + 3m * otherTargetDebuff
                         + CalculateCollectionInventoryRisk(end)
                         + CalculateCompressionRisk(end, context);

        decimal reactive = 0.5m * heartWall * (IsActive(end, NameOfTearId) ? 1.5m : 1m);
        (decimal carry, decimal replay) = CalculateCarryRisks(projection, context);
        return new EnemyActionRiskScore(attack, survival, engine, reactive + carry + replay);
    }

    private static decimal CalculateAbilityRisk(
        EnemyProjectionEndState end,
        EnemyActionRiskContext context)
    {
        decimal risk = IsActive(end, SorrowfulRainId) || IsActive(end, CardIntentSorrowfulRainId) ? 12m : 0m;
        risk += IsActive(end, AdayumeId) || IsActive(end, CardIntentAdayumeId) ? 15m : 0m;
        HashSet<string> excluded = new(StringComparer.Ordinal)
        {
            StrengthId, DexterityId, HeartWallId, DuckAndCoverId, HeartBeatId,
            SorrowfulRainId, CardIntentSorrowfulRainId, AdayumeId, CardIntentAdayumeId, NameOfTearId
        };
        excluded.UnionWith(context.AdditionalDefensivePowerIds);
        risk += end.EnemyPowers
            .Where(pair => !excluded.Contains(pair.Key))
            .Sum(pair => 5m * Math.Max(decimal.Zero, pair.Value));
        return risk;
    }

    private static decimal CalculateCollectionInventoryRisk(EnemyProjectionEndState end) =>
        end.AvailableCollections.Sum(item => IsStarStone(item.Definition) ? 5m : 3m);

    private static decimal CalculateCompressionRisk(
        EnemyProjectionEndState end,
        EnemyActionRiskContext context)
    {
        decimal weight = context.Phase switch
        {
            EnemyCardPhase.Phase2 => 1m,
            EnemyCardPhase.Phase3 => 3m,
            _ => 0m
        };
        int reusable = end.Cards.Count(card =>
            card.SourcePhase == context.Phase &&
            !card.CarryAcrossPhase &&
            card.Zone != EnemyCardZone.Exhaust);
        return weight * Math.Max(0, context.PhaseInitialTemplateInstanceCount - reusable);
    }

    private static (decimal Carry, decimal Replay) CalculateCarryRisks(
        LiveActionProjection projection,
        EnemyActionRiskContext context)
    {
        decimal carry = 0m;
        decimal replay = 0m;
        foreach (EnemyProjectedCardZoneState token in projection.EndState.Cards.Where(card => card.CarryAcrossPhase))
        {
            decimal coefficient = ZoneCoefficient(token.Zone);
            if (coefficient == decimal.Zero)
            {
                continue;
            }

            EnemyCardDefinition definition = context.ContentDirectory.CreateDefinition(token.CardId).Definition;
            decimal body = OneExecutionForecast(definition, token, projection, projection.EndState);
            carry += coefficient * (body + ChainContinuationForecast(
                definition,
                projection,
                projection.EndState,
                context,
                depth: 1,
                visited: new HashSet<EnemyCardId> { token.CardId }));
            replay += coefficient * token.ReplayCount * body;
            if (context.PendingDeferredReplayIncrements.TryGetValue(token.InstanceKey, out int pending))
            {
                replay += coefficient * pending * body;
            }
        }

        return (carry, replay);
    }

    private static decimal ChainContinuationForecast(
        EnemyCardDefinition parent,
        LiveActionProjection projection,
        EnemyProjectionEndState end,
        EnemyActionRiskContext context,
        int depth,
        HashSet<EnemyCardId> visited)
    {
        if (depth > 3 || parent.ComposeResultCardId is not { } childId || !visited.Add(childId))
        {
            return decimal.Zero;
        }

        EnemyCardDefinition child = context.ContentDirectory.CreateDefinition(childId).Definition;
        decimal feasibility = CalculateMaterialFeasibility(child, end, context.ContentDirectory);
        if (feasibility == decimal.Zero)
        {
            return decimal.Zero;
        }

        EnemyProjectedCardZoneState? existing = end.Cards.FirstOrDefault(card =>
            card.CarryAcrossPhase && card.CardId == childId);
        decimal next = OneExecutionForecast(child, existing, projection, end);
        decimal continuation = ChainContinuationForecast(child, projection, end, context, depth + 1, visited);
        return 0.6m * feasibility * ((1m + (existing?.ReplayCount ?? 0)) * next + continuation);
    }

    private static decimal CalculateMaterialFeasibility(
        EnemyCardDefinition definition,
        EnemyProjectionEndState end,
        EnemyCardContentDirectory contentDirectory)
    {
        if (definition.MaterialRequests.Count == 0)
        {
            return 1m;
        }

        List<MegaCrit.Sts2.Core.Entities.Cards.CardType> cardTypes = end.Cards
            .Where(card => card.Zone != EnemyCardZone.Exhaust)
            .Select(card => contentDirectory.CreateDefinition(card.CardId).CardModel.Type)
            .ToList();
        List<EnemyCollectionDefinition> collections = end.AvailableCollections.Select(item => item.Definition).ToList();
        int required = definition.MaterialRequests.Sum(request => request.Requirements.Sum(item => item.Count));
        int suppliedByCards = CountSatisfiedRequirements(definition.MaterialRequests, cardTypes, []);
        if (suppliedByCards >= required)
        {
            return 1m;
        }

        int suppliedTogether = CountSatisfiedRequirements(definition.MaterialRequests, cardTypes, collections);
        if (suppliedTogether >= required)
        {
            return suppliedByCards == 0 ? 0.25m : 0.5m;
        }

        return decimal.Zero;
    }

    private static int CountSatisfiedRequirements(
        IReadOnlyList<EnemyMaterialRequest> requests,
        List<MegaCrit.Sts2.Core.Entities.Cards.CardType> cardTypes,
        List<EnemyCollectionDefinition> collections)
    {
        List<MegaCrit.Sts2.Core.Entities.Cards.CardType> remainingCards = [.. cardTypes];
        List<EnemyCollectionDefinition> remainingCollections = [.. collections];
        int supplied = 0;
        foreach (EnemyMaterialRequirement requirement in requests.SelectMany(request => request.Requirements))
        {
            for (int index = 0; index < requirement.Count; index++)
            {
                int cardIndex = remainingCards.FindIndex(type => requirement.CardType is null || type == requirement.CardType);
                if (cardIndex >= 0)
                {
                    remainingCards.RemoveAt(cardIndex);
                    supplied++;
                    continue;
                }

                int collectionIndex = remainingCollections.FindIndex(item =>
                    item.IsEpiphany || requirement.CardType is null || item.MaterialCardType == requirement.CardType);
                if (collectionIndex >= 0)
                {
                    remainingCollections.RemoveAt(collectionIndex);
                    supplied++;
                }
            }
        }

        return supplied;
    }

    private static decimal OneExecutionForecast(
        EnemyCardDefinition definition,
        EnemyProjectedCardZoneState? token,
        LiveActionProjection projection,
        EnemyProjectionEndState end)
    {
        EnemyCardScoreProfile profile = definition.ScoreProfile;
        decimal attack = decimal.Zero;
        int hitCount = 0;
        foreach (EnemyAttackAllEffect effect in definition.Effects.OfType<EnemyAttackAllEffect>())
        {
            attack += effect.Damage * effect.HitCount;
            hitCount += effect.HitCount;
        }

        foreach (EnemyFrozenXAttackAllEffect effect in definition.Effects.OfType<EnemyFrozenXAttackAllEffect>())
        {
            int resolvedX;
            if (token is not null &&
            projection.EffectiveCardStates.TryGetValue(token.InstanceKey, out EnemyFrozenEffectiveCardState? frozen) &&
                frozen.FrozenX is { } frozenX)
            {
                resolvedX = frozenX;
            }
            else
            {
                int frozenCount = projection.EffectiveCardStates.Values.Count(state => state.WasCounted);
                int multiplier = effect.DoubleAtDistinctExhaustDefinitionCount > 0 &&
                                 end.Cards.Where(card => card.Zone == EnemyCardZone.Exhaust)
                                     .Select(card => card.CardId)
                                     .Distinct()
                                     .Count() >= effect.DoubleAtDistinctExhaustDefinitionCount
                    ? 2
                    : 1;
                resolvedX = Math.Max(0, 6 - frozenCount) * multiplier;
            }

            attack += effect.Damage * resolvedX;
            hitCount += resolvedX;
        }

        if (hitCount == 0)
        {
            attack = profile.Attack;
        }
        else
        {
            attack += end.GetEnemyPower(StrengthId) * hitCount;
            if (SumTargetPower(end, VulnerableId) > decimal.Zero)
            {
                attack *= 1.5m;
            }
        }

        return attack
               + 0.65m * profile.Block
               + 10m * profile.Strength
               + 6m * profile.Dexterity
               + 3m * profile.AtField
               + 6m * profile.Vulnerable
               + 3m * profile.OtherDebuff
               + 3m * profile.NormalCollection
               + 5m * profile.StarStone;
    }

    private static decimal SumTargetPower(EnemyProjectionEndState end, string powerId) =>
        end.TargetPowers.Values.Sum(powers =>
            powers.TryGetValue(powerId, out decimal amount) ? Math.Max(decimal.Zero, amount) : decimal.Zero);

    private static bool IsActive(EnemyProjectionEndState end, string powerId) => end.GetEnemyPower(powerId) > 0m;

    private static bool IsStarStone(EnemyCollectionDefinition definition) =>
        definition.CardModelType == typeof(StarStone) ||
        definition.CollectionId.Contains("STAR_STONE", StringComparison.OrdinalIgnoreCase);

    private static decimal ZoneCoefficient(EnemyCardZone zone) => zone switch
    {
        EnemyCardZone.Retained => 0.75m,
        EnemyCardZone.Draw or EnemyCardZone.Discard => 0.45m,
        EnemyCardZone.Exhaust => 0.15m,
        _ => decimal.Zero
    };

    private static string StablePowerId(Type type) => type.FullName ?? type.Name;
}
