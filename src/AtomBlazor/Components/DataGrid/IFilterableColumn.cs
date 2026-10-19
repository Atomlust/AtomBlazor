using AtomBlazor.Primitives.Filtering;
using AtomBlazor.Primitives;
namespace AtomBlazor;

/// <summary>
/// 支持按列筛选的列的内部接口。
/// 提供构建筛选 UI 和构造筛选表达式所需的元数据。
/// </summary>
internal interface IFilterableColumn
{
    /// <summary>
    /// 获取该列数据类型对应的筛选字段类型。
    /// </summary>
    internal FilterFieldType GetFilterFieldType();

    /// <summary>
    /// 获取 Enum 类型筛选字段的预定义选项，其他类型返回 null。
    /// </summary>
    internal IEnumerable<SelectOption<string>>? GetFilterOptions();

    /// <summary>
    /// 获取用于 <see cref="Primitives.Filtering.FilterCondition.Field"/> 属性的字段名。
    /// 通常为表达式中的属性名。
    /// </summary>
    internal string GetFilterFieldName();
}
