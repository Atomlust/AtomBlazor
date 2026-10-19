using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的面积系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个面积系列（折线加下方填充区域）。
/// 支持平滑曲线、阶梯插值、可配置的填充不透明度，以及经子
/// <see cref="Fill"/> 组件实现的渐变填充。
/// </para>
/// <para>
/// 内部使用带 areaStyle 配置的 ECharts line 类型。
/// 必须置于提供 <see cref="ChartBase"/> 级联值的图表组件内部。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;AreaChart Data="@data"&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
///     &lt;Area DataKey="desktop" Name="Desktop" Curve="CurveType.Smooth" FillOpacity="0.3"&gt;
///         &lt;Fill&gt;
///             &lt;LinearGradient&gt;
///                 &lt;GradientStop Offset="0" Color="var(--chart-1)" Opacity="0.8" /&gt;
///                 &lt;GradientStop Offset="1" Color="var(--chart-1)" Opacity="0.1" /&gt;
///             &lt;/LinearGradient&gt;
///         &lt;/Fill&gt;
///     &lt;/Area&gt;
/// &lt;/AreaChart&gt;
/// </code>
/// </example>
public partial class Area : StackableSeriesBase
{
    /// <summary>
    /// 获取或设置存放各数据点 X 值的属性。
    /// </summary>
    /// <remarks>
    /// 若要在数值或时间 X 轴上绘图——各点位于自身 X 坐标
    /// 而非等距位置（例如不规则采样间隔）——请设置本属性。常规
    /// 类目面积图请留空，此时 X 轴经 <see cref="XAxis.DataKey"/>
    /// 提供标签。
    /// </remarks>
    [Parameter]
    public string? XDataKey { get; set; }

    /// <summary>
    /// 获取或设置曲线插值类型。
    /// </summary>
    /// <remarks>
    /// 控制数据点的连接方式。默认为 <see cref="CurveType.Linear"/>。
    /// </remarks>
    [Parameter]
    public CurveType Curve { get; set; } = CurveType.Linear;

    /// <summary>
    /// 获取或设置折线描边宽度（像素）。
    /// </summary>
    [Parameter]
    public int StrokeWidth { get; set; } = 2;

    /// <summary>
    /// 获取或设置折线下方填充区域的不透明度。
    /// </summary>
    /// <remarks>
    /// 取值介于 0（全透明）与 1（不透明）之间。默认为 0.4。
    /// </remarks>
    [Parameter]
    public double FillOpacity { get; set; } = 0.4;

    /// <summary>
    /// 获取或设置数据点圆点是否可见。
    /// </summary>
    [Parameter]
    public bool ShowDots { get; set; }

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var resolvedColor = GetResolvedColor();
        var fillColor = GetResolvedFillColor();

        var series = new EChartsSeriesOption
        {
            Type = "line",
            Name = GetResolvedName(),
            Data = GetPointData(XDataKey),
            ShowSymbol = ShowDots,
            AreaStyle = new EChartsAreaStyleOption
            {
                Opacity = fillColor != null ? 1.0 : FillOpacity,
                Color = fillColor
            },
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

        if (Stacked)
        {
            series.Stack = StackGroup;
        }

        switch (Curve)
        {
            case CurveType.Smooth:
                series.Smooth = true;
                break;
            case CurveType.Step:
                series.Step = "middle";
                break;
            case CurveType.StepBefore:
                series.Step = "start";
                break;
            case CurveType.StepAfter:
                series.Step = "end";
                break;
        }

        return series;
    }
}
