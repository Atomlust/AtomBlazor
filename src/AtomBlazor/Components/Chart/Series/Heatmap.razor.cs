using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的热力图系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个热力图系列。数据经 <see cref="XKey"/>、
/// <see cref="YKey"/> 与 <see cref="ValueKey"/> 以 X-Y-值三元组提取。
/// 与 <see cref="VisualMap"/> 组合件搭配使用以配置颜色渐变。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;HeatmapChart Data="@data"&gt;
///     &lt;XAxis DataKey="day" /&gt;
///     &lt;YAxis DataKey="hour" /&gt;
///     &lt;VisualMap Min="0" Max="100" /&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Heatmap XKey="dayIndex" YKey="hourIndex" ValueKey="value" /&gt;
/// &lt;/HeatmapChart&gt;
/// </code>
/// </example>
public partial class Heatmap : SeriesBase
{
    /// <summary>
    /// 获取或设置 X 坐标（列索引或类目索引）的属性名。
    /// </summary>
    [Parameter]
    public string? XKey { get; set; }

    /// <summary>
    /// 获取或设置 Y 坐标（行索引或类目索引）的属性名。
    /// </summary>
    [Parameter]
    public string? YKey { get; set; }

    /// <summary>
    /// 获取或设置单元格数值的属性名。
    /// </summary>
    [Parameter]
    public string? ValueKey { get; set; }

    /// <summary>
    /// 获取或设置是否在每个单元格内显示数据标签（label）。
    /// </summary>
    [Parameter]
    public bool ShowLabel { get; set; }

    /// <summary>
    /// 获取或设置 label 格式化字符串。
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
            Type = "heatmap",
            ItemStyle = new EChartsItemStyleOption { Color = GetResolvedFillColor() ?? GetResolvedColor() },
            Name = GetResolvedName(),
            Emphasis = new EChartsEmphasisOption
            {
                Disabled = true
            }
        };

        if (ShowLabel || !string.IsNullOrEmpty(LabelFormatter))
        {
            series.Label = new EChartsLabelOption
            {
                Show = true,
                Formatter = LabelFormatter,
                Color = LabelColor ?? "var(--foreground)",
                FontSize = LabelFontSize,
                TextBorderWidth = 0
            };
        }

        if (!string.IsNullOrEmpty(XKey) && !string.IsNullOrEmpty(YKey) && !string.IsNullOrEmpty(ValueKey))
        {
            var xValues = DataExtractor.ExtractValues(ParentChart?.Data, XKey);
            var yValues = DataExtractor.ExtractValues(ParentChart?.Data, YKey);
            var cellValues = DataExtractor.ExtractValues(ParentChart?.Data, ValueKey);
            var count = Math.Min(Math.Min(xValues.Count, yValues.Count), cellValues.Count);

            var data = new List<object?>(count);
            for (var i = 0; i < count; i++)
            {
                data.Add(new object?[] { xValues[i], yValues[i], cellValues[i] });
            }

            series.Data = data;
        }

        return series;
    }
}
