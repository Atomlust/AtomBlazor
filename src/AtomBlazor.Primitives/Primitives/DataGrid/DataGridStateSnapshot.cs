using AtomBlazor.Primitives.Filtering;


namespace AtomBlazor.Primitives;

/// <summary>
/// 用于持久化的 DataGrid 状态可序列化快照。
/// 包含排序、列可见性/顺序/宽度以及页大小。
/// 消费者可将其序列化为 JSON 并存入 localStorage、数据库等处。
/// </summary>
public class DataGridStateSnapshot
{
    /// <summary>
    /// 获取或设置排序定义。
    /// </summary>
    public List<SortDefinitionSnapshot> SortDefinitions { get; set; } = new();

    /// <summary>
    /// 获取或设置列状态条目。
    /// </summary>
    public List<ColumnStateSnapshot> ColumnStates { get; set; } = new();

    /// <summary>
    /// 获取或设置页大小。
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 获取或设置列过滤快照。
    /// </summary>
    public List<ColumnFilterSnapshot> ColumnFilters { get; set; } = new();

    /// <summary>
    /// 获取或设置最外层分组定义的快照。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="GroupDefinitions"/> 一同读写，使得本版本保存的快照
    /// 仍能在针对单层分组编写的版本中还原，反之旧版本保存的快照也能在
    /// 本版本中还原。
    /// </remarks>
    public GroupDefinitionSnapshot? GroupDefinition { get; set; }

    /// <summary>
    /// 获取或设置全部分组定义快照，由最外层开始。
    /// </summary>
    public List<GroupDefinitionSnapshot> GroupDefinitions { get; set; } = new();
}

/// <summary>
/// 单个排序定义的可序列化快照。
/// </summary>
public class SortDefinitionSnapshot
{
    /// <summary>
    /// 获取或设置列 ID。
    /// </summary>
    public string ColumnId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置排序方向。
    /// </summary>
    public SortDirection Direction { get; set; }
}

/// <summary>
/// 单列状态的可序列化快照。
/// </summary>
public class ColumnStateSnapshot
{
    /// <summary>
    /// 获取或设置列 ID。
    /// </summary>
    public string ColumnId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置列是否可见。
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 获取或设置列宽（例如 "200px"）。为 null 表示自动。
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// 获取或设置列的显示顺序。
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// 单列过滤状态的可序列化快照。
/// </summary>
public class ColumnFilterSnapshot
{
    /// <summary>
    /// 获取或设置列 ID。
    /// </summary>
    public string ColumnId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置过滤运算符。
    /// </summary>
    public FilterOperator Operator { get; set; }

    /// <summary>
    /// 获取或设置主过滤值。
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 获取或设置次过滤值（用于区间类运算符）。
    /// </summary>
    public object? ValueEnd { get; set; }
}

/// <summary>
/// 分组定义的可序列化快照。
/// </summary>
public class GroupDefinitionSnapshot
{
    /// <summary>
    /// 获取或设置要按其分组的列 ID。
    /// </summary>
    public string ColumnId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置分组排序方向。
    /// </summary>
    public SortDirection GroupSortDirection { get; set; }
}
