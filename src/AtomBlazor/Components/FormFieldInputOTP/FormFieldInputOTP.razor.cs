using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="InputOTP"/> 提供的表单字段封装组件，
/// 自动展示标签、帮助文本与错误消息。
/// </summary>
public partial class FormFieldInputOTP : FormFieldBase
{
    /// <summary>
    /// 获取或设置当前 OTP 值。
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置 HTML name 特性。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置 OTP 的位数。
    /// </summary>
    [Parameter]
    public int Length { get; set; } = 6;

    /// <summary>
    /// 获取或设置所有数位填满时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnComplete { get; set; }

    /// <summary>
    /// 获取或设置输入是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置输入是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置是否在分组之间显示分隔符。
    /// </summary>
    [Parameter]
    public bool ShowSeparator { get; set; } = true;

    /// <summary>
    /// 获取或设置每组的位数（用于分隔符摆放）。
    /// </summary>
    [Parameter]
    public int GroupSize { get; set; } = 3;

    /// <summary>
    /// 获取或设置自定义分隔符内容。
    /// </summary>
    [Parameter]
    public RenderFragment? Separator { get; set; }

    /// <summary>
    /// 获取或设置输入格的尺寸变体。
    /// </summary>
    [Parameter]
    public InputOTPSize Size { get; set; } = InputOTPSize.Default;

    /// <summary>
    /// 获取或设置是否用星号（*）屏蔽输入值。
    /// </summary>
    [Parameter]
    public bool MaskInput { get; set; }

    /// <summary>
    /// 获取或设置接受的字符类型。
    /// </summary>
    [Parameter]
    public InputOTPInputMode InputMode { get; set; } = InputOTPInputMode.Numbers;

    /// <summary>
    /// 获取或设置应用到每个 OTP 输入格的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(string value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
