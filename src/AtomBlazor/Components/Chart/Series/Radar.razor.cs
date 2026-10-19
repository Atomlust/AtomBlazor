using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的雷达系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个雷达（蜘蛛/网）系列。数据值作为多边形绘制在雷达轴上。
/// 支持可配置的填充不透明度、数据点（dot）可见性
/// 与描边宽度。
/// </para>
/// <para>
/// 必须置于提供 <see cref="ChartBase"/> 级联值的图表组件内部，
/// 且该图表已配置雷达坐标系。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;RadarChart Data="@data"&gt;
///     &lt;Radar DataKey="desktop" Name="Desktop" FillOpacity="0.3" /&gt;
///     &lt;Radar DataKey="mobile" Name="Mobile" FillOpacity="0.3" /&gt;
/// &lt;/RadarChart&gt;
/// </code>
/// </example>
public partial class Radar : SeriesBase
{
    /// <summary>
    /// 获取或设置雷达多边形内填充区域的不透明度。
    /// </summary>
    /// <remarks>
    /// 取值介于 0（透明）与 1（不透明）之间。默认为 0.25。
    /// 设为 0 时仅显示轮廓。
    /// </remarks>
    [Parameter]
    public double FillOpacity { get; set; } = 0.25;

    /// <summary>
    /// 获取或设置雷达顶点上的数据点（dot）是否可见。
    /// </summary>
    [Parameter]
    public bool ShowDots { get; set; } = true;

    /// <summary>
    /// 获取或设置数据点尺寸（像素）。
    /// </summary>
    [Parameter]
    public int DotSize { get; set; } = 4;

    /// <summary>
    /// 获取或设置线条描边宽度（像素）。
    /// </summary>
    [Parameter]
    public int StrokeWidth { get; set; } = 2;

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var resolvedColor = GetResolvedColor();
        var data = GetSeriesData();

        var series = new EChartsSeriesOption
        {
            Type = "radar",
            Name = GetResolvedName(),
            SymbolSize = ShowDots ? DotSize : 0,
            LineStyle = new EChartsLineStyleOption
            {
                Width = StrokeWidth,
                Color = resolvedColor
            },
            ItemStyle = resolvedColor != null
                ? new EChartsItemStyleOption { Color = resolvedColor }
                : null,
            Emphasis = new EChartsEmphasisOption { Disabled = true }
        };

        if (FillOpacity > 0)
        {
            series.AreaStyle = new EChartsAreaStyleOption
            {
                Opacity = FillOpacity,

                // 经 GetResolvedFillColor 取得：FillColor 字段从未被赋值，直接读它
                // 会忽略系列内部设置的 Fill。
                Color = GetResolvedFillColor()
            };
        }

        // 雷达系列的数据结构为 [{value: [...], name: "..."}]
        series.Data = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["value"] = data,
                ["name"] = GetResolvedName()
            }
        };

        return series;
    }
}
