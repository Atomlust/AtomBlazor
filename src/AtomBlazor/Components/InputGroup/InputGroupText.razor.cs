using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 在 InputGroup 附加内容中展示静态文本的容器。
/// </summary>
/// <remarks>
/// <para>
/// InputGroupText 为与输入框并排展示的文本提供一致样式，
/// 如货币符号、URL 前缀、域名后缀或字符计数器。
/// </para>
/// <para>
/// 特性：
/// - 低调的柔和文本颜色
/// - 图标与文本的正确间距
/// - 较小字号使比例更协调
/// - 灵活内容支持（文本、图标或两者兼有）
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;!-- 货币前缀 --&gt;
/// &lt;InputGroup&gt;
///     &lt;InputGroupAddon Align="InputGroupAlign.InlineStart"&gt;
///         &lt;InputGroupText&gt;$&lt;/InputGroupText&gt;
///     &lt;/InputGroupAddon&gt;
///     &lt;InputGroupInput Type="InputType.Number" Placeholder="0.00" /&gt;
/// &lt;/InputGroup&gt;
///
/// &lt;!-- 字符计数器 --&gt;
/// &lt;InputGroup&gt;
///     &lt;InputGroupTextarea Placeholder="Write a comment..." /&gt;
///     &lt;InputGroupAddon Align="InputGroupAlign.BlockEnd"&gt;
///         &lt;InputGroupText&gt;0 / 280&lt;/InputGroupText&gt;
///     &lt;/InputGroupAddon&gt;
/// &lt;/InputGroup&gt;
///
/// &lt;!-- 带图标的 URL 前缀 --&gt;
/// &lt;InputGroup&gt;
///     &lt;InputGroupAddon Align="InputGroupAlign.InlineStart"&gt;
///         &lt;InputGroupText&gt;
///             &lt;GlobeIcon /&gt;
///             https://
///         &lt;/InputGroupText&gt;
///     &lt;/InputGroupAddon&gt;
///     &lt;InputGroupInput Placeholder="example.com" /&gt;
/// &lt;/InputGroup&gt;
/// </code>
/// </example>
public partial class InputGroupText : ComponentBase
{
    /// <summary>
    /// 获取或设置子内容（文本或图标）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取文本容器计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 提供低调柔和的样式并为图标留出正确间距，
    /// 使用较小字号与输入文本形成呼应。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 基础文本样式
        "atom:text-sm atom:text-muted-foreground",
        "atom:flex atom:items-center atom:gap-0.5", // 更小的间隙，排布更紧凑
        // 图标尺寸适配
        "atom:[&>svg]:size-4",
        // 自定义类
        Class
    );
}
