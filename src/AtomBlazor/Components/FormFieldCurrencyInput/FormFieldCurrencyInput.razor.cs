using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="CurrencyInput"/> 提供的表单字段封装组件，
/// 自动展示标签、帮助文本与错误消息。
/// </summary>
public partial class FormFieldCurrencyInput : FormFieldBase
{
    /// <summary>
    /// 获取或设置当前十进制值。
    /// </summary>
    [Parameter]
    public decimal Value { get; set; }

    /// <summary>
    /// 获取或设置值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<decimal> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<decimal>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置 HTML name 特性。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置 ISO 4217 币种代码。
    /// </summary>
    [Parameter]
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>
    /// 获取或设置是否显示币种符号。
    /// </summary>
    [Parameter]
    public bool ShowSymbol { get; set; } = true;

    /// <summary>
    /// 获取或设置允许的最小值。
    /// </summary>
    [Parameter]
    public decimal? Min { get; set; }

    /// <summary>
    /// 获取或设置允许的最大值。
    /// </summary>
    [Parameter]
    public decimal? Max { get; set; }

    /// <summary>
    /// 获取或设置是否允许负值。
    /// </summary>
    [Parameter]
    public bool AllowNegative { get; set; } = true;

    /// <summary>
    /// 获取或设置显示模式是否使用千分位分隔符。
    /// </summary>
    [Parameter]
    public bool UseThousandSeparator { get; set; } = true;

    /// <summary>
    /// 获取或设置占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

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
    /// 获取或设置是否在键入过程中禁用防抖。
    /// </summary>
    [Parameter]
    public bool DisableDebounce { get; set; }

    /// <summary>
    /// 获取或设置防抖间隔（毫秒）。
    /// </summary>
    [Parameter]
    public int DebounceInterval { get; set; } = 500;

    /// <summary>
    /// 获取或设置是否在输入框获得焦点时用鼠标滚轮增减值。
    /// 缺省为 <c>false</c>；参见 <see cref="CurrencyInput.EnableWheelStep"/>。
    /// </summary>
    [Parameter]
    public bool EnableWheelStep { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 CurrencyInput 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(decimal value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
