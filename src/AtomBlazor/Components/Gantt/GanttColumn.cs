using System.Globalization;
using AtomBlazor.Primitives.Gantt;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 时间轴旁任务列表中的一列，声明于 <c>Gantt</c> 的
/// <c>Columns</c> 之内。
/// </summary>
/// <typeparam name="TItem">源项的类型。</typeparam>
/// <remarks>
/// <para>
/// 各列携带的是像素宽度而非空间份额。甘特图须先知道任务列表
/// 有多宽才能安放第一根横条，而只有浏览器排完表才定得下的宽度，
/// 是图表无从据以绘图的宽度。
/// </para>
/// <para>
/// 首个声明的列承载展开器与缩进，除非另有列设置了
/// <see cref="IsTree"/>。
/// </para>
/// </remarks>
public class GanttColumn<TItem> : ComponentBase, IDisposable
{
    private bool registered;
    private string? snapshot;

    /// <summary>
    /// 获取或设置本列所属的图表。由父组件供给。
    /// </summary>
    [CascadingParameter]
    public Gantt<TItem>? Parent { get; set; }

    /// <summary>
    /// 获取或设置稳定标识符，在同一图表各列之间唯一。
    /// </summary>
    /// <remarks>
    /// 缺省取 <see cref="Title"/>。两列同名时，或拖得的列宽须在
    /// 改名之后仍归其所有时，请设置本项。
    /// </remarks>
    [Parameter]
    public string? Key { get; set; }

    /// <summary>
    /// 获取或设置表头标题。
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置列的绘制宽度（像素）。
    /// </summary>
    [Parameter]
    public int Width { get; set; } = 140;

    /// <summary>
    /// 获取或设置单元取值的读取器。设置了 <see cref="ChildContent"/> 时即被忽略。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本读取器按任务原样读取，排序比较的也是它。这对汇总项很要紧：
    /// 其横条显示的是其子项的跨度，而这里显示的是汇总项
    /// 自备的日期。列须与身旁横条一致时，
    /// 请用 <see cref="ChildContent"/>。
    /// </para>
    /// <para>
    /// 树列上两者皆不设置，即显示任务名称。
    /// </para>
    /// </remarks>
    [Parameter]
    public Func<TItem, object?>? Value { get; set; }

    /// <summary>
    /// 获取或设置取值书写格式，取标准 .NET 格式字符串。
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// 获取或设置单元内容，用以取代 <see cref="Value"/>。
    /// </summary>
    /// <remarks>
    /// 上下文即行——上卷之后的任务——故 <c>context.Start</c> 是横条上的
    /// 日期，<c>context.Item</c> 是来源任务。排序仍读
    /// <see cref="Value"/>，因为它须在行存在之前就能运行。
    /// </remarks>
    [Parameter]
    public RenderFragment<GanttRow<TItem>>? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置内容居于列的哪一侧。
    /// </summary>
    [Parameter]
    public GanttColumnAlign Align { get; set; } = GanttColumnAlign.Start;

    /// <summary>
    /// 获取或设置本列是否承载展开器与缩进。
    /// </summary>
    /// <remarks>
    /// 仅一列能胜任。皆不设置时，首个声明的列接此差事。
    /// </remarks>
    [Parameter]
    public bool IsTree { get; set; }

    /// <summary>
    /// 获取或设置标题是否用于给任务排序。
    /// </summary>
    /// <remarks>
    /// 排序只重排同一父下诸任务，绝不把任何一个挪出所属分支，因为
    /// 子项压过父项便不再是计划了。
    /// </remarks>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    /// 获取或设置本列取值的先后次序。缺省用取值自身的次序。
    /// </summary>
    [Parameter]
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>
    /// 获取本列的已知标识符。
    /// </summary>
    internal string EffectiveKey => string.IsNullOrEmpty(Key) ? Title : Key;

    /// <summary>
    /// 获取列的绘制宽度；拖其列缘可覆写。
    /// </summary>
    internal int EffectiveWidth => Parent?.WidthOf(this) ?? Width;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"{nameof(GanttColumn<TItem>)} has to be declared inside a {nameof(Gantt<TItem>)}'s Columns.");
        }

        Parent.Register(this);
        registered = true;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // 只收改变图表观感的设置，委托一个都不收：标记里写的 lambda
        // 每趟渲染都是新对象，比较它们便会回回报一次变化，
        // 父组件于是无休无止地重建。
        var current = $"{EffectiveKey}|{Title}|{Width}|{Align}|{IsTree}|{Sortable}|{Format}";

        if (registered && snapshot is not null && snapshot != current)
        {
            Parent?.Invalidate();
        }

        snapshot = current;
    }

    /// <summary>
    /// 为某行读取单元的文本。
    /// </summary>
    /// <param name="row">该行。</param>
    /// <param name="isTree">
    /// 本列是否为承载树的那一列——由图表裁定的结果。
    /// </param>
    /// <returns>文本；列无所显示时为 null。</returns>
    /// <remarks>
    /// 答案由外面传入而非从 <see cref="IsTree"/> 读，因为哪列承载树是
    /// 图表的裁决而非本列自己的：各列皆不设置时，首个声明的列
    /// 接此差事。若问过自身的参数，一列未被告知自己是树列的树列便
    /// 一无所回，凡自声明各列的图表其首列皆成空白——
    /// 展开器与缩进画在，旁侧却无名称。
    /// </remarks>
    internal string? Text(GanttRow<TItem> row, bool isTree)
    {
        if (Value is null)
        {
            // 唯有树列回落到任务名称。其余无值可读的列一律留空，
            // 否则未配置的列会把名称再印一遍。
            return isTree ? row.Text : null;
        }

        var value = Value(row.Item);
        if (value is null)
        {
            return null;
        }

        return Format is { Length: > 0 } format && value is IFormattable formattable
            ? formattable.ToString(format, CultureInfo.CurrentCulture)
            : value.ToString();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 把列从其图表中撤出。
    /// </summary>
    /// <param name="disposing">自 <see cref="Dispose()"/> 调用时为 <see langword="true"/>。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && registered)
        {
            Parent?.Unregister(this);
            registered = false;
        }
    }
}
