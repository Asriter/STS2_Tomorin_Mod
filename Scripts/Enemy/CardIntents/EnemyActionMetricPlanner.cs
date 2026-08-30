using MegaCrit.Sts2.Core.Entities.Cards;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 保存一次行动准备所需的唯一战斗随机源与当前战斗评分投影。
/// </summary>
public sealed class EnemyPlanningContext
{
    /// <summary>
    /// 创建行动准备上下文。
    /// </summary>
    /// <param name="randomSource">只允许推进战斗 RNG 的唯一入口。</param>
    /// <param name="scoreContext">可选准备时当前战斗评分投影。</param>
    public EnemyPlanningContext(
        IEnemyCardRandomSource randomSource,
        EnemyCardScoreContext? scoreContext = null,
        Func<EnemyCardCombatState, IEnemyCardRandomSource, EnemyPreparationCycle>? createPreparationCycle = null)
    {
        RandomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        ScoreContext = scoreContext ?? EnemyCardScoreContext.Identity;
        CreatePreparationCycle = createPreparationCycle ??
            (static (_, _) => new EnemyPreparationCycle(
                frozenPreparationCollection: null,
                delta: EnemyPreparedPreActionInventoryDelta.Empty));
    }

    /// <summary>获取唯一战斗随机源。</summary>
    public IEnemyCardRandomSource RandomSource { get; }

    /// <summary>获取准备时当前战斗评分投影。</summary>
    public EnemyCardScoreContext ScoreContext { get; }

    /// <summary>获取一次准备调用只能执行一次的收藏品周期工厂。</summary>
    public Func<EnemyCardCombatState, IEnemyCardRandomSource, EnemyPreparationCycle> CreatePreparationCycle { get; }
}

/// <summary>
/// 按指标配方从左到右抽取实例，并以事务候选实现双软锁和最后候选强制提交。
/// </summary>
public sealed class EnemyActionMetricPlanner
{
    private readonly EnemyCardPlanningRules _rules;
    private readonly EnemyCardScoreCalculator _scoreCalculator;
    private readonly EnemyPreparedResolutionPlanner _resolutionPlanner = new();

    /// <summary>
    /// 创建行动指标规划器。
    /// </summary>
    /// <param name="rules">不可变指标与软锁规则。</param>
    /// <param name="scoreCalculator">一次本体直接贡献评分器。</param>
    public EnemyActionMetricPlanner(
        EnemyCardPlanningRules rules,
        EnemyCardScoreCalculator scoreCalculator)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _scoreCalculator = scoreCalculator ?? throw new ArgumentNullException(nameof(scoreCalculator));
    }

    /// <summary>
    /// 从权威状态准备并提交一项冻结行动；拒绝候选只保留 RNG 推进。
    /// </summary>
    /// <param name="state">五牌区唯一权威状态。</param>
    /// <param name="context">当前战斗随机源和准备时评分投影。</param>
    /// <returns>已原子写入状态的冻结行动。</returns>
    public PreparedEnemyCardAction Prepare(
        EnemyCardCombatState state,
        EnemyPlanningContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        if (state.PreparedAction is not null || state.RuntimePhase != EnemyCardRuntimePhase.Idle)
        {
            throw new InvalidOperationException("只有 Idle 且不存在冻结行动的状态才能准备新行动。");
        }

        if (state.FrozenPreparationCollection is not null || state.FrozenPreparationDelta is not null)
        {
            throw new InvalidOperationException("当前状态已经冻结准备周期，不能再次调用准备工厂。");
        }

        if (state.CurrentCards.Count > 0)
        {
            throw new InvalidOperationException("准备新行动前当前指标牌区必须为空。");
        }

        EnemyPreparationCycle preparationCycle = context.CreatePreparationCycle(state, context.RandomSource) ??
                                                  throw new InvalidOperationException("准备周期工厂返回了空对象。");
        state.StorePreparationCycle(preparationCycle);
        List<string> attemptDiagnostics = [];
        bool encounteredIncomplete = false;

        for (int attempt = 1; attempt <= _rules.MaxCandidateAttempts; attempt++)
        {
            EnemyCardPlanningStateSnapshot candidate = state.CreatePlanningSnapshot();
            EnemyActionRecipe recipe = SelectRecipe(state.LastMetric, context.RandomSource);
            RecipeFillResult fill = FillRecipe(candidate, recipe, context.RandomSource);
            if (!fill.IsComplete)
            {
                encounteredIncomplete = true;
                attemptDiagnostics.Add(
                    $"第 {attempt} 次候选 {recipe.Metric} 结构不完整：{fill.Diagnostic}");
                continue;
            }

            IReadOnlyList<BaseEnemyCard> selected = fill.Selected;
            EnemyCardScore score = _scoreCalculator.Calculate(selected, context.ScoreContext);
            bool overLock = score.Attack > _rules.StaticLocks.Attack ||
                            score.Total > _rules.StaticLocks.Total;
            bool isFinalAttempt = attempt == _rules.MaxCandidateAttempts;
            if (overLock && !isFinalAttempt)
            {
                attemptDiagnostics.Add(
                    $"第 {attempt} 次候选 {recipe.Metric} 超锁：Attack={score.Attack}, Total={score.Total}。");
                continue;
            }

            EnemyPreparedPlanningState resolutionTransaction = state.CreatePreparedPlanningState(
                candidate,
                preparationCycle.Delta);
            EnemyEffectiveCardLedger effectiveCardLedger = new();
            PreparedEnemyCardSource[] sources = state.RetainedCards.Concat(selected)
                .Select(card => _resolutionPlanner.PlanSource(
                    card,
                    checked(resolutionTransaction.GetReplayCount(card.InstanceKey) + 1),
                    resolutionTransaction,
                    context.RandomSource,
                    _rules.StepLimit,
                    effectiveCardLedger))
                .ToArray();
            EnemySoftLockDiagnostic diagnostic = new(
                score,
                _rules.StaticLocks.Attack,
                _rules.StaticLocks.Total,
                attempt,
                attempt - 1,
                overLock && isFinalAttempt);
            PreparedEnemyCardAction action = new(
                recipe.Metric,
                state.RetainedCards,
                selected,
                sources,
                diagnostic,
                preparationCycle.Delta,
                effectiveCardLedger.States.Values);
            state.CommitPreparedAction(candidate, action);
            return action;
        }

        if (encounteredIncomplete)
        {
            string faultDiagnostic =
                $"候选上限内存在结构不完整且没有行动提交。{string.Join(" ", attemptDiagnostics)}";
            state.MarkFault(faultDiagnostic);
            throw new EnemyCandidatePlanningException(faultDiagnostic);
        }

        throw new InvalidOperationException("候选循环未能在配置上限内提交行动。");
    }

    /// <summary>
    /// 从首回合全集或排除 LastMetric 的后续集合中按正整数权重选择配方。
    /// </summary>
    /// <param name="lastMetric">上次已提交指标；首回合为空。</param>
    /// <param name="randomSource">战斗随机源。</param>
    /// <returns>本候选使用的指标配方。</returns>
    private EnemyActionRecipe SelectRecipe(
        EnemyActionMetric? lastMetric,
        IEnemyCardRandomSource randomSource)
    {
        EnemyWeightedActionRecipe[] available = _rules.WeightedRecipes
            .Where(weightedRecipe =>
                lastMetric is null || weightedRecipe.Recipe.Metric != lastMetric.Value)
            .OrderBy(weightedRecipe => weightedRecipe.Recipe.Metric)
            .ToArray();
        if (available.Length == 0)
        {
            throw new InvalidOperationException("后续回合排除 LastMetric 后没有可选行动指标。");
        }

        int selectedWeight = randomSource.NextIndex(checked(available.Sum(recipe => recipe.Weight)));
        foreach (EnemyWeightedActionRecipe weightedRecipe in available)
        {
            if (selectedWeight < weightedRecipe.Weight)
            {
                return weightedRecipe.Recipe;
            }

            selectedWeight -= weightedRecipe.Weight;
        }

        throw new InvalidOperationException("加权行动指标选择未能解析有效配方。");
    }

    /// <summary>
    /// 严格按配方左到右抽取牌，缺少匹配时从非空抽牌堆随机兜底。
    /// </summary>
    /// <param name="candidate">本候选独占的牌区事务副本。</param>
    /// <param name="recipe">固定槽位配方。</param>
    /// <param name="randomSource">战斗随机源。</param>
    /// <returns>按槽位顺序选中的互异实例。</returns>
    private static RecipeFillResult FillRecipe(
        EnemyCardPlanningStateSnapshot candidate,
        EnemyActionRecipe recipe,
        IEnemyCardRandomSource randomSource)
    {
        List<BaseEnemyCard> selected = new(recipe.Slots.Count);
        List<string> diagnostics = [];
        ComposeMaterialBindingState materialBindings = new(recipe.EnforceComposeMaterialBindings);
        for (int slotIndex = 0; slotIndex < recipe.Slots.Count; slotIndex++)
        {
            EnemyActionSlotRule slot = recipe.Slots[slotIndex];
            CardType? requiredMaterialType = null;
            if (slot.MustMatchSelectedComposeMaterial &&
                !materialBindings.TryPeek(out requiredMaterialType))
            {
                diagnostics.Add($"槽位 {slotIndex} 没有待绑定的 Compose request 单位。");
                continue;
            }

            EnsureDrawAvailable(candidate, randomSource);
            if (candidate.DrawPile.Count == 0)
            {
                diagnostics.Add($"槽位 {slotIndex} 无剩余牌，固定槽位不可省略。");
                continue;
            }

            int[] hardEligibleIndices = candidate.DrawPile
                .Select((card, index) => (card, index))
                .Where(pair => IsHardEligible(
                    pair.card,
                    slot,
                    selected,
                    recipe.Constraints,
                    requiredMaterialType))
                .Select(pair => pair.index)
                .ToArray();
            if (hardEligibleIndices.Length == 0)
            {
                diagnostics.Add($"槽位 {slotIndex} 没有满足定义、素材与候选约束的牌。");
                continue;
            }

            int[] matchingIndices = slot.RequiredTag is null
                ? hardEligibleIndices
                : hardEligibleIndices
                    .Where(index =>
                        (candidate.DrawPile[index].Definition.Tags & slot.RequiredTag.Value) != EnemyCardTag.None)
                    .ToArray();
            int[] eligibleIndices = matchingIndices.Length > 0 ? matchingIndices : hardEligibleIndices;
            int chosenIndex = eligibleIndices[randomSource.NextIndex(eligibleIndices.Length)];
            BaseEnemyCard chosen = candidate.DrawPile[chosenIndex];
            candidate.DrawPile.RemoveAt(chosenIndex);
            candidate.CurrentCards.Add(chosen);
            selected.Add(chosen);
            if (slot.MustMatchSelectedComposeMaterial)
            {
                materialBindings.Consume();
            }
            else
            {
                materialBindings.RegisterSource(chosen);
            }
        }

        if (materialBindings.HasPending)
        {
            diagnostics.Add($"候选结束时仍有 {materialBindings.PendingCount} 个 Compose request 单位未绑定。");
        }

        return new RecipeFillResult(
            Array.AsReadOnly(selected.ToArray()),
            diagnostics.Count == 0,
            string.Join(" ", diagnostics));
    }

    /// <summary>
    /// DefinitionId、Compose 素材请求与候选数量约束都是硬资格，随机兜底不得绕过。
    /// </summary>
    private static bool IsHardEligible(
        BaseEnemyCard card,
        EnemyActionSlotRule slot,
        IReadOnlyList<BaseEnemyCard> selected,
        EnemyCandidateConstraints constraints,
        CardType? requiredMaterialType)
    {
        if (slot.AllowedDefinitionIds is not null && !slot.AllowedDefinitionIds.Contains(card.CardId))
        {
            return false;
        }

        if (slot.MustMatchSelectedComposeMaterial && card.CardModel.Type != requiredMaterialType)
        {
            return false;
        }

        return IsWithinConstraints(selected.Append(card), constraints);
    }

    /// <summary>验证加入一张牌后的 Compose 结构计数没有超过阶段配方上限。</summary>
    private static bool IsWithinConstraints(
        IEnumerable<BaseEnemyCard> cards,
        EnemyCandidateConstraints constraints)
    {
        BaseEnemyCard[] snapshot = cards.ToArray();
        int composeSources = snapshot.Count(IsComposeSource);
        int immediateAttackComposeSources = snapshot.Count(card =>
            IsComposeSource(card) &&
            card.CardModel.Type == CardType.Attack);
        int composeSourcesProducingImmediateAttack = snapshot.Count(card =>
            IsComposeSource(card) &&
            (card.EffectClasses & EnemyCardEffectClass.ImmediateAttackProducer) != EnemyCardEffectClass.None);
        return composeSources <= constraints.MaxComposeSources &&
               immediateAttackComposeSources <= constraints.MaxImmediateAttackComposeSources &&
               composeSourcesProducingImmediateAttack <= constraints.MaxComposeSourcesProducingImmediateAttack;
    }

    private static bool IsComposeSource(BaseEnemyCard card) =>
        card.Definition.MaterialRequests.Any(request => request.PaymentKind == EnemyMaterialPaymentKind.Compose);

    /// <summary>按来源、request、requirement 与 Count 顺序保存尚未绑定的 Compose 素材单位。</summary>
    private sealed class ComposeMaterialBindingState
    {
        private readonly bool _enabled;
        private readonly Queue<CardType> _pending = new();

        public ComposeMaterialBindingState(bool enabled)
        {
            _enabled = enabled;
        }

        public bool HasPending => _pending.Count > 0;
        public int PendingCount => _pending.Count;

        public bool TryPeek(out CardType? cardType)
        {
            if (_pending.TryPeek(out CardType required))
            {
                cardType = required;
                return true;
            }

            cardType = null;
            return false;
        }

        public void Consume() => _pending.Dequeue();

        public void RegisterSource(BaseEnemyCard source)
        {
            if (!_enabled)
            {
                return;
            }

            foreach (EnemyMaterialRequest request in source.Definition.MaterialRequests)
            {
                if (request.PaymentKind != EnemyMaterialPaymentKind.Compose)
                {
                    continue;
                }

                foreach (EnemyMaterialRequirement requirement in request.Requirements)
                {
                    CardType required = requirement.CardType ?? throw new InvalidOperationException(
                        "Compose request 的需求必须具有确定 CardType。");
                    for (int count = 0; count < requirement.Count; count++)
                    {
                        _pending.Enqueue(required);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 仅当抽牌堆为空时把弃牌堆回洗；非空抽牌堆绝不为匹配标签提前回洗。
    /// </summary>
    /// <param name="candidate">候选事务副本。</param>
    /// <param name="randomSource">战斗随机源。</param>
    private static void EnsureDrawAvailable(
        EnemyCardPlanningStateSnapshot candidate,
        IEnemyCardRandomSource randomSource)
    {
        if (candidate.DrawPile.Count > 0 || candidate.DiscardPile.Count == 0)
        {
            return;
        }

        candidate.DrawPile.AddRange(candidate.DiscardPile);
        candidate.DiscardPile.Clear();
        for (int index = candidate.DrawPile.Count - 1; index > 0; index--)
        {
            int swapIndex = randomSource.NextIndex(index + 1);
            (candidate.DrawPile[index], candidate.DrawPile[swapIndex]) =
                (candidate.DrawPile[swapIndex], candidate.DrawPile[index]);
        }
    }

    private sealed record RecipeFillResult(
        IReadOnlyList<BaseEnemyCard> Selected,
        bool IsComplete,
        string Diagnostic);

}
