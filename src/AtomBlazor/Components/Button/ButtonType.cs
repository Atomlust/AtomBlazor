namespace AtomBlazor;

/// <summary>
/// 定义 HTML button 元素的 type 特性值。
/// </summary>
/// <remarks>
/// 控制按钮元素用于 HTML 表单时的行为，与 button 元素的 HTML "type" 特性一一对应。
/// 参见：https://developer.mozilla.org/en-US/docs/Web/HTML/Element/button#type
/// </remarks>
public enum ButtonType
{
    /// <summary>
    /// 提交按钮（type="submit"）。
    /// 点击时把表单数据提交给服务器。
    /// 这是表单内按钮的默认行为。
    /// </summary>
    Submit,

    /// <summary>
    /// 重置按钮（type="reset"）。
    /// 点击时把所有表单控件恢复为初始值。
    /// 慎用，容易让用户困惑。
    /// </summary>
    Reset,

    /// <summary>
    /// 普通按钮（type="button"）。
    /// 既不提交也不重置表单。
    /// 表单外的按钮或带自定义点击处理的按钮的默认值，
    /// 可防止误触发表单提交。
    /// </summary>
    Button
}
