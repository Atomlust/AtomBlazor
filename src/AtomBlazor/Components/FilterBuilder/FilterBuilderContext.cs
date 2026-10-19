using AtomBlazor.Primitives.Filtering;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 由 <see cref="FilterBuilder"/> 级联给子组件的上下文。
/// 提供对可用字段与配置的访问。
/// </summary>
public class FilterBuilderContext
{
    /// <summary>字段选择器是否可搜索。</summary>
    public bool SearchableFields { get; init; }
    /// <summary>自定义字段值编辑器，以字段名为键。</summary>
    public IReadOnlyDictionary<string, RenderFragment<FilterValueEditorContext>>? ValueEditors { get; init; }

    /// <summary>
    /// 获取可供筛选的字段。
    /// </summary>
    public required IEnumerable<FilterField> Fields { get; init; }

    /// <summary>
    /// 获取筛选组的最大嵌套深度。
    /// </summary>
    public int MaxDepth { get; init; }

    /// <summary>
    /// 获取允许的最大条件总数，null 表示不限。
    /// </summary>
    public int? MaxConditions { get; init; }

    /// <summary>
    /// 获取新分组的默认逻辑运算符。
    /// </summary>
    public LogicalOperator DefaultOperator { get; init; }

    /// <summary>
    /// 获取一个值，指示是否启用紧凑布局模式。
    /// </summary>
    public bool Compact { get; init; }

    /// <summary>
    /// 获取用于计算条件总数的根筛选定义。
    /// </summary>
    public required FilterDefinition RootFilter { get; init; }

    /// <summary>
    /// 获取筛选改变时要调用的回调。
    /// </summary>
    public required Action NotifyChanged { get; init; }

    /// <summary>
    /// 若新增一个条件会超出 MaxConditions 限制则返回 true。
    /// </summary>
    public bool IsAtConditionLimit => MaxConditions.HasValue && RootFilter.TotalConditionCount >= MaxConditions.Value;
}
