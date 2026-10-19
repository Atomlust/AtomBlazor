namespace AtomBlazor;

/// <summary>
/// 表示 <see cref="InputField{TValue}"/> 组件在解析或校验用户输入时发生的错误。
/// </summary>
/// <remarks>
/// 当用户在失焦时留下了无法转换或无法校验通过的值，
/// 通过 <see cref="InputField{TValue}.OnParseError"/> 回调抛出本异常。
/// 携带原始输入字符串、目标类型与错误种类，用于上下文相关的错误报告。
/// </remarks>
/// <example>
/// <code>
/// &lt;InputField TValue="int" @bind-Value="age" OnParseError="HandleError" /&gt;
///
/// @code {
///     private void HandleError(InputParseException ex)
///     {
///         var message = ex.ErrorKind switch
///         {
///             InputFieldErrorKind.Parse =&gt; $"'{ex.RawInput}' is not a valid {ex.TargetType.Name}.",
///             InputFieldErrorKind.PatternValidation =&gt; $"'{ex.RawInput}' does not match the required format.",
///             InputFieldErrorKind.ValueValidation =&gt; $"The value is not within the allowed range.",
///             _ =&gt; "Invalid input."
///         };
///     }
/// }
/// </code>
/// </example>
public class InputParseException : Exception
{
    /// <summary>
    /// 获取解析或校验失败的原始输入字符串。
    /// </summary>
    public string RawInput { get; }

    /// <summary>
    /// 获取输入无法转换到的目标类型。
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// 获取发生的错误种类。
    /// </summary>
    public InputFieldErrorKind ErrorKind { get; }

    /// <summary>
    /// 初始化 <see cref="InputParseException"/> 类的新实例。
    /// </summary>
    /// <param name="rawInput">解析或校验失败的原始输入字符串。</param>
    /// <param name="targetType">输入无法转换到的目标类型。</param>
    /// <param name="errorKind">发生的错误种类。</param>
    /// <param name="innerException">导致失败的内部异常。</param>
    public InputParseException(string rawInput, Type targetType, InputFieldErrorKind errorKind, Exception innerException)
        : base(FormatMessage(rawInput, targetType, errorKind), innerException)
    {
        RawInput = rawInput;
        TargetType = targetType;
        ErrorKind = errorKind;
    }

    /// <summary>
    /// 以 <see cref="InputFieldErrorKind.Parse"/> 为默认错误种类，
    /// 初始化 <see cref="InputParseException"/> 类的新实例。
    /// </summary>
    /// <param name="rawInput">解析失败的原始输入字符串。</param>
    /// <param name="targetType">输入无法转换到的目标类型。</param>
    /// <param name="innerException">导致解析失败的内部异常。</param>
    public InputParseException(string rawInput, Type targetType, Exception innerException)
        : this(rawInput, targetType, InputFieldErrorKind.Parse, innerException)
    {
    }

    private static string FormatMessage(string rawInput, Type targetType, InputFieldErrorKind errorKind) => errorKind switch
    {
        InputFieldErrorKind.Parse => $"Failed to parse '{rawInput}' as {targetType.Name}.",
        InputFieldErrorKind.PatternValidation => $"Input '{rawInput}' does not match the required pattern for {targetType.Name}.",
        InputFieldErrorKind.ValueValidation => $"Value parsed from '{rawInput}' failed validation for {targetType.Name}.",
        _ => $"Failed to parse '{rawInput}' as {targetType.Name}."
    };
}
