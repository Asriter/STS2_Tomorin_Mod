using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
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
    /// <param name="model">解析后的固定内容模型；原版商店和篝火为 <see langword="null"/>。</param>
    /// <returns>当前点属于舞台固定路线时返回 <see langword="true"/>。</returns>
    public static bool TryResolveCurrentRoom(IRunState runState, out RoomType roomType, out AbstractModel? model)
    {
        roomType = RoomType.Unassigned;
        model = null;

        if (runState.Act is not Acts.Stage || !StageActMap.TryGetNode(runState.Map, runState.CurrentMapPoint, out var node))
        {
            return false;
        }

        roomType = node.RoomType;
        model = node.Kind switch
        {
            StageRouteNodeKind.Ancient => ModelDb.AncientEvent<GiraffeAncient>().ToMutable(),
            StageRouteNodeKind.FirstEvent or StageRouteNodeKind.SecondEvent => ModelDb.Event<FeedTheCat>().ToMutable(),
            StageRouteNodeKind.Elite => ModelDb.Encounter<MechaKnightElite>().ToMutable(),
            StageRouteNodeKind.Boss => runState.Act.BossEncounter?.ToMutable()
                ?? throw new InvalidOperationException(
                    $"[Stage] 章节 {runState.Act.Id} 到达 Boss 节点时缺少第一 Boss；" +
                    $"地图节点={runState.CurrentMapPoint}，路线节点={node.Kind}。"),
            StageRouteNodeKind.Shop or StageRouteNodeKind.RestSite => null,
            _ => throw new InvalidOperationException($"[Stage] 未预期的舞台路线节点：{node.Kind}"),
        };

        return true;
    }
}
