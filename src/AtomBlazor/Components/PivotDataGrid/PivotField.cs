using AtomBlazor.Primitives.Pivot;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 透视表的一个分组层级，声明在其 <c>Rows</c> 或 <c>Columns</c> 之内。
/// </summary>
/// <typeparam name="TItem">源数据项的类型。</typeparam>
/// <remarks>
/// 在一条轴上声明两个字段，第二个就嵌套在第一个之内。字段属于哪条轴
/// 取决于它写在哪个片段里，因此同一个声明要在行与列之间移动，
/// 只需在标记中把它挪过去。
/// </remarks>
public class PivotField<TItem> : ComponentBase, IDisposable
{
    private bool registered;
    private string? snapshot;

    /// <summary>
    /// 获取或设置该字段所属的透视表。由父组件提供。
    /// </summary>
    [CascadingParameter]
    public PivotDataGrid<TItem>? Parent { get; set; }

    /// <summary>
    /// 获取或设置该字段声明所在的轴。由父组件提供。
    /// </summary>
    [CascadingParameter(Name = "PivotAxis")]
    public PivotAxis Axis { get; set; }

    /// <summary>
    /// 获取或设置一个稳定标识符，在同一张表的各字段之间必须唯一。
    /// </summary>
    /// <remarks>
    /// 默认为 <see cref="Title"/>。当两个字段共用一个标题，或字段选择器的状态需要挺过标题变更时，
    /// 请显式设置它。
    /// </remarks>
    [Parameter]
    public string? Key { get; set; }

    /// <summary>
    /// 获取或设置该层级显示的标题。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置用于读取分组依据值的函数。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, object?> Value { get; set; } = default!;

    /// <summary>
    /// 获取或设置分组值如何变成其标题。默认使用值自身的文本。
    /// </summary>
    /// <remarks>
    /// 用它来分桶或格式化——日期归入季度、数字归入档位——而不是对格式化后的字符串分组，
    /// 这样各组仍然按其真实值排序。
    /// </remarks>
    [Parameter]
    public Func<object?, string>? Label { get; set; }

    /// <summary>
    /// 获取或设置该层级各组的排序方式。默认使用值自身的顺序。
    /// </summary>
    [Parameter]
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>
    /// 获取或设置该层级的各组是否逆序排列。
    /// </summary>
    [Parameter]
    public bool Descending { get; set; }

    /// <summary>
    /// 获取或设置该字段是否参与表格。
    /// </summary>
    /// <remarks>
    /// 字段选择器切换的就是这一项，因此在这里隐藏的字段仍是已声明的，
    /// 不改动标记即可将其找回。
    /// </remarks>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 获取该字段实际使用的标识符。
    /// </summary>
    internal string EffectiveKey => string.IsNullOrEmpty(Key) ? Title : Key;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"{nameof(PivotField<TItem>)} has to be declared inside a {nameof(PivotDataGrid<TItem>)}'s Rows or Columns.");
        }

        Parent.Register(this);
        registered = true;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Only the settings that change what the table looks like, and none of the delegates: a
        // lambda written in markup is a new object on every render, so comparing them would report
        // a change every time and the parent would rebuild for ever.
        var current = $"{EffectiveKey}|{Title}|{Visible}|{Descending}|{Axis}";

        if (registered && snapshot is not null && snapshot != current)
        {
            Parent?.Invalidate();
        }

        snapshot = current;
    }

    /// <summary>
    /// 构建该声明所代表的无头（headless）字段。
    /// </summary>
    internal AtomBlazor.Primitives.Pivot.PivotField<TItem> ToField() =>
        new(EffectiveKey, Title, Value)
        {
            Label = Label,
            Comparer = Comparer,
            Descending = Descending,
        };

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 将该字段从其表格中撤出。
    /// </summary>
    /// <param name="disposing">从 <see cref="Dispose()"/> 调用时为 <see langword="true"/>。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && registered)
        {
            Parent?.Unregister(this);
            registered = false;
        }
    }
}
