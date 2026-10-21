using System.Text.Json.Serialization;

namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 表示一棵由逻辑运算符组合的筛选条件与嵌套分组构成的树。
/// </summary>
public class FilterDefinition
{
    /// <summary>
    /// 获取单调递增的版本号，每次筛选编辑都会变化。
    /// 供使用方检测原地修改。
    /// </summary>
    [JsonIgnore]
    public int Version { get; set; }

    /// <summary>
    /// 获取或设置组合条件与分组所用的逻辑运算符（AND/OR）。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<LogicalOperator>))]
    public LogicalOperator Operator { get; set; } = LogicalOperator.And;

    /// <summary>
    /// 获取或设置本分组中的筛选条件列表。
    /// </summary>
    public List<FilterCondition> Conditions { get; set; } = new();

    /// <summary>
    /// 获取或设置嵌套筛选分组列表。
    /// </summary>
    public List<FilterDefinition> Groups { get; set; } = new();

    /// <summary>
    /// 获取一个值，指示此筛选是否既无条件也无分组。
    /// </summary>
    public bool IsEmpty => Conditions.Count == 0 && Groups.Count == 0;

    /// <summary>
    /// 获取本分组及所有嵌套分组中的条件总数。
    /// </summary>
    public int TotalConditionCount
    {
        get
        {
            var count = Conditions.Count;
            foreach (var group in Groups)
            {
                count += group.TotalConditionCount;
            }
            return count;
        }
    }

    /// <summary>
    /// 创建此筛选定义的深拷贝，包含全部条件与嵌套分组。
    /// </summary>
    public FilterDefinition Clone()
    {
        return new FilterDefinition
        {
            Operator = Operator,
            Conditions = Conditions.Select(c => c.Clone()).ToList(),
            Groups = Groups.Select(g => g.Clone()).ToList()
        };
    }
}
