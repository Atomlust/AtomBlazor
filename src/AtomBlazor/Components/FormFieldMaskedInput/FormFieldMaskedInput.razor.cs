using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="MaskedInput"/> 提供的表单字段封装组件，
/// 自动展示标签、帮助文本与错误消息。
/// </summary>
public partial class FormFieldMaskedInput : FormFieldBase
{
    /// <summary>
    /// 获取或设置未掩码值（不带格式的原始输入）。
    /// </summary>
    [Parameter]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置未掩码值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<string>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置 HTML name 特性。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置预定义掩码预设。
    /// </summary>
    [Parameter]
    public MaskPreset Preset { get; set; } = MaskPreset.Custom;

    /// <summary>
    /// 获取或设置自定义掩码模式（当 Preset 为 Custom 时）。
    /// 掩码字符：9 = 数字，A = 字母，* = 字母或数字。
    /// </summary>
    [Parameter]
    public string? Mask { get; set; }

    /// <summary>
    /// 获取或设置未填充位置的占位字符。
    /// </summary>
    [Parameter]
    public char PlaceholderChar { get; set; } = '_';

    /// <summary>
    /// 获取或设置是否显示带占位字符的掩码。
    /// </summary>
    [Parameter]
    public bool ShowMask { get; set; } = true;

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
    /// 获取或设置应用到内部 MaskedInput 元素的额外 CSS 类。
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
