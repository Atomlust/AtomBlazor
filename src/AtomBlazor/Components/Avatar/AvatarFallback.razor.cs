using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 当 AvatarImage 加载失败或不可用时展示回退内容。
/// </summary>
/// <remarks>
/// <para>
/// AvatarFallback 为头像图片提供优雅降级。当图片无法加载或未提供时，
/// 该组件展示替代内容，如姓名缩写、图标或自定义标记。
/// </para>
/// <para>
/// 特性：
/// - 内容自动居中
/// - 来自设计体系的一致性背景色
/// - 支持文本、图标或自定义内容
/// - 无障碍且语义化
/// - 自动匹配父级 Avatar 尺寸
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Avatar&gt;
///     &lt;AvatarImage Source="@userImageUrl" Alt="@userName" /&gt;
///     &lt;AvatarFallback&gt;@GetInitials(userName)&lt;/AvatarFallback&gt;
/// &lt;/Avatar&gt;
///
/// &lt;Avatar&gt;
///     &lt;AvatarFallback&gt;
///         &lt;LucideIcon Name="user" Size="16" /&gt;
///     &lt;/AvatarFallback&gt;
/// &lt;/Avatar&gt;
/// </code>
/// </example>
public partial class AvatarFallback : ComponentBase
{
    /// <summary>
    /// 获取或设置作为回退内容渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含：
    /// - 姓名缩写（如 John Doe 对应 "JD"）
    /// - 图标组件（如 Name 为 "user" 的 LucideIcon）
    /// - 自定义标记或组件
    /// 内容会在头像内自动居中。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到回退容器上的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后，便于样式覆盖，
    /// 例如自定义背景色。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取回退容器计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 应用的样式包括：
    /// - 撑满 Avatar 容器的宽高
    /// - 居中的内容（flex 布局）
    /// - 来自设计体系的背景色与文字色
    /// - 姓名缩写的字重
    /// </remarks>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:h-full atom:w-full atom:items-center atom:justify-center atom:rounded-full",
        "atom:bg-muted atom:text-muted-foreground atom:font-medium",
        Class
    );
}
