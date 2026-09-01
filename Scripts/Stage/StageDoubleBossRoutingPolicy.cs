namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 决定存在隐藏 Stage 候选时，原版“双 Boss 仅作用于最后章节”的规则应落到哪个可游玩章节。
/// </summary>
public static class StageDoubleBossRoutingPolicy
{
    public sealed record RelocationPlan<TBossId>(
        int TargetActIndex,
        int OriginalTargetActIndex,
        IReadOnlyList<TBossId> EligibleBossIds,
        int PreferredBossIndex)
        where TBossId : notnull;

    /// <summary>
    /// 唯一 Stage 的前一章节才是正常流程的最终可游玩章节；不存在唯一合法 Stage 时保留原版最后章节语义。
    /// </summary>
    /// <param name="isStageByActIndex">按 Run 章节顺序标记每一项是否为 Stage。</param>
    /// <returns>应当承载 A10 双 Boss 的章节索引。</returns>
    public static int FindDoubleBossTargetIndex(IReadOnlyList<bool> isStageByActIndex)
    {
        ArgumentNullException.ThrowIfNull(isStageByActIndex);
        if (isStageByActIndex.Count == 0)
        {
            throw new ArgumentException("章节列表不能为空。", nameof(isStageByActIndex));
        }

        int stageIndex = -1;
        for (int index = 0; index < isStageByActIndex.Count; index++)
        {
            if (!isStageByActIndex[index])
            {
                continue;
            }

            if (stageIndex >= 0)
            {
                return isStageByActIndex.Count - 1;
            }

            stageIndex = index;
        }

        return stageIndex > 0
            ? stageIndex - 1
            : isStageByActIndex.Count - 1;
    }

    /// <summary>
    /// 生成第二 Boss 的重定向决策。返回 null 表示保留原版分配。
    /// </summary>
    public static RelocationPlan<TBossId>? CreateRelocationPlan<TBossId>(
        bool hasDoubleBoss,
        IReadOnlyList<bool> isStageByActIndex,
        TBossId primaryBossId,
        IReadOnlyList<TBossId> allBossIds,
        bool hasMisplacedBoss,
        TBossId misplacedBossId)
        where TBossId : notnull
    {
        ArgumentNullException.ThrowIfNull(isStageByActIndex);
        ArgumentNullException.ThrowIfNull(allBossIds);
        if (!hasDoubleBoss)
        {
            return null;
        }

        int targetActIndex = FindDoubleBossTargetIndex(isStageByActIndex);
        int originalTargetActIndex = isStageByActIndex.Count - 1;
        if (targetActIndex == originalTargetActIndex)
        {
            return null;
        }

        EqualityComparer<TBossId> comparer = EqualityComparer<TBossId>.Default;
        TBossId[] eligibleBossIds = allBossIds
            .Where(bossId => !comparer.Equals(bossId, primaryBossId))
            .ToArray();
        int preferredBossIndex = hasMisplacedBoss
            ? Array.FindIndex(eligibleBossIds, bossId => comparer.Equals(bossId, misplacedBossId))
            : -1;

        return new RelocationPlan<TBossId>(
            targetActIndex,
            originalTargetActIndex,
            eligibleBossIds,
            preferredBossIndex);
    }

    /// <summary>
    /// 先写入正确章节，再清除原版误写的末尾章节槽位。
    /// </summary>
    public static void ApplyRelocation<TBossId, TBoss>(
        RelocationPlan<TBossId> plan,
        TBoss secondBoss,
        Action<int, TBoss> setSecondBoss,
        Action<int> clearSecondBoss)
        where TBossId : notnull
        where TBoss : notnull
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(secondBoss);
        ArgumentNullException.ThrowIfNull(setSecondBoss);
        ArgumentNullException.ThrowIfNull(clearSecondBoss);

        setSecondBoss(plan.TargetActIndex, secondBoss);
        clearSecondBoss(plan.OriginalTargetActIndex);
    }
}
