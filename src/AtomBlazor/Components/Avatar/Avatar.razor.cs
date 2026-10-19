using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 展示用户图片并支持回退内容的头像组件。
/// </summary>
/// <remarks>
/// <para>
/// Avatar 组件遵循 shadcn/ui 设计体系，为用户图片提供圆形容器。
/// 当图片加载失败或不可用时，自动回退到姓名缩写或图标。
/// </para>
/// <para>
/// 特性：
/// - 多种尺寸变体（Small、Default、Large、ExtraLarge）
/// - 自动的图片失败回退处理
/// - 带正确 ARIA 标签的无障碍支持
/// - 支持姓名缩写、图片与自定义内容
/// - 通过 CSS 变量兼容暗色模式
/// - RTL（从右到左）支持
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Avatar&gt;
///     &lt;AvatarImage Source="https://example.com/avatar.jpg" Alt="User Name" /&gt;
///     &lt;AvatarFallback&gt;UN&lt;/AvatarFallback&gt;
/// &lt;/Avatar&gt;
///
/// &lt;Avatar Size="AvatarSize.Large"&gt;
///     &lt;AvatarFallback&gt;JD&lt;/AvatarFallback&gt;
/// &lt;/Avatar&gt;
/// </code>
/// </example>
public partial class Avatar : ComponentBase
{
    /// <summary>
    /// 获取或设置在头像内部渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置头像的尺寸变体。
    /// </summary>
    [Parameter]
    public AvatarSize Size { get; set; } = AvatarSize.Default;

    /// <summary>
    /// 获取或设置应用到头像容器上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置是否在头像上显示圆点指示器。
    /// </summary>
    [Parameter]
    public bool ShowDot { get; set; }

    /// <summary>
    /// 获取或设置圆点指示器的自定义 CSS 类（例如用 "atom:bg-green-500" 表示在线状态）。
    /// 未指定时默认使用 "atom:bg-primary"。
    /// </summary>
    [Parameter]
    public string? DotClass { get; set; }

    /// <summary>
    /// 级联的头像组上下文；处于组内时头像会加上分隔边框。
    /// </summary>
    [CascadingParameter(Name = "AvatarGroup")]
    internal AvatarGroup? AvatarGroupContext { get; set; }

    private string CssClass => ClassNames.cn(
        "atom:relative atom:flex atom:shrink-0 atom:overflow-hidden atom:rounded-full",
        Size switch
        {
            AvatarSize.Small => "atom:h-8 atom:w-8 atom:text-xs",
            AvatarSize.Default => "atom:h-10 atom:w-10 atom:text-sm",
            AvatarSize.Large => "atom:h-12 atom:w-12 atom:text-base",
            AvatarSize.ExtraLarge => "atom:h-16 atom:w-16 atom:text-lg",
            _ => "atom:h-10 atom:w-10 atom:text-sm"
        },
        AvatarGroupContext != null ? "atom:border-2 atom:border-background" : null,
        Class
    );

    private string DotSizeClass => Size switch
    {
        AvatarSize.Small => "atom:h-2 atom:w-2",
        AvatarSize.Default => "atom:h-2.5 atom:w-2.5",
        AvatarSize.Large => "atom:h-3 atom:w-3",
        AvatarSize.ExtraLarge => "atom:h-3.5 atom:w-3.5",
        _ => "atom:h-2.5 atom:w-2.5"
    };

    private string DotCssClass => ClassNames.cn(
        "atom:absolute atom:bottom-0 atom:end-0 atom:block atom:rounded-full atom:ring-2 atom:ring-background",
        DotSizeClass,
        DotClass ?? "atom:bg-primary"
    );
}
