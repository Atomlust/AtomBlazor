using AtomBlazor.Primitives;

using AtomBlazor.Primitives.Pivot;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 透视表的一个值：对落入单元格的项算什么。
/// </summary>
/// <typeparam name="TItem">源数据项的类型。</typeparam>
/// <remarks>
/// 声明两个值，每个单元格就会显示两个数字，这正是列标题要追加
/// 一行来为它们命名的原因。
/// </remarks>
public class PivotValue<TItem> : ComponentBase, IDisposable
{
    private bool registered;
    private string? snapshot;

    /// <summary>
    /// 获取或设置该值所属的透视表。由父组件提供。
    /// </summary>
    [CascadingParameter]
    public PivotDataGrid<TItem>? Parent { get; set; }

    /// <summary>
    /// 获取或设置一个稳定标识符，在同一张表的各值之间必须唯一。
    /// </summary>
    /// <remarks>默认为 <see cref="Title"/>。</remarks>
    [Parameter]
    public string? Key { get; set; }

    /// <summary>
    /// 获取或设置该值显示的标题。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置应用到单元格内各项上的函数。
    /// </summary>
    /// <remarks>
    /// 当设置了 <see cref="Aggregate"/> 时被忽略。<see cref="AggregateFunction.Count"/> 是唯一
    /// 无需 <see cref="Value"/> 也能工作的函数。
    /// </remarks>
    [Parameter]
    public AggregateFunction Function { get; set; } = AggregateFunction.Sum;

    /// <summary>
    /// 获取或设置从项中读取待聚合数值的函数。
    /// </summary>
    [Parameter]
    public Func<TItem, object?>? Value { get; set; }

    /// <summary>
    /// 获取或设置自行算出单元格值的函数。
    /// </summary>
    /// <remarks>
    /// 每个单元格调用一次，每个总计也各调用一次；总计拿到的是其下的每一项而非它所
    /// 覆盖的单元格——正因如此，比值或中位数才能算对，
    /// 而"和之和"不行。
    /// </remarks>
    [Parameter]
    public Func<IEnumerable<TItem>, object?>? Aggregate { get; set; }

    /// <summary>
    /// 获取或设置用于打印该值的格式字符串，例如 <c>C0</c> 或 <c>N2</c>。
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// 获取或设置承载该值的单元格的模板。
    /// </summary>
    /// <remarks>
    /// 对包括总计在内的每个单元格都生效。上下文会说明当前是哪一种，因此一个模板即可兼顾两者，
    /// 并可为总计另行设样式。
    /// </remarks>
    [Parameter]
    public RenderFragment<PivotCellContext<TItem>>? Template { get; set; }

    /// <summary>
    /// 获取或设置该值自身标题的模板。
    /// </summary>
    [Parameter]
    public RenderFragment<PivotValue<TItem>>? HeaderTemplate { get; set; }

    /// <summary>
    /// 获取或设置该值是否参与表格。
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 获取该值实际使用的标识符。
    /// </summary>
    internal string EffectiveKey => string.IsNullOrEmpty(Key) ? Title : Key;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"{nameof(PivotValue<TItem>)} has to be declared inside a {nameof(PivotDataGrid<TItem>)}'s Values.");
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
        var current = $"{EffectiveKey}|{Title}|{Visible}|{Function}|{Format}|{Aggregate is not null}";

        if (registered && snapshot is not null && snapshot != current)
        {
            Parent?.Invalidate();
        }

        snapshot = current;
    }

    /// <summary>
    /// 构建该声明所代表的无头（headless）度量。
    /// </summary>
    internal PivotMeasure<TItem> ToMeasure()
    {
        if (Aggregate is not null)
        {
            return new PivotMeasure<TItem>(EffectiveKey, Title, Aggregate) { Format = Format };
        }

        return new PivotMeasure<TItem>(EffectiveKey, Title, Function, Value) { Format = Format };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 将该值从其表格中撤出。
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
