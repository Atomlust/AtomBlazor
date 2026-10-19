using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>
/// 承载环绕在输入控件周围的附加内容的容器。
/// </summary>
/// <remarks>
/// <para>
/// InputGroupAddon 允许在输入框的不同位置添加图标、按钮、文本或其他元素。
/// 它负责焦点委托：点击附加内容时会聚焦关联的输入控件。
/// </para>
/// <para>
/// 特性：
/// - 四种对齐位置：InlineStart、InlineEnd、BlockStart、BlockEnd
/// - 自动把点击委托聚焦到同级的输入控件
/// - 灵活的内容支持（图标、按钮、文本等）
/// - 依据内容类型智能调整内边距
/// - 与 InputGroup 统一样式的无缝集成
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;!-- 起始侧图标 --&gt;
/// &lt;InputGroup&gt;
/// &lt;InputGroupAddon Align="InputGroupAlign.InlineStart"&gt;
/// &lt;SearchIcon /&gt;
/// &lt;/InputGroupAddon&gt;
/// &lt;InputGroupInput Placeholder="Search..." /&gt;
/// &lt;/InputGroup&gt;
///
/// &lt;!-- 结束侧按钮 --&gt;
/// &lt;InputGroup&gt;
/// &lt;InputGroupInput Placeholder="Email address" /&gt;
/// &lt;InputGroupAddon Align="InputGroupAlign.InlineEnd"&gt;
/// &lt;InputGroupButton&gt;Send&lt;/InputGroupButton&gt;
/// &lt;/InputGroupAddon&gt;
/// &lt;/InputGroup&gt;
///
/// &lt;!-- 下方字符计数 --&gt;
/// &lt;InputGroup&gt;
/// &lt;InputGroupTextarea Placeholder="Write a comment..." /&gt;
/// &lt;InputGroupAddon Align="InputGroupAlign.BlockEnd"&gt;
/// &lt;InputGroupText&gt;0 / 280&lt;/InputGroupText&gt;
/// &lt;/InputGroupAddon&gt;
/// &lt;/InputGroup&gt;
/// </code>
/// </example>
public partial class InputGroupAddon : ComponentBase
{
    // 源组件注入了 IJSRuntime 但未在任何渲染/生命周期路径中使用
    // （焦点委托完全由 CSS 与兄弟节点选择器实现）。 照搬保留注入
    // 便于后续源升级时若 addon 引入 JS 行为可直接对齐。
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>
    /// 获取或设置附加内容的对齐位置。
    /// </summary>
    /// <remarks>
    /// 决定附加内容出现在输入控件的哪个方位：
    /// - InlineStart：左侧（RTL 下为右侧）
    /// - InlineEnd：右侧（RTL 下为左侧）
    /// - BlockStart：输入上方
    /// - BlockEnd：输入下方
    /// </remarks>
    [Parameter]
    public InputGroupAlign Align { get; set; } = InputGroupAlign.InlineStart;

    /// <summary>
    /// 获取或设置附加内容容器内部渲染的子内容。
    /// </summary>
    /// <remarks>
    /// 可包含图标、按钮、文本或任意其他内容。
    /// 组件会依据内容类型自动调整内边距。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到附加内容容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到附加内容容器的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取与 <see cref="Align"/> 对应的 data-align 属性值。
    /// </summary>
    private string AlignValue => Align switch
    {
        InputGroupAlign.InlineStart => "inline-start",
        InputGroupAlign.InlineEnd => "inline-end",
        InputGroupAlign.BlockStart => "block-start",
        InputGroupAlign.BlockEnd => "block-end",
        _ => "inline-start"
    };

    /// <summary>
    /// 获取附加内容容器计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 样式随对齐位置变化：
    /// </para>
    /// <list type="bullet">
    /// <item>InlineStart/InlineEnd：横向内边距、flex 对齐</item>
    /// <item>BlockStart/BlockEnd：纵向内边距、占满宽度</item>
    /// </list>
    /// <para>
    /// 子元素为按钮时自动以负外边距与压缩内边距进行适配。
    /// </para>
    /// </remarks>
    private string CssClass
    {
        get
        {
            var baseClasses = Align switch
            {
                InputGroupAlign.InlineStart => "atom:flex atom:items-center atom:gap-2 atom:text-muted-foreground",
                InputGroupAlign.InlineEnd => "atom:flex atom:items-center atom:gap-2 atom:text-muted-foreground",
                InputGroupAlign.BlockStart => "atom:flex atom:items-center atom:justify-start atom:gap-2 atom:text-muted-foreground",
                InputGroupAlign.BlockEnd => "atom:flex atom:items-center atom:justify-start atom:gap-2 atom:text-muted-foreground",
                _ => "atom:flex atom:items-center atom:gap-2 atom:text-muted-foreground"
            };

            var alignmentClasses = Align switch
            {
                InputGroupAlign.InlineStart => "atom:ps-3 atom:pe-0 atom:py-0", // 起始侧留白，与输入控件之间不额外留距（输入控件自带 ps-3）
                InputGroupAlign.InlineEnd => "atom:pe-3 atom:ps-0 atom:py-0", // 结束侧留白，与输入控件之间不额外留距（输入控件自带 pe-3）
                InputGroupAlign.BlockStart => "atom:px-3 atom:pt-2 atom:pb-1 atom:w-full", // 去掉边框以无缝融合
                InputGroupAlign.BlockEnd => "atom:px-3 atom:pb-2 atom:pt-1 atom:w-full", // 去掉边框以无缝融合
                _ => "atom:ps-3 atom:pe-0 atom:py-0"
            };

            // 子元素为按钮时适配——四边形成统一的 6px 间距
            var buttonAdjustments = Align switch
            {
                InputGroupAlign.InlineStart => "atom:has-[>button]:ps-1.5 atom:has-[>button]:!py-1.5", // 四边统一 6px 间距
                InputGroupAlign.InlineEnd => "atom:has-[>button]:pe-1.5 atom:has-[>button]:!py-1.5", // 四边统一 6px 间距
                InputGroupAlign.BlockStart => "atom:has-[>button]:-mt-1 atom:has-[>button]:pt-1",
                InputGroupAlign.BlockEnd => "atom:has-[>button]:-mb-1 atom:has-[>button]:pb-1",
                _ => ""
            };

            return ClassNames.cn(
                baseClasses,
                alignmentClasses,
                buttonAdjustments,
                Class
            );
        }
    }
}
