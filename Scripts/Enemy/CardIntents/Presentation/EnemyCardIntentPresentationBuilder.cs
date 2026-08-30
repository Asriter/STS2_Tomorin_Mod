namespace STS2_Tomorin_Mod.Enemy.CardIntents.Presentation;

/// <summary>
/// 把完整结构投影纯映射为按公开实例键归属、可由 Godot 视图消费的逐牌 Intent 展示模型。
/// </summary>
public static class EnemyCardIntentPresentationBuilder
{
    /// <summary>
    /// 按公开卡列顺序构建不可变展示，并将局部非法结构限制在对应卡牌的 Unknown 中。
    /// </summary>
    /// <param name="cardList">当前公开且顺序权威的敌人卡列。</param>
    /// <param name="projection">从冻结 DFS 计划派生的实时结构投影。</param>
    /// <returns>不引用 Godot、战斗命令或本地玩家上下文的纯展示模型。</returns>
    public static EnemyCardListPresentation Build(
        IReadOnlyList<BaseEnemyCard> cardList,
        LiveActionProjection projection)
    {
        ArgumentNullException.ThrowIfNull(cardList);
        ArgumentNullException.ThrowIfNull(projection);

        BaseEnemyCard[] cards = cardList
            .Select(card => card ?? throw new ArgumentException("公开敌人卡列不能包含空卡牌。", nameof(cardList)))
            .ToArray();
        List<string> diagnostics = projection.Diagnostics.ToList();
        Dictionary<EnemyCardInstanceKey, int> publicKeyCounts = cards
            .GroupBy(card => card.InstanceKey)
            .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<EnemyCardInstanceKey, List<EnemyCardReplayProjection>> unitsByRoot = [];
        bool hasOrphanUnit = false;

        foreach (EnemyCardReplayProjection unit in projection.Units)
        {
            if (unit is null || unit.RootSourceKey is null)
            {
                diagnostics.Add("实时投影包含空单元或空根来源键，无法归属到公开卡牌。");
                hasOrphanUnit = true;
                continue;
            }

            if (!unitsByRoot.TryGetValue(unit.RootSourceKey, out List<EnemyCardReplayProjection>? rootUnits))
            {
                rootUnits = [];
                unitsByRoot.Add(unit.RootSourceKey, rootUnits);
            }

            rootUnits.Add(unit);
            if (!publicKeyCounts.ContainsKey(unit.RootSourceKey))
            {
                diagnostics.Add($"实时投影根来源 {unit.RootSourceKey} 不存在于公开卡列。");
                hasOrphanUnit = true;
            }
        }

        List<EnemyCardIntentPresentation> presentations = new(cards.Length);
        foreach (BaseEnemyCard card in cards)
        {
            EnemyCardInstanceKey key = card.InstanceKey;
            List<string> localDiagnostics = [];
            List<EnemyCardEffectIntentPresentation> effects = [];

            if (publicKeyCounts[key] > 1)
            {
                localDiagnostics.Add($"公开卡列包含重复实例键 {key}，无法唯一关联逐牌投影。");
            }
            else if (!unitsByRoot.TryGetValue(key, out List<EnemyCardReplayProjection>? units) || units.Count == 0)
            {
                localDiagnostics.Add($"公开卡牌 {key} 缺少对应的实时投影单元。");
            }
            else
            {
                BuildCardEffects(key, units, effects, localDiagnostics);
            }

            if (localDiagnostics.Count > 0)
            {
                diagnostics.AddRange(localDiagnostics);
                effects.Add(new EnemyUnknownPresentation(string.Join(" | ", localDiagnostics)));
            }

            presentations.Add(new EnemyCardIntentPresentation(key, card, effects));
        }

        bool requiresGlobalUnknown = !projection.IsComplete || hasOrphanUnit;
        if (!projection.IsComplete && projection.Diagnostics.Count == 0)
        {
            diagnostics.Add("实时投影被标记为不完整，但没有提供具体诊断。");
        }

        return new EnemyCardListPresentation(presentations, requiresGlobalUnknown, diagnostics);
    }

    /// <summary>
    /// 按首次攻击基础值顺序归并一个根来源的全部 DFS 单元，并追加固定类别顺序的展示项。
    /// </summary>
    /// <param name="rootKey">当前公开根来源实例键。</param>
    /// <param name="units">严格保持投影 DFS 顺序的关联单元。</param>
    /// <param name="effects">接收固定类别顺序结果的集合。</param>
    /// <param name="diagnostics">接收只污染当前卡牌的诊断集合。</param>
    private static void BuildCardEffects(
        EnemyCardInstanceKey rootKey,
        IReadOnlyList<EnemyCardReplayProjection> units,
        ICollection<EnemyCardEffectIntentPresentation> effects,
        ICollection<string> diagnostics)
    {
        List<decimal> attackOrder = [];
        Dictionary<decimal, int> hitCounts = [];
        bool hasDefense = false;
        bool hasBuff = false;
        bool hasDebuff = false;

        foreach (EnemyCardReplayProjection unit in units)
        {
            if (!ValidateUnitIdentity(rootKey, unit, diagnostics))
            {
                continue;
            }

            if (unit.EnemyBlockDelta > decimal.Zero)
            {
                hasDefense = true;
            }
            else if (unit.EnemyBlockDelta < decimal.Zero)
            {
                diagnostics.Add($"根来源 {rootKey} 的单元包含无法映射的负敌人格挡变化。");
            }

            if (unit.EnemyPowerDeltas is null)
            {
                diagnostics.Add($"根来源 {rootKey} 的单元缺少敌人 Power 投影字典。");
            }
            else if (unit.EnemyPowerDeltas.Values.Any(delta => delta != decimal.Zero))
            {
                hasBuff = true;
            }

            IReadOnlyList<decimal>? canonicalHits = ReadCanonicalDamageHits(rootKey, unit, diagnostics, ref hasDebuff);
            if (canonicalHits is null)
            {
                continue;
            }

            foreach (decimal baseDamage in canonicalHits)
            {
                if (!hitCounts.TryAdd(baseDamage, 1))
                {
                    hitCounts[baseDamage]++;
                    continue;
                }

                attackOrder.Add(baseDamage);
            }
        }

        foreach (decimal baseDamage in attackOrder)
        {
            effects.Add(new EnemyAttackPresentation(baseDamage, hitCounts[baseDamage]));
        }

        if (hasDefense)
        {
            effects.Add(new EnemyDefendPresentation());
        }

        if (hasBuff)
        {
            effects.Add(new EnemyBuffPresentation());
        }

        if (hasDebuff)
        {
            effects.Add(new EnemyDebuffPresentation());
        }
    }

    /// <summary>
    /// 验证投影单元仍保留合法根来源、实际执行身份和非负重放索引。
    /// </summary>
    /// <param name="rootKey">当前分组的公开根来源键。</param>
    /// <param name="unit">待验证的 DFS 投影单元。</param>
    /// <param name="diagnostics">接收局部结构诊断的集合。</param>
    /// <returns>身份字段可安全继续读取时为 <see langword="true"/>。</returns>
    private static bool ValidateUnitIdentity(
        EnemyCardInstanceKey rootKey,
        EnemyCardReplayProjection unit,
        ICollection<string> diagnostics)
    {
        if (unit.RootSourceKey != rootKey)
        {
            diagnostics.Add($"投影单元根来源 {unit.RootSourceKey} 与分组来源 {rootKey} 不一致。");
            return false;
        }

        if (unit.ExecutingCardKey is null || !unit.ExecutingCardId.IsValid || unit.ReplayIndex < 0)
        {
            diagnostics.Add($"根来源 {rootKey} 的投影单元包含非法执行身份或重放索引。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 验证所有目标具有相同的标准攻击基础命中结构，并返回不会按玩家数重复计数的首目标序列。
    /// </summary>
    /// <param name="rootKey">用于错误定位的公开根来源键。</param>
    /// <param name="unit">待读取的单次执行投影。</param>
    /// <param name="diagnostics">接收局部伤害或目标结构诊断的集合。</param>
    /// <param name="hasDebuff">接收玩家目标是否包含 Power 变化。</param>
    /// <returns>合法时返回首目标的基础命中序列；非法时返回空引用。</returns>
    private static IReadOnlyList<decimal>? ReadCanonicalDamageHits(
        EnemyCardInstanceKey rootKey,
        EnemyCardReplayProjection unit,
        ICollection<string> diagnostics,
        ref bool hasDebuff)
    {
        if (unit.Targets is null)
        {
            diagnostics.Add($"根来源 {rootKey} 的投影单元缺少玩家目标集合。");
            return null;
        }

        if (unit.Targets.Count == 0)
        {
            return [];
        }

        HashSet<string> targetIds = new(StringComparer.Ordinal);
        decimal[]? canonical = null;
        foreach (EnemyTargetProjection target in unit.Targets)
        {
            if (target is null || string.IsNullOrWhiteSpace(target.TargetId) || !targetIds.Add(target.TargetId))
            {
                diagnostics.Add($"根来源 {rootKey} 的投影单元包含空目标或重复目标标识。");
                return null;
            }

            if (target.PowerDeltas is null || target.DamageHits is null)
            {
                diagnostics.Add($"根来源 {rootKey} 的目标 {target.TargetId} 缺少 Power 或伤害投影。");
                return null;
            }

            if (target.PowerDeltas.Values.Any(delta => delta != decimal.Zero))
            {
                hasDebuff = true;
            }

            EnemyDamageHitProjection[] hits = target.DamageHits.ToArray();
            if (hits.Any(hit => hit is null || hit.BaseDamage <= decimal.Zero || hit.ProjectedDamage < decimal.Zero))
            {
                diagnostics.Add($"根来源 {rootKey} 的目标 {target.TargetId} 包含非法标准攻击命中。");
                return null;
            }

            decimal[] current = hits.Select(hit => hit.BaseDamage).ToArray();
            if (canonical is null)
            {
                canonical = current;
            }
            else if (!canonical.SequenceEqual(current))
            {
                diagnostics.Add($"根来源 {rootKey} 的各玩家目标具有不一致的基础攻击命中结构。");
                return null;
            }
        }

        return canonical ?? [];
    }
}
