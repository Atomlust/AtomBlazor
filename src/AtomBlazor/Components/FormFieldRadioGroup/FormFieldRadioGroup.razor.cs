using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="RadioGroup{TValue}"/> 提供的表单字段封装组件，自动呈现标签、
/// 帮助文本与错误消息。
/// </summary>
/// <typeparam name="TValue">与单选项关联的值类型。</typeparam>
public partial class FormFieldRadioGroup<TValue> : FormFieldBase
{
    /// <summary>
    /// 获取或设置当前选中的值。
    /// </summary>
    [Parameter]
    public TValue Value { get; set; } = default!;

    /// <summary>
    /// 获取或设置选中值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置是否禁用整个单选组。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置单选组是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 RadioGroup 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <summary>
    /// 获取或设置渲染在单选组内部的内容，
    /// 应包含 RadioGroupItem 组件。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(TValue value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
