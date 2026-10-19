using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的仪表盘系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个仪表盘（speedometer/meter，指针表盘）系列，用于 KPI
/// 可视化，支持可配置的指针、progress 弧与数值显示。
/// </para>
/// <para>
/// 数据以简单数值形式经 <see cref="SeriesBase.DataKey"/> 提供——其提取首个
/// 数据点的值——或经图表的数据源提供。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;GaugeChart Data="@data"&gt;
///     &lt;Gauge DataKey="value" Name="Speed" Min="0" Max="100" ShowProgress="true" /&gt;
/// &lt;/GaugeChart&gt;
/// </code>
/// </example>
public partial class Gauge : SeriesBase
{
    /// <summary>
    /// 获取或设置仪表盘刻度范围的最小值。
    /// </summary>
    [Parameter]
    public double Min { get; set; }

    /// <summary>
    /// 获取或设置仪表盘刻度范围的最大值。
    /// </summary>
    [Parameter]
    public double Max { get; set; } = 100;

    /// <summary>
    /// 获取或设置仪表盘的起始角（度）。
    /// </summary>
    /// <remarks>
    /// 0 位于钟面 3 点方向。默认为 225（约 7 点方向）。
    /// </remarks>
    [Parameter]
    public int StartAngle { get; set; } = 225;

    /// <summary>
    /// 获取或设置仪表盘的终止角（度）。
    /// </summary>
    /// <remarks>
    /// 默认为 -45，与默认 <see cref="StartAngle"/> 合成 270 度弧。
    /// </remarks>
    [Parameter]
    public int EndAngle { get; set; } = -45;

    /// <summary>
    /// 获取或设置仪表盘刻度范围的分割段数。
    /// </summary>
    [Parameter]
    public int SplitNumber { get; set; } = 10;

    /// <summary>
    /// 获取或设置是否显示 progress 弧。
    /// </summary>
    [Parameter]
    public bool ShowProgress { get; set; }

    /// <summary>
    /// 获取或设置 progress 弧的宽度（像素）。
    /// </summary>
    [Parameter]
    public int ProgressWidth { get; set; } = 10;

    /// <summary>
    /// 获取或设置 progress 弧两端是否为圆头（round cap）。
    /// </summary>
    [Parameter]
    public bool ProgressRoundCap { get; set; } = true;

    /// <summary>
    /// 获取或设置 progress 弧的颜色。
    /// </summary>
    [Parameter]
    public string? ProgressColor { get; set; }

    /// <summary>
    /// 获取或设置是否显示指针。
    /// </summary>
    [Parameter]
    public bool ShowPointer { get; set; } = true;

    /// <summary>
    /// 获取或设置指针长度（占仪表盘半径的百分比）。
    /// </summary>
    [Parameter]
    public string PointerLength { get; set; } = "60%";

    /// <summary>
    /// 获取或设置指针的宽度（像素）。
    /// </summary>
    [Parameter]
    public int PointerWidth { get; set; } = 6;

    /// <summary>
    /// 获取或设置是否显示轴线弧。
    /// </summary>
    [Parameter]
    public bool ShowAxisLine { get; set; } = true;

    /// <summary>
    /// 获取或设置轴线弧的宽度（像素）。
    /// </summary>
    [Parameter]
    public int AxisLineWidth { get; set; } = 10;

    /// <summary>
    /// 获取或设置是否显示数值详情文本。
    /// </summary>
    [Parameter]
    public bool ShowDetail { get; set; } = true;

    /// <summary>
    /// 获取或设置数值详情的 ECharts formatter 字符串。
    /// </summary>
    [Parameter]
    public string DetailFormatter { get; set; } = "{value}";

    /// <summary>
    /// 获取或设置数值详情的字号（像素）。
    /// </summary>
    [Parameter]
    public int DetailFontSize { get; set; } = 24;

    /// <summary>
    /// 获取或设置数值详情文本的颜色。
    /// </summary>
    [Parameter]
    public string? DetailColor { get; set; }

    /// <summary>
    /// 获取或设置是否显示仪表盘下方的标题文本。
    /// </summary>
    [Parameter]
    public bool ShowTitle { get; set; } = true;

    /// <summary>
    /// 获取或设置标题文本的字号（像素）。
    /// </summary>
    [Parameter]
    public int TitleFontSize { get; set; } = 14;

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var resolvedColor = GetResolvedColor();

        var series = new EChartsSeriesOption
        {
            Type = "gauge",
            Name = GetResolvedName(),
            Min = Min,
            Max = Max,
            StartAngle = StartAngle,
            EndAngle = EndAngle,
            SplitNumber = SplitNumber,
            Pointer = new EChartsGaugePointerOption
            {
                Show = ShowPointer,
                Length = PointerLength,
                Width = PointerWidth,
                ItemStyle = resolvedColor != null
                    ? new EChartsItemStyleOption { Color = resolvedColor }
                    : null
            },
            AxisLine = new EChartsGaugeAxisLineOption
            {
                Show = ShowAxisLine,
                LineStyle = new EChartsGaugeAxisLineStyleOption
                {
                    Width = AxisLineWidth,
                    Color = new object[]
                    {
                        new object[] { 1, "var(--muted)" }
                    }
                }
            },
            AxisTick = new EChartsAxisTickOption
            {
                Show = false
            },
            SplitLine = new EChartsGaugeSplitLineOption
            {
                Show = true,
                Length = 10,
                LineStyle = new EChartsLineStyleOption
                {
                    Color = "var(--muted-foreground)",
                    Width = 1
                }
            },
            Detail = new EChartsGaugeDetailOption
            {
                Show = ShowDetail,
                Formatter = DetailFormatter,
                FontSize = DetailFontSize,
                FontWeight = "bold",
                Color = DetailColor ?? "var(--foreground)",
                ValueAnimation = true,
                OffsetCenter = [0, "70%"]
            },
            Title = new EChartsSeriesTitleOption
            {
                Show = ShowTitle,
                FontSize = TitleFontSize,
                Color = "var(--muted-foreground)",
                OffsetCenter = [0, "90%"]
            },
            Emphasis = new EChartsEmphasisOption { Disabled = true }
        };

        if (ShowProgress)
        {
            series.Progress = new EChartsGaugeProgressOption
            {
                Show = true,
                Width = ProgressWidth,
                RoundCap = ProgressRoundCap,
                Color = ProgressColor ?? resolvedColor
            };
        }

        // 提取仪表盘数据：[{value: X, name: "Label"}]
        var rawData = GetSeriesData();
        if (rawData.Count > 0)
        {
            var gaugeData = new List<object?>();
            var resolvedName = GetResolvedName();

            foreach (var value in rawData)
            {
                gaugeData.Add(new Dictionary<string, object?>
                {
                    ["value"] = value,
                    ["name"] = resolvedName
                });
            }

            series.Data = gaugeData;
        }

        return series;
    }
}
