using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的玫瑰图（南丁格尔图）系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 玫瑰图是一种扇区半径亦随数值变化的饼图，因此它继承 <see cref="Pie"/> 提供的
/// 全部参数——环形内孔、标签、引导线、子 <see cref="CenterLabel"/>——并在其上追加
/// <see cref="Mode"/>。直接自 <see cref="Pie"/> 而非把饼图重写一遍，两者方能
/// 步调一致：对饼图 label 或中心标签处理的任何修复
/// 玫瑰图同样受益。
/// </para>
/// <para>
/// 默认的 <see cref="RoseMode.Radius"/> 保留饼图成比例的圆心角并叠加半径变化，于是
/// 一个扇区把同一个数字编码了两遍，小数值依旧可见。<see cref="RoseMode.Area"/> 则给
/// 每个扇区相等的圆心角、仅半径变化；当类目是一个固定集合——十二个月、七天——
/// 而非整体的组成部分时，它才是诚实的选择。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;RoseChart Data="@data"&gt;
/// &lt;ChartTooltip /&gt;
/// &lt;Rose DataKey="visitors" NameKey="browser" Mode="RoseMode.Area" /&gt;
/// &lt;/RoseChart&gt;
/// </code>
/// </example>
public class Rose : Pie
{
    /// <summary>
    /// 获取或设置值如何映射到扇区。
    /// </summary>
    /// <remarks>
    /// 映射到 ECharts 的 <c>roseType</c> 选项。
    /// </remarks>
    [Parameter]
    public RoseMode Mode { get; set; } = RoseMode.Radius;

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var series = base.BuildSeriesCore();

        series.RoseType = Mode == RoseMode.Area ? "area" : "radius";

        return series;
    }
}
