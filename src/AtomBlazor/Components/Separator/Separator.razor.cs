using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 用于在视觉或语义上分隔内容的分隔符组件。
/// </summary>
/// <remarks>
/// <para>
/// Separator 组件提供灵活的分割线，可用于在各部分内容之间创建视觉区隔。
/// 它遵循 shadcn/ui 设计体系，同时支持水平与垂直两种方向。
/// </para>
/// <para>
/// 特性：
/// - 水平与垂直方向
/// - 语义模式与装饰模式（影响 ARIA 属性）
/// - 携带正确的 ARIA 角色，满足无障碍要求
/// - 支持 RTL（从右到左）书写方向
/// - 通过 CSS 变量兼容暗色模式
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Separator Orientation="SeparatorOrientation.Horizontal" /&gt;
///
/// &lt;Separator Orientation="SeparatorOrientation.Vertical" Decorative="false" /&gt;
/// </code>
/// </example>
public partial class Separator : ComponentBase
{
    /// <summary>
    /// 获取或设置分隔符的方向。
    /// </summary>
    /// <remarks>
    /// 决定分隔符以水平还是垂直方式显示，默认为 <see cref="SeparatorOrientation.Horizontal"/>。
    /// </remarks>
    [Parameter]
    public SeparatorOrientation Orientation { get; set; } = SeparatorOrientation.Horizontal;

    /// <summary>
    /// 获取或设置分隔符是否为纯装饰性元素。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 为 true（默认）时，分隔符按装饰性处理（role="none"），对辅助技术不可见。
    /// </para>
    /// <para>
    /// 为 false 时，分隔符具有语义（role="separator"），会被屏幕阅读器播报，
    /// 并通过 aria-orientation 指明方向。
    /// </para>
    /// <para>
    /// 当分隔符承载有意义的内容层级信息时，应设置为 false。
    /// </para>
    /// </remarks>
    [Parameter]
    public bool Decorative { get; set; } = true;

    /// <summary>
    /// 获取或设置应用到分隔符上的额外 CSS 类。
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

    /// <summary>可见的线条样式。</summary>
    [Parameter] public SeparatorLineStyle LineStyle { get; set; }

    private string CssClass => ClassNames.cn(
        "atom:shrink-0 atom:bg-border",
        Orientation == SeparatorOrientation.Horizontal
            ? "atom:h-[1px] atom:w-full"
            : "atom:h-full atom:w-[1px]",
        LineStyle != SeparatorLineStyle.Solid ? "atom:bg-transparent atom:border-border" : null,
        LineStyle == SeparatorLineStyle.Dashed ? "atom:border-dashed" : LineStyle == SeparatorLineStyle.Dotted ? "atom:border-dotted" : null,
        LineStyle != SeparatorLineStyle.Solid ? Orientation == SeparatorOrientation.Horizontal ? "atom:h-0 atom:border-t" : "atom:w-0 atom:border-s" : null,
        Class
    );

    private AtomBlazor.Primitives.SeparatorOrientation MappedOrientation =>
        Orientation == SeparatorOrientation.Vertical
            ? AtomBlazor.Primitives.SeparatorOrientation.Vertical
            : AtomBlazor.Primitives.SeparatorOrientation.Horizontal;
}
