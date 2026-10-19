namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的饼图/环形图（pie/donut）组件。
/// </summary>
/// <remarks>
/// <para>
/// PieChart 组件提供圆形可视化，用于展示占比与部分对整体的关系。
/// 通过 Pie 系列上的内/外半径控制可创建环形图变体。
/// 富文本中心文字请使用 CenterLabel。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;PieChart Data="@data" Height="350px"&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Pie DataKey="value" NameKey="name" InnerRadius="60%" OuterRadius="80%"&gt;
///         &lt;CenterLabel Title="Total" Value="1,234" /&gt;
///     &lt;/Pie&gt;
/// &lt;/PieChart&gt;
/// </code>
/// </para>
/// </remarks>
public class PieChart : ChartBase
{
    internal override string DataSlot => "pie-chart";

    internal override string SeriesType => "pie";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // 饼图不使用 XAxis、YAxis 或 Grid
        option.XAxis = null;
        option.YAxis = null;
        option.Grid = null;
    }
}
