namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 表示只停留在已提交原子步骤边界上的深度优先执行游标。
/// </summary>
public sealed class EnemyCardExecutionCursor
{
    private IReadOnlyList<int> _stepPath = Array.Empty<int>();

    /// <summary>获取或设置下一项来源步骤索引。</summary>
    public int SourceIndex { get; set; }

    /// <summary>获取或设置当前来源牌的下一次尝试索引。</summary>
    public int ReplayIndex { get; set; }

    /// <summary>获取当前递归各层下一步骤索引的不可修改路径。</summary>
    public IReadOnlyList<int> StepPath => _stepPath;

    /// <summary>获取或设置已经完整提交的步骤总数。</summary>
    public int CommittedStepCount { get; set; }

    /// <summary>获取或设置行动是否已经完成。</summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// 复制并替换当前递归步骤路径，任何负分量都会被拒绝。
    /// </summary>
    /// <param name="stepPath">从单元根到当前递归层的下一步骤索引。</param>
    public void SetStepPath(IEnumerable<int> stepPath)
    {
        ArgumentNullException.ThrowIfNull(stepPath);
        int[] copied = stepPath.ToArray();
        if (copied.Any(component => component < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(stepPath), "递归步骤路径不能包含负分量。 ");
        }

        _stepPath = Array.AsReadOnly(copied);
    }

    /// <summary>
    /// 创建当前游标的独立副本，供同步 DTO 和事务校验使用。
    /// </summary>
    /// <returns>不共享可变字段的新游标。</returns>
    public EnemyCardExecutionCursor Clone()
    {
        EnemyCardExecutionCursor clone = new()
        {
            SourceIndex = SourceIndex,
            ReplayIndex = ReplayIndex,
            CommittedStepCount = CommittedStepCount,
            IsCompleted = IsCompleted
        };
        clone.SetStepPath(StepPath);
        return clone;
    }

    /// <summary>
    /// 判断游标字段是否满足非负且完成态闭合的不变量。
    /// </summary>
    /// <returns>全部字段可安全用于恢复时为真。</returns>
    public bool IsValid() =>
        SourceIndex >= 0 && ReplayIndex >= 0 &&
        StepPath.All(component => component >= 0) && CommittedStepCount >= 0;
}
