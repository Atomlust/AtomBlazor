namespace AtomBlazor;

/// <summary>
/// 提供处理图表颜色与 CSS 变量的工具。
/// </summary>
/// <remarks>
/// <para>
/// AtomBlazor 以 CSS 自定义属性承载图表颜色，从而与主题系统
/// 及深色模式支持无缝集成。默认图表颜色
/// （--chart-1 至 --chart-5）定义于 OKLCH 色彩空间，
/// 以获得感知均匀性。
/// </para>
/// <para>
/// 颜色变量定义在 AtomBlazor 的主题 CSS 中：
/// - 浅色模式使用鲜明且区分度高的色相
/// - 深色模式调整亮度以保证在深色背景上可见
/// </para>
/// </remarks>
public static class ChartColor
{
    /// <summary>
    /// 按顺序排列的默认图表颜色 CSS 变量。
    /// </summary>
    /// <remarks>
    /// 这些变量对应主题中定义的 --chart-1 至 --chart-5。
    /// 超出的颜色循环回到开头并调整不透明度。
    /// </remarks>
    public static readonly string[] DefaultColors =
    [
        "var(--chart-1)",
        "var(--chart-2)",
        "var(--chart-3)",
        "var(--chart-4)",
        "var(--chart-5)"
    ];

    /// <summary>
    /// 获取指定序号对应的默认图表颜色。
    /// </summary>
    /// <param name="index">系列（series）序号（从零起）。</param>
    /// <returns>CSS 颜色值（自定义属性引用）。</returns>
    /// <remarks>
    /// 颜色在 5 个默认图表颜色间循环。序号超过 4 时，
    /// 颜色从头重复。
    /// </remarks>
    public static string GetDefault(int index)
    {
        var normalizedIndex = Math.Abs(index) % DefaultColors.Length;
        return DefaultColors[normalizedIndex];
    }

    /// <summary>
    /// 图表颜色 1——主图表颜色（蓝/青色系）。
    /// </summary>
    public const string Chart1 = "var(--chart-1)";

    /// <summary>
    /// 图表颜色 2——次图表颜色（绿/蓝绿色系）。
    /// </summary>
    public const string Chart2 = "var(--chart-2)";

    /// <summary>
    /// 图表颜色 3——第三图表颜色（金/黄色系）。
    /// </summary>
    public const string Chart3 = "var(--chart-3)";

    /// <summary>
    /// 图表颜色 4——第四图表颜色（紫/紫罗兰色系）。
    /// </summary>
    public const string Chart4 = "var(--chart-4)";

    /// <summary>
    /// 图表颜色 5——第五图表颜色（珊瑚/红色系）。
    /// </summary>
    public const string Chart5 = "var(--chart-5)";

    /// <summary>
    /// 为指定数量的系列创建颜色数组。
    /// </summary>
    /// <param name="count">需要配色的系列数。</param>
    /// <returns>CSS 颜色值数组。</returns>
    public static string[] GetColors(int count)
    {
        var colors = new string[count];
        for (var i = 0; i < count; i++)
        {
            colors[i] = GetDefault(i);
        }
        return colors;
    }
}
