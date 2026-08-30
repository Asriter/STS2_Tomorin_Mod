using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Stage;

namespace STS2_Tomorin_Mod.Patch;

/// <summary>
/// 仅在新 Run 创建时注册隐藏舞台候选章节及其可序列化进度状态。
/// </summary>
[HarmonyPatch(typeof(RunState), nameof(RunState.CreateForNewRun))]
internal static class StageRunRegistrationPatch
{
    /// <summary>
    /// 在原版构造 RunState 前按基础资格插入 Stage 和进度 Modifier；旧存档恢复不会经过此入口。
    /// </summary>
    /// <param name="players">新 Run 的完整玩家列表。</param>
    /// <param name="acts">可被替换的章节列表。</param>
    /// <param name="modifiers">可被替换的同步 Modifier 列表。</param>
    /// <param name="gameMode">新 Run 的游戏模式。</param>
    [HarmonyPrefix]
    private static void Prefix(
        IReadOnlyList<Player> players,
        ref IReadOnlyList<ActModel> acts,
        ref IReadOnlyList<ModifierModel> modifiers,
        GameMode gameMode)
    {
        if (!StageRegistrationPolicy.ShouldRegister(players, gameMode))
        {
            return;
        }

        var registeredActs = StageRegistrationPolicy.RegisterAfterGlory(acts, players, gameMode);
        acts = registeredActs;
        if (!StageRegistrationPolicy.ContainsStage(acts) || modifiers.Any(modifier => modifier is StageRunProgressModifier))
        {
            return;
        }

        modifiers = modifiers.Append(ModelDb.Modifier<StageRunProgressModifier>().ToMutable()).ToArray();
    }
}
