namespace STS2_Tomorin_Mod.Enemy.CardIntents.Presentation;

public static class EnemyCardIntentPresentationBuilder
{
    public static EnemyCardListPresentation Build(IReadOnlyList<BaseEnemyCard> cardList, LiveActionProjection projection)
    {
        ArgumentNullException.ThrowIfNull(cardList);
        EnemyIntentTimeline timeline = new(cardList.Select(card => new EnemyIntentTimelineEntry(
            EnemyIntentDisplayKey.ForCard(card.InstanceKey), card.CardModel, card.DescriptionOverride,
            EnemyIntentTimelineRole.Source, false, card.InstanceKey, card.CardId)));
        EnemyIntentEffectProjection[] rootEffects = projection.Units.Select(unit => new EnemyIntentEffectProjection(
            EnemyIntentDisplayKey.ForCard(unit.RootSourceKey), unit.RootSourceKey, unit.ExecutingCardKey,
            unit.ExecutingCardId, unit.ReplayIndex, unit.Targets, unit.EnemyBlockDelta, unit.EnemyPowerDeltas)).ToArray();
        EnemyCardListPresentation built = Build(timeline, new LiveActionProjection(
            projection.Units,
            projection.IsComplete,
            projection.Diagnostics,
            rootEffects,
            unavailableCardKeys: projection.UnavailableCardKeys));
        return new EnemyCardListPresentation(
            cardList.Select((card, index) => new EnemyCardIntentPresentation(
                card.InstanceKey,
                card,
                built.Cards[index].Effects,
                built.Cards[index].IsDimmed)),
            built.RequiresGlobalUnknown,
            built.Diagnostics);
    }

    public static EnemyCardListPresentation Build(EnemyIntentTimeline timeline, LiveActionProjection projection)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(projection);
        List<string> diagnostics = timeline.Diagnostics.Concat(projection.Diagnostics).ToList();
        Dictionary<EnemyIntentDisplayKey, int> keyCounts = timeline.Entries.GroupBy(entry => entry.DisplayKey)
            .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<EnemyIntentDisplayKey, List<EnemyIntentEffectProjection>> effectsByKey = [];
        bool hasOrphan = false;
        foreach (EnemyIntentEffectProjection effect in projection.TimelineEffects)
        {
            if (effect is null || effect.DisplayKey is null)
            {
                diagnostics.Add("实时投影包含空效果切片或空展示键。");
                hasOrphan = true;
                continue;
            }

            if (!effectsByKey.TryGetValue(effect.DisplayKey, out List<EnemyIntentEffectProjection>? list))
            {
                list = [];
                effectsByKey.Add(effect.DisplayKey, list);
            }

            list.Add(effect);
            if (!keyCounts.ContainsKey(effect.DisplayKey))
            {
                diagnostics.Add($"效果切片 {effect.DisplayKey} 不存在于结构时间线。");
                hasOrphan = true;
            }
        }

        List<EnemyCardIntentPresentation> cards = [];
        foreach (EnemyIntentTimelineEntry entry in timeline.Entries)
        {
            bool isUnavailable = entry.CardInstanceKey is EnemyCardInstanceKey cardKey &&
                                 projection.UnavailableCardKeys.Contains(cardKey);
            EnemyIntentTimelineEntry displayedEntry = isUnavailable && !entry.IsDimmed
                ? entry with { IsDimmed = true }
                : entry;
            List<string> localDiagnostics = [];
            List<EnemyCardEffectIntentPresentation> effects = [];
            if (keyCounts[entry.DisplayKey] > 1)
            {
                localDiagnostics.Add($"结构时间线包含重复展示键 {entry.DisplayKey}。");
            }
            else if (effectsByKey.TryGetValue(entry.DisplayKey, out List<EnemyIntentEffectProjection>? slices))
            {
                BuildEffects(entry.DisplayKey, slices, effects, localDiagnostics);
            }
            else if (RequiresProjection(entry.Role) && !isUnavailable)
            {
                localDiagnostics.Add($"展示槽 {entry.DisplayKey} 缺少实时效果投影。");
            }

            if (localDiagnostics.Count > 0)
            {
                diagnostics.AddRange(localDiagnostics);
                effects.Add(new EnemyUnknownPresentation(string.Join(" | ", localDiagnostics)));
            }

            cards.Add(new EnemyCardIntentPresentation(displayedEntry, effects));
        }

        if (!projection.IsComplete && projection.Diagnostics.Count == 0)
        {
            diagnostics.Add("实时投影被标记为不完整，但没有提供具体诊断。");
        }

        return new EnemyCardListPresentation(cards, !projection.IsComplete || hasOrphan, diagnostics);
    }

    private static bool RequiresProjection(EnemyIntentTimelineRole role) => role is
        EnemyIntentTimelineRole.Source or EnemyIntentTimelineRole.ImmediateCard or EnemyIntentTimelineRole.ComposeToken;

    private static void BuildEffects(
        EnemyIntentDisplayKey displayKey,
        IReadOnlyList<EnemyIntentEffectProjection> slices,
        ICollection<EnemyCardEffectIntentPresentation> effects,
        ICollection<string> diagnostics)
    {
        List<decimal> attackOrder = [];
        Dictionary<decimal, int> hitCounts = [];
        bool hasDefense = false;
        bool hasBuff = false;
        bool hasDebuff = false;
        foreach (EnemyIntentEffectProjection slice in slices)
        {
            if (slice.ReplayIndex < 0 || slice.RootSourceKey is null)
            {
                diagnostics.Add($"展示槽 {displayKey} 包含非法效果身份。");
                continue;
            }

            if (slice.EnemyBlockDelta > decimal.Zero) hasDefense = true;
            else if (slice.EnemyBlockDelta < decimal.Zero) diagnostics.Add($"展示槽 {displayKey} 包含负敌人格挡变化。");
            if (slice.EnemyPowerDeltas is null) diagnostics.Add($"展示槽 {displayKey} 缺少敌人 Power 投影。");
            else if (slice.EnemyPowerDeltas.Values.Any(delta => delta != decimal.Zero)) hasBuff = true;

            IReadOnlyList<decimal>? hits = ReadCanonicalDamageHits(displayKey, slice, diagnostics, ref hasDebuff);
            if (hits is null) continue;
            foreach (decimal damage in hits)
            {
                if (!hitCounts.TryAdd(damage, 1)) hitCounts[damage]++;
                else attackOrder.Add(damage);
            }
        }

        foreach (decimal damage in attackOrder) effects.Add(new EnemyAttackPresentation(damage, hitCounts[damage]));
        if (hasDefense) effects.Add(new EnemyDefendPresentation());
        if (hasBuff) effects.Add(new EnemyBuffPresentation());
        if (hasDebuff) effects.Add(new EnemyDebuffPresentation());
    }

    private static IReadOnlyList<decimal>? ReadCanonicalDamageHits(
        EnemyIntentDisplayKey displayKey,
        EnemyIntentEffectProjection slice,
        ICollection<string> diagnostics,
        ref bool hasDebuff)
    {
        if (slice.Targets is null)
        {
            diagnostics.Add($"展示槽 {displayKey} 缺少玩家目标集合。");
            return null;
        }

        if (slice.Targets.Count == 0) return [];
        HashSet<string> targetIds = new(StringComparer.Ordinal);
        decimal[]? canonical = null;
        foreach (EnemyTargetProjection target in slice.Targets)
        {
            if (target is null || string.IsNullOrWhiteSpace(target.TargetId) || !targetIds.Add(target.TargetId))
            {
                diagnostics.Add($"展示槽 {displayKey} 包含空目标或重复目标。");
                return null;
            }

            if (target.PowerDeltas is null || target.DamageHits is null)
            {
                diagnostics.Add($"展示槽 {displayKey} 的目标 {target.TargetId} 缺少投影。");
                return null;
            }

            if (target.PowerDeltas.Values.Any(delta => delta != decimal.Zero)) hasDebuff = true;
            EnemyDamageHitProjection[] hits = target.DamageHits.ToArray();
            if (hits.Any(hit => hit is null || hit.BaseDamage <= decimal.Zero || hit.ProjectedDamage < decimal.Zero))
            {
                diagnostics.Add($"展示槽 {displayKey} 包含非法标准攻击命中。");
                return null;
            }

            decimal[] current = hits.Select(hit => hit.BaseDamage).ToArray();
            if (canonical is null) canonical = current;
            else if (!canonical.SequenceEqual(current))
            {
                diagnostics.Add($"展示槽 {displayKey} 的各目标命中结构不一致。");
                return null;
            }
        }

        return canonical ?? [];
    }
}
