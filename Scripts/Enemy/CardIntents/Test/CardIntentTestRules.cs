using System.Collections.ObjectModel;

namespace STS2_Tomorin_Mod.Enemy.CardIntents.Test;

/// <summary>
/// 集中保存测试敌人行动指标配方、双软锁与候选次数上限。
/// </summary>
public sealed class CardIntentTestRules : EnemyCardPlanningRules
{
    /// <summary>
    /// 创建一组不可变测试敌人规划规则。
    /// </summary>
    /// <param name="attackLock">攻击伤害软锁。</param>
    /// <param name="totalScoreLock">总评分软锁。</param>
    /// <param name="maxCandidateAttempts">候选评估上限；最后一次强制提交。</param>
    /// <param name="recipes">每种指标唯一且非空的槽位配方。</param>
    /// <param name="stepLimit">准备模拟与实际结算共享的有限步骤上限。</param>
    /// <param name="initialStarStoneCount">新战斗初始化时追加的星石数量。</param>
    public CardIntentTestRules(
        decimal attackLock,
        decimal totalScoreLock,
        int maxCandidateAttempts,
        IEnumerable<EnemyActionRecipe> recipes,
        int stepLimit = 256,
        int initialStarStoneCount = 5)
        : this(new TestRuleInput(
            attackLock,
            totalScoreLock,
            maxCandidateAttempts,
            CopyRecipes(recipes),
            stepLimit,
            initialStarStoneCount))
    {
    }

    private CardIntentTestRules(TestRuleInput input)
        : base(
            CreateLimits(input.AttackLock, input.TotalScoreLock),
            CreateLimits(input.AttackLock, input.TotalScoreLock),
            input.MaxCandidateAttempts,
            input.StepLimit,
            input.Recipes.Select(recipe => new EnemyWeightedActionRecipe(recipe, 1)))
    {
        if (input.InitialStarStoneCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.InitialStarStoneCount),
                "初始星石数量不能为负数。 ");
        }

        AttackLock = input.AttackLock;
        TotalScoreLock = input.TotalScoreLock;
        InitialStarStoneCount = input.InitialStarStoneCount;
        Recipes = new ReadOnlyDictionary<EnemyActionMetric, EnemyActionRecipe>(
            input.Recipes.ToDictionary(recipe => recipe.Metric));
    }

    /// <summary>获取设计确认的测试敌人默认规则。</summary>
    public static CardIntentTestRules Default { get; } = new(
        attackLock: 80m,
        totalScoreLock: 100m,
        maxCandidateAttempts: 3,
        recipes:
        [
            new EnemyActionRecipe(
                EnemyActionMetric.Gain,
                [EnemyCardTag.Ability, EnemyCardTag.Gain, EnemyCardTag.Defense]),
            new EnemyActionRecipe(
                EnemyActionMetric.Attack,
                [EnemyCardTag.Attack, EnemyCardTag.Attack, null, null]),
            new EnemyActionRecipe(
                EnemyActionMetric.ComposeTest,
                [EnemyCardTag.CollectionGenerator, EnemyCardTag.Defense, EnemyCardTag.Compose])
        ]);

    /// <summary>获取攻击伤害软锁。</summary>
    public decimal AttackLock { get; }

    /// <summary>获取总评分软锁。</summary>
    public decimal TotalScoreLock { get; }

    /// <summary>获取新战斗初始化时追加的星石数量。</summary>
    public int InitialStarStoneCount { get; }

    /// <summary>获取按指标索引的不可修改配方集合。</summary>
    public IReadOnlyDictionary<EnemyActionMetric, EnemyActionRecipe> Recipes { get; }

    /// <summary>
    /// 使用默认锁和候选上限创建仅含指定配方的领域测试规则。
    /// </summary>
    /// <param name="recipe">唯一待测试配方。</param>
    /// <returns>不复制任何固定阈值到测试断言的规则对象。</returns>
    public static CardIntentTestRules ForTesting(EnemyActionRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new CardIntentTestRules(
            Default.AttackLock,
            Default.TotalScoreLock,
            Default.MaxCandidateAttempts,
            [recipe]);
    }

    private static EnemySoftLockLimits CreateLimits(decimal attackLock, decimal totalScoreLock)
    {
        if (attackLock < decimal.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(attackLock), "攻击软锁不能为负数。");
        }

        if (totalScoreLock < decimal.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalScoreLock), "总评分软锁不能为负数。");
        }

        return new EnemySoftLockLimits(attackLock, totalScoreLock);
    }

    private static EnemyActionRecipe[] CopyRecipes(IEnumerable<EnemyActionRecipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        return recipes.ToArray();
    }

    private sealed record TestRuleInput(
        decimal AttackLock,
        decimal TotalScoreLock,
        int MaxCandidateAttempts,
        EnemyActionRecipe[] Recipes,
        int StepLimit,
        int InitialStarStoneCount);
}
