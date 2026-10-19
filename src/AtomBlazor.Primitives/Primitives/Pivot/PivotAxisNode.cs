namespace AtomBlazor.Primitives.Pivot;

/// <summary>
/// 数据透视表行轴或列轴上的一个标题节点。
/// </summary>
/// <remarks>
/// 这些标题构成一棵树，该轴上的每个字段对应一层。渲染器遍历这棵树生成标题单元格，
/// 并用 <see cref="Span"/> 决定 colspan 或 rowspan，随后遍历
/// <see cref="PivotTable{TItem}.RowLeaves"/> 和 <see cref="PivotTable{TItem}.ColumnLeaves"/>
/// 生成表体。
/// </remarks>
public sealed class PivotAxisNode
{
    private readonly List<PivotAxisNode> children = [];

    internal PivotAxisNode(object? key, string label, int depth, PivotAxisNode? parent, bool isTotal)
    {
        Key = key;
        Label = label;
        Depth = depth;
        Parent = parent;
        IsTotal = isTotal;
    }

    /// <summary>
    /// 获取该标题所分组的原始值；总计节点为 <see langword="null"/>。
    /// </summary>
    public object? Key { get; }

    /// <summary>
    /// 获取标题的文本。
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// 获取该标题在轴上的深度，从 0 开始计数。
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// 获取包含该标题的上一级标题；处于顶层时为 <see langword="null"/>。
    /// </summary>
    public PivotAxisNode? Parent { get; }

    /// <summary>
    /// 获取该标题是小计或总计（而非一组数据）的标志。
    /// </summary>
    public bool IsTotal { get; }

    /// <summary>
    /// 获取嵌套在该标题之下的子标题。
    /// </summary>
    public IReadOnlyList<PivotAxisNode> Children => children;

    /// <summary>
    /// 获取该标题之下再无嵌套（即它对应一行或一列单元格）的标志。
    /// </summary>
    public bool IsLeaf => children.Count == 0;

    /// <summary>
    /// 获取该标题之下的叶子数量，即其 colspan 或 rowspan。
    /// </summary>
    public int Span { get; private set; } = 1;

    /// <summary>
    /// 获取该叶子在绘制顺序（含总计）中沿轴的位置；非叶子标题为 -1。
    /// </summary>
    public int LeafIndex { get; internal set; } = -1;

    /// <summary>
    /// 获取该叶子在承载真实数据的叶子中的位置；总计或非叶子标题为 -1。
    /// </summary>
    /// <remarks>
    /// 总计穿插绘制在数据之间，因此绘制顺序与数据顺序并非同一序列。
    /// 该索引指向条目被分组时所在的桶。
    /// </remarks>
    public int DataIndex { get; internal set; } = -1;

    /// <summary>
    /// 获取从轴顶端直到该标题的各级键。
    /// </summary>
    /// <returns>各级键，由外向内排列。</returns>
    public IReadOnlyList<object?> Path()
    {
        var path = new List<object?>();
        for (var node = this; node is not null; node = node.Parent)
        {
            path.Insert(0, node.Key);
        }

        return path;
    }

    internal void Add(PivotAxisNode child) => children.Add(child);

    internal void SetSpan(int span) => Span = span;
}
