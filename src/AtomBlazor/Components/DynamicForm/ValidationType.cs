namespace AtomBlazor;

/// <summary>
/// 定义应用于动态表单字段的验证类型。
/// </summary>
public enum ValidationType
{
    /// <summary>字段必须具有非空值。</summary>
    Required,

    /// <summary>字符串值必须满足最小长度。</summary>
    MinLength,

    /// <summary>字符串值不得超过最大长度。</summary>
    MaxLength,

    /// <summary>数字值必须大于或等于最小值。</summary>
    Min,

    /// <summary>数字值必须小于或等于最大值。</summary>
    Max,

    /// <summary>字符串值必须匹配指定的正则表达式模式。</summary>
    Pattern,

    /// <summary>字符串值必须是合法的电子邮件地址。</summary>
    Email,

    /// <summary>字符串值必须是合法的 URL。</summary>
    Url,

    /// <summary>字符串值必须是合法的电话号码。</summary>
    Phone,

    /// <summary>使用通过元数据提供的委托进行自定义验证。</summary>
    Custom
}

/// <summary>
/// 应用于动态表单字段的验证规则。
/// </summary>
public class FieldValidation
{
    /// <summary>
    /// 获取或设置验证类型。
    /// </summary>
    public ValidationType Type { get; set; }

    /// <summary>
    /// 获取或设置验证参数值（如最大长度数字、正则表达式模式字符串）。
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 获取或设置自定义错误消息。为 null 时生成默认消息。
    /// </summary>
    public string? Message { get; set; }
}
