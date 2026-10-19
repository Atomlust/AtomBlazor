using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 所有可组合图表系列组件的抽象基类。
/// </summary>
/// <remarks>
/// <para>
/// SeriesBase 为系列组件提供共享功能：数据提取、颜色解析、
/// 名称解析与堆叠支持。每个具体系列（Line、Bar、
/// Area、Pie、Radar、RadialBar）都继承本类并实现 BuildSeriesCore。
/// </para>
/// <para>
/// 系列组件经级联参数向父 <see cref="ChartBase"/> 注册，并在渲染期间
/// 向图表的 option 树贡献一个 <see cref="EChartsSeriesOption"/>。
/// </para>
/// </remarks>
public abstract class SeriesBase : ComponentBase, IChartSeries, IDisposable
{
    [CascadingParameter]
    protected ChartBase? ParentChart { get; set; }

    /// <summary>
    /// 获取或设置用于从图表数据提取系列值的属性名。
    /// </summary>
    /// <remarks>
    /// 对应每个数据项上的一个属性。例如数据项带有 "Desktop" 属性时，
    /// 将 DataKey 设为 "Desktop" 即可为本系列提取那些值。
    /// </remarks>
    [Parameter]
    public string? DataKey { get; set; }

    /// <summary>
    /// 获取或设置本系列的展示名称。
    /// </summary>
    /// <remarks>
    /// 用于 legend 与 tooltip。未设置时先退到 <see cref="ChartConfig"/>
    /// 里配置的标签，再退到 <see cref="DataKey"/> 值。
    /// </remarks>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置本系列的显式颜色。
    /// </summary>
    /// <remarks>
    /// 接受任何合法的 CSS 颜色值或 CSS 变量引用（如 "var(--chart-1)"）。
    /// 未设置时退到 <see cref="ChartConfig"/> 中配置的颜色。
    /// </remarks>
    [Parameter]
    public string? Color { get; set; }

    /// <summary>
    /// 获取或设置子内容，其中可含 <see cref="Fill"/> 组件。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置直接的填充颜色值（字符串或渐变对象）。
    /// </summary>
    internal object? FillColor { get; set; }

    /// <summary>
    /// 获取或设置用于惰性求值的填充提供者。
    /// </summary>
    /// <remarks>
    /// 由 <see cref="Fill"/> 组件设置。BuildFill 在 <see cref="BuildSeriesCore"/>
    /// 期间（OnAfterRenderAsync）被惰性调用，届时全部子组件都已存在。
    /// </remarks>
    internal IFillComponent? FillProvider { get; set; }

    /// <summary>
    /// 获取解析后的填充颜色，优先经 FillProvider 惰性求值。
    /// </summary>
    protected object? GetResolvedFillColor() =>
        FillProvider?.BuildFill() ?? FillColor;

    protected override void OnInitialized() =>
        ParentChart?.RegisterComponent(this);

    void IChartComponent.ApplyTo(EChartsOption option) =>
        ApplyToOption(option);

    /// <summary>
    /// 把本系列应用到图表 option。若除添加系列外还须改动非系列的 option
    /// 属性（如轴配置），请重写本方法。
    /// </summary>
    private protected virtual void ApplyToOption(EChartsOption option)
    {
        var series = BuildSeriesCore();
        option.Series.Add(series);
    }

    EChartsSeriesOption IChartSeries.BuildSeries() => BuildSeriesCore();

    /// <summary>
    /// 为本系列类型构建 ECharts 系列选项。
    /// </summary>
    /// <returns>配置齐备的 <see cref="EChartsSeriesOption"/>。</returns>
    internal abstract EChartsSeriesOption BuildSeriesCore();

    /// <summary>
    /// 从父图表的数据中为本系列提取数值数据。
    /// </summary>
    /// <returns>按 <see cref="DataKey"/> 属性提取出的值列表。</returns>
    protected List<object?> GetSeriesData()
    {
        if (string.IsNullOrEmpty(DataKey))
        {
            return [];
        }

        return DataExtractor.ExtractValues(ParentChart?.Data, DataKey);
    }

    /// <summary>
    /// 当给出 <paramref name="xDataKey"/> 时，为本系列提取显式的 <c>[x, y]</c> 点对，
    /// 未给出时退回到扁平的 <see cref="GetSeriesData"/> 列表。
    /// </summary>
    /// <param name="xDataKey">
    /// 承载各点 X 值的属性；取 <see langword="null"/> 则 X 按位置计。
    /// </param>
    /// <remarks>
    /// <para>
    /// 扁平的 Y 值列表会让 ECharts 从点的 *index* 推出 X，这在 category 轴上是对的，
    /// 却会在 <see cref="AxisType.Value"/>、<see cref="AxisType.Time"/> 或 <see cref="AxisType.Log"/>
    /// 轴上无声丢掉真实的 X 值——这类轴无视自身的 <c>data</c>，点子遂按序数位置
    /// 绘制。只有成对输出，才谈得上一张
    /// 真正的 X:Y 散点图。
    /// </para>
    /// <para>
    /// 缺任一边的行产出 null 项而非半截点对，后者会被 ECharts
    /// 按被强转的零值渲染。null 项在绘制时会被跳过；而且——因为诸如逐点符号定尺寸这类
    /// 调用方会把本列表按序号与源数据拉链对照——返回的列表
    /// 始终与图表数据保持平行，而不是被压实。
    /// </para>
    /// </remarks>
    /// <returns>二元 <c>[x, y]</c> 数组的列表，或扁平的 Y 列表。</returns>
    protected List<object?> GetPointData(string? xDataKey)
    {
        if (string.IsNullOrEmpty(xDataKey))
        {
            return GetSeriesData();
        }

        if (string.IsNullOrEmpty(DataKey))
        {
            return [];
        }

        var xValues = DataExtractor.ExtractValues(ParentChart?.Data, xDataKey);
        var yValues = DataExtractor.ExtractValues(ParentChart?.Data, DataKey);
        var count = Math.Min(xValues.Count, yValues.Count);
        var points = new List<object?>(count);

        for (var i = 0; i < count; i++)
        {
            points.Add(xValues[i] == null || yValues[i] == null
                ? null
                : new[] { xValues[i], yValues[i] });
        }

        return points;
    }

    /// <summary>
    /// 解析本系列的颜色：先看显式的 Color 参数，再看 ChartConfig，
    /// 两者皆无则返回 null。
    /// </summary>
    /// <returns>CSS 颜色字符串；未配置颜色时为 null。</returns>
    protected string? GetResolvedColor()
    {
        if (!string.IsNullOrEmpty(Color))
        {
            return Color;
        }

        if (!string.IsNullOrEmpty(DataKey) && ParentChart?.Config != null)
        {
            var configColor = ParentChart.Config.GetColor(DataKey);
            return configColor;
        }

        return null;
    }

    /// <summary>
    /// 解析本系列的展示名称：先看显式的 Name 参数，再看 ChartConfig 的标签，
    /// 最后退到 DataKey。
    /// </summary>
    /// <returns>解析出的系列展示名称。</returns>
    protected string? GetResolvedName()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            return Name;
        }

        if (!string.IsNullOrEmpty(DataKey) && ParentChart?.Config != null)
        {
            return ParentChart.Config.GetLabel(DataKey);
        }

        return DataKey;
    }

    /// <summary>
    /// 把 <see cref="LabelPosition"/> 枚举值转成其 ECharts 字符串表示。
    /// </summary>
    protected static string ToEChartsPosition(LabelPosition position) => position switch
    {
        LabelPosition.Top => "top",
        LabelPosition.Bottom => "bottom",
        LabelPosition.Left => "left",
        LabelPosition.Right => "right",
        LabelPosition.Inside => "inside",
        LabelPosition.InsideLeft => "insideLeft",
        LabelPosition.InsideRight => "insideRight",
        LabelPosition.InsideTop => "insideTop",
        LabelPosition.InsideBottom => "insideBottom",
        LabelPosition.Outside => "outside",
        LabelPosition.Center => "center",
        LabelPosition.Middle => "middle",
        _ => "top"
    };

    public void Dispose()
    {
        ParentChart?.UnregisterComponent(this);
        GC.SuppressFinalize(this);
    }
}
