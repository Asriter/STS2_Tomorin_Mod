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
    /// <summary>进入舞台必须由同一玩家完整持有的遗物稳定模型标识集合。</summary>
    public static IReadOnlySet<ModelId> RequiredStageRelics { get; } = new HashSet<ModelId>
    {
        ModelDb.Relic<AnonGuitar>().Id,
        ModelDb.Relic<RaanaGuitar>().Id,
        ModelDb.Relic<SoyoBase>().Id,
        ModelDb.Relic<TakiDrum>().Id,
    };

    /// <summary>
    /// 判断当前 Glory 是否应该在奖励完成后进入隐藏舞台。
    /// </summary>
    /// <param name="runState">参与同步的完整 Run 状态。</param>
    /// <returns>只有所有已确认条件同时成立时才返回 <see langword="true"/>。</returns>
    public static bool IsEligible(IRunState runState)
    {
        if (runState.GameMode == GameMode.Daily || runState.Act is not Glory)
        {
            return false;
        }

        if (!runState.Players.Any(player => player.Character is Tomorin))
        {
            return false;
        }

        if (!HasAdjacentUniqueStageCandidate(runState) || StageRunProgressModifier.Find(runState)?.HasDefeatedFullPowerOblivionis != true)
        {
            return false;
        }

        return runState.Players.Any(HasAllRequiredRelics);
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
    /// 判断指定玩家是否独自覆盖舞台所需的全部遗物。
    /// </summary>
    /// <param name="player">待检查的玩家；不按角色、在线或存活状态过滤。</param>
    /// <returns>该玩家持有完整所需遗物集合时返回 <see langword="true"/>。</returns>
    public static bool HasAllRequiredRelics(Player player)
    {
        var heldRelics = player.Relics.Select(relic => relic.Id).ToHashSet();
        return CoversRequiredRelics(heldRelics, RequiredStageRelics);
    }

    /// <summary>
    /// 判断同一个持有集合是否覆盖完整需求集合，不依赖遗物数量字面量或玩家角色状态。
    /// </summary>
    /// <typeparam name="T">稳定标识的值类型。</typeparam>
    /// <param name="held">同一玩家持有的稳定标识集合。</param>
    /// <param name="required">必须由该玩家完整覆盖的稳定标识集合。</param>
    /// <returns>持有集合覆盖全部需求时返回 <see langword="true"/>。</returns>
    public static bool CoversRequiredRelics<T>(IEnumerable<T> held, IReadOnlySet<T> required)
        where T : notnull
    {
        return required.IsSubsetOf(held.ToHashSet());
    }
}
