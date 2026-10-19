namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的玫瑰图（南丁格尔图）组件。
/// </summary>
/// <remarks>
/// <para>
/// 玫瑰图是一种扇区半径亦随数值变化的饼图，扇区一长，数值之大便一目了然。
/// 它适合那些差异大到等半径扇区难以排序的类目集合——
/// 饼图要你去比较角度，玫瑰图则让你比较长短。
/// </para>
/// <para>
/// 该系列的底层是 ECharts 饼图，因此 <see cref="Pie"/> 提供的一切——标签、
/// 环形内孔、<see cref="CenterLabel"/>——在 <see cref="Rose"/> 上同样可用。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;RoseChart Data="@data" Height="350px"&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Rose DataKey="visitors" NameKey="browser" Mode="RoseMode.Radius" /&gt;
/// &lt;/RoseChart&gt;
/// </code>
/// </para>
/// </remarks>
public class RoseChart : ChartBase
{
    internal override string DataSlot => "rose-chart";

    /// <summary>
    /// ECharts 系列类型，即 <c>pie</c>——玫瑰图只是以可变半径绘制的饼图，
    /// 并非自有的系列类型。如实上报它，正是该图表得以获得饼图本就具备的
    /// 条目触发式 tooltip 与居中式图例布局的原因。
    /// </summary>
    internal override string SeriesType => "pie";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // 玫瑰图不使用 XAxis、YAxis 或 Grid
        option.XAxis = null;
        option.YAxis = null;
        option.Grid = null;
    }
}
