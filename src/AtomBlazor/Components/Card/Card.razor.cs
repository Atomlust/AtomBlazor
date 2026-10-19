using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 以视觉上独立的容器展示结构化内容的卡片组件。
/// </summary>
/// <remarks>
/// <para>
/// Card 组件是卡片内容的根容器，提供带边框、圆角与阴影的容器。
/// 它遵循 shadcn/ui 设计体系，并与其他卡片子组件无缝集成。
/// </para>
/// <para>
/// 特性：
/// - 圆角与柔和阴影
/// - 响应式的边框与背景样式
/// - 可通过额外 CSS 类定制
/// - 通过 CSS 变量兼容暗色模式
/// - 语义化 HTML 结构，利于无障碍
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Card&gt;
///     &lt;CardHeader&gt;
///         &lt;CardTitle&gt;Card Title&lt;/CardTitle&gt;
///         &lt;CardDescription&gt;Card description&lt;/CardDescription&gt;
///     &lt;/CardHeader&gt;
///     &lt;CardContent&gt;
///         Card content goes here
///     &lt;/CardContent&gt;
///     &lt;CardFooter&gt;
///         Footer content
///     &lt;/CardFooter&gt;
/// &lt;/Card&gt;
/// </code>
/// </example>
public partial class Card : ComponentBase
{
    /// <summary>
    /// 获取或设置在卡片内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含 CardHeader、CardContent 与 CardFooter 组件，也可包含任意 Blazor 标记。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到卡片上的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取卡片元素计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 将基础卡片样式与自定义类合并，使用 cn() 工具做智能类合并与 Tailwind 冲突消解。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 基础卡片样式（来自 shadcn/ui）
        "atom:rounded-lg atom:border atom:bg-card atom:text-card-foreground atom:shadow-(--atom-card-shadow)",
        // 自定义类（若提供）
        Class
    );
}
