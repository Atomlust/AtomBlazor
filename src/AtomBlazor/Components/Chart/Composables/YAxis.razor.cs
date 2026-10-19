using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为图表配置 Y 轴。必须置于图表组件之内。
/// </summary>
/// <remarks>
/// <para>
/// YAxis 是可组合图表组件，经级联参数向父图表注册，
/// 并在构建 option 期间施加 Y 轴配置。
/// </para>
/// <para>
/// 默认情况下 YAxis 使用 value 类型并显示分隔线以提升可读性。
/// 轴线与刻度线则隐藏，以求干净现代的外观。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;LineChart Data="@data"&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis ShowGrid="true" /&gt;
/// &lt;/LineChart&gt;
/// </code>
/// </example>
public partial class YAxis : ComponentBase, IChartComponent, IDisposable
{
    [CascadingParameter]
    private ChartBase? ParentChart { get; set; }

    /// <summary>
    /// 获取或设置用于从图表数据提取类别值的属性名。
    /// </summary>
    /// <remarks>
    /// 设置后，将按该属性名自动从每个数据项提取类别标签。
    /// 通常与 <see cref="AxisType.Category"/> 搭配用于热力图的 Y 轴。
    /// </remarks>
    [Parameter]
    public string? DataKey { get; set; }

    /// <summary>
    /// 获取或设置轴类型。
    /// </summary>
    /// <remarks>
    /// 数值数据默认为 <see cref="AxisType.Value"/>。
    /// </remarks>
    [Parameter]
    public AxisType Type { get; set; } = AxisType.Value;

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
    /// <remarks>
    /// Y 轴默认为 <c>true</c>，以便读数时比较数值更省力。
    /// </remarks>
    [Parameter]
    public bool ShowGrid { get; set; } = true;

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
    [Parameter]
    public int? LabelRotate { get; set; }

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
    /// 为 <c>true</c> 时，轴范围由数据决定而非从零起始。
    /// 对数值聚在窄区间内的 K 线及其他金融图表，这一项必不可少。
    /// </remarks>
    [Parameter]
    public bool? Scale { get; set; }

    protected override void OnInitialized() =>
        ParentChart?.RegisterComponent(this);

    void IChartComponent.ApplyTo(EChartsOption option)
    {
        var effectiveType = !string.IsNullOrEmpty(DataKey) && Type == AxisType.Value
            ? AxisType.Category
            : Type;

        var axis = new EChartsAxisOption
        {
            Type = effectiveType switch
            {
                AxisType.Category => "category",
                AxisType.Value => "value",
                AxisType.Time => "time",
                AxisType.Log => "log",
                _ => "value"
            },
            Show = Show,
            Name = Name,
            Min = Min,
            Max = Max,
            Scale = Scale,
            AxisLine = new EChartsAxisLineOption
            {
                Show = Show && ShowAxisLine
            },
            AxisTick = new EChartsAxisTickOption
            {
                Show = Show && ShowTicks
            },
            SplitLine = new EChartsSplitLineOption
            {
                Show = Show && ShowGrid,
                LineStyle = Show && ShowGrid
                    ? new EChartsLineStyleOption
                    {
                        Color = GridColor ?? "var(--border)",
                        Type = ToEChartsLineStyle(GridType)
                    }
                    : null
            },
            AxisLabel = Show
                ? new EChartsAxisLabelOption
                {
                    Color = LabelColor ?? "var(--muted-foreground)",
                    FontSize = LabelFontSize,
                    Rotate = LabelRotate
                }
                : new EChartsAxisLabelOption { Show = false }
        };

        if (!string.IsNullOrEmpty(DataKey) && ParentChart?.Data != null)
        {
            axis.Data = DataExtractor.ExtractDistinctStringValues(ParentChart.Data, DataKey);
        }

        // 父图表交换轴时（如横向 BarChart），
        // YAxis 的配置写入物理 X 轴
        if (ParentChart?.SwapAxes == true)
        {
            option.XAxis = axis;
        }
        else
        {
            option.YAxis = axis;
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
