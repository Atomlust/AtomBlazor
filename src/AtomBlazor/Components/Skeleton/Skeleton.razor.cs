using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 在数据加载完成前展示内容预览占位的骨架组件。
/// </summary>
/// <remarks>
/// <para>
/// Skeleton 组件遵循 shadcn/ui 设计系统，为数据加载状态提供视觉反馈，
/// 通过 CSS 动画产生脉冲效果表示内容正在加载。
/// </para>
/// <para>
/// 常见使用场景：
/// <list type="bullet">
/// <item>列表、卡片与表格的加载状态</item>
/// <item>头像与用户信息占位</item>
/// <item>表单字段的加载指示</item>
/// <item>图片与媒体内容的占位</item>
/// </list>
/// </para>
/// <para>
/// 组件支持两种形状变体（矩形与圆形），样式使用 Tailwind CSS，
/// 加载动画使用 animate-pulse 工具类。
/// </para>
/// </remarks>
/// <example>
/// 基础矩形骨架：
/// <code>
/// &lt;Skeleton Class="h-12 w-12" /&gt;
/// </code>
///
/// 圆形头像骨架：
/// <code>
/// &lt;Skeleton Shape="SkeletonShape.Circular" Class="h-12 w-12" /&gt;
/// </code>
///
/// 卡片用的多个骨架：
/// <code>
/// &lt;div class="space-y-2"&gt;
///     &lt;Skeleton Class="h-4 w-[250px]" /&gt;
///     &lt;Skeleton Class="h-4 w-[200px]" /&gt;
/// &lt;/div&gt;
/// </code>
/// </example>
public partial class Skeleton : ComponentBase
{
    /// <summary>
    /// 获取或设置骨架的形状变体。
    /// </summary>
    /// <value>
    /// <see cref="SkeletonShape"/> 值。默认为 <see cref="SkeletonShape.Rectangular"/>。
    /// </value>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="SkeletonShape.Rectangular"/>：带圆角的默认矩形形状</item>
    /// <item><see cref="SkeletonShape.Circular"/>：圆形，适合头像占位</item>
    /// </list>
    /// </remarks>
    [Parameter]
    public SkeletonShape Shape { get; set; } = SkeletonShape.Rectangular;

    /// <summary>
    /// 获取或设置应用到骨架元素上的额外 CSS 类。
    /// </summary>
    /// <value>
    /// 一个或多个 CSS 类名组成的字符串，或 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 用此参数自定义骨架的尺寸与间距。
    /// 常用 Tailwind 工具类：
    /// <list type="bullet">
    /// <item>高度：<c>h-4</c>、<c>h-12</c>、<c>h-[200px]</c></item>
    /// <item>宽度：<c>w-full</c>、<c>w-[250px]</c>、<c>w-1/2</c></item>
    /// <item>外边距：<c>mb-2</c>、<c>mt-4</c></item>
    /// </list>
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置骨架的显式宽度（如 "200px"、"50%"）。
    /// 以内联样式应用。动态尺寸请优先使用它，而不是 Tailwind 任意值。
    /// </summary>
    [Parameter]
    public string? Width { get; set; }

    /// <summary>
    /// 获取或设置骨架的显式高度（如 "20px"、"2rem"）。
    /// 以内联样式应用。动态尺寸请优先使用它，而不是 Tailwind 任意值。
    /// </summary>
    [Parameter]
    public string? Height { get; set; }

    /// <summary>
    /// 获取显式尺寸的内联样式字符串。
    /// </summary>
    private string? InlineStyle
    {
        get
        {
            if (Width is null && Height is null)
            {
                return null;
            }

            var parts = new List<string>();
            if (Width is not null)
            {
                parts.Add($"width:{Width}");
            }
            if (Height is not null)
            {
                parts.Add($"height:{Height}");
            }
            return string.Join(";", parts);
        }
    }

    private string? MergedInlineStyle => InlineStyleMerge.Merge(InlineStyle, AdditionalAttributes);

    /// <summary>
    /// 获取骨架元素计算后的 CSS 类字符串。
    /// </summary>
    /// <value>
    /// 要应用到骨架 div 元素上的全部 CSS 类组成的字符串。
    /// </value>
    /// <remarks>
    /// 类字符串包含：
    /// <list type="bullet">
    /// <item>基础动画：<c>animate-pulse</c> 产生加载效果</item>
    /// <item>背景：<c>bg-muted</c> 使用主题的中性色</item>
    /// <item>形状专属类：<c>rounded-md</c> 或 <c>rounded-full</c></item>
    /// <item>自定义类：经由 <see cref="Class"/> 参数传入的类</item>
    /// </list>
    /// </remarks>
    private string CssClass => ClassNames.cn(
        "atom:animate-pulse atom:bg-muted",
        Shape switch
        {
            SkeletonShape.Circular => "atom:rounded-full",
            SkeletonShape.Rectangular => "atom:rounded-md",
            _ => "atom:rounded-md"
        },
        Class
    );
}
