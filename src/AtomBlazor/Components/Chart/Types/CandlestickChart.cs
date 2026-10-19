namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的 K 线图（candlestick）组件。
/// </summary>
/// <remarks>
/// <para>
/// CandlestickChart 组件提供金融 OHLC（开-高-低-收）K 线
/// 可视化。阳线（bullish）与阴线（bearish）自动着色。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;CandlestickChart Data="@data" Height="350px"&gt;
///     &lt;XAxis DataKey="date" /&gt;
///     &lt;YAxis /&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Candlestick OpenKey="open" CloseKey="close" HighKey="high" LowKey="low" /&gt;
/// &lt;/CandlestickChart&gt;
/// </code>
/// </para>
/// </remarks>
public class CandlestickChart : ChartBase
{
    internal override string DataSlot => "candlestick-chart";

    internal override string SeriesType => "candlestick";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // Candlestick 用 itemStyle.color/color0 承载阳/阴线颜色。
        // 阻止全局调色板覆写系列的 itemStyle。
        option.Color = new List<object> { "transparent" };

        option.Grid ??= new EChartsGridOption
        {
            Left = "12",
            Right = "12",
            Top = "10",
            Bottom = "0",
            ContainLabel = true
        };

        option.YAxis ??= new EChartsAxisOption
        {
            Type = "value",
            Scale = true,
            SplitLine = new EChartsSplitLineOption
            {
                Show = true,
                LineStyle = new EChartsLineStyleOption
                {
                    Color = "var(--border)",
                    Type = "dashed"
                }
            },
            AxisLabel = new EChartsAxisLabelOption
            {
                Color = "var(--muted-foreground)"
            }
        };
    }
}
