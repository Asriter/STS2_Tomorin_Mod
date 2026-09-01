using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Acts;
using STS2_Tomorin_Mod.Characters;
using STS2_Tomorin_Mod.Relics;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 在 Glory 奖励完成后的统一同步点计算进入舞台的完整资格。
/// </summary>
public static class StageEligibility
{
    /// <summary>同一玩家进入舞台所需的最少候选遗物数量。</summary>
    public const int MinimumRequiredStageRelicCount = 2;

    /// <summary>进入舞台时参与计数的遗物稳定模型标识集合。</summary>
    public static IReadOnlySet<ModelId> RequiredStageRelics => RequiredStageRelicsHolder.Value;

    /// <summary>
    /// 判断当前 Glory 是否应该在奖励完成后进入隐藏舞台。
    /// </summary>
    /// <param name="runState">参与同步的完整 Run 状态。</param>
    /// <returns>只有所有已确认条件同时成立时才返回 <see langword="true"/>。</returns>
    public static bool IsEligible(IRunState runState)
    {
        //TODO 
        return true;
        if (runState.GameMode == GameMode.Daily || runState.Act is not Glory)
        {
            return false;
        }

        if (!runState.Players.Any(player => player.Character is Tomorin))
        {
            return false;
        }

        if (!HasAdjacentUniqueStageCandidate(runState) ||
            StageRunProgressModifier.Find(runState)?.HasDefeatedFullPowerOblivionis != true)
        {
            return false;
        }

        return runState.Players.Any(HasMinimumRequiredRelics);
    }

    /// <summary>
    /// 验证 Stage 在章节列表中仅出现一次且紧邻当前 Glory；异常时安全拒绝转层。
    /// </summary>
    /// <param name="runState">当前局状态。</param>
    /// <returns>章节顺序满足设计约束时返回 <see langword="true"/>。</returns>
    public static bool HasAdjacentUniqueStageCandidate(IRunState runState)
    {
        var stageId = ModelDb.Act<Acts.Stage>().Id;
        var stageIndexes = runState.Acts
            .Select((act, index) => (act, index))
            .Where(pair => pair.act.Id == stageId)
            .Select(pair => pair.index)
            .ToArray();

        return stageIndexes.Length == 1 && stageIndexes[0] == runState.CurrentActIndex + 1;
    }

    /// <summary>
    /// 判断指定玩家是否独自持有足够数量的不同候选遗物。
    /// </summary>
    /// <param name="player">待检查的玩家；不按角色、在线或存活状态过滤。</param>
    /// <returns>该玩家持有的不同候选遗物达到最低要求时返回 <see langword="true"/>。</returns>
    public static bool HasMinimumRequiredRelics(Player player)
    {
        var heldRelics = player.Relics.Select(relic => relic.Id).ToHashSet();
        return CoversMinimumRequiredRelics(
            heldRelics,
            RequiredStageRelics,
            MinimumRequiredStageRelicCount);
    }

    /// <summary>
    /// 判断同一个持有集合与候选集合的不同元素交集是否达到最低要求。
    /// </summary>
    /// <typeparam name="T">稳定标识的值类型。</typeparam>
    /// <param name="held">同一玩家持有的稳定标识集合。</param>
    /// <param name="required">参与计数的候选稳定标识集合。</param>
    /// <param name="minimumRequiredCount">必须由同一持有集合覆盖的最低不同元素数量。</param>
    /// <returns>交集数量达到最低要求时返回 <see langword="true"/>。</returns>
    public static bool CoversMinimumRequiredRelics<T>(
        IEnumerable<T> held,
        IReadOnlySet<T> required,
        int minimumRequiredCount)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(required);
        if (minimumRequiredCount <= 0 || minimumRequiredCount > required.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumRequiredCount),
                minimumRequiredCount,
                "最低候选遗物数量必须大于零且不能超过候选集合大小。");
        }

        var heldSet = held.ToHashSet();
        return required.Count(heldSet.Contains) >= minimumRequiredCount;
    }

    /// <summary>延迟到实际资格检查时再访问 ModelDb，避免纯集合逻辑依赖模型库初始化。</summary>
    private static class RequiredStageRelicsHolder
    {
        internal static readonly IReadOnlySet<ModelId> Value = new HashSet<ModelId>
        {
            ModelDb.Relic<AnonGuitar>().Id,
            ModelDb.Relic<RaanaGuitar>().Id,
            ModelDb.Relic<SoyoBase>().Id,
            ModelDb.Relic<TakiDrum>().Id,
        };
    }
}
