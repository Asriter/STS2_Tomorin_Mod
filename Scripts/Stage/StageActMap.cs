using MegaCrit.Sts2.Core.Map;

namespace STS2_Tomorin_Mod.Stage;

/// <summary>
/// 从 <see cref="StageRouteDefinition"/> 生成无分支、无随机替换的舞台地图。
/// </summary>
public sealed class StageActMap : ActMap
{
    private const int MapWidth = 7;
    private const int CenterColumn = MapWidth / 2;

    private readonly MapPoint[,] _grid = new MapPoint[MapWidth, StageRouteDefinition.Nodes.Count];
    private readonly Dictionary<MapPoint, StageRouteNode> _nodesByPoint = [];

    /// <summary>
    /// 创建位于地图中线、只包含确定性单路线的舞台地图。
    /// </summary>
    public StageActMap()
    {
        MapPoint? previous = null;

        for (var row = 0; row < StageRouteDefinition.Nodes.Count; row++)
        {
            var node = StageRouteDefinition.Nodes[row];
            var mapPoint = new MapPoint(CenterColumn, row)
            {
                PointType = node.MapPointType,
                CanBeModified = false,
            };

            previous?.AddChildPoint(mapPoint);
            _grid[CenterColumn, row] = mapPoint;
            _nodesByPoint.Add(mapPoint, node);
            previous = mapPoint;
        }

        startMapPoints.Add(StartingMapPoint);
    }

    /// <summary>获取可见路线起点，即长颈鹿先古之民节点。</summary>
    public override MapPoint StartingMapPoint => _grid[CenterColumn, 0];

    /// <summary>获取可见路线终点，即 Crychic 亡灵首领节点。</summary>
    public override MapPoint BossMapPoint => _grid[CenterColumn, StageRouteDefinition.Nodes.Count - 1];

    /// <summary>暴露引擎要求的地图网格；网格外没有可见或可进入节点。</summary>
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
