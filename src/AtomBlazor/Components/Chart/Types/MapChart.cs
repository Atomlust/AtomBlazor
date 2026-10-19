namespace AtomBlazor;

/// <summary>
/// 按数据值为各国家着色的世界地图图（choropleth）。
/// </summary>
/// <remarks>
/// 请添加 <see cref="Map"/> 以绑定国家与数值。色阶会依数据自动推导；
/// 如需自定义其范围与颜色，请添加 <see cref="VisualMap"/>。国界线随库一并打包，
/// 仅在用到地图时才加载。
/// </remarks>
public class MapChart : ChartBase
{
    internal override string DataSlot => "map-chart";

    internal override string SeriesType => "map";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // 地图无需笛卡尔坐标轴或网格即可自行定位。
    }
}
