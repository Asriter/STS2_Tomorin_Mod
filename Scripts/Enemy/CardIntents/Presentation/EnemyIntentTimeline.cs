using MegaCrit.Sts2.Core.Models;
using STS2_Tomorin_Mod.Enemy.CardIntents.Test;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.Presentation;

/// <summary>标识复合 Intent 中一个可独立显示和归属效果的稳定槽位。</summary>
public sealed record EnemyIntentDisplayKey
{
    public EnemyIntentDisplayKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Intent 展示键不能为空。", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static EnemyIntentDisplayKey ForCard(EnemyCardInstanceKey key) =>
        new($"CARD:{(key ?? throw new ArgumentNullException(nameof(key))).Value}");

    public static EnemyIntentDisplayKey ForCollection(string collectionInstanceId) =>
        new($"COLLECTION:{collectionInstanceId}");

    public override string ToString() => Value;
}

/// <summary>描述结构时间线中的卡牌或收藏品角色。</summary>
public enum EnemyIntentTimelineRole
{
    Source,
    InsufficientMaterial,
    ConsumedCard,
    ConsumedCollection,
    ImmediateCard,
    ComposeToken
}

/// <summary>冻结计划派生的一项稳定展示结构，不包含实时数值。</summary>
public sealed record EnemyIntentTimelineEntry(
    EnemyIntentDisplayKey DisplayKey,
    CardModel CardModel,
    string DescriptionOverride,
    EnemyIntentTimelineRole Role,
    bool IsDimmed,
    EnemyCardInstanceKey? CardInstanceKey,
    EnemyCardId? CardId,
    string? CollectionInstanceId = null,
    string? CollectionId = null);

/// <summary>按真实深度优先结算关系排列的只读展示时间线。</summary>
public sealed class EnemyIntentTimeline
{
    public EnemyIntentTimeline(IEnumerable<EnemyIntentTimelineEntry> entries, IEnumerable<string>? diagnostics = null)
    {
        Entries = Array.AsReadOnly((entries ?? throw new ArgumentNullException(nameof(entries))).ToArray());
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public IReadOnlyList<EnemyIntentTimelineEntry> Entries { get; }

    public IReadOnlyList<string> Diagnostics { get; }
}

/// <summary>只从冻结计划构建展示顺序，绝不读取或修改实时战斗状态。</summary>
public static class EnemyIntentTimelineBuilder
{
    public static EnemyIntentTimeline Build(
        PreparedEnemyCardAction action,
        EnemyCardContentDirectory? contentDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dictionary<EnemyCardInstanceKey, BaseEnemyCard> knownCards = action.Sources
            .ToDictionary(source => source.SourceKey, source => source.SourceCard);
        HashSet<EnemyCardInstanceKey> consumedCards = [];
        foreach (PreparedEnemyCardSource source in action.Sources)
        {
            foreach (PreparedEnemyCardUnitPlan unit in source.Units)
            {
                CollectConsumedCards(unit.OrderedSteps, consumedCards);
            }
        }

        List<EnemyIntentTimelineEntry> entries = [];
        List<string> diagnostics = [];
        HashSet<EnemyIntentDisplayKey> emitted = [];
        foreach (PreparedEnemyCardSource source in action.Sources)
        {
            if (source.Units.Count == 0 && consumedCards.Contains(source.SourceKey))
            {
                continue;
            }

            if (source.Units.Count == 0)
            {
                AddCard(source.SourceCard, EnemyIntentTimelineRole.InsufficientMaterial, true);
                continue;
            }

            AddCard(source.SourceCard, EnemyIntentTimelineRole.Source, false);
            foreach (PreparedEnemyCardUnitPlan unit in source.Units)
            {
                AppendSteps(unit.OrderedSteps, unit.RootSourceKey);
            }
        }

        return new EnemyIntentTimeline(entries, diagnostics);

        void AppendUnit(PreparedEnemyCardUnitPlan unit, EnemyIntentTimelineRole role)
        {
            BaseEnemyCard card = ResolveCard(unit.ExecutingCardKey, unit.ExecutingCardId);
            AddCard(card, role, false, unit.ExecutingCardKey, unit.ExecutingCardId);
            AppendSteps(unit.OrderedSteps, unit.RootSourceKey);
        }

        void AppendSteps(IEnumerable<PreparedEnemyResolutionStep> steps, EnemyCardInstanceKey rootSourceKey)
        {
            foreach (PreparedEnemyResolutionStep step in steps)
            {
                switch (step)
                {
                    case PreparedConsumedCardStep consumed:
                    {
                        EnemyCardId cardId = consumed.ControlledChild?.ExecutingCardId ??
                                             (knownCards.TryGetValue(consumed.MaterialKey, out BaseEnemyCard? material)
                                                 ? material.CardId
                                                 : default);
                        if (!cardId.IsValid)
                        {
                            diagnostics.Add($"被消费卡牌 {consumed.MaterialKey} 缺少可显示定义。");
                            break;
                        }

                        BaseEnemyCard card = ResolveCard(consumed.MaterialKey, cardId);
                        AddCard(card, EnemyIntentTimelineRole.ConsumedCard, true, consumed.MaterialKey, cardId);
                        if (consumed.ControlledChild is not null)
                        {
                            AppendSteps(consumed.ControlledChild.OrderedSteps, rootSourceKey);
                        }

                        break;
                    }
                    case PreparedConsumedCollectionStep collection:
                        AddCollection(collection);
                        AppendSteps(collection.Children, rootSourceKey);
                        break;
                    case PreparedComposeResultStep compose when compose.ImmediateChild is not null:
                        AppendUnit(compose.ImmediateChild, EnemyIntentTimelineRole.ComposeToken);
                        foreach (PreparedEnemyCardUnitPlan replay in compose.AdditionalReplayUnits)
                        {
                            AppendUnit(replay, EnemyIntentTimelineRole.ComposeToken);
                        }

                        break;
                    case PreparedImmediateCardStep immediate:
                        AppendUnit(immediate.Child, EnemyIntentTimelineRole.ImmediateCard);
                        foreach (PreparedEnemyCardUnitPlan replay in immediate.AdditionalReplayUnits)
                        {
                            AppendUnit(replay, EnemyIntentTimelineRole.ImmediateCard);
                        }

                        break;
                    case PreparedRecoveryStep
                    {
                        Kind: EnemyPreparedRecoveryKind.Card,
                        ImmediateCardChild: not null
                    } recovery:
                        AppendUnit(recovery.ImmediateCardChild, EnemyIntentTimelineRole.ImmediateCard);
                        foreach (PreparedEnemyCardUnitPlan replay in recovery.AdditionalReplayUnits)
                        {
                            AppendUnit(replay, EnemyIntentTimelineRole.ImmediateCard);
                        }

                        break;
                }
            }
        }

        void AddCollection(PreparedConsumedCollectionStep step)
        {
            EnemyIntentDisplayKey key = EnemyIntentDisplayKey.ForCollection(step.CollectionInstanceId);
            if (!emitted.Add(key))
            {
                return;
            }

            EnemyCollectionDefinition definition =
                contentDirectory?.CollectionCatalog.TryGet(step.CollectionId, out EnemyCollectionDefinition? registered) == true
                    ? registered!
                    : CardIntentTestCollectionCatalog.Catalog.GetRequired(step.CollectionId);
            CardModel model = definition.ResolveCardModel();

            entries.Add(new EnemyIntentTimelineEntry(
                key,
                model,
                string.Empty,
                EnemyIntentTimelineRole.ConsumedCollection,
                true,
                null,
                null,
                step.CollectionInstanceId,
                step.CollectionId));
        }

        BaseEnemyCard ResolveCard(EnemyCardInstanceKey key, EnemyCardId id)
        {
            if (knownCards.TryGetValue(key, out BaseEnemyCard? existing))
            {
                return existing;
            }

            BaseEnemyCard created = contentDirectory?.DefinitionFactories.ContainsKey(id) == true
                ? contentDirectory.CreateDefinition(id)
                : CardIntentTestCardCatalog.CreateCard(id);
            knownCards[key] = created;
            return created;
        }

        void AddCard(
            BaseEnemyCard card,
            EnemyIntentTimelineRole role,
            bool isDimmed,
            EnemyCardInstanceKey? explicitKey = null,
            EnemyCardId? explicitId = null)
        {
            EnemyCardInstanceKey key = explicitKey ?? card.InstanceKey;
            EnemyIntentDisplayKey displayKey = EnemyIntentDisplayKey.ForCard(key);
            if (!emitted.Add(displayKey))
            {
                return;
            }

            entries.Add(new EnemyIntentTimelineEntry(
                displayKey,
                card.CardModel,
                card.DescriptionOverride,
                role,
                isDimmed,
                key,
                explicitId ?? card.CardId));
        }
    }

    private static void CollectConsumedCards(
        IEnumerable<PreparedEnemyResolutionStep> steps,
        ISet<EnemyCardInstanceKey> result)
    {
        foreach (PreparedEnemyResolutionStep step in steps)
        {
            switch (step)
            {
                case PreparedConsumedCardStep card:
                    result.Add(card.MaterialKey);
                    if (card.ControlledChild is not null)
                    {
                        CollectConsumedCards(card.ControlledChild.OrderedSteps, result);
                    }

                    break;
                case PreparedConsumedCollectionStep collection:
                    CollectConsumedCards(collection.Children, result);
                    break;
                case PreparedComposeResultStep compose:
                    if (compose.ImmediateChild is not null)
                    {
                        CollectConsumedCards(compose.ImmediateChild.OrderedSteps, result);
                    }

                    foreach (PreparedEnemyCardUnitPlan replay in compose.AdditionalReplayUnits)
                    {
                        CollectConsumedCards(replay.OrderedSteps, result);
                    }

                    break;
                case PreparedImmediateCardStep immediate:
                    CollectConsumedCards(immediate.Child.OrderedSteps, result);
                    foreach (PreparedEnemyCardUnitPlan replay in immediate.AdditionalReplayUnits)
                    {
                        CollectConsumedCards(replay.OrderedSteps, result);
                    }

                    break;
                case PreparedRecoveryStep recovery when recovery.ImmediateCardChild is not null:
                    CollectConsumedCards(recovery.ImmediateCardChild.OrderedSteps, result);
                    foreach (PreparedEnemyCardUnitPlan replay in recovery.AdditionalReplayUnits)
                    {
                        CollectConsumedCards(replay.OrderedSteps, result);
                    }

                    break;
            }
        }
    }
}
