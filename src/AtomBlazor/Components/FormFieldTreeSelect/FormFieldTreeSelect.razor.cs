using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 TreeSelect 组件提供的表单字段封装组件，
/// 在树形选择器外围加上标签、帮助文本与校验消息。
/// </summary>
/// <typeparam name="TItem">层级中条目的类型。</typeparam>
/// <remarks>
/// 单选模式绑定 <see cref="Value"/>；<see cref="Multiple"/> 多选模式绑定
/// <see cref="Values"/>。请绑定与当前模式匹配的那个表达式——字段标识符
/// （因而也是校验消息的归属）取自被设置的那一个。
/// </remarks>
public partial class FormFieldTreeSelect<TItem> : FormFieldBase
{
    /// <summary>
    /// 获取或设置层级的根条目集合。
    /// </summary>
    [Parameter, EditorRequired]
    public IEnumerable<TItem> Items { get; set; } = [];

    /// <summary>
    /// 获取或设置取条目唯一键的选择器。
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TItem, string> ValueField { get; set; } = default!;

    /// <summary>
    /// 获取或设置取条目展示文本的选择器。
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TItem, string> TextField { get; set; } = default!;

    /// <summary>
    /// 获取或设置取条目子级的选择器。
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TItem, IEnumerable<TItem>?> ChildrenProperty { get; set; } = default!;

    /// <summary>
    /// 获取或设置单选模式下选中的键。
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置选中键变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定键的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置多选模式下选中的键集合。
    /// </summary>
    [Parameter]
    public HashSet<string> Values { get; set; } = [];

    /// <summary>
    /// 获取或设置选中键集合变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<HashSet<string>> ValuesChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定键集合的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<HashSet<string>>>? ValuesExpression { get; set; }

    /// <summary>
    /// 获取或设置是否允许多选。
    /// </summary>
    [Parameter]
    public bool Multiple { get; set; }

    /// <summary>
    /// 获取或设置是否仅允许选中叶子条目。
    /// </summary>
    [Parameter]
    public bool LeafOnly { get; set; }

    /// <summary>
    /// 获取或设置是否展示搜索框。
    /// </summary>
    [Parameter]
    public bool ShowSearch { get; set; } = true;

    /// <summary>
    /// 获取或设置控件旁是否展示清除按钮。
    /// </summary>
    [Parameter]
    public bool Clearable { get; set; } = true;

    /// <summary>
    /// 获取或设置未选中任何内容时展示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置控件是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置字段是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置随值一同提交的表单字段名。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置应用到内部树形选择器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() =>
        Multiple ? ValuesExpression : ValueExpression;

    private async Task HandleValueChanged(string? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }

    private async Task HandleValuesChanged(HashSet<string> values)
    {
        Values = values;
        await ValuesChanged.InvokeAsync(values);
        NotifyFieldChanged();
    }
}
