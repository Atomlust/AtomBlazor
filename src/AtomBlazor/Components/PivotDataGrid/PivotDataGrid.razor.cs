using System.Globalization;
using AtomBlazor.Primitives.Pivot;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 交叉表：一个字段排在侧边，另一个排在顶部，两者相交处呈现算出的值。
/// </summary>
/// <typeparam name="TItem">源数据项的类型。</typeparam>
/// <remarks>
/// <para>
/// 这是唯一一张列来自数据而非来自声明的网格。这与 <c>DataGrid</c> 分组的差别
/// 全部在此：分组的网格仍然只有你写下的那些列，
/// 而透视表为它发现的每一个值长出一列。
/// </para>
/// <para>
/// 字段声明在 <c>Rows</c> 与 <c>Columns</c> 之内，值声明在 <c>Values</c> 之内。
/// 字段属于哪条轴取决于它写在哪个片段里，
/// 因此在行与列之间移动字段，就是在标记中移动它。
/// </para>
/// <para>
/// 总计由其下的每一项算出，而不是由其覆盖的单元格算出。
/// 平均值之平均并不是平均值，
/// 这个差异在它出错之前都看不出来。
/// </para>
/// </remarks>
public partial class PivotDataGrid<TItem> : ComponentBase
{
    private readonly List<PivotField<TItem>> fields = [];
    private readonly List<PivotValue<TItem>> values = [];

    /// <summary>
    /// 字段选择器已关闭的那些项的键。
    /// </summary>
    /// <remarks>
    /// 记在这里而不是记在声明组件上，因为子组件的参数属于写标记的人：从外面设置它，
    /// 下一次父组件渲染时就会被还原。
    /// </remarks>
    private readonly HashSet<string> hidden = new(StringComparer.Ordinal);

    private PivotTable<TItem>? table;
    private bool stale = true;
    private string? buildError;
    private int page;

    /// <summary>
    /// 获取或设置要做交叉表的数据项。
    /// </summary>
    [Parameter]
    public IEnumerable<TItem>? Data { get; set; }

    /// <summary>
    /// 获取或设置成为行标题层级的字段，最外层的在前。
    /// </summary>
    [Parameter]
    public RenderFragment? Rows { get; set; }

    /// <summary>
    /// 获取或设置成为列标题层级的字段，最外层的在前。
    /// </summary>
    [Parameter]
    public RenderFragment? Columns { get; set; }

    /// <summary>
    /// 获取或设置每个单元格算出的值。至少需要一个。
    /// </summary>
    [Parameter]
    public RenderFragment? Values { get; set; }

    /// <summary>
    /// 获取或设置表格带有哪几种总计和小计。
    /// </summary>
    /// <remarks>
    /// 这些名称要按"所点之物的总计"来读：<c>RowTotals</c> 在每一行末尾追加一列，
    /// <c>ColumnTotals</c> 在底部追加一行。
    /// </remarks>
    [Parameter]
    public PivotTotals Totals { get; set; } = PivotTotals.Grand;

    /// <summary>
    /// 获取或设置是否显示字段选择器。
    /// </summary>
    /// <remarks>
    /// 选择器切换已声明字段与值的开关。在这里关掉的字段仍是已声明的，
    /// 不改动标记即可把它带回来。
    /// </remarks>
    [Parameter]
    public bool ShowFieldPicker { get; set; }

    /// <summary>
    /// 获取或设置每页容纳的顶层行分组数。为 null 时全部显示。
    /// </summary>
    /// <remarks>
    /// 分页作用于最外层行分组而非逐行作用，因此一个分组绝不会被页边界拆开，
    /// 其小计也总是与它同页。
    /// </remarks>
    [Parameter]
    public int? PageSize { get; set; }

    /// <summary>
    /// 获取或设置分页器所在的位置。
    /// </summary>
    [Parameter]
    public PivotPagerPosition PagerPosition { get; set; } = PivotPagerPosition.Bottom;

    /// <summary>
    /// 获取或设置滚动区域的高度（以 CSS 长度表示）。为 null 时让表格自行伸展。
    /// </summary>
    /// <remarks>
    /// 设置它，正文滚动时标题保持不动，
    /// 这正是宽交叉表可读的关键。
    /// </remarks>
    [Parameter]
    public string? Height { get; set; }

    /// <summary>
    /// 获取或设置是否隔行为底色（斑马纹）。
    /// </summary>
    [Parameter]
    public bool AlternatingRows { get; set; } = true;

    /// <summary>
    /// 获取或设置单元格之间绘制哪些网格线。
    /// </summary>
    [Parameter]
    public PivotGridLines GridLines { get; set; } = PivotGridLines.Both;

    /// <summary>
    /// 获取或设置表格是否正在等待数据。
    /// </summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// 获取或设置没有可交叉的数据时显示的消息。
    /// </summary>
    [Parameter]
    public string? EmptyMessage { get; set; }

    /// <summary>
    /// 获取或设置总计所带的标题。
    /// </summary>
    [Parameter]
    public string? TotalLabel { get; set; }

    /// <summary>
    /// 获取或设置当分组依据值为空时该组携带的标题。
    /// </summary>
    [Parameter]
    public string? BlankLabel { get; set; }

    /// <summary>
    /// 获取或设置行标题上方空白单元格的模板。
    /// </summary>
    /// <remarks>默认使用行字段的标题，即用来给各行命名的内容。</remarks>
    [Parameter]
    public RenderFragment? CornerTemplate { get; set; }

    /// <summary>
    /// 获取或设置行或列标题的模板。
    /// </summary>
    [Parameter]
    public RenderFragment<PivotHeaderContext>? HeaderTemplate { get; set; }

    /// <summary>
    /// 获取或设置点击单元格时调用的回调。
    /// </summary>
    /// <remarks>
    /// 设置它，单元格就变成按钮；上下文携带单元格背后的数据项，即钻取时要展示的内容。
    /// 不设置则单元格保持纯文本。
    /// </remarks>
    [Parameter]
    public EventCallback<PivotCellContext<TItem>> OnCellClick { get; set; }

    /// <summary>
    /// 获取或设置每当表格重建后调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<PivotTable<TItem>?> OnBuilt { get; set; }

    /// <summary>
    /// 获取或设置表格的无障碍名称。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置 splat 到根元素的附加属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取最近一次构建出的交叉表；尚无可取时（第一次构建前）为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 请在事件处理程序或 <c>OnAfterRender</c> 中读取它。
    /// 若要在父组件的标记中展示它的任何内容，请使用 <see cref="OnBuilt"/>，
    /// 因为父组件先于其子组件重新渲染而生成的自己的标记。
    /// </remarks>
    public PivotTable<TItem>? Table => table;

    /// <summary>
    /// 按声明顺序获取声明在行轴上的字段。
    /// </summary>
    internal IReadOnlyList<PivotField<TItem>> RowFields =>
        [.. fields.Where(f => f.Axis == PivotAxis.Row)];

    /// <summary>
    /// 获取某字段是否参与表格；除非选择器另有指示，否则以其声明为准。
    /// </summary>
    /// <param name="field">该字段。</param>
    /// <returns>字段处于显示状态时为 <see langword="true"/>。</returns>
    internal bool IsShown(PivotField<TItem> field) =>
        field.Visible && !hidden.Contains(FieldKey(field));

    /// <summary>
    /// 获取某值是否参与表格；除非选择器另有指示，否则以其声明为准。
    /// </summary>
    /// <param name="value">该值。</param>
    /// <returns>值处于显示状态时为 <see langword="true"/>。</returns>
    internal bool IsShown(PivotValue<TItem> value) =>
        value.Visible && !hidden.Contains(ValueKey(value));

    /// <summary>字段在选择器中的键，与共享同一标题的值相互区分开。</summary>
    private static string FieldKey(PivotField<TItem> field) =>
        $"{field.Axis}:{field.EffectiveKey}";

    /// <summary>值在选择器中的键。</summary>
    private static string ValueKey(PivotValue<TItem> value) => $"value:{value.EffectiveKey}";

    /// <summary>
    /// 按声明顺序获取声明在列轴上的字段。
    /// </summary>
    internal IReadOnlyList<PivotField<TItem>> ColumnFields =>
        [.. fields.Where(f => f.Axis == PivotAxis.Column)];

    /// <summary>
    /// 获取为单元格声明的各值。
    /// </summary>
    internal IReadOnlyList<PivotValue<TItem>> ValueColumns => values;

    /// <summary>
    /// 获取实际处于显示状态的各值，列正是由它们构建。
    /// </summary>
    internal IReadOnlyList<PivotValue<TItem>> VisibleValues =>
        [.. values.Where(IsShown)];

    /// <summary>
    /// 添加一个声明在本表 <c>Rows</c> 或 <c>Columns</c> 中的字段。
    /// </summary>
    /// <param name="field">该字段。</param>
    internal void Register(PivotField<TItem> field)
    {
        fields.Add(field);
        Invalidate();
    }

    /// <summary>
    /// 添加一个声明在本表 <c>Values</c> 中的值。
    /// </summary>
    /// <param name="value">该值。</param>
    internal void Register(PivotValue<TItem> value)
    {
        values.Add(value);
        Invalidate();
    }

    /// <summary>
    /// 将一个字段撤出。
    /// </summary>
    /// <param name="field">该字段。</param>
    internal void Unregister(PivotField<TItem> field)
    {
        fields.Remove(field);
        Invalidate();
    }

    /// <summary>
    /// 将一个值撤出。
    /// </summary>
    /// <param name="value">该值。</param>
    internal void Unregister(PivotValue<TItem> value)
    {
        values.Remove(value);
        Invalidate();
    }

    /// <summary>
    /// 将交叉表标记为需要重新构建。
    /// </summary>
    /// <remarks>
    /// 构建并不免费，所以它在下一次渲染前只发生一次，
    /// 而不是子组件每报一次参数变化就构建一回。
    /// </remarks>
    internal void Invalidate()
    {
        stale = true;
        StateHasChanged();
    }

    /// <summary>
    /// 切换字段的开关并重建。
    /// </summary>
    /// <param name="field">要切换的字段。</param>
    internal void ToggleField(PivotField<TItem> field)
    {
        var key = FieldKey(field);
        if (!hidden.Remove(key))
        {
            hidden.Add(key);
        }

        Invalidate();
    }

    /// <summary>
    /// 切换值的开关并重建。
    /// </summary>
    /// <param name="value">要切换的值。</param>
    internal void ToggleValue(PivotValue<TItem> value)
    {
        var key = ValueKey(value);

        // The last one standing stays on: a pivot with no values has nothing in its cells.
        if (!hidden.Contains(key) && VisibleValues.Count == 1)
        {
            return;
        }

        if (!hidden.Remove(key))
        {
            hidden.Add(key);
        }

        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => stale = true;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!stale)
        {
            return;
        }

        var previous = table;
        var previousError = buildError;
        Rebuild();

        // Compared by value, not by reference. Rebuild always makes a new table, so a reference
        // test is always "changed" — and OnBuilt calls StateHasChanged on whoever handles it,
        // whose re-render sets this component's parameters again, which marks it stale again,
        // which rebuilds again. That circle has nothing to stop it, and it hangs the circuit on
        // Server and the tab on WebAssembly.
        var tableChanged = !SameTable(previous, table);
        if (tableChanged || !string.Equals(previousError, buildError, StringComparison.Ordinal))
        {
            if (tableChanged && OnBuilt.HasDelegate)
            {
                await OnBuilt.InvokeAsync(table);
            }

            StateHasChanged();
        }
    }

    /// <summary>
    /// 获取两次构建是否画出同一张交叉表。
    /// </summary>
    /// <param name="left">上一次构建的表，可能为 null。</param>
    /// <param name="right">刚刚构建的表，可能为 null。</param>
    /// <returns>当读者或处理程序能察觉的任何差异都不存在时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// <para>
    /// 先比较标题，标题一致才比较单元格，于是常见情形——
    /// 父组件原样重新渲染——只需走一遍标题而不必走整张网格。
    /// 即使真的走到单元格，也只是对重建刚刚算出的值多扫一遍，
    /// 量级与画出它们相同。
    /// </para>
    /// <para>
    /// 单元格必须纳入比较。形状未变而数字挪动的透视表，
    /// 恰恰是处理程序盯着要响应的情形；只比到标题为止的
    /// 比较会报告什么都没发生。
    /// </para>
    /// </remarks>
    private static bool SameTable(PivotTable<TItem>? left, PivotTable<TItem>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left.ItemCount != right.ItemCount
            || left.RowLeaves.Count != right.RowLeaves.Count
            || left.ColumnLeaves.Count != right.ColumnLeaves.Count
            || left.Measures.Count != right.Measures.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Measures.Count; i++)
        {
            if (!string.Equals(left.Measures[i].Key, right.Measures[i].Key, StringComparison.Ordinal)
                || !string.Equals(left.Measures[i].Title, right.Measures[i].Title, StringComparison.Ordinal)
                || !string.Equals(left.Measures[i].Format, right.Measures[i].Format, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!SameHeadings(left.RowRoots, right.RowRoots)
            || !SameHeadings(left.ColumnRoots, right.ColumnRoots))
        {
            return false;
        }

        for (var row = 0; row < left.RowLeaves.Count; row++)
        {
            for (var column = 0; column < left.ColumnLeaves.Count; column++)
            {
                for (var measure = 0; measure < left.Measures.Count; measure++)
                {
                    var before = left.GetValue(left.RowLeaves[row], left.ColumnLeaves[column], measure);
                    var after = right.GetValue(right.RowLeaves[row], right.ColumnLeaves[column], measure);

                    if (!Equals(before, after))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool SameHeadings(IReadOnlyList<PivotAxisNode> left, IReadOnlyList<PivotAxisNode> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        for (var i = 0; i < left.Count; i++)
        {
            if (!Equals(left[i].Key, right[i].Key)
                || !string.Equals(left[i].Label, right[i].Label, StringComparison.Ordinal)
                || left[i].Depth != right[i].Depth
                || left[i].IsTotal != right[i].IsTotal
                || left[i].Span != right[i].Span
                || !SameHeadings(left[i].Children, right[i].Children))
            {
                return false;
            }
        }

        return true;
    }

    private void Rebuild()
    {
        stale = false;
        buildError = null;

        var rowFields = RowFields.Where(IsShown).Select(f => f.ToField()).ToList();
        var columnFields = ColumnFields.Where(IsShown).Select(f => f.ToField()).ToList();
        var measures = VisibleValues.Select(v => v.ToMeasure()).ToList();

        if (Data is null || measures.Count == 0 || (rowFields.Count == 0 && columnFields.Count == 0))
        {
            table = null;
            return;
        }

        try
        {
            table = PivotBuilder.Build(
                Data,
                rowFields,
                columnFields,
                measures,
                Totals,
                TotalLabel ?? Localizer["PivotDataGrid.Total"],
                BlankLabel ?? Localizer["PivotDataGrid.Blank"]);
        }
        catch (ArgumentException error)
        {
            // A table that cannot be built is a declaration problem, not a bug. Say so in the
            // markup rather than throwing, which on Blazor Server would take the circuit down.
            table = null;
            buildError = error.Message;
        }

        page = table is null ? 0 : Math.Clamp(page, 0, Math.Max(0, PageCount - 1));
    }

    /// <summary>
    /// 获取顶层行分组一共有多少页。
    /// </summary>
    private int PageCount
    {
        get
        {
            if (table is null || PageSize is not > 0)
            {
                return 1;
            }

            // The grand total is not a group and never counts towards a page.
            var groups = table.RowRoots.Count(r => !r.IsTotal);
            return Math.Max(1, (int)Math.Ceiling(groups / (double)PageSize.Value));
        }
    }

    /// <summary>
    /// 获取本页显示的行叶节点；总计行始终留在最后一页。
    /// </summary>
    private IReadOnlyList<PivotAxisNode> PagedRowLeaves
    {
        get
        {
            if (table is null)
            {
                return [];
            }

            if (PageSize is not > 0)
            {
                return table.RowLeaves;
            }

            var groups = table.RowRoots.Where(r => !r.IsTotal).ToList();
            var take = groups.Skip(page * PageSize.Value).Take(PageSize.Value).ToHashSet();

            var leaves = table.RowLeaves.Where(leaf => take.Contains(RootOf(leaf))).ToList();

            if (page == PageCount - 1)
            {
                leaves.AddRange(table.RowLeaves.Where(leaf => leaf.IsTotal && leaf.Parent is null));
            }

            return leaves;
        }
    }

    private static PivotAxisNode RootOf(PivotAxisNode node)
    {
        var current = node;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        return current;
    }

    private void GoToPage(int target)
    {
        page = Math.Clamp(target, 0, Math.Max(0, PageCount - 1));
        StateHasChanged();
    }

    /// <summary>
    /// 获取行标题有几层深，即它们占据多少列。
    /// </summary>
    private int RowDepth => Math.Max(1, RowFields.Count(IsShown));

    /// <summary>
    /// 获取列标题有几层深，即它们占据多少行标题。
    /// </summary>
    private int ColumnDepth => ColumnFields.Count(IsShown);

    /// <summary>
    /// 获取标题是否需要最后一行来为每个值命名。
    /// </summary>
    /// <remarks>
    /// 只有一个值时，它的名字归入角落单元格，而不是在每一列上重复。
    /// </remarks>
    private bool ShowValueHeaderRow => VisibleValues.Count > 1;

    /// <summary>
    /// 从左到右获取某一层级的列标题。
    /// </summary>
    private List<PivotAxisNode> ColumnLevel(int depth)
    {
        if (table is null)
        {
            return [];
        }

        var level = new List<PivotAxisNode>();
        Collect(table.ColumnRoots);
        return level;

        void Collect(IEnumerable<PivotAxisNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Depth == depth)
                {
                    level.Add(node);
                }
                else if (node.Depth < depth)
                {
                    Collect(node.Children);
                }
            }
        }
    }

    /// <summary>
    /// 获取某一行要绘制的行标题单元格，并已追踪
    /// 上方各行已经跨越了哪些。
    /// </summary>
    /// <remarks>
    /// 一个标题只绘制一次并跨越其下的每一行，
    /// 因此每行只绘制从本行开始的那些祖先标题。
    /// </remarks>
    private List<(PivotAxisNode Node, int RowSpan, int ColumnSpan)> RowHeaderCells(
        PivotAxisNode leaf,
        Dictionary<int, int> remaining)
    {
        var cells = new List<(PivotAxisNode, int, int)>();

        var chain = new List<PivotAxisNode>();
        for (var node = leaf; node is not null; node = node.Parent)
        {
            chain.Insert(0, node);
        }

        foreach (var node in chain)
        {
            var depth = node.Depth;

            if (remaining.TryGetValue(depth, out var left) && left > 0)
            {
                remaining[depth] = left - 1;
                continue;
            }

            // A total sits shallower than the data leaves, so its label takes the columns the
            // levels below it would have used.
            var columnSpan = node.IsLeaf ? Math.Max(1, RowDepth - depth) : 1;

            cells.Add((node, node.Span, columnSpan));
            remaining[depth] = node.Span - 1;

            if (node.IsLeaf)
            {
                for (var deeper = depth + 1; deeper < RowDepth; deeper++)
                {
                    remaining[deeper] = 0;
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// 用其度量的格式字符串格式化单元格的值。
    /// </summary>
    private static string Format(object? value, PivotMeasure<TItem> measure)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(measure.Format) && value is IFormattable formattable)
        {
            return formattable.ToString(measure.Format, CultureInfo.CurrentCulture);
        }

        return value.ToString() ?? string.Empty;
    }

    private PivotCellContext<TItem> CellContext(PivotAxisNode row, PivotAxisNode column, int measureIndex)
    {
        var measure = table!.Measures[measureIndex];
        var value = table.GetValue(row, column, measureIndex);

        return new PivotCellContext<TItem>(row, column, measure, measureIndex, value, Format(value, measure), table);
    }

    private async Task OnCellActivatedAsync(PivotCellContext<TItem> context)
    {
        if (OnCellClick.HasDelegate)
        {
            await OnCellClick.InvokeAsync(context);
        }
    }

    private string RootClass => ClassNames.cn("atom:w-full atom:space-y-2", Class);

    private string TableClass => ClassNames.cn(
        "atom:w-full atom:border-collapse atom:text-sm",
        GridLines is PivotGridLines.Both or PivotGridLines.Vertical ? "atom:[&_td]:border-e atom:[&_th]:border-e" : null,
        GridLines is PivotGridLines.Both or PivotGridLines.Horizontal ? "atom:[&_tr]:border-b" : null,
        "atom:[&_td]:border-border atom:[&_th]:border-border atom:[&_tr]:border-border");

    private static string HeaderCellClass(bool isTotal) => ClassNames.cn(
        "atom:bg-muted atom:px-3 atom:py-2 atom:text-start atom:font-medium atom:whitespace-nowrap",
        isTotal ? "atom:font-semibold" : null);

    private string BodyCellClass(PivotCellContext<TItem> context) => ClassNames.cn(
        "atom:px-3 atom:py-1.5 atom:text-end atom:tabular-nums atom:whitespace-nowrap",
        context.IsTotal ? "atom:bg-muted/50 atom:font-semibold" : null,
        OnCellClick.HasDelegate ? "atom:cursor-pointer atom:hover:bg-accent" : null);

    private string RowClass(int index) => ClassNames.cn(
        AlternatingRows && index % 2 == 1 ? "atom:bg-muted/30" : null);
}
