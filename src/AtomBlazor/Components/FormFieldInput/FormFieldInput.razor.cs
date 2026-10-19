using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 将 <see cref="InputField{TValue}"/> 与 <see cref="Field"/>、<see cref="FieldLabel"/>、
/// <see cref="FieldDescription"/>、<see cref="FieldError"/> 组合成开箱即用的完整表单字段的
/// 快捷封装组件。
/// </summary>
/// <remarks>
/// <para>
/// FormFieldInput 在基础组件 <see cref="InputField{TValue}"/> 之上提供更高层次的抽象，
/// 自动处理标签关联、帮助文本展示，并根据底层 InputField 的错误种类渲染错误消息。
/// </para>
/// <para>
/// 若需要完全掌控布局与错误展示，请直接使用 <see cref="InputField{TValue}"/>
/// 配合 <see cref="Field"/> 组件体系。
/// </para>
/// <para>
/// 特性：
/// - 自动完成标签与控件的 for/id 关联
/// - 错误态下自动隐藏帮助文本
/// - 按错误种类自动生成上下文化的错误消息
/// - 通过 <see cref="FormFieldBase.ErrorText"/> 支持手动覆盖错误文本
/// - 透传全部 <see cref="InputField{TValue}"/> 参数
/// - 自动接线 ARIA 属性（describedby、invalid）
/// </para>
/// </remarks>
/// <typeparam name="TValue">要绑定的值类型。</typeparam>
/// <example>
/// <code>
/// &lt;FormFieldInput TValue="int"
///            @bind-Value="age"
///            Label="Age"
///            HelperText="Must be 18 or older"
///            Validation="v =&gt; v &gt;= 18" /&gt;
/// </code>
/// </example>
public partial class FormFieldInput<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] TValue> : FormFieldBase
{
    private InputField<TValue>? _inputRef;
    private bool _hasError;
    private string? _errorMessage;

    // --- InputField 透传参数 ---

    /// <summary>
    /// 获取或设置当前的强类型值。
    /// </summary>
    [Parameter]
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置强类型值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置可选的自定义转换器。
    /// </summary>
    [Parameter]
    public InputConverter<TValue>? Converter { get; set; }

    /// <summary>
    /// 获取或设置显示格式字符串。
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// 获取或设置输入类型。
    /// </summary>
    [Parameter]
    public InputType Type { get; set; } = InputType.Text;

    /// <summary>
    /// 获取或设置占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置输入框是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置输入框是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置解析后的值校验函数。
    /// </summary>
    [Parameter]
    public Func<TValue, bool>? Validation { get; set; }

    /// <summary>
    /// 获取或设置解析前校验的正则表达式。
    /// </summary>
    [Parameter]
    public string? ValidationPattern { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 InputField 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <summary>
    /// 获取或设置 ValueChanged 的触发时机。
    /// </summary>
    [Parameter]
    public UpdateTiming UpdateTiming { get; set; } = UpdateTiming.OnChange;

    /// <summary>
    /// 获取或设置防抖延迟（毫秒）。
    /// </summary>
    [Parameter]
    public int DebounceInterval { get; set; } = 500;

    /// <summary>
    /// 获取或设置解析或校验错误发生时的回调。
    /// </summary>
    [Parameter]
    public EventCallback<InputParseException> OnParseError { get; set; }

    /// <summary>
    /// 获取或设置错误状态被清除时的回调。
    /// </summary>
    [Parameter]
    public EventCallback OnErrorCleared { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// 由 <c>@bind-Value</c> 自动提供，透传给内部 InputField。
    /// </summary>
    [Parameter]
    public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置 HTML name 属性，透传给内部 InputField。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取表单字段当前是否存在错误。
    /// </summary>
    public bool HasError => _hasError;

    /// <summary>
    /// 获取底层 InputField 组件引用。
    /// </summary>
    public InputField<TValue>? InputFieldRef => _inputRef;

    /// <summary>
    /// 让底层输入框元素获得焦点。
    /// </summary>
    public ValueTask FocusAsync() => _inputRef?.FocusAsync() ?? ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override bool IsInvalid => _hasError || base.IsInvalid;

    /// <inheritdoc />
    protected override string? DescribedById => IsInvalid
        ? ErrorId
        : !string.IsNullOrEmpty(HelperText) ? DescriptionId : null;

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(TValue? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
    }

    private async Task HandleParseError(InputParseException ex)
    {
        _hasError = true;
        _errorMessage = ErrorText ?? GenerateErrorMessage(ex);

        if (OnParseError.HasDelegate)
        {
            await OnParseError.InvokeAsync(ex);
        }
    }

    private async Task HandleErrorCleared()
    {
        _hasError = false;
        _errorMessage = null;

        if (OnErrorCleared.HasDelegate)
        {
            await OnErrorCleared.InvokeAsync();
        }
    }

    private static string GenerateErrorMessage(InputParseException ex) => ex.ErrorKind switch
    {
        InputFieldErrorKind.Parse => $"'{ex.RawInput}' is not a valid {GetFriendlyTypeName(ex.TargetType)}.",
        InputFieldErrorKind.PatternValidation => $"'{ex.RawInput}' does not match the required format.",
        InputFieldErrorKind.ValueValidation => "The entered value is not valid.",
        _ => "Invalid input."
    };

    private static string GetFriendlyTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string))
        {
            return "text value";
        }
        if (underlying == typeof(int) || underlying == typeof(long))
        {
            return "whole number";
        }
        if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal))
        {
            return "number";
        }
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(DateOnly))
        {
            return "date";
        }
        if (underlying == typeof(TimeOnly))
        {
            return "time";
        }
        if (underlying == typeof(Guid))
        {
            return "GUID";
        }
        if (underlying == typeof(bool))
        {
            return "true/false value";
        }

        return underlying.Name.ToLowerInvariant();
    }
}
