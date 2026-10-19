namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的桑基图组件。
/// </summary>
/// <remarks>
/// <para>
/// 桑基图展示一个量在各阶段之间流动时如何拆分与重组——
/// 网站流量、部门预算、流程中的能量皆可。节点即阶段，两节点间的缎带
/// 按沿其流动的数值成比例绘制。
/// </para>
/// <para>
/// 请绑定链路（links）集合而非节点集合：<see cref="Sankey"/> 从每行读取源节点名、目标节点名
/// 与数值，并据此推导出节点列表。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;SankeyChart Data="@links" Height="400px"&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Sankey SourceKey="from" TargetKey="to" DataKey="value" /&gt;
/// &lt;/SankeyChart&gt;
/// </code>
/// </para>
/// </remarks>
public class SankeyChart : ChartBase
{
    internal override string DataSlot => "sankey-chart";

    internal override string SeriesType => "sankey";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // 桑基图依自身的 left/top/right/bottom 定位，没有可挂靠的坐标轴。
        option.XAxis = null;
        option.YAxis = null;
        option.Grid = null;
    }
}
