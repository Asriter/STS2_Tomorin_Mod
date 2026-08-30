namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 定义一项可由投影与实际结算共享的敌人卡牌效果。
/// </summary>
public interface IEnemyCardEffectNode
{
    /// <summary>获取参与定义指纹和重连校验的稳定效果程序标识。</summary>
    string ProgramId { get; }

    /// <summary>
    /// 在纯模拟上下文中应用效果，不得推进随机数或修改真实战斗对象。
    /// </summary>
    /// <param name="context">当前投影模拟上下文。</param>
    void Simulate(EnemyCardSimulationContext context);

    /// <summary>
    /// 在真实结算上下文中执行效果。
    /// </summary>
    /// <param name="context">当前真实结算上下文。</param>
    /// <returns>效果完成任务。</returns>
    Task ExecuteAsync(EnemyCardExecutionContext context);
}

/// <summary>
/// 使用显式稳定标识与成对模拟/执行委托构造通用效果节点。
/// </summary>
public sealed class EnemyCardEffectNode : IEnemyCardEffectNode
{
    private readonly Action<EnemyCardSimulationContext> _simulate;
    private readonly Func<EnemyCardExecutionContext, Task> _executeAsync;

    /// <summary>
    /// 创建一项通用效果节点。
    /// </summary>
    /// <param name="programId">跨运行保持稳定的效果程序标识。</param>
    /// <param name="simulate">无副作用的模拟实现。</param>
    /// <param name="executeAsync">真实战斗执行实现。</param>
    public EnemyCardEffectNode(
        string programId,
        Action<EnemyCardSimulationContext> simulate,
        Func<EnemyCardExecutionContext, Task> executeAsync)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            throw new ArgumentException("敌人效果程序标识不能为空。", nameof(programId));
        }

        ProgramId = programId;
        _simulate = simulate ?? throw new ArgumentNullException(nameof(simulate));
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
    }

    /// <inheritdoc />
    public string ProgramId { get; }

    /// <inheritdoc />
    public void Simulate(EnemyCardSimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _simulate(context);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(EnemyCardExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _executeAsync(context);
    }
}
