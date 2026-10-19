using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="Switch"/> 提供的表单字段封装组件，
/// 自动展示标签、帮助文本与错误消息。
/// </summary>
public partial class FormFieldSwitch : FormFieldBase
{
    /// <summary>
    /// 获取或设置开关是否处于打开（checked）状态。
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
    /// 获取或设置开关的尺寸变体。
    /// </summary>
    [Parameter]
    public SwitchSize Size { get; set; } = SwitchSize.Medium;

    /// <summary>
    /// 获取或设置开关是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 Switch 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => CheckedExpression;

    private async Task HandleCheckedChanged(bool value)
    {
        Checked = value;
        await CheckedChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
