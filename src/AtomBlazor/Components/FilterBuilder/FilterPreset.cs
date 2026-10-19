using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Filtering;

namespace AtomBlazor;

/// <summary>可复用的筛选条件。构建器在编辑前会先克隆其定义。</summary>
public sealed record FilterPreset(string Id, string Label, FilterDefinition Filter);

/// <summary>已保存的筛选选项如何呈现。</summary>
public enum FilterPresetDisplay
{
    /// <summary>一排带按下状态的按钮。</summary>
    Buttons,
    /// <summary>一个紧凑的下拉框。</summary>
    Dropdown
}

/// <summary>自定义字段编辑器的当前值与变更回调。</summary>
public sealed class FilterValueEditorContext
{
    private readonly Primitives.Filtering.FilterCondition condition;
    private readonly EventCallback changed;

    internal FilterValueEditorContext(FilterField field, Primitives.Filtering.FilterCondition condition, EventCallback changed)
    {
        Field = field;
        this.condition = condition;
        this.changed = changed;
    }

    /// <summary>正在被编辑的字段。</summary>
    public FilterField Field { get; }
    /// <summary>当前选中的比较运算符。</summary>
    public FilterOperator Operator => condition.Operator;
    /// <summary>当前值。</summary>
    public object? Value => condition.Value;
    /// <summary>范围比较的 upper 值。</summary>
    public object? ValueEnd => condition.ValueEnd;

    /// <summary>更新活动草稿，并沿用构建器常规的 apply/debounce 行为。</summary>
    public async Task SetValueAsync(object? value, object? valueEnd = null)
    {
        condition.Value = value;
        condition.ValueEnd = valueEnd;
        await changed.InvokeAsync();
    }
}
