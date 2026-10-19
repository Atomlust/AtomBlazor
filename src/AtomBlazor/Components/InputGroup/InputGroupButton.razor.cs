using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为在 InputGroup 内使用而优化的按钮组件。
/// </summary>
/// <remarks>
/// <para>
/// InputGroupButton 是与 InputGroup 集成的专用按钮。
/// 它去掉了阴影并调整尺寸，与输入控件无缝并存。
/// </para>
/// <para>
/// 特性：
/// - 默认更小尺寸（Small），比例更协调
/// - 无阴影，集成更干净
/// - 与 Button 组件参数完全兼容
/// - 在组上下文中自动调整尺寸
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;InputGroup&gt;
///     &lt;InputGroupInput Placeholder="Search..." /&gt;
///     &lt;InputGroupAddon Align="InputGroupAlign.InlineEnd"&gt;
///         &lt;InputGroupButton&gt;Search&lt;/InputGroupButton&gt;
///     &lt;/InputGroupAddon&gt;
/// &lt;/InputGroup&gt;
/// </code>
/// </example>
public partial class InputGroupButton : ComponentBase
{
    /// <summary>
    /// 获取或设置按钮类型（submit、button、reset）。
    /// </summary>
    [Parameter]
    public ButtonType Type { get; set; } = ButtonType.Button;

    /// <summary>
    /// 获取或设置按钮视觉变体。
    /// </summary>
    [Parameter]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Default;

    /// <summary>
    /// 获取或设置按钮尺寸。
    /// </summary>
    /// <remarks>
    /// 默认为 Small，在输入组中比例更协调。
    /// </remarks>
    [Parameter]
    public ButtonSize Size { get; set; } = ButtonSize.Small;

    /// <summary>
    /// 获取或设置按钮是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置无障碍 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置点击事件处理器。
    /// </summary>
    [Parameter]
    public EventCallback OnClick { get; set; }

    /// <summary>
    /// 获取或设置按钮内显示的图标。
    /// </summary>
    [Parameter]
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// 获取或设置图标相对按钮文本的位置。
    /// </summary>
    [Parameter]
    public IconPosition IconPosition { get; set; } = IconPosition.Start;

    /// <summary>
    /// 获取或设置子内容（按钮文本）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到按钮元素的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取按钮计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 为与 InputGroup 集成提供最少量默认样式：
    /// 去掉边框与阴影（由 InputGroup 提供），并保证尺寸正确。
    /// </para>
    /// <para>
    /// 自定义样式（内边距、圆角等）请使用 Class 参数，
    /// TailwindMerge 会把自定义类与默认类智能合并。
    /// </para>
    /// <para>
    /// 常见定制模式：
    /// </para>
    /// <code>
    /// &lt;!-- 紧凑 ghost 按钮，悬停圆角 --&gt;
    /// &lt;InputGroupButton Variant="ButtonVariant.Ghost" Class="!px-1 !py-1 !rounded-md"&gt;
    ///     Search In...
    /// &lt;/InputGroupButton&gt;
    ///
    /// &lt;!-- 标准操作按钮 --&gt;
    /// &lt;InputGroupButton Class="!px-2.5 !py-0.5 !rounded-sm"&gt;
    ///     Search
    /// &lt;/InputGroupButton&gt;
    ///
    /// &lt;!-- 纯图标按钮 --&gt;
    /// &lt;InputGroupButton Variant="ButtonVariant.Ghost" Class="!px-1.5 !py-1.5 !rounded-md"&gt;
    ///     &lt;MoreIcon /&gt;
    /// &lt;/InputGroupButton&gt;
    /// </code>
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 与 InputGroup 集成的必要样式
        "atom:!border-0",         // 无边框（边框由 InputGroup 提供）
        "atom:!shadow-none",      // 无阴影（阴影由 InputGroup 提供）
        "atom:!h-auto",           // 高度自适应容器
        "atom:!text-sm",          // 适配输入上下文的较小字号
        "atom:transition-colors", // 平滑颜色过渡

        // 用户自定义（内边距、圆角等）
        Class
    );
}
