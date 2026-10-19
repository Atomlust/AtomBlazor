using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为图表配置 X 轴。必须置于图表组件之内。
/// </summary>
/// <remarks>
/// <para>
/// XAxis 是可组合图表组件，经级联参数向父图表注册，
/// 并在构建 option 期间施加 X 轴配置。
/// </para>
/// <para>
/// 默认情况下 XAxis 使用 category 类型并隐藏轴线与刻度，外观干净。
/// 设置 <see cref="DataKey"/> 后，类别标签会自动从图表数据中提取。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BarChart Data="@data"&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
/// &lt;/BarChart&gt;
/// </code>
/// </example>
public partial class XAxis : ComponentBase, IChartComponent, IDisposable
{
    [CascadingParameter]
    private ChartBase? ParentChart { get; set; }

    /// <summary>
    /// 获取或设置用于从图表数据提取类别值的属性名。
    /// </summary>
    /// <remarks>
    /// 设置后，将按该属性名自动从每个数据项提取类别标签。
    /// </remarks>
    [Parameter]
    public string? DataKey { get; set; }

    /// <summary>
    /// 获取或设置轴类型。
    /// </summary>
    /// <remarks>
    /// 类别数据默认为 <see cref="AxisType.Category"/>。
    /// 数值轴用 <see cref="AxisType.Value"/>，时间序列用 <see cref="AxisType.Time"/>，
    /// 对数刻度用 <see cref="AxisType.Log"/>。
    /// </remarks>
    [Parameter]
    public AxisType Type { get; set; } = AxisType.Category;

    /// <summary>
    /// 获取或设置轴是否可见。
    /// </summary>
    [Parameter]
    public bool Show { get; set; } = true;

    /// <summary>
    /// 获取或设置轴线是否可见。
    /// </summary>
    [Parameter]
    public bool ShowAxisLine { get; set; }

    /// <summary>
    /// 获取或设置轴刻度线是否可见。
    /// </summary>
    [Parameter]
    public bool ShowTicks { get; set; }

    /// <summary>
    /// 获取或设置该轴是否显示分隔线。
    /// </summary>
    [Parameter]
    public bool ShowGrid { get; set; }

    /// <summary>
    /// 获取或设置分隔线的颜色。
    /// </summary>
    /// <remarks>
    /// 未指定时默认为 <c>var(--border)</c>，与设计系统的边框颜色一致。
    /// </remarks>
    [Parameter]
    public string? GridColor { get; set; }

    /// <summary>
    /// 获取或设置分隔线的样式。
    /// </summary>
    /// <remarks>
    /// 默认为 <see cref="LineStyleType.Dashed"/>。
    /// </remarks>
    [Parameter]
    public LineStyleType GridType { get; set; } = LineStyleType.Dashed;

    /// <summary>
    /// 获取或设置轴标签是否绘制在图表区内而非区外。
    /// </summary>
    /// <remarks>
    /// 为 <c>true</c> 时，类别标签定位于图表区内，与柱体或
    /// 数据区域相叠。适合希望标签出现在柱体内部的
    /// 横向柱状图。默认为 <c>false</c>。
    /// </remarks>
    [Parameter]
    public bool LabelInside { get; set; }

    /// <summary>
    /// 获取或设置轴标签的颜色。
    /// </summary>
    /// <remarks>
    /// 未指定时默认为 <c>var(--muted-foreground)</c>，与设计系统的弱化文本颜色一致。
    /// </remarks>
    [Parameter]
    public string? LabelColor { get; set; }

    /// <summary>
    /// 获取或设置轴标签的字号（像素）。
    /// </summary>
    [Parameter]
    public int? LabelFontSize { get; set; }

    /// <summary>
    /// 获取或设置轴标签的旋转角度（度）。
    /// </summary>
    /// <remarks>
    /// 适合本会相互重叠的长类别标签。正值顺时针旋转。
    /// </remarks>
    [Parameter]
    public int? LabelRotate { get; set; }

    /// <summary>
    /// 获取或设置轴边界与数据之间是否留有间隙。
    /// </summary>
    /// <remarks>
    /// 为 <c>true</c>（柱状图默认）时，数据在每个类别带中居中。
    /// 为 <c>false</c> 时，数据点对齐到类别边界（折线/面积图推荐）。
    /// 为 <c>null</c> 时采用 ECharts 默认（category 轴为 true）。
    /// </remarks>
    [Parameter]
    public bool? BoundaryGap { get; set; }

    /// <summary>
    /// 获取或设置随轴展示的轴名称。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置轴的最小值。
    /// </summary>
    /// <remarks>
    /// 可为数字，或字符串 <c>"dataMin"</c> 以取数据最小值。
    /// </remarks>
    [Parameter]
    public object? Min { get; set; }

    /// <summary>
    /// 获取或设置轴的最大值。
    /// </summary>
    /// <remarks>
    /// 可为数字，或字符串 <c>"dataMax"</c> 以取数据最大值。
    /// </remarks>
    [Parameter]
    public object? Max { get; set; }

    /// <summary>
    /// 获取或设置轴刻度是否自适应数据范围。
    /// </summary>
    /// <remarks>
    /// 仅适用于 value 轴；category 轴会忽略它。value 轴默认把零点包含在内，
    /// 这对量级是诚实的选择，但数据远离零点时便浪费绘图区——
    /// 160-190 的高度对一条从 0 起始的轴只占图表的最后六分之一。此类场合请设置
    /// 本项；而在距零点的远近本身就是读者应见之处的场合，请保持关闭。<see cref="Min"/> 与
    /// <see cref="Max"/> 同时设置时被忽略。
    /// </remarks>
    [Parameter]
    public bool? Scale { get; set; }

    protected override void OnInitialized() =>
        ParentChart?.RegisterComponent(this);

    void IChartComponent.ApplyTo(EChartsOption option)
    {
        var axis = new EChartsAxisOption
        {
            Type = Type switch
            {
                AxisType.Category => "category",
                AxisType.Value => "value",
                AxisType.Time => "time",
                AxisType.Log => "log",
                _ => "category"
            },
            BoundaryGap = BoundaryGap ?? (ParentChart?.SeriesType == "line" ? false : null),
            Show = Show,
            Name = Name,
            Min = Min,
            Max = Max,
            Scale = Scale,
            Z = LabelInside ? 10 : null,
            AxisLine = new EChartsAxisLineOption
            {
                Show = ShowAxisLine
            },
            AxisTick = new EChartsAxisTickOption
            {
                Show = ShowTicks
            },
            SplitLine = new EChartsSplitLineOption
            {
                Show = ShowGrid,
                LineStyle = ShowGrid
                    ? new EChartsLineStyleOption
                    {
                        Color = GridColor ?? "var(--border)",
                        Type = ToEChartsLineStyle(GridType)
                    }
                    : null
            },
            AxisLabel = new EChartsAxisLabelOption
            {
                Inside = LabelInside ? true : null,
                Color = LabelColor ?? "var(--muted-foreground)",
                FontSize = LabelFontSize,
                Rotate = LabelRotate
            }
        };

        if (!string.IsNullOrEmpty(DataKey) && ParentChart?.Data != null)
        {
            axis.Data = DataExtractor.ExtractDistinctStringValues(ParentChart.Data, DataKey);
        }

        // 父图表交换轴时（如横向 BarChart），
        // XAxis 的配置写入物理 Y 轴并加 inverse，
        // 使类别自上而下渲染而非自下而上
        if (ParentChart?.SwapAxes == true)
        {
            axis.Inverse = true;
            option.YAxis = axis;
        }
        else
        {
            option.XAxis = axis;
        }
    }

    private static string ToEChartsLineStyle(LineStyleType type) => type switch
    {
        LineStyleType.Solid => "solid",
        LineStyleType.Dotted => "dotted",
        _ => "dashed"
    };

    public void Dispose()
    {
        ParentChart?.UnregisterComponent(this);
        GC.SuppressFinalize(this);
    }
}
