namespace AtomBlazor;

/// <summary>
/// 定义 InputGroup 附加内容（Addon）的对齐位置。
/// </summary>
/// <remarks>
/// <para>
/// InputGroupAlign 指定附加内容在 InputGroup 内相对输入控件的摆放位置。
/// </para>
/// <para>
/// 可选位置：
/// - InlineStart：置于输入起始侧（LTR 为左，RTL 为右）
/// - InlineEnd：置于输入结束侧（LTR 为右，RTL 为左）
/// - BlockStart：置于输入上方
/// - BlockEnd：置于输入下方
/// </para>
/// </remarks>
public enum InputGroupAlign
{
    /// <summary>
    /// 附加内容置于行内起始侧（LTR 为左，RTL 为右）。
    /// </summary>
    /// <remarks>
    /// 常见用法：搜索图标、货币符号、URL 协议前缀。
    /// </remarks>
    InlineStart,

    /// <summary>
    /// 附加内容置于行内结束侧（LTR 为右，RTL 为左）。
    /// </summary>
    /// <remarks>
    /// 常见用法：操作按钮、校验图标、清除按钮。
    /// </remarks>
    InlineEnd,

    /// <summary>
    /// 附加内容置于输入上方（块起始侧）。
    /// </summary>
    /// <remarks>
    /// 常见用法：标签、描述、字段标题。
    /// </remarks>
    BlockStart,

    /// <summary>
    /// 附加内容置于输入下方（块结束侧）。
    /// </summary>
    /// <remarks>
    /// 常见用法：字符计数器、帮助文本、校验消息。
    /// </remarks>
    BlockEnd
}
