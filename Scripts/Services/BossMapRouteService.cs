using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Tomorin_Mod.Services;

/// <summary>
/// 描述修改当前章节第一 Boss 的稳定结果。
/// </summary>
internal enum PrimaryBossChangeResult
{
    /// <summary>目标已经位于第一或第二 Boss 槽位，章节状态保持不变。</summary>
    AlreadySelected,

    /// <summary>目标原本不存在，现已写入第一 Boss 槽位。</summary>
    PrimaryBossChanged,
}

/// <summary>
/// 集中修改当前章节的第一 Boss，并在成功写入后刷新已创建的地图 Boss 节点。
/// </summary>
internal static class BossMapRouteService
{
    /// <summary>
    /// 使用稳定模型标识去重目标 Boss；目标不存在时只替换第一 Boss，绝不修改第二 Boss。
    /// </summary>
    /// <param name="runState">包含当前章节和地图的权威运行状态。</param>
    /// <param name="targetBoss">准备放入第一 Boss 槽位的规范 Boss Encounter。</param>
    /// <returns>目标已经存在或第一 Boss 已经成功改变。</returns>
    /// <exception cref="ArgumentNullException">运行状态或目标 Encounter 缺失时抛出。</exception>
    /// <exception cref="ArgumentException">目标 Encounter 不是 Boss 房间时抛出。</exception>
    /// <exception cref="InvalidOperationException">当前章节缺少第一 Boss 时抛出。</exception>
    internal static PrimaryBossChangeResult ChangePrimaryBoss(
        IRunState runState,
        EncounterModel targetBoss)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(targetBoss);

        if (targetBoss.RoomType != RoomType.Boss)
        {
            throw new ArgumentException(
                $"目标 Encounter {targetBoss.Id} 的房间类型不是 Boss。",
                nameof(targetBoss));
        }

        ActModel act = runState.Act ?? throw new InvalidOperationException("当前 Run 缺少 Act，无法修改第一 Boss。");
        EncounterModel currentBoss = act.BossEncounter ?? throw new InvalidOperationException(
            $"当前章节 {act.Id} 缺少第一 Boss，无法应用目标 {targetBoss.Id}。");

        if (currentBoss.Id == targetBoss.Id || act.SecondBossEncounter?.Id == targetBoss.Id)
        {
            return PrimaryBossChangeResult.AlreadySelected;
        }

        act.SetBossEncounter(targetBoss.CanonicalInstance.ToMutable());
        BossMapVisualSynchronizer.RefreshCurrentBossVisuals(runState);
        return PrimaryBossChangeResult.PrimaryBossChanged;
    }
}
