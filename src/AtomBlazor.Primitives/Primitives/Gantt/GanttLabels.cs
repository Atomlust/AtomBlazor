using System.Globalization;

namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 构建时间轴表头所用的措辞与文化区。
/// </summary>
/// <remarks>
/// 月名与日名取自 <see cref="Culture"/>。唯有两个没有自有日期格式的
/// 表头——周号与季度号——在此拼写。
/// </remarks>
public sealed class GanttLabels
{
    /// <summary>
    /// 获取共享实例：用当前文化区与英文缩写。
    /// </summary>
    public static GanttLabels Default { get; } = new();

    /// <summary>
    /// 获取构建周表头所用格式。<c>{0}</c> 为 ISO 8601 周号。
    /// </summary>
    public string WeekFormat { get; init; } = "W{0}";

    /// <summary>
    /// 获取构建季度表头所用格式。<c>{0}</c> 为季度，取 1 至 4。
    /// </summary>
    public string QuarterFormat { get; init; } = "Q{0}";

    /// <summary>
    /// 获取月名与日名的来源文化区。null 用当前文化区。
    /// </summary>
    public CultureInfo? Culture { get; init; }

    /// <summary>
    /// 获取格式化所用文化区。
    /// </summary>
    internal CultureInfo Resolved => Culture ?? CultureInfo.CurrentCulture;
}
