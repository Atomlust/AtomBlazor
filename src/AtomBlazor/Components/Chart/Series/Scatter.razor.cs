using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的散点/气泡系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个散点系列。支持可配置的符号尺寸，
/// 含经 <see cref="SymbolSizeKey"/> 实现的气泡图逐项定尺寸。
/// </para>
/// <para>
/// 必须置于提供 <see cref="ChartBase"/> 级联值的图表组件内部。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ScatterChart Data="@data"&gt;
///     &lt;XAxis Type="AxisType.Value" /&gt;
///     &lt;YAxis /&gt;
///     &lt;Scatter XDataKey="x" DataKey="y" Name="Series A" SymbolSize="12" /&gt;
/// &lt;/ScatterChart&gt;
/// </code>
/// </example>
public partial class Scatter : StackableSeriesBase
{
    /// <summary>
    /// 获取或设置承载各点 X 值的属性。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 真正的 X:Y 散点图请设置本值——各点会绘制在
    /// <see cref="AxisType.Value"/>、<see cref="AxisType.Time"/> 或 <see cref="AxisType.Log"/> 轴上各自独立的 X 坐标处。
    /// </para>
    /// <para>
    /// 类目散点图请留空不设——各点按其所在行的位置绘制，
    /// X 轴经 <see cref="XAxis.DataKey"/> 自行提供标签。
    /// 注意数值轴会无视这些标签，因此数值型 X 值只有在
    /// 由本值提供时才生效——在系列上，而非在轴上。
    /// </para>
    /// </remarks>
    [Parameter]
    public string? XDataKey { get; set; }

    /// <summary>
    /// 获取或设置散点符号尺寸（像素）。
    /// </summary>
    [Parameter]
    public int SymbolSize { get; set; } = 10;

    /// <summary>
    /// 获取或设置用于从图表数据逐项提取符号尺寸的属性名。
    /// </summary>
    /// <remarks>
    /// 设置后，每个数据点的符号尺寸从该属性读取，从而支持
    /// 以点尺寸表示第三维的气泡图可视化。
    /// </remarks>
    [Parameter]
    public string? SymbolSizeKey { get; set; }

    /// <summary>
    /// 获取或设置是否在每个数据点上显示数据标签（label）。
    /// </summary>
    [Parameter]
    public bool ShowLabel { get; set; }

    /// <summary>
    /// 获取或设置数据标签相对数据点的位置。
    /// </summary>
    [Parameter]
    public LabelPosition LabelPosition { get; set; } = LabelPosition.Top;

    /// <summary>
    /// 获取或设置 label 格式化字符串。
    /// </summary>
    /// <remarks>
    /// ECharts formatter 字符串。用 <c>{b}</c> 表示名称，<c>{c}</c> 表示数值。
    /// </remarks>
    [Parameter]
    public string? LabelFormatter { get; set; }

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var resolvedColor = GetResolvedColor();
        var effectiveColor = GetResolvedFillColor() ?? resolvedColor;
        var rawData = GetPointData(XDataKey);

        var series = new EChartsSeriesOption
        {
            Type = "scatter",
            Name = GetResolvedName(),
            SymbolSize = SymbolSize,
            Emphasis = new EChartsEmphasisOption { Disabled = true }
        };

        if (!string.IsNullOrEmpty(SymbolSizeKey))
        {
            var sizes = DataExtractor.ExtractValues(ParentChart?.Data, SymbolSizeKey);
            var itemData = new List<object?>(rawData.Count);

            for (var i = 0; i < rawData.Count; i++)
            {
                var size = (i < sizes.Count) ? sizes[i] : null;
                itemData.Add(new Dictionary<string, object?>
                {
                    ["value"] = rawData[i],
                    ["symbolSize"] = size
                });
            }

            series.Data = itemData;
            series.SymbolSize = null;
        }
        else
        {
            series.Data = rawData;
        }

        if (effectiveColor != null)
        {
            series.ItemStyle = new EChartsItemStyleOption
            {
                Color = effectiveColor
            };
        }

        if (ShowLabel || !string.IsNullOrEmpty(LabelFormatter))
        {
            series.Label = new EChartsLabelOption
            {
                Show = true,
                Position = ToEChartsPosition(LabelPosition),
                Formatter = LabelFormatter,
                Color = "var(--foreground)",
                TextBorderWidth = 0
            };
        }

        if (Stacked)
        {
            series.Stack = StackGroup;
        }

        return series;
    }
}
