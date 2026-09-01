using MegaCrit.Sts2.Core.Map;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 从 <see cref="StageRouteDefinition"/> 生成无分支、无随机替换的舞台地图。
/// </summary>
public sealed class StageActMap : ActMap
{
    private const int MapWidth = 7;
    private const int CenterColumn = MapWidth / 2;

    private readonly MapPoint[,] _grid = new MapPoint[MapWidth, StageRouteDefinition.Nodes.Count - 1];
    private readonly Dictionary<MapPoint, StageRouteNode> _nodesByPoint = [];
    private readonly MapPoint _startingMapPoint;
    private readonly MapPoint _bossMapPoint;

    /// <summary>
    /// 创建位于地图中线、只包含确定性单路线的舞台地图。
    /// </summary>
    public StageActMap()
    {
        var startingNode = StageRouteDefinition.Nodes[0];
        _startingMapPoint = new MapPoint(CenterColumn, 0)
        {
            PointType = startingNode.MapPointType,
            CanBeModified = false,
        };
        _nodesByPoint.Add(_startingMapPoint, startingNode);

        MapPoint previous = _startingMapPoint;

        for (var routeIndex = 1; routeIndex < StageRouteDefinition.Nodes.Count - 1; routeIndex++)
        {
            var node = StageRouteDefinition.Nodes[routeIndex];
            var mapPoint = new MapPoint(CenterColumn, routeIndex)
            {
                PointType = node.MapPointType,
                CanBeModified = false,
            };

            previous.AddChildPoint(mapPoint);
            _grid[CenterColumn, routeIndex] = mapPoint;
            _nodesByPoint.Add(mapPoint, node);

            if (routeIndex == 1)
            {
                startMapPoints.Add(mapPoint);
            }

            previous = mapPoint;
        }

        var bossNode = StageRouteDefinition.Nodes[^1];
        _bossMapPoint = new MapPoint(CenterColumn, _grid.GetLength(1))
        {
            PointType = bossNode.MapPointType,
            CanBeModified = false,
        };
        previous.AddChildPoint(_bossMapPoint);
        _nodesByPoint.Add(_bossMapPoint, bossNode);
    }

    /// <summary>获取可见路线起点，即长颈鹿先古之民节点。</summary>
    public override MapPoint StartingMapPoint => _startingMapPoint;

    /// <summary>获取可见路线终点，即当前章节权威第一 Boss 节点。</summary>
    public override MapPoint BossMapPoint => _bossMapPoint;

    /// <summary>暴露引擎要求的普通节点网格；特殊起点与首领点由引擎单独处理。</summary>
    protected override MapPoint[,] Grid => _grid;

    /// <summary>
    /// 从本局当前地图与地图点引用解析舞台路线语义。
    /// </summary>
    /// <param name="map">待检查的地图。</param>
    /// <param name="mapPoint">待解析的地图点。</param>
    /// <param name="node">解析成功时对应的固定路线节点。</param>
    /// <returns>当地图点属于舞台固定路线时返回 <see langword="true"/>。</returns>
    public static bool TryGetNode(ActMap? map, MapPoint? mapPoint, out StageRouteNode node)
    {
        node = null!;
        if (map is not StageActMap stageMap || mapPoint == null)
        {
            return false;
        }

        return stageMap._nodesByPoint.TryGetValue(mapPoint, out node!);
    }
}
