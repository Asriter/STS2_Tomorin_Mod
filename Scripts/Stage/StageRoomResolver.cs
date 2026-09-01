using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2_Tomorin_Mod.Acts;
using STS2_Tomorin_Mod.Encounters;
using STS2_Tomorin_Mod.Events;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 将舞台固定路线节点解析为指定内容模型，绕过随机事件和首领选择池。
/// </summary>
public static class StageRoomResolver
{
    /// <summary>
    /// 解析当前地图点的固定房间类型和内容模型。
    /// </summary>
    /// <param name="runState">当前局状态。</param>
    /// <param name="roomType">解析后的房间类型。</param>
    /// <param name="model">
    /// 解析后的固定内容模型；事件保持 canonical，遭遇使用 mutable，原版商店和篝火为 <see langword="null"/>。
    /// </param>
    /// <returns>当前点属于舞台固定路线时返回 <see langword="true"/>。</returns>
    public static bool TryResolveCurrentRoom(
        IRunState runState,
        MapPointType requestedMapPointType,
        out RoomType roomType,
        out AbstractModel? model)
    {
        roomType = RoomType.Unassigned;
        model = null;

        if (runState.Act is not Acts.Stage ||
            !StageActMap.TryGetNode(runState.Map, runState.CurrentMapPoint, out var node) ||
            node.MapPointType != requestedMapPointType)
        {
            return false;
        }

        roomType = node.RoomType;
        model = node.Kind switch
        {
            StageRouteNodeKind.Ancient => ModelDb.AncientEvent<GiraffeAncient>(),
            StageRouteNodeKind.FirstEvent => ModelDb.Event<StageSupplyEvent>(),
            StageRouteNodeKind.FateGuidance => ModelDb.Event<FateGuidance>(),
            StageRouteNodeKind.Elite => ModelDb.Encounter<BandMemberEncounter>().ToMutable(),
            StageRouteNodeKind.Boss => runState.Act.BossEncounter?.ToMutable()
                ?? throw new InvalidOperationException(
                    $"[Stage] 章节 {runState.Act.Id} 到达 Boss 节点时缺少第一 Boss；" +
                    $"地图节点={runState.CurrentMapPoint}，路线节点={node.Kind}。"),
            StageRouteNodeKind.Shop or StageRouteNodeKind.RestSite => null,
            _ => throw new InvalidOperationException($"[Stage] 未预期的舞台路线节点：{node.Kind}"),
        };

        return true;
    }

    /// <summary>
    /// 按已完成的舞台房间数解析即将进入或读档重建的固定问号事件。
    /// </summary>
    /// <remarks>
    /// 运行存档不会保存正在进行中的房间。读档重建房间时，目标坐标不保证已经反映在
    /// <see cref="IRunState.CurrentMapPoint"/> 中；已完成房间历史在该时点仍然稳定。
    /// </remarks>
    public static EventModel ResolveEventForCurrentProgress(IRunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        if (runState.Act is not Acts.Stage)
        {
            throw new InvalidOperationException("只有舞台章节可以解析舞台固定事件。");
        }

        if (runState.CurrentActIndex < 0 || runState.CurrentActIndex >= runState.MapPointHistory.Count)
        {
            throw new InvalidOperationException(
                $"舞台事件解析缺少当前章节历史：章节索引={runState.CurrentActIndex}，历史数={runState.MapPointHistory.Count}。");
        }

        int completedRoomCount = runState.MapPointHistory[runState.CurrentActIndex].Count;
        if (completedRoomCount < 0 || completedRoomCount >= StageRouteDefinition.Nodes.Count)
        {
            throw new InvalidOperationException(
                $"舞台事件解析遇到越界进度：已完成房间数={completedRoomCount}。");
        }

        return StageRouteDefinition.Nodes[completedRoomCount].Kind switch
        {
            StageRouteNodeKind.FirstEvent => ModelDb.Event<StageSupplyEvent>(),
            StageRouteNodeKind.FateGuidance => ModelDb.Event<FateGuidance>(),
            var kind => throw new InvalidOperationException(
                $"舞台进度 {completedRoomCount} 对应 {kind}，不是固定问号事件。"),
        };
    }
}
