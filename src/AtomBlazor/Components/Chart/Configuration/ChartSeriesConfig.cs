namespace AtomBlazor;

/// <summary>
/// 单个图表系列的配置，含其标签与颜色。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="ChartConfig"/> 配合使用，定义图表系列的外观
/// 与标签。颜色可用 CSS 自定义属性，以与主题系统
/// 无缝集成。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var config = new ChartSeriesConfig
/// {
///     Label = "Desktop Users",
///     Color = "var(--chart-1)"
/// };
/// </code>
/// </example>
public class ChartSeriesConfig
{
    /// <summary>
    /// 获取或设置该系列的展示标签。
    /// </summary>
    /// <remarks>
    /// 该标签出现在 tooltip、legend 与无障碍描述中。
    /// 应简短而达意。
    /// </remarks>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置该系列的颜色。
    /// </summary>
    /// <remarks>
    /// 支持 CSS 自定义属性（如 "var(--chart-1)"）、十六进制颜色、
    /// RGB/RGBA 值或任何合法的 CSS 颜色值。
    /// 默认图表颜色使用 --chart-1 至 --chart-5 这组 CSS 变量，
    /// 其定义于 AtomBlazor 的主题体系。
    /// </remarks>
    public string Color { get; set; } = string.Empty;
}
