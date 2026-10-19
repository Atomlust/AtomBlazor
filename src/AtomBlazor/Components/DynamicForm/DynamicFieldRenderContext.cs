namespace AtomBlazor;

/// <summary>
/// 传递给动态表单自定义字段渲染器的上下文，提供字段定义、当前值及更新值的回调。
/// </summary>
public class DynamicFieldRenderContext
{
    /// <summary>
    /// 获取此字段的字段定义。
    /// </summary>
    public FormFieldDefinition Field { get; }

    /// <summary>
    /// 获取字段的当前值。
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// 获取字段值变化时调用的回调。
    /// </summary>
    public Func<object?, Task> ValueChanged { get; }

    /// <summary>
    /// 获取此字段是否被禁用（来自字段定义或表单级覆盖）。
    /// </summary>
    public bool Disabled { get; }

    /// <summary>
    /// 获取此字段是否为只读（来自字段定义或表单级覆盖）。
    /// </summary>
    public bool ReadOnly { get; }

    /// <summary>
    /// 初始化 <see cref="DynamicFieldRenderContext"/> 类的新实例。
    /// </summary>
    public DynamicFieldRenderContext(
        FormFieldDefinition field,
        object? value,
        Func<object?, Task> valueChanged,
        bool disabled,
        bool readOnly)
    {
        Field = field;
        Value = value;
        ValueChanged = valueChanged;
        Disabled = disabled;
        ReadOnly = readOnly;
    }
}
