namespace AtomBlazor.Primitives;

/// <summary>
/// 表示扁平化渲染列表中的组头行。
/// 包含分组键、项计数、子项以及聚合结果。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class DataGridGroupRow<TData> where TData : class
{
    /// <summary>
    /// 获取分组键值（即该组对应分组列的值）。
    /// </summary>
    public object? Key { get; init; }

    /// <summary>
    /// 获取标识该组的有序键路径，由最外层开始。
    /// </summary>
    /// <remarks>
    /// 折叠状态以此键路径为键。分组嵌套后原始键会有歧义：键为 <c>Active</c>
    /// 的组在每个部门下都存在，折叠其中一个会把它全部折叠。
    /// 默认值为由 <see cref="Key"/> 构造的单段路径。
    /// </remarks>
    public GroupPath Path { get; init; } = GroupPath.Root;

    /// <summary>
    /// 获取该组的嵌套深度，从 0 开始。最外层为 0。
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// 获取直接嵌套在本组内的子组，由最外层开始；最内层为空列表。
    /// </summary>
    public IReadOnlyList<DataGridGroupRow<TData>> Children { get; init; } =
        Array.Empty<DataGridGroupRow<TData>>();

    /// <summary>
    /// 获取所按以分组的列 ID。
    /// </summary>
    public required string ColumnId { get; init; }

    /// <summary>
    /// 获取分组列的显示名称。
    /// </summary>
    public string? ColumnTitle { get; init; }

    /// <summary>
    /// 获取该组中数据项的数量。
    /// </summary>
    public int ItemCount { get; init; }

    /// <summary>
    /// 获取属于该组的数据项，包含其下每个嵌套组中的项，
    /// 因此聚合会逐级汇总而不是只统计最内层。
    /// </summary>
    public required IReadOnlyList<TData> Items { get; init; }

    /// <summary>
    /// 获取该组的聚合结果，以列 ID 为键。
    /// </summary>
    public Dictionary<string, AggregateResult> Aggregates { get; init; } = new();
}
