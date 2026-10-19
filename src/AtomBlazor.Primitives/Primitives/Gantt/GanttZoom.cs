namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 时间轴单个槽所覆盖的时长。
/// </summary>
/// <remarks>
/// 缩放档位一并选定表头两层：次级层即此处所名的单位，主级层
/// 则是其上的下一个自然单位。各次级槽一律同宽绘制，故
/// <see cref="Month"/> 缩放下二月与三月一般大小，纵有一方短上三日。
/// </remarks>
public enum GanttZoom
{
    /// <summary>小时，以日为上层。</summary>
    Hour,

    /// <summary>日，以月为上层。</summary>
    Day,

    /// <summary>周，以月为上层。</summary>
    Week,

    /// <summary>月，以年为上层。</summary>
    Month,

    /// <summary>季度，以年为上层。</summary>
    Quarter,

    /// <summary>年，其上再无他层。</summary>
    Year,
}
