using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的 K 线（candlestick）系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个 K 线（OHLC）系列。需要开盘、收盘、最高、
/// 最低四个数值各自的四个数据键。常用于金融数据可视化。
/// </para>
/// <para>
/// ECharts candlestick 数据格式要求值的顺序为：[open, close, low, high]。
/// 组件会自动从数据键完成这一提取。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;CandlestickChart Data="@data"&gt;
///     &lt;XAxis DataKey="date" /&gt;
///     &lt;YAxis /&gt;
///     &lt;Candlestick OpenKey="open" CloseKey="close" HighKey="high" LowKey="low" /&gt;
/// &lt;/CandlestickChart&gt;
/// </code>
/// </example>
public partial class Candlestick : SeriesBase
{
    /// <summary>
    /// 获取或设置开盘价数值的属性名。
    /// </summary>
    [Parameter]
    public string? OpenKey { get; set; }

    /// <summary>
    /// 获取或设置收盘价数值的属性名。
    /// </summary>
    [Parameter]
    public string? CloseKey { get; set; }

    /// <summary>
    /// 获取或设置最高价数值的属性名。
    /// </summary>
    [Parameter]
    public string? HighKey { get; set; }

    /// <summary>
    /// 获取或设置最低价数值的属性名。
    /// </summary>
    [Parameter]
    public string? LowKey { get; set; }

    /// <summary>
    /// 获取或设置阳线（上涨）蜡烛的颜色。
    /// </summary>
    /// <remarks>
    /// 默认为 <c>#22c55e</c>（绿色）。当收盘价 &gt; 开盘价时施加。
    /// </remarks>
    [Parameter]
    public string BullColor { get; set; } = "#22c55e";

    /// <summary>
    /// 获取或设置阴线（下跌）蜡烛的颜色。
    /// </summary>
    /// <remarks>
    /// 默认为 <c>#ef4444</c>（红色）。当收盘价 &lt; 开盘价时施加。
    /// </remarks>
    [Parameter]
    public string BearColor { get; set; } = "#ef4444";

    /// <summary>
    /// 获取或设置每根 K 线的宽度。
    /// </summary>
    /// <remarks>
    /// 接受百分比字符串（如 "60%"）或像素值（如 "20"）。
    /// 为 null 时由 ECharts 自动适配蜡烛宽度。
    /// </remarks>
    [Parameter]
    public string? BarWidth { get; set; }

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var series = new EChartsSeriesOption
        {
            Type = "candlestick",
            Name = GetResolvedName(),
            BarWidth = BarWidth,
            ItemStyle = new EChartsItemStyleOption
            {
                Color = GetResolvedFillColor() ?? GetResolvedColor() ?? BullColor,
                Color0 = BearColor,
                BorderColor = GetResolvedColor() ?? BullColor,
                BorderColor0 = BearColor
            },
            Emphasis = new EChartsEmphasisOption { Disabled = true }
        };

        if (!string.IsNullOrEmpty(OpenKey) &&
            !string.IsNullOrEmpty(CloseKey) &&
            !string.IsNullOrEmpty(HighKey) &&
            !string.IsNullOrEmpty(LowKey))
        {
            var opens = DataExtractor.ExtractValues(ParentChart?.Data, OpenKey);
            var closes = DataExtractor.ExtractValues(ParentChart?.Data, CloseKey);
            var highs = DataExtractor.ExtractValues(ParentChart?.Data, HighKey);
            var lows = DataExtractor.ExtractValues(ParentChart?.Data, LowKey);
            var count = Math.Min(Math.Min(opens.Count, closes.Count), Math.Min(highs.Count, lows.Count));

            var data = new List<object?>(count);
            for (var i = 0; i < count; i++)
            {
                // ECharts candlestick 数据格式：[open, close, low, high]
                data.Add(new object?[] { opens[i], closes[i], lows[i], highs[i] });
            }

            series.Data = data;
        }

        return series;
    }
}
