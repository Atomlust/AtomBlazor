using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 在 Avatar 组件内展示图片，并自动处理加载错误。
/// </summary>
/// <remarks>
/// <para>
/// AvatarImage 渲染用户的头像图片。若图片加载失败，组件会自动隐藏自身，
/// 让位于同级的 AvatarFallback 显示。
/// </para>
/// <para>
/// 特性：
/// - 自动的错误处理与回退
/// - 通过 alt 文本提供无障碍支持
/// - 优化的图片渲染（纵横比、object-fit）
/// - 与 Avatar 容器无缝集成
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Avatar&gt;
///     &lt;AvatarImage Source="https://example.com/user.jpg" Alt="John Doe" /&gt;
///     &lt;AvatarFallback&gt;JD&lt;/AvatarFallback&gt;
/// &lt;/Avatar&gt;
/// </code>
/// </example>
public partial class AvatarImage : ComponentBase
{
    /// <summary>
    /// 获取或设置要展示的图片 URL。
    /// </summary>
    /// <remarks>
    /// 应为合法的图片来源。若加载失败，组件会隐藏自身并交由
    /// AvatarFallback 显示。
    /// </remarks>
    [Parameter]
    public string? Source { get; set; }

    /// <summary>
    /// 获取或设置图片的替代文本。
    /// </summary>
    /// <remarks>
    /// 对无障碍至关重要。屏幕阅读器用它向视障用户描述图片，
    /// 应说明头像代表谁（如 "John Doe"、"User avatar"）。
    /// </remarks>
    [Parameter]
    public string? Alt { get; set; }

    /// <summary>
    /// 获取或设置应用到图片上的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取图片元素计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 应用样式以保证在圆形头像容器内的正确尺寸、定位与渲染。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        "atom:absolute atom:inset-0 atom:aspect-square atom:h-full atom:w-full atom:object-cover",
        Class
    );

    private bool isLoaded = true;
    private bool hasError;

    /// <summary>
    /// 处理图片加载错误：隐藏图片。
    /// </summary>
    /// <remarks>
    /// 当图片加载失败（404、CORS 错误等）时，将 hasError 置为 true，
    /// 使图片不再渲染，让 AvatarFallback 得以显示。
    /// </remarks>
    // 事件处理器触发的重渲染由框架自动完成，无需显式 StateHasChanged（BL0012）。
    private void HandleError() => hasError = true;

    /// <summary>
    /// 当 Source 参数变化时重置错误状态。
    /// </summary>
    protected override void OnParametersSet()
    {
        // 来源变化时重置错误状态
        hasError = false;
        isLoaded = !string.IsNullOrWhiteSpace(Source);
    }
}
