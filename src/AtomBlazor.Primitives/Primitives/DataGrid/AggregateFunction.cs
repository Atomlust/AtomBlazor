namespace AtomBlazor.Primitives;

/// <summary>
/// 定义可在分组数据上计算的聚合函数。
/// </summary>
public enum AggregateFunction
{
    /// <summary>
    /// 不使用聚合函数。
    /// </summary>
    None = 0,

    /// <summary>
    /// 组内项的计数。
    /// </summary>
    Count,

    /// <summary>
    /// 组内数值的求和。
    /// </summary>
    Sum,

    /// <summary>
    /// 组内数值的平均值。
    /// </summary>
    Average,

    /// <summary>
    /// 组内的最小值。
    /// </summary>
    Min,

    /// <summary>
    /// 组内的最大值。
    /// </summary>
    Max
}

/// <summary>
/// 分组内某一特定列的聚合计算结果。
/// </summary>
public class AggregateResult
{
    /// <summary>
    /// 获取所应用的聚合函数。
    /// </summary>
    public required AggregateFunction Function { get; init; }

    /// <summary>
    /// 获取计算得到的聚合值。
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// 获取计算该聚合值所针对的列 ID。
    /// </summary>
    public required string ColumnId { get; init; }

    /// <summary>
    /// 获取用于显示聚合值的格式字符串（例如 "C0"、"N2"）。
    /// </summary>
    public string? Format { get; init; }
}
