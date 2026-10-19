using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="Cascader{TItem}"/> 提供的表单字段封装组件，
/// 在级联选择器外围自动展示标签、帮助文本与校验消息。
/// </summary>
/// <typeparam name="TItem">层级条目类型。</typeparam>
public partial class FormFieldCascader<TItem> : FormFieldBase
{
    /// <summary>
    /// 获取或设置层级的根条目集合。
    /// </summary>
    [Parameter, EditorRequired]
    public IEnumerable<TItem> Items { get; set; } = [];

    /// <summary>
    /// 获取或设置选取条目唯一键的选择器。
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TItem, string> ValueField { get; set; } = default!;

    /// <summary>
    /// 获取或设置选取条目显示文本的选择器。
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TItem, string> TextField { get; set; } = default!;

    /// <summary>
    /// 获取或设置选取条目子级的选择器。
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TItem, IEnumerable<TItem>?> ChildrenProperty { get; set; } = default!;

    /// <summary>
    /// 获取或设置选中节点的键。其完整路径由 <see cref="Items"/> 推导。
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
    /// 获取或设置在保持子级可导航的同时是否允许选中分支。
    /// </summary>
    [Parameter]
    public bool ChangeOnSelect { get; set; }

    /// <summary>
    /// 获取或设置是否显示全路径搜索框。
    /// </summary>
    [Parameter]
    public bool ShowSearch { get; set; } = true;

    /// <summary>
    /// 获取或设置是否在控件旁显示清除按钮。
    /// </summary>
    [Parameter]
    public bool Clearable { get; set; } = true;

    /// <summary>
    /// 获取或设置未选中时显示的占位文本。
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
    /// 获取或设置随值一起提交的表单字段名。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置应用到内部级联选择器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(string? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
