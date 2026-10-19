using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 把输入控件与图标、按钮、文本等附加内容组合在一起的容器组件。
/// </summary>
/// <remarks>
/// <para>
/// InputGroup 组件提供灵活的体系，用上下文信息与操作增强表单输入。
/// 它支持多种附加内容对齐位置，并统一管理整个组的聚焦状态。
/// </para>
/// <para>
/// 特性：
/// - 整组统一的边框与聚焦管理
/// - 多种附加内容对齐方式（inline-start、inline-end、block-start、block-end）
/// - 输入框与文本域的高度自适应
/// - 通过内部控件呈现错误态
/// - 通过 CSS 变量兼容暗色模式
/// - 与既有 Input、Button 组件无缝集成
/// </para>
/// <para>
/// 组件利用 CSS 选择器（has-[...]）探测子组件类型与状态，
/// 依据内容自动调整样式。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;InputGroup&gt;
///     &lt;InputGroupAddon Align="InputGroupAlign.InlineStart"&gt;
///         &lt;SearchIcon /&gt;
///     &lt;/InputGroupAddon&gt;
///     &lt;InputGroupInput Placeholder="Search..." /&gt;
/// &lt;/InputGroup&gt;
///
/// &lt;InputGroup&gt;
///     &lt;InputGroupInput Type="InputType.Email" Placeholder="Enter email" /&gt;
///     &lt;InputGroupAddon Align="InputGroupAlign.InlineEnd"&gt;
///         &lt;InputGroupButton&gt;Subscribe&lt;/InputGroupButton&gt;
///     &lt;/InputGroupAddon&gt;
/// &lt;/InputGroup&gt;
/// </code>
/// </example>
public partial class InputGroup : ComponentBase
{
    /// <summary>
    /// 获取或设置输入组内部渲染的子内容。
    /// </summary>
    /// <remarks>
    /// 通常包含 InputGroupInput/InputGroupTextarea 与 InputGroupAddon 组件。
    /// 子组件的顺序影响视觉布局与键盘导航。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到输入组容器的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类通过 TailwindMerge 与组件基础类合并，
    /// 可智能解析类名冲突。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到输入组容器的额外属性。
    /// </summary>
    /// <remarks>
    /// 可添加自定义 data 属性、ARIA 属性或事件处理器。
    /// </remarks>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取输入组容器计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 基础样式与依据子内容动态叠加的类组合而成：
    /// </para>
    /// <list type="bullet">
    /// <item>基础：边框、阴影、圆角、flex 容器</item>
    /// <item>高度：输入框用 h-9，文本域或块状附加内容用 h-auto</item>
    /// <item>对齐：依据行内附加内容位置动态调整内边距</item>
    /// <item>聚焦：任一子控件聚焦时改变边框颜色</item>
    /// <item>错误：任一子控件带 aria-invalid 时改变边框颜色</item>
    /// </list>
    /// <para>
    /// 使用现代 CSS :has() 伪类探测子元素状态。
    /// </para>
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 基础容器样式
        "atom:group/input-group atom:relative atom:flex atom:w-full",
        "atom:border atom:border-input atom:rounded-md atom:shadow-sm",
        "atom:bg-background atom:overflow-hidden",
        "atom:transition-colors atom:outline-none",

        // 输入框的默认高度与对齐
        "atom:h-9 atom:min-w-0 atom:items-center",

        // 含文本域或块状附加内容时高度自适应
        "atom:has-[>textarea]:h-auto atom:has-[>textarea]:items-start",
        "atom:has-[>[data-align=block-start]]:h-auto atom:has-[>[data-align=block-start]]:flex-col atom:has-[>[data-align=block-start]]:items-stretch",
        "atom:has-[>[data-align=block-end]]:h-auto atom:has-[>[data-align=block-end]]:flex-col atom:has-[>[data-align=block-end]]:items-stretch",

        // 存在行内附加内容时压缩输入内边距，保持紧凑间距
        "atom:has-[>[data-align=inline-start]]:[&>input]:ps-1",
        "atom:has-[>[data-align=inline-start]]:[&>textarea]:ps-1",
        "atom:has-[>[data-align=inline-end]]:[&>input]:pe-1",
        "atom:has-[>[data-align=inline-end]]:[&>textarea]:pe-1",

        // 附加内容为按钮时进一步压缩，保证贴合
        "atom:has-[>[data-align=inline-start]>button]:[&>input]:ps-1.5",
        "atom:has-[>[data-align=inline-start]>button]:[&>textarea]:ps-1.5",
        "atom:has-[>[data-align=inline-end]>button]:[&>input]:pe-1.5",
        "atom:has-[>[data-align=inline-end]>button]:[&>textarea]:pe-1.5",

        // 行内附加内容含按钮时压缩输入纵向内边距，保持间距均匀
        "atom:has-[>[data-align=inline-start]>button]:[&>input]:py-1.5",
        "atom:has-[>[data-align=inline-end]>button]:[&>input]:py-1.5",
        "atom:has-[>[data-align=inline-start]>button]:[&>textarea]:py-1.5",
        "atom:has-[>[data-align=inline-end]>button]:[&>textarea]:py-1.5",

        // 聚焦状态——组内任一控件聚焦时
        "atom:has-[[data-slot=input-group-control]:focus-visible]:border-ring",

        // 错误状态——组内任一控件带 aria-invalid 时
        "atom:has-[[data-slot=input-group-control][aria-invalid=true]]:border-destructive",

        // 自定义类
        Class
    );
}
