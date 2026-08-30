using STS2_Tomorin_Mod.Powers;

namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 定义每个成功执行单元和作词成功后的敌人能力钩子，允许领域测试替换为无副作用实现。
/// </summary>
public interface IEnemyAbilityHookDispatcher
{
    /// <summary>在一次作词成功后执行能力钩子。</summary>
    Task AfterComposeAsync(EnemyCardExecutionContext context);

    /// <summary>在任意执行单元成功后执行能力钩子。</summary>
    Task AfterSuccessfulUnitAsync(EnemyCardExecutionContext context);
}

/// <summary>
/// 以固定顺序分发测试敌人的悲伤之雨与过堕幻能力钩子。
/// </summary>
public sealed class EnemyAbilityHookDispatcher : IEnemyAbilityHookDispatcher
{
    /// <summary>
    /// 在一次作词成功后根据悲伤之雨层数获得对应心之壁。
    /// </summary>
    /// <param name="context">真实敌人牌执行上下文。</param>
    /// <returns>能力触发完成任务。</returns>
    public async Task AfterComposeAsync(EnemyCardExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        CardIntentSorrowfulRainPower? rain = context.Owner.Creature.Powers
            .OfType<CardIntentSorrowfulRainPower>()
            .FirstOrDefault();
        if (rain is not null && rain.Amount > decimal.Zero)
        {
            await context.ApplyEnemyPowerAsync<AtFieldPower>(rain.Amount);
        }
    }

    /// <summary>
    /// 在普通、重放、即时或受控灵感执行单元成功后触发一次过堕幻。
    /// </summary>
    /// <param name="context">真实敌人牌执行上下文。</param>
    /// <returns>能力触发完成任务。</returns>
    public async Task AfterSuccessfulUnitAsync(EnemyCardExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        CardIntentAdayumePower? adayume = context.Owner.Creature.Powers
            .OfType<CardIntentAdayumePower>()
            .FirstOrDefault();
        if (adayume is null || adayume.Amount <= decimal.Zero)
        {
            return;
        }

        await context.ExecuteDefendAsync(adayume.Amount);
        await context.ApplyEnemyPowerAsync<AtFieldPower>(adayume.Amount);
    }
}
