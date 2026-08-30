using MegaCrit.Sts2.Core.Models;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 对全部有效玩家造成相同多段伤害，并共享真实执行与纯模拟语义。
/// </summary>
public sealed class EnemyAttackAllEffect : IEnemyCardEffectNode
{
    /// <summary>
    /// 创建全体攻击效果。
    /// </summary>
    /// <param name="programId">稳定效果程序标识。</param>
    /// <param name="damage">每次命中的规范基础伤害。</param>
    /// <param name="hitCount">独立命中次数。</param>
    public EnemyAttackAllEffect(string programId, decimal damage, int hitCount = 1)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            throw new ArgumentException("攻击效果必须具有稳定程序标识。", nameof(programId));
        }

        if (damage < decimal.Zero || hitCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(damage));
        }

        ProgramId = programId;
        Damage = damage;
        HitCount = hitCount;
    }

    /// <inheritdoc />
    public string ProgramId { get; }

    /// <summary>获取每次命中的基础伤害。</summary>
    public decimal Damage { get; }

    /// <summary>获取独立命中次数。</summary>
    public int HitCount { get; }

    /// <inheritdoc />
    public void Simulate(EnemyCardSimulationContext context) => context.AddDamageToAll(Damage, HitCount);

    /// <inheritdoc />
    public Task ExecuteAsync(EnemyCardExecutionContext context) => context.ExecuteAttackAllAsync(Damage, HitCount);
}

/// <summary>
/// 使用冻结有效牌元数据决定命中次数的全体 X 攻击。
/// </summary>
public sealed class EnemyFrozenXAttackAllEffect : IEnemyCardEffectNode
{
    public EnemyFrozenXAttackAllEffect(
        string programId,
        decimal damage,
        int doubleAtDistinctExhaustDefinitionCount = 0)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            throw new ArgumentException("X 攻击效果必须具有稳定程序标识。", nameof(programId));
        }

        if (damage < decimal.Zero || doubleAtDistinctExhaustDefinitionCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage));
        }

        ProgramId = programId;
        Damage = damage;
        DoubleAtDistinctExhaustDefinitionCount = doubleAtDistinctExhaustDefinitionCount;
    }

    /// <inheritdoc />
    public string ProgramId { get; }

    /// <summary>获取每次命中的规范基础伤害。</summary>
    public decimal Damage { get; }

    /// <summary>获取触发两倍次数所需的不同消耗牌定义数；零表示不翻倍。</summary>
    public int DoubleAtDistinctExhaustDefinitionCount { get; }

    /// <summary>在冻结 X 之前从候选事务只读解析倍率。</summary>
    public int ResolveMultiplier(EnemyPreparedPlanningState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (DoubleAtDistinctExhaustDefinitionCount == 0)
        {
            return 1;
        }

        int distinctDefinitions = state.ExhaustPile
            .Select(card => card.CardId)
            .Distinct()
            .Count();
        return distinctDefinitions >= DoubleAtDistinctExhaustDefinitionCount ? 2 : 1;
    }

    /// <inheritdoc />
    public void Simulate(EnemyCardSimulationContext context)
    {
        EnemyFrozenEffectiveCardState frozen = context.GetCurrentEffectiveCardState(requireFrozenX: true);
        context.AddDamageToAll(Damage, frozen.FrozenX!.Value);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(EnemyCardExecutionContext context)
    {
        EnemyFrozenEffectiveCardState frozen = context.GetCurrentEffectiveCardState(requireFrozenX: true);
        return frozen.FrozenX!.Value == 0
            ? Task.CompletedTask
            : context.ExecuteAttackAllAsync(Damage, frozen.FrozenX.Value);
    }
}

/// <summary>
/// 使敌人自身获得格挡的共享效果节点。
/// </summary>
public sealed class EnemyBlockEffect : IEnemyCardEffectNode
{
    /// <summary>
    /// 创建敌人格挡效果。
    /// </summary>
    /// <param name="programId">稳定效果程序标识。</param>
    /// <param name="block">规范基础格挡。</param>
    public EnemyBlockEffect(string programId, decimal block)
    {
        if (string.IsNullOrWhiteSpace(programId) || block < decimal.Zero)
        {
            throw new ArgumentException("格挡效果标识不能为空且数值不能为负。", nameof(programId));
        }

        ProgramId = programId;
        Block = block;
    }

    /// <inheritdoc />
    public string ProgramId { get; }

    /// <summary>获取规范基础格挡。</summary>
    public decimal Block { get; }

    /// <inheritdoc />
    public void Simulate(EnemyCardSimulationContext context) => context.AddEnemyBlock(Block);

    /// <inheritdoc />
    public Task ExecuteAsync(EnemyCardExecutionContext context) => context.ExecuteDefendAsync(Block);
}

/// <summary>
/// 向敌人自身施加标准 Power 的共享效果节点。
/// </summary>
/// <typeparam name="TPower">标准 Power 模型类型。</typeparam>
public sealed class EnemySelfPowerEffect<TPower> : IEnemyCardEffectNode
    where TPower : PowerModel, new()
{
    /// <summary>
    /// 创建敌人自身 Power 效果。
    /// </summary>
    /// <param name="programId">稳定效果程序标识。</param>
    /// <param name="amount">规范层数。</param>
    public EnemySelfPowerEffect(string programId, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            throw new ArgumentException("Power 效果必须具有稳定程序标识。", nameof(programId));
        }

        ProgramId = programId;
        Amount = amount;
    }

    /// <inheritdoc />
    public string ProgramId { get; }

    /// <summary>获取规范层数。</summary>
    public decimal Amount { get; }

    /// <inheritdoc />
    public void Simulate(EnemyCardSimulationContext context) =>
        context.AddEnemyPower(typeof(TPower).FullName ?? typeof(TPower).Name, Amount);

    /// <inheritdoc />
    public Task ExecuteAsync(EnemyCardExecutionContext context) => context.ApplyEnemyPowerAsync<TPower>(Amount);
}

/// <summary>
/// 向全部有效玩家施加标准负面 Power 的共享效果节点。
/// </summary>
/// <typeparam name="TPower">标准 Power 模型类型。</typeparam>
public sealed class EnemyAllPlayersPowerEffect<TPower> : IEnemyCardEffectNode
    where TPower : PowerModel, new()
{
    /// <summary>
    /// 创建全体玩家 Power 效果。
    /// </summary>
    /// <param name="programId">稳定效果程序标识。</param>
    /// <param name="amount">对每名玩家施加的规范层数。</param>
    public EnemyAllPlayersPowerEffect(string programId, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            throw new ArgumentException("Power 效果必须具有稳定程序标识。", nameof(programId));
        }

        ProgramId = programId;
        Amount = amount;
    }

    /// <inheritdoc />
    public string ProgramId { get; }

    /// <summary>获取对每名玩家施加的规范层数。</summary>
    public decimal Amount { get; }

    /// <inheritdoc />
    public void Simulate(EnemyCardSimulationContext context) =>
        context.AddTargetPowerToAll(typeof(TPower).FullName ?? typeof(TPower).Name, Amount);

    /// <inheritdoc />
    public Task ExecuteAsync(EnemyCardExecutionContext context) =>
        context.ApplyPowerToAllPlayersAsync<TPower>(Amount);
}
