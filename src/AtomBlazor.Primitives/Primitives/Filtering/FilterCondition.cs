using System.Text.Json.Serialization;

namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 表示一个由字段、运算符和值构成的单条筛选条件。
/// </summary>
public class FilterCondition
{
    /// <summary>
    /// 获取或设置此条件的唯一标识符。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// 获取或设置要筛选的字段名。必须与某个 <see cref="FilterField.Name"/> 匹配。
    /// </summary>
    public string Field { get; set; } = "";

    /// <summary>
    /// 获取或设置此条件的比较运算符。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<FilterOperator>))]
    public FilterOperator Operator { get; set; }

    /// <summary>
    /// 获取或设置主筛选值。
    /// 类型取决于字段类型（string、double、DateTime、bool，In/NotIn 使用 string[]）。
    /// </summary>
    [JsonConverter(typeof(FilterValueJsonConverter))]
    public object? Value { get; set; }

    /// <summary>
    /// 获取或设置次筛选值，供 <see cref="FilterOperator.Between"/> 之类的区间运算符使用。
    /// </summary>
    [JsonConverter(typeof(FilterValueJsonConverter))]
    public object? ValueEnd { get; set; }

    /// <summary>
    /// 创建此条件的深拷贝。
    /// </summary>
    public FilterCondition Clone()
    {
        return new FilterCondition
        {
            Id = Id,
            Field = Field,
            Operator = Operator,
            Value = CloneValue(Value),
            ValueEnd = CloneValue(ValueEnd)
        };
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            string[] arr => (string[])arr.Clone(),
            Array arr => arr.Clone(),
            _ => value
        };
    }
}
