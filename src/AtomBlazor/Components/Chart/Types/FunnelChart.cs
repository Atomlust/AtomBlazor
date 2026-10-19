namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的漏斗图组件。
/// </summary>
/// <remarks>
/// <para>
/// FunnelChart 组件为转化管线、销售流程与逐级筛选提供漏斗可视化。
/// 各段按数值定尺寸，可按升序、降序排序，或按数据
/// 顺序呈现。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;FunnelChart Data="@data" Height="350px"&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;ChartLegend /&gt;
///     &lt;Funnel DataKey="value" NameKey="stage" Sort="FunnelSort.Descending" /&gt;
/// &lt;/FunnelChart&gt;
/// </code>
/// </para>
/// </remarks>
public class FunnelChart : ChartBase
{
    internal override string DataSlot => "funnel-chart";

    internal override string SeriesType => "funnel";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // 漏斗图不使用 XAxis、YAxis 或 Grid
        option.XAxis = null;
        option.YAxis = null;
        option.Grid = null;
    }
}
