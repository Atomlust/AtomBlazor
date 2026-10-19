namespace AtomBlazor;

/// <summary>
/// 动态表单字段值变化时的事件参数。
/// </summary>
public class FormFieldChangedEventArgs : EventArgs
{
    /// <summary>
    /// 获取发生变化的字段名称。
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// 获取字段的先前值。
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// 获取字段的新值。
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// 初始化 <see cref="FormFieldChangedEventArgs"/> 类的新实例。
    /// </summary>
    public FormFieldChangedEventArgs(string fieldName, object? oldValue, object? newValue)
    {
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
