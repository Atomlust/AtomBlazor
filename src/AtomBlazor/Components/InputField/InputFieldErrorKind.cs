namespace AtomBlazor;

/// <summary>
/// 标识 <see cref="InputField{TValue}"/> 组件中发生的错误种类。
/// </summary>
public enum InputFieldErrorKind
{
    /// <summary>
    /// 原始输入字符串无法解析/转换为目标类型。
    /// </summary>
    Parse,

    /// <summary>
    /// 原始输入字符串与 <see cref="InputField{TValue}.ValidationPattern"/> 正则表达式不匹配。
    /// </summary>
    PatternValidation,

    /// <summary>
    /// 解析出的值被 <see cref="InputField{TValue}.Validation"/> 函数拒绝。
    /// </summary>
    ValueValidation
}
