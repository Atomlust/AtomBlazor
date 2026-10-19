using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的径向柱图组件。
/// </summary>
/// <remarks>
/// <para>
/// RadialBarChart 组件借助 ECharts 极坐标（polar）把数值
/// 呈现为绕中心点的圆弧。它适合展示进度、完成百分比
/// 或对比值。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;RadialBarChart Data="@data" Height="250px"&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;RadialBar DataKey="visitors" NameKey="browser" /&gt;
/// &lt;/RadialBarChart&gt;
/// </code>
/// </para>
/// </remarks>
public class RadialBarChart : ChartBase
{
    /// <summary>
    /// 获取或设置起始角度（度）。
    /// </summary>
    /// <remarks>
    /// 默认为 90（圆顶）。ECharts 的角度自 3 点钟方向起逆时针计量。
    /// </remarks>
    [Parameter]
    public int StartAngle { get; set; } = 90;

    /// <summary>
    /// 获取或设置结束角度（度）。
    /// </summary>
    /// <remarks>
    /// 整圆时默认为 -270。在 StartAngle = 90 的前提下，设为 -90 得半圆，设为 0 得四分之一圆。
    /// </remarks>
    [Parameter]
    public int EndAngle { get; set; } = -270;

    /// <summary>
    /// 获取或设置极坐标系的内半径（百分比）。
    /// </summary>
    [Parameter]
    public string InnerRadius { get; set; } = "30%";

    /// <summary>
    /// 获取或设置极坐标系的外半径（百分比）。
    /// </summary>
    [Parameter]
    public string OuterRadius { get; set; } = "80%";

    internal override string DataSlot => "radial-bar-chart";

    internal override string SeriesType => "bar";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        option.XAxis = null;
        option.YAxis = null;
        option.Grid = null;

        option.Polar = new EChartsPolarOption
        {
            Radius = new List<string> { InnerRadius, OuterRadius }
        };

        option.AngleAxis = new EChartsAngleAxisOption
        {
            StartAngle = StartAngle,

            // 此前该值被省略，导致无论 EndAngle 怎么写，圆弧总是扫满整圆——
            // 半圆仪表盘也会被画成整圆。
            EndAngle = EndAngle,
            Show = false
        };

        option.RadiusAxis = new EChartsRadiusAxisOption
        {
            Type = "category",
            Show = false
        };
    }
}
