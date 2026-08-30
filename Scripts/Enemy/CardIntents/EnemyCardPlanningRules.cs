namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 集中保存来源牌静态评分使用的不可变权重。
/// </summary>
public static class EnemyCardScoreWeights
{
    public const decimal Block = 0.65m;
    public const decimal Strength = 10m;
    public const decimal Dexterity = 6m;
    public const decimal HeartWall = 3m;
    public const decimal OtherPersistentPower = 5m;
    public const decimal Vulnerable = 6m;
    public const decimal OtherDebuff = 3m;
    public const decimal NormalCollection = 3m;
    public const decimal StarStone = 5m;
    public const decimal DeferredTokenHint = 0.5m;
}

/// <summary>
/// 保存攻击和总分两个软锁上限。
/// </summary>
public sealed record EnemySoftLockLimits(decimal Attack, decimal Total);

/// <summary>
/// 将行动指标配方与其正整数选择权重绑定。
/// </summary>
public sealed record EnemyWeightedActionRecipe
{
    public EnemyWeightedActionRecipe(EnemyActionRecipe recipe, int weight)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        Weight = weight > 0 ? weight : throw new ArgumentOutOfRangeException(nameof(weight));
    }

    public EnemyActionRecipe Recipe { get; }
    public int Weight { get; }
}

/// <summary>
/// 提供敌人卡牌行动规划共享的锁、候选和配方配置。
/// </summary>
public class EnemyCardPlanningRules
{
    public EnemyCardPlanningRules(
        EnemySoftLockLimits staticLocks,
        EnemySoftLockLimits fullLocks,
        int maxCandidateAttempts,
        int stepLimit,
        IEnumerable<EnemyWeightedActionRecipe> recipes)
    {
        StaticLocks = staticLocks ?? throw new ArgumentNullException(nameof(staticLocks));
        FullLocks = fullLocks ?? throw new ArgumentNullException(nameof(fullLocks));
        MaxCandidateAttempts = maxCandidateAttempts > 0
            ? maxCandidateAttempts
            : throw new ArgumentOutOfRangeException(nameof(maxCandidateAttempts));
        StepLimit = stepLimit > 0
            ? stepLimit
            : throw new ArgumentOutOfRangeException(nameof(stepLimit));
        EnemyWeightedActionRecipe[] copied = (recipes ?? throw new ArgumentNullException(nameof(recipes))).ToArray();
        if (copied.Length == 0 ||
            copied.Any(recipe => recipe is null) ||
            copied.Select(recipe => recipe.Recipe.Metric).Distinct().Count() != copied.Length)
        {
            throw new ArgumentException("每个行动指标必须恰好注册一项正权重配方。", nameof(recipes));
        }

        long totalWeight = 0;
        foreach (EnemyWeightedActionRecipe recipe in copied)
        {
            totalWeight = checked(totalWeight + recipe.Weight);
            if (totalWeight > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recipes),
                    "行动指标配方的总权重不能超过随机源可接受的 Int32 上界。");
            }
        }

        WeightedRecipes = Array.AsReadOnly(copied);
    }

    public EnemySoftLockLimits StaticLocks { get; }
    public EnemySoftLockLimits FullLocks { get; }
    public int MaxCandidateAttempts { get; }
    public int StepLimit { get; }
    public IReadOnlyList<EnemyWeightedActionRecipe> WeightedRecipes { get; }
}
