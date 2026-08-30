using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>影灯敌人牌系统支持的稳定能力身份。</summary>
public enum ShadowAbilityId
{
    SorrowfulRain,
    Adayume,
    HeartBeat,
    DuckAndCover,
    NameOfTear,
    UnwantedSixth
}

/// <summary>定义真实执行与纯模拟必须成对实现的敌人能力钩子。</summary>
public interface IEnemyAbilityHookDispatcher
{
    Task BeforePreparationAsync(EnemyCardExecutionContext context) => Task.CompletedTask;
    void SimulateBeforePreparation(EnemyCardSimulationContext context) { }
    Task AfterComposeAsync(EnemyCardExecutionContext context) => Task.CompletedTask;
    void SimulateAfterCompose(EnemyCardSimulationContext context) { }
    Task AfterSuccessfulUnitAsync(EnemyCardExecutionContext context) => Task.CompletedTask;
    void SimulateAfterSuccessfulUnit(EnemyCardSimulationContext context) { }
    Task AfterBlockGainAsync(EnemyCardExecutionContext context, decimal gainedBlock) => Task.CompletedTask;
    void SimulateAfterBlockGain(EnemyCardSimulationContext context, decimal gainedBlock) { }
    Task AfterNormalLifecycleExhaustAsync(EnemyCardExecutionContext context, BaseEnemyCard card) =>
        Task.CompletedTask;
    void SimulateAfterNormalLifecycleExhaust(EnemyCardSimulationContext context, EnemyCardInstanceKey cardKey) { }
}

/// <summary>以稳定顺序分发影灯六类能力，并保持真实与模拟增量一致。</summary>
public sealed class EnemyAbilityHookDispatcher : IEnemyAbilityHookDispatcher
{
    public async Task BeforePreparationAsync(EnemyCardExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetEnemyPowerAmount<DuckAndCoverPower>() <= decimal.Zero)
        {
            return;
        }

        decimal heartWall = context.GetEnemyPowerAmount<AtFieldPower>();
        if (heartWall > decimal.Zero)
        {
            await context.ExecuteDefendAsync(heartWall);
        }
    }

    public void SimulateBeforePreparation(EnemyCardSimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetEnemyPowerAmount<DuckAndCoverPower>() > decimal.Zero)
        {
            decimal heartWall = context.GetEnemyPowerAmount<AtFieldPower>();
            if (heartWall > decimal.Zero)
            {
                context.AddEnemyBlock(heartWall);
            }
        }
    }

    public async Task AfterComposeAsync(EnemyCardExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        decimal stacks = context.GetEnemyPowerAmount<CardIntentSorrowfulRainPower>();
        if (stacks > decimal.Zero)
        {
            await context.ApplyEnemyPowerAsync<AtFieldPower>(
                stacks * CardIntentSorrowfulRainPower.HeartWallPerStack);
        }
    }

    public void SimulateAfterCompose(EnemyCardSimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        decimal stacks = context.GetEnemyPowerAmount<CardIntentSorrowfulRainPower>();
        if (stacks > decimal.Zero)
        {
            context.AddEnemyPower(
                StablePowerId<AtFieldPower>(),
                stacks * CardIntentSorrowfulRainPower.HeartWallPerStack);
        }
    }

    public async Task AfterSuccessfulUnitAsync(EnemyCardExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        decimal stacks = context.GetEnemyPowerAmount<CardIntentAdayumePower>();
        if (stacks <= decimal.Zero)
        {
            return;
        }

        decimal amount = stacks * CardIntentAdayumePower.BlockAndHeartWallPerStack;
        await context.ExecuteDefendAsync(amount);
        await context.ApplyEnemyPowerAsync<AtFieldPower>(amount);
    }

    public void SimulateAfterSuccessfulUnit(EnemyCardSimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        decimal stacks = context.GetEnemyPowerAmount<CardIntentAdayumePower>();
        if (stacks <= decimal.Zero)
        {
            return;
        }

        decimal amount = stacks * CardIntentAdayumePower.BlockAndHeartWallPerStack;
        context.AddEnemyBlock(amount);
        context.AddEnemyPower(StablePowerId<AtFieldPower>(), amount);
    }

    public async Task AfterBlockGainAsync(EnemyCardExecutionContext context, decimal gainedBlock)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (gainedBlock <= decimal.Zero)
        {
            return;
        }

        decimal stacks = context.GetTransientAbilityAmount<CardIntentUnwantedSixthPower>();
        if (stacks > decimal.Zero)
        {
            await context.ApplyEnemyPowerAsync<AtFieldPower>(
                stacks * CardIntentUnwantedSixthPower.HeartWallPerBlockGrant);
        }
    }

    public void SimulateAfterBlockGain(EnemyCardSimulationContext context, decimal gainedBlock)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (gainedBlock <= decimal.Zero)
        {
            return;
        }

        decimal stacks = context.GetTransientAbilityAmount<CardIntentUnwantedSixthPower>();
        if (stacks > decimal.Zero)
        {
            context.AddEnemyPower(
                StablePowerId<AtFieldPower>(),
                stacks * CardIntentUnwantedSixthPower.HeartWallPerBlockGrant);
        }
    }

    public async Task AfterNormalLifecycleExhaustAsync(EnemyCardExecutionContext context, BaseEnemyCard card)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(card);
        decimal stacks = context.GetEnemyPowerAmount<CardIntentHeartBeatPower>();
        if (stacks > decimal.Zero)
        {
            await context.ExecuteDefendAsync(stacks * CardIntentHeartBeatPower.BlockPerExhaust);
        }
    }

    public void SimulateAfterNormalLifecycleExhaust(
        EnemyCardSimulationContext context,
        EnemyCardInstanceKey cardKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cardKey);
        decimal stacks = context.GetEnemyPowerAmount<CardIntentHeartBeatPower>();
        if (stacks > decimal.Zero)
        {
            context.AddEnemyBlock(stacks * CardIntentHeartBeatPower.BlockPerExhaust);
        }
    }

    internal static string StablePowerId<TPower>() => typeof(TPower).FullName ?? typeof(TPower).Name;
}
