namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的仪表盘组件。
/// </summary>
/// <remarks>
/// <para>
/// GaugeChart 组件提供仪表盘（speedometer/meter，指针表盘）可视化，用于 KPI 展示。
/// 支持可配置的指针、progress 弧、轴线样式与数值详情显示。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;GaugeChart Data="@data" Height="350px"&gt;
///     &lt;Gauge DataKey="value" Name="Speed" Min="0" Max="200"
///              ShowProgress="true" Color="var(--chart-1)" /&gt;
/// &lt;/GaugeChart&gt;
/// </code>
/// </para>
/// </remarks>
public class GaugeChart : ChartBase
{
    internal override string DataSlot => "gauge-chart";

    internal override string SeriesType => "gauge";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // 仪表盘不使用 XAxis、YAxis 或 Grid
        option.XAxis = null;
        option.YAxis = null;
        option.Grid = null;
    }
}
