using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 定义舞台地图中唯一允许出现的房间语义、地图点类型和房间类型。
/// </summary>
public static class StageRouteDefinition
{
    /// <summary>按行进顺序排列的唯一舞台路线。</summary>
    public static IReadOnlyList<StageRouteNode> Nodes { get; } =
    [
        new(StageRouteNodeKind.Ancient, MapPointType.Ancient, RoomType.Event),
        new(StageRouteNodeKind.FirstEvent, MapPointType.Unknown, RoomType.Event),
        new(StageRouteNodeKind.Elite, MapPointType.Elite, RoomType.Elite),
        new(StageRouteNodeKind.Shop, MapPointType.Shop, RoomType.Shop),
        new(StageRouteNodeKind.SecondEvent, MapPointType.Unknown, RoomType.Event),
        new(StageRouteNodeKind.RestSite, MapPointType.RestSite, RoomType.RestSite),
        new(StageRouteNodeKind.Boss, MapPointType.Boss, RoomType.Boss),
    ];
}

/// <summary>
/// 标识固定路线中一个节点的业务含义。
/// </summary>
public enum StageRouteNodeKind
{
    /// <summary>长颈鹿先古之民节点。</summary>
    Ancient,
    /// <summary>第一次固定喂猫事件。</summary>
    FirstEvent,
    /// <summary>固定机甲骑士精英节点。</summary>
    Elite,
    /// <summary>原版商店节点。</summary>
    Shop,
    /// <summary>第二次固定喂猫事件。</summary>
    SecondEvent,
    /// <summary>原版篝火节点。</summary>
    RestSite,
    /// <summary>固定 Crychic 亡灵首领节点。</summary>
    Boss,
}

/// <summary>
/// 描述一个固定路线节点在地图与房间系统中的表现。
/// </summary>
/// <param name="Kind">节点的稳定业务语义。</param>
/// <param name="MapPointType">地图上显示的节点类型。</param>
/// <param name="RoomType">进入节点时必须创建的房间类型。</param>
public sealed record StageRouteNode(StageRouteNodeKind Kind, MapPointType MapPointType, RoomType RoomType);
