namespace STS2_Tomorin_Mod.Enemy.CardIntents;

/// <summary>
/// 定义敌人卡牌逻辑唯一允许使用的战斗随机索引入口。
/// </summary>
public interface IEnemyCardRandomSource
{
    /// <summary>
    /// 从零到上界开区间取得一个索引。
    /// </summary>
    /// <param name="exclusiveUpperBound">必须大于零的开区间上界。</param>
    /// <returns>合法随机索引。</returns>
    int NextIndex(int exclusiveUpperBound);
}

/// <summary>
/// 把生产战斗 RNG 或领域测试替身包装为经过边界校验的唯一随机源。
/// </summary>
public sealed class EnemyCardRandomSource : IEnemyCardRandomSource
{
    private readonly Func<int, int> _nextIndex;

    /// <summary>
    /// 创建随机源包装。
    /// </summary>
    /// <param name="nextIndex">必须按给定开区间上界返回合法索引的战斗 RNG 委托。</param>
    public EnemyCardRandomSource(Func<int, int> nextIndex)
    {
        _nextIndex = nextIndex ?? throw new ArgumentNullException(nameof(nextIndex));
    }

    /// <inheritdoc />
    public int NextIndex(int exclusiveUpperBound)
    {
        if (exclusiveUpperBound <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound), "随机索引上界必须大于零。");
        }

        int result = _nextIndex(exclusiveUpperBound);
        if (result < 0 || result >= exclusiveUpperBound)
        {
            throw new InvalidOperationException(
                $"战斗 RNG 为上界 {exclusiveUpperBound} 返回了非法索引 {result}。");
        }

        return result;
    }
}
