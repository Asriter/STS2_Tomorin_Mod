using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Acts;
using STS2_Tomorin_Mod.Characters;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 集中处理新 Run 的舞台候选章节注册和 Glory 后插入规则。
/// </summary>
public static class StageRegistrationPolicy
{
    /// <summary>
    /// 判断新 Run 是否具有注册隐藏舞台候选章节的基础资格。
    /// </summary>
    /// <param name="players">新 Run 的完整玩家列表。</param>
    /// <param name="gameMode">新 Run 的游戏模式。</param>
    /// <returns>非每日挑战且至少包含一名 Tomorin 时返回 <see langword="true"/>。</returns>
    public static bool ShouldRegister(IReadOnlyList<Player> players, GameMode gameMode)
    {
        return ShouldRegister(gameMode == GameMode.Daily, players.Select(player => player.Character is Tomorin));
    }

    /// <summary>
    /// 使用与游戏模型无关的语义输入判断基础注册资格，供确定性策略测试复用。
    /// </summary>
    /// <param name="isDaily">当前是否为每日挑战。</param>
    /// <param name="tomorinPlayers">逐玩家表示其是否为 Tomorin 的序列。</param>
    /// <returns>非每日挑战且序列中至少存在一名 Tomorin 时返回 <see langword="true"/>。</returns>
    public static bool ShouldRegister(bool isDaily, IEnumerable<bool> tomorinPlayers)
    {
        return !isDaily && tomorinPlayers.Any(isTomorin => isTomorin);
    }

    /// <summary>
    /// 在 Glory 紧后插入唯一舞台候选章节，同时保留其他自定义章节之间的相对顺序。
    /// </summary>
    /// <param name="acts">原始新 Run 章节列表。</param>
    /// <param name="players">新 Run 的完整玩家列表。</param>
    /// <param name="gameMode">新 Run 的游戏模式。</param>
    /// <returns>完成插入后的章节列表；不满足资格或已经包含 Stage 时返回原列表。</returns>
    public static IReadOnlyList<ActModel> RegisterAfterGlory(
        IReadOnlyList<ActModel> acts,
        IReadOnlyList<Player> players,
        GameMode gameMode)
    {
        if (!ShouldRegister(players, gameMode) || ContainsStage(acts))
        {
            return acts;
        }

        var gloryIndex = acts.ToList().FindIndex(act => act is Glory);
        if (gloryIndex < 0)
        {
            return acts;
        }

        var registeredActs = acts.ToList();
        registeredActs.Insert(gloryIndex + 1, ModelDb.Act<Acts.Stage>().ToMutable());
        return registeredActs;
    }

    /// <summary>
    /// 使用稳定模型标识判断章节列表是否已包含 Stage，避免对可变模型实例做类型身份假设。
    /// </summary>
    /// <param name="acts">待检查的章节列表。</param>
    /// <returns>包含唯一或重复 Stage 模型时返回 <see langword="true"/>。</returns>
    public static bool ContainsStage(IEnumerable<ActModel> acts)
    {
        var stageId = ModelDb.Act<Acts.Stage>().Id;
        return acts.Any(act => act.Id == stageId);
    }
}
