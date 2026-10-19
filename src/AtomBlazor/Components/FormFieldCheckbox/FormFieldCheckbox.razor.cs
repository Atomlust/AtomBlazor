using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="Checkbox"/> 提供的表单字段封装组件，自动呈现标签、
/// 帮助文本与错误消息。
/// </summary>
public partial class FormFieldCheckbox : FormFieldBase
{
    /// <summary>
    /// 获取或设置复选框是否选中。
    /// </summary>
    [Parameter]
    public bool Checked { get; set; }

    /// <summary>
    /// 获取或设置选中状态变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<bool>>? CheckedExpression { get; set; }

    /// <summary>
    /// 获取或设置 HTML name 属性，透传给内部 Checkbox
    /// 在 EditForm 内未显式设置时由 CheckedExpression 自动。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置复选框是否处于不确定态。
    /// </summary>
    [Parameter]
    public bool Indeterminate { get; set; }

    /// <summary>
    /// 获取或设置不确定态变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<bool> IndeterminateChanged { get; set; }

    /// <summary>
    /// 获取或设置复选框是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置复选框在表单上下文中是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 Checkbox 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (!parameters.TryGetValue<FieldOrientation>(nameof(Orientation), out _))
        {
            Orientation = FieldOrientation.HorizontalEnd;
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => CheckedExpression;

    private FieldOrientation EffectiveOrientation => Orientation is FieldOrientation.HorizontalEnd or FieldOrientation.Horizontal or FieldOrientation.VerticalEnd
        ? FieldOrientation.Vertical
        : Orientation;

    private async Task HandleCheckedChanged(bool value)
    {
        Checked = value;
        await CheckedChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
