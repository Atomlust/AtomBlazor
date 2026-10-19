using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="CheckboxGroup{TValue}"/> 提供的表单字段封装组件，自动呈现标签、
/// 帮助文本与错误消息。
/// </summary>
/// <typeparam name="TValue">每个复选框值的类型。</typeparam>
public partial class FormFieldCheckboxGroup<TValue> : FormFieldBase
{
    /// <summary>标识用于 EditForm 校验的绑定字段。</summary>
    [Parameter] public Expression<Func<IReadOnlyCollection<TValue>>>? ValuesExpression { get; set; }

    /// <summary>
    /// 获取或设置当前选中的值集合。
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<TValue> Values { get; set; } = Array.Empty<TValue>();

    /// <summary>
    /// 获取或设置选中值集合变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyCollection<TValue>> ValuesChanged { get; set; }

    /// <summary>
    /// 获取或设置是否显示"全选"复选框。
    /// </summary>
    [Parameter]
    public bool ShowSelectAll { get; set; }

    /// <summary>
    /// 获取或设置全选复选框的标签文本。
    /// </summary>
    [Parameter]
    public string SelectAllLabel { get; set; } = "Select all";

    /// <summary>
    /// 获取或设置是否禁用组内全部条目。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置 CheckboxGroupItem 组件内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 CheckboxGroup 的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValuesExpression;

    private async Task HandleValuesChanged(IReadOnlyCollection<TValue> values)
    {
        Values = values;
        await ValuesChanged.InvokeAsync(values);
        NotifyFieldChanged();
    }
}
