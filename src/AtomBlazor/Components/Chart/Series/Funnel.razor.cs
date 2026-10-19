using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的漏斗系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个漏斗系列。数据以名称-值对形式提取：值用
/// <see cref="SeriesBase.DataKey"/>，标签用 <see cref="NameKey"/>。适用于可视化
/// 转化漏斗、销售管线与逐级筛选。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FunnelChart Data="@data"&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Funnel DataKey="value" NameKey="stage" /&gt;
/// &lt;/FunnelChart&gt;
/// </code>
/// </example>
public partial class Funnel : SeriesBase
{
    /// <summary>
    /// 获取或设置用于从图表数据提取类目名称的属性名。
    /// </summary>
    [Parameter]
    public string? NameKey { get; set; }

    /// <summary>
    /// 获取或设置漏斗图各段的排序方式。
    /// </summary>
    [Parameter]
    public FunnelSort Sort { get; set; } = FunnelSort.Descending;

    /// <summary>
    /// 获取或设置漏斗图各段的水平对齐方式。
    /// </summary>
    [Parameter]
    public FunnelAlign Align { get; set; } = FunnelAlign.Center;

    /// <summary>
    /// 获取或设置漏斗图各段之间的间隔（像素）。
    /// </summary>
    [Parameter]
    public int Gap { get; set; } = 2;

    /// <summary>
    /// 获取或设置最小段尺寸，取百分比或像素值。
    /// </summary>
    [Parameter]
    public string MinSize { get; set; } = "0%";

    /// <summary>
    /// 获取或设置最大段尺寸，取百分比或像素值。
    /// </summary>
    [Parameter]
    public string MaxSize { get; set; } = "100%";

    /// <summary>
    /// 获取或设置是否在每个段上显示标签。
    /// </summary>
    [Parameter]
    public bool ShowLabels { get; set; } = true;

    /// <summary>
    /// 获取或设置 label 相对漏斗各段的位置。
    /// </summary>
    [Parameter]
    public LabelPosition LabelPosition { get; set; } = LabelPosition.Inside;

    /// <summary>
    /// 获取或设置 ECharts label 格式化字符串。
    /// </summary>
    [Parameter]
    public string? LabelFormatter { get; set; }

    /// <summary>
    /// 获取或设置 label 文本颜色。
    /// </summary>
    [Parameter]
    public string? LabelColor { get; set; }

    /// <summary>
    /// 获取或设置 label 字号（像素）。
    /// </summary>
    [Parameter]
    public int? LabelFontSize { get; set; }

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var series = new EChartsSeriesOption
        {
            Type = "funnel",
            Name = GetResolvedName(),
            Sort = Sort switch
            {
                FunnelSort.Ascending => "ascending",
                FunnelSort.None => "none",
                _ => "descending"
            },
            FunnelAlign = Align switch
            {
                FunnelAlign.Left => "left",
                FunnelAlign.Right => "right",
                _ => "center"
            },
            Gap = Gap > 0 ? Gap : null,
            MinSize = MinSize,
            MaxSize = MaxSize,
            Left = "10%",
            Top = "10",
            Right = "10%",
            Bottom = "10",
            Width = "80%",
            Label = new EChartsLabelOption
            {
                Show = ShowLabels,
                Position = ToEChartsPosition(LabelPosition),
                Formatter = LabelFormatter,
                Color = LabelColor ?? "var(--foreground)",
                FontSize = LabelFontSize,
                TextBorderWidth = 0
            },
            ItemStyle = new EChartsItemStyleOption
            {
                // 饼图或漏斗图会自图表调色板为每个扇区着色；系列上显式的 Color 则
                // 把所有扇区涂成同色。此前该参数不生效——它被继承了、被展示了，
                // 然后被忽略了。
                Color = GetResolvedColor(),
                BorderColor = "var(--background)",
                BorderWidth = Gap > 0 ? Gap : null
            },
            Emphasis = new EChartsEmphasisOption { Disabled = true }
        };

        if (!string.IsNullOrEmpty(DataKey) && !string.IsNullOrEmpty(NameKey))
        {
            var pairs = DataExtractor.ExtractNameValuePairs(ParentChart?.Data, NameKey, DataKey);
            series.Data = new List<object?>(pairs.Select(p => (object?)p));
        }

        return series;
    }
}
