using MegaCrit.Sts2.Core.Models.Powers;
using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>允许准备规划器把效果节点的随机收藏品选择冻结为显式步骤。</summary>
public interface IEnemyFrozenCollectionGenerationEffect
{
    IReadOnlyList<EnemyCollectionDefinition> FreezeCollections(
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource randomSource);
}

/// <summary>影灯正式目录复用的效果与条件构造入口。</summary>
public static class ShadowTomorinEffects
{
    public static IEnemyCardEffectNode DynamicHeartWallAttackAll(string programId) =>
        new ShadowDynamicHeartWallAttackAllEffect(programId);

    public static IEnemyCardPlayCondition RequireHeartWall(string programId, decimal required = 4m) =>
        new ShadowHeartWallAtLeastCondition(programId, required);

    public static IEnemyCardEffectNode ConsumeHeartWallGainStrength(
        string programId,
        decimal heartWall = 4m,
        decimal strength = 1m) =>
        new ShadowConsumeHeartWallGainStrengthEffect(programId, heartWall, strength);

    public static IEnemyCardEffectNode ConsumeAvailableCollections(
        string programId,
        int maximumCount = 3,
        decimal dexterityPerItem = 1m,
        decimal heartWallPerItem = 1m) =>
        new ShadowConsumeAvailableCollectionsEffect(
            programId,
            maximumCount,
            dexterityPerItem,
            heartWallPerItem);

    public static IEnemyCardPlayCondition RequireNonComposeSource(string programId) =>
        new ShadowHasNonComposeSourceCondition(programId);

    public static IEnemyCardEffectNode ConsumeNonComposeSource(string programId) =>
        new ShadowConsumeNonComposeSourceEffect(programId);

    public static IEnemyCardEffectNode ActivateUnwantedSixth(string programId, decimal stacks = 1m) =>
        new ShadowActivateUnwantedSixthEffect(programId, stacks);

    public static IEnemyCardEffectNode GenerateFrozenCollections(
        string programId,
        IReadOnlyList<(EnemyCollectionDefinition Definition, int Weight)> weightedPool,
        int count = 1) =>
        new ShadowFrozenCollectionGenerationEffect(programId, weightedPool, count);

    public static IEnemyCardEffectNode FrozenXAttackAll(
        string programId,
        decimal damage,
        int doubleAtDistinctExhaustDefinitionCount = 0) =>
        new EnemyFrozenXAttackAllEffect(
            programId,
            damage,
            doubleAtDistinctExhaustDefinitionCount);
}

public sealed class ShadowDynamicHeartWallAttackAllEffect : IEnemyCardEffectNode
{
    public ShadowDynamicHeartWallAttackAllEffect(string programId)
    {
        ProgramId = RequireProgramId(programId);
    }

    public string ProgramId { get; }

    public void Simulate(EnemyCardSimulationContext context) =>
        context.AddDamageToAll(9m + 3m * context.GetEnemyPowerAmount<AtFieldPower>());

    public Task ExecuteAsync(EnemyCardExecutionContext context) =>
        context.ExecuteAttackAllAsync(9m + 3m * context.GetEnemyPowerAmount<AtFieldPower>());

    private static string RequireProgramId(string value) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("效果 ProgramId 不能为空。", nameof(value));
}

public sealed class ShadowHeartWallAtLeastCondition : IEnemyCardPlayCondition
{
    public ShadowHeartWallAtLeastCondition(string programId, decimal required)
    {
        if (string.IsNullOrWhiteSpace(programId) || required <= decimal.Zero)
        {
            throw new ArgumentException("心之壁条件必须具有稳定标识和正需求。", nameof(programId));
        }

        ProgramId = programId;
        Required = required;
    }

    public string ProgramId { get; }
    public decimal Required { get; }

    public bool CanPlan(EnemyPreparedPlanningState state, BaseEnemyCard card) => true;
    public bool CanSimulate(EnemyCardSimulationContext context) =>
        context.GetEnemyPowerAmount<AtFieldPower>() >= Required;
    public bool CanExecute(EnemyCardExecutionContext context) =>
        context.GetEnemyPowerAmount<AtFieldPower>() >= Required;
}

public sealed class ShadowConsumeHeartWallGainStrengthEffect : IEnemyCardEffectNode
{
    public ShadowConsumeHeartWallGainStrengthEffect(string programId, decimal heartWall, decimal strength)
    {
        if (string.IsNullOrWhiteSpace(programId) || heartWall <= decimal.Zero || strength <= decimal.Zero)
        {
            throw new ArgumentException("心之壁转力量效果参数无效。", nameof(programId));
        }

        ProgramId = programId;
        HeartWall = heartWall;
        Strength = strength;
    }

    public string ProgramId { get; }
    public decimal HeartWall { get; }
    public decimal Strength { get; }

    public void Simulate(EnemyCardSimulationContext context)
    {
        context.AddEnemyPower(EnemyAbilityHookDispatcher.StablePowerId<AtFieldPower>(), -HeartWall);
        context.AddEnemyPower(EnemyAbilityHookDispatcher.StablePowerId<StrengthPower>(), Strength);
    }

    public async Task ExecuteAsync(EnemyCardExecutionContext context)
    {
        await context.ModifyEnemyPowerAsync<AtFieldPower>(-HeartWall);
        await context.ApplyEnemyPowerAsync<StrengthPower>(Strength);
    }
}

public sealed class ShadowConsumeAvailableCollectionsEffect : IEnemyCardEffectNode
{
    public ShadowConsumeAvailableCollectionsEffect(
        string programId,
        int maximumCount,
        decimal dexterityPerItem,
        decimal heartWallPerItem)
    {
        if (string.IsNullOrWhiteSpace(programId) || maximumCount < 1 ||
            dexterityPerItem < decimal.Zero || heartWallPerItem < decimal.Zero)
        {
            throw new ArgumentException("可选收藏品消费效果参数无效。", nameof(programId));
        }

        ProgramId = programId;
        MaximumCount = maximumCount;
        DexterityPerItem = dexterityPerItem;
        HeartWallPerItem = heartWallPerItem;
    }

    public string ProgramId { get; }
    public int MaximumCount { get; }
    public decimal DexterityPerItem { get; }
    public decimal HeartWallPerItem { get; }

    public void Simulate(EnemyCardSimulationContext context)
    {
        int count = context.ConsumeAvailableCollections(MaximumCount);
        if (count == 0)
        {
            return;
        }

        context.AddEnemyPower(
            EnemyAbilityHookDispatcher.StablePowerId<DexterityPower>(),
            count * DexterityPerItem);
        context.AddEnemyPower(
            EnemyAbilityHookDispatcher.StablePowerId<AtFieldPower>(),
            count * HeartWallPerItem);
    }

    public async Task ExecuteAsync(EnemyCardExecutionContext context)
    {
        int count = await context.ConsumeAvailableCollectionsAsync(MaximumCount);
        if (count == 0)
        {
            return;
        }

        await context.ApplyEnemyPowerAsync<DexterityPower>(count * DexterityPerItem);
        await context.ApplyEnemyPowerAsync<AtFieldPower>(count * HeartWallPerItem);
    }
}

public sealed class ShadowHasNonComposeSourceCondition : IEnemyCardPlayCondition
{
    public ShadowHasNonComposeSourceCondition(string programId)
    {
        ProgramId = !string.IsNullOrWhiteSpace(programId)
            ? programId
            : throw new ArgumentException("非 Compose 来源条件标识不能为空。", nameof(programId));
    }

    public string ProgramId { get; }
    public bool CanPlan(EnemyPreparedPlanningState state, BaseEnemyCard card) =>
        state.CurrentCards.Any(candidate =>
            candidate.InstanceKey != card.InstanceKey &&
            !candidate.Definition.Tags.HasFlag(EnemyCardTag.Compose) &&
            candidate.Definition.MaterialRequests.All(request =>
                request.PaymentKind != EnemyMaterialPaymentKind.Compose));
    public bool CanSimulate(EnemyCardSimulationContext context) => context.HasNonComposeSource();
    public bool CanExecute(EnemyCardExecutionContext context) => context.HasNonComposeSource();
}

public sealed class ShadowConsumeNonComposeSourceEffect : IEnemyCardEffectNode
{
    public ShadowConsumeNonComposeSourceEffect(string programId)
    {
        ProgramId = !string.IsNullOrWhiteSpace(programId)
            ? programId
            : throw new ArgumentException("非 Compose 来源消费标识不能为空。", nameof(programId));
    }

    public string ProgramId { get; }
    public void Simulate(EnemyCardSimulationContext context)
    {
        if (!context.TryConsumeFirstNonComposeSource())
        {
            context.MarkIncomplete("冻结行动缺少可消费的非 Compose 来源牌。");
        }
    }

    public Task ExecuteAsync(EnemyCardExecutionContext context) =>
        context.TryConsumeFirstNonComposeSource()
            ? Task.CompletedTask
            : Task.FromException(new InvalidOperationException("冻结行动缺少可消费的非 Compose 来源牌。"));
}

public sealed class ShadowActivateUnwantedSixthEffect : IEnemyCardEffectNode
{
    public ShadowActivateUnwantedSixthEffect(string programId, decimal stacks)
    {
        if (string.IsNullOrWhiteSpace(programId) || stacks <= decimal.Zero)
        {
            throw new ArgumentException("行动能力必须具有稳定标识和正层数。", nameof(programId));
        }

        ProgramId = programId;
        Stacks = stacks;
    }

    public string ProgramId { get; }
    public decimal Stacks { get; }
    public void Simulate(EnemyCardSimulationContext context) =>
        context.AddTransientAbility<CardIntentUnwantedSixthPower>(Stacks);
    public Task ExecuteAsync(EnemyCardExecutionContext context)
    {
        context.AddTransientAbility<CardIntentUnwantedSixthPower>(Stacks);
        return Task.CompletedTask;
    }
}

public sealed class ShadowFrozenCollectionGenerationEffect :
    IEnemyCardEffectNode,
    IEnemyFrozenCollectionGenerationEffect
{
    private readonly IReadOnlyList<(EnemyCollectionDefinition Definition, int Weight)> _weightedPool;

    public ShadowFrozenCollectionGenerationEffect(
        string programId,
        IReadOnlyList<(EnemyCollectionDefinition Definition, int Weight)> weightedPool,
        int count)
    {
        ArgumentNullException.ThrowIfNull(weightedPool);
        if (string.IsNullOrWhiteSpace(programId) || count < 1 || count > weightedPool.Count ||
            weightedPool.Any(item => item.Definition is null || item.Weight <= 0) ||
            weightedPool.Select(item => item.Definition.CollectionId).Distinct(StringComparer.Ordinal).Count() !=
            weightedPool.Count)
        {
            throw new ArgumentException("冻结收藏品生成器的标识、数量、权重或定义无效。", nameof(weightedPool));
        }

        ProgramId = programId;
        Count = count;
        _weightedPool = Array.AsReadOnly(weightedPool.ToArray());
    }

    public string ProgramId { get; }
    public int Count { get; }
    public void Simulate(EnemyCardSimulationContext context) { }
    public Task ExecuteAsync(EnemyCardExecutionContext context) => Task.CompletedTask;

    public IReadOnlyList<EnemyCollectionDefinition> FreezeCollections(
        EnemyPreparedPlanningState state,
        IEnemyCardRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(randomSource);
        List<(EnemyCollectionDefinition Definition, int Weight)> remaining = _weightedPool.ToList();
        List<EnemyCollectionDefinition> selected = [];
        for (int index = 0; index < Count; index++)
        {
            int totalWeight = checked(remaining.Sum(item => item.Weight));
            int roll = remaining.Count == 1 ? 0 : randomSource.NextIndex(totalWeight);
            int selectedIndex = 0;
            while (roll >= remaining[selectedIndex].Weight)
            {
                roll -= remaining[selectedIndex].Weight;
                selectedIndex++;
            }

            selected.Add(remaining[selectedIndex].Definition);
            remaining.RemoveAt(selectedIndex);
        }

        return selected.AsReadOnly();
    }
}
