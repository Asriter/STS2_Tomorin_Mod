using STS2_Tomorin_Mod.Enemy.CardIntents.Test;

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
        EnemyCardScoreContext? scoreContext = null)
    {
        RandomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        ScoreContext = scoreContext ?? EnemyCardScoreContext.Identity;
    }

    /// <summary>获取唯一战斗随机源。</summary>
    public IEnemyCardRandomSource RandomSource { get; }

    /// <summary>获取准备时当前战斗评分投影。</summary>
    public EnemyCardScoreContext ScoreContext { get; }
}

/// <summary>
/// 按指标配方从左到右抽取实例，并以事务候选实现双软锁和最后候选强制提交。
/// </summary>
public sealed class EnemyActionMetricPlanner
{
    private readonly CardIntentTestRules _rules;
    private readonly EnemyCardScoreCalculator _scoreCalculator;
    private readonly EnemyPreparedResolutionPlanner _resolutionPlanner = new();

    /// <summary>
    /// 创建行动指标规划器。
    /// </summary>
    /// <param name="rules">不可变指标与软锁规则。</param>
    /// <param name="scoreCalculator">一次本体直接贡献评分器。</param>
    public EnemyActionMetricPlanner(
        CardIntentTestRules rules,
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

        if (state.CurrentCards.Count > 0)
        {
            throw new InvalidOperationException("准备新行动前当前指标牌区必须为空。");
        }

        for (int attempt = 1; attempt <= _rules.MaxCandidateAttempts; attempt++)
        {
            EnemyCardPlanningStateSnapshot candidate = state.CreatePlanningSnapshot();
            EnemyActionRecipe recipe = SelectRecipe(state.LastMetric, context.RandomSource);
            IReadOnlyList<BaseEnemyCard> selected = FillRecipe(candidate, recipe, context.RandomSource);
            EnemyCardScore score = _scoreCalculator.Calculate(selected, context.ScoreContext);
            bool overLock = score.Attack > _rules.AttackLock || score.Total > _rules.TotalScoreLock;
            bool isFinalAttempt = attempt == _rules.MaxCandidateAttempts;
            if (overLock && !isFinalAttempt)
            {
                continue;
            }

            EnemyPreparedPlanningState resolutionTransaction = state.CreatePreparedPlanningState(candidate);
            PreparedEnemyCardSource[] sources = state.RetainedCards.Concat(selected)
                .Select(card => _resolutionPlanner.PlanSource(
                    card,
                    checked(resolutionTransaction.GetReplayCount(card.InstanceKey) + 1),
                    resolutionTransaction,
                    context.RandomSource,
                    _rules.StepLimit))
                .ToArray();
            EnemySoftLockDiagnostic diagnostic = new(
                score,
                _rules.AttackLock,
                _rules.TotalScoreLock,
                attempt,
                attempt - 1,
                overLock && isFinalAttempt);
            PreparedEnemyCardAction action = new(
                recipe.Metric,
                state.RetainedCards,
                selected,
                sources,
                diagnostic);
            state.CommitPreparedAction(candidate, action);
            return action;
        }

        throw new InvalidOperationException("候选循环未能在配置上限内提交行动。");
    }

    /// <summary>
    /// 从首回合全集或排除 LastMetric 的后续集合中等概率选择配方。
    /// </summary>
    /// <param name="lastMetric">上次已提交指标；首回合为空。</param>
    /// <param name="randomSource">战斗随机源。</param>
    /// <returns>本候选使用的指标配方。</returns>
    private EnemyActionRecipe SelectRecipe(
        EnemyActionMetric? lastMetric,
        IEnemyCardRandomSource randomSource)
    {
        EnemyActionRecipe[] available = _rules.Recipes.Values
            .Where(recipe => lastMetric is null || recipe.Metric != lastMetric.Value)
            .OrderBy(recipe => recipe.Metric)
            .ToArray();
        if (available.Length == 0)
        {
            throw new InvalidOperationException("后续回合排除 LastMetric 后没有可选行动指标。");
        }

        return available[randomSource.NextIndex(available.Length)];
    }

    /// <summary>
    /// 严格按配方左到右抽取牌，缺少匹配时从非空抽牌堆随机兜底。
    /// </summary>
    /// <param name="candidate">本候选独占的牌区事务副本。</param>
    /// <param name="recipe">固定槽位配方。</param>
    /// <param name="randomSource">战斗随机源。</param>
    /// <returns>按槽位顺序选中的互异实例。</returns>
    private static IReadOnlyList<BaseEnemyCard> FillRecipe(
        EnemyCardPlanningStateSnapshot candidate,
        EnemyActionRecipe recipe,
        IEnemyCardRandomSource randomSource)
    {
        List<BaseEnemyCard> selected = new(recipe.Slots.Count);
        foreach (EnemyCardTag? slot in recipe.Slots)
        {
            EnsureDrawAvailable(candidate, randomSource);
            if (candidate.DrawPile.Count == 0)
            {
                continue;
            }

            int[] matchingIndices = slot is null
                ? Enumerable.Range(0, candidate.DrawPile.Count).ToArray()
                : candidate.DrawPile
                    .Select((card, index) => (card, index))
                    .Where(pair => (pair.card.Definition.Tags & slot.Value) != EnemyCardTag.None)
                    .Select(pair => pair.index)
                    .ToArray();
            int[] eligibleIndices = matchingIndices.Length > 0
                ? matchingIndices
                : Enumerable.Range(0, candidate.DrawPile.Count).ToArray();
            int chosenIndex = eligibleIndices[randomSource.NextIndex(eligibleIndices.Length)];
            BaseEnemyCard chosen = candidate.DrawPile[chosenIndex];
            candidate.DrawPile.RemoveAt(chosenIndex);
            candidate.CurrentCards.Add(chosen);
            selected.Add(chosen);
        }

        return Array.AsReadOnly(selected.ToArray());
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

}
