using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的折线系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个折线系列。支持平滑曲线、阶梯插值、
/// 虚线，以及可配置的数据点显隐与尺寸。
/// </para>
/// <para>
/// 必须置于提供 <see cref="ChartBase"/> 级联值的图表组件内部。
/// 支持子 <see cref="Fill"/> 组件实现渐变填充。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;LineChart Data="@data"&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
///     &lt;Line DataKey="desktop" Name="Desktop" Curve="CurveType.Smooth" /&gt;
///     &lt;Line DataKey="mobile" Name="Mobile" Dashed="true" /&gt;
/// &lt;/LineChart&gt;
/// </code>
/// </example>
public partial class Line : StackableSeriesBase
{
    /// <summary>
    /// 获取或设置存放各数据点 X 值的属性。
    /// </summary>
    /// <remarks>
    /// 若要在数值或时间 X 轴上绘图——各点位于自身 X 坐标
    /// 而非等距位置（例如不规则采样间隔）——请设置本属性。常规
    /// 类目折线请留空，此时 X 轴经 <see cref="XAxis.DataKey"/>
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
    /// 获取或设置数据点圆点是否可见。
    /// </summary>
    [Parameter]
    public bool ShowDots { get; set; } = true;

    /// <summary>
    /// 获取或设置数据点圆点的尺寸（像素）。
    /// </summary>
    [Parameter]
    public int DotSize { get; set; } = 4;

    /// <summary>
    /// 获取或设置折线是否以虚线描边渲染。
    /// </summary>
    [Parameter]
    public bool Dashed { get; set; }

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var resolvedColor = GetResolvedColor();

        // 经 GetResolvedFillColor 读取，而非 FillColor 字段。该字段从不被赋值——
        // Fill 改为将自身注册为 FillProvider——因此直接读取它意味着
        // line 或 radar 系列内的 Fill 不会生效。
        var fillColor = GetResolvedFillColor();

        var series = new EChartsSeriesOption
        {
            Type = "line",
            Name = GetResolvedName(),
            Data = GetPointData(XDataKey),
            ShowSymbol = ShowDots,
            SymbolSize = ShowDots ? DotSize : 0,
            LineStyle = new EChartsLineStyleOption
            {
                Width = StrokeWidth,
                Color = resolvedColor,
                Type = Dashed ? "dashed" : null
            },
            ItemStyle = resolvedColor != null
                ? new EChartsItemStyleOption { Color = fillColor ?? resolvedColor }
                : fillColor != null
                    ? new EChartsItemStyleOption { Color = fillColor }
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
