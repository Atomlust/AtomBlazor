using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 带样式的触发器内容组件，展示由 CollapsibleTrigger 控制显隐的折叠内容。
/// </summary>
/// <remarks>
/// <para>
/// CollapsibleContent 组件是 CollapsibleContent 基元的样式封装，在应用 shadcn/ui
/// 样式的同时保留全部行为与无障碍能力。
/// </para>
/// <para>
/// 本组件必须作为 <see cref="Collapsible"/> 组件的子组件使用。
/// </para>
/// <para>
/// 无障碍特性：
/// <list type="bullet">
/// <item>语义化 region 角色</item>
/// <item>aria-hidden 属性反映内容可见性</item>
/// <item>条件渲染（而非仅用 CSS 隐藏）</item>
/// <item>通过 CSS 过渡实现平滑动画（带样式时）</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 基础折叠内容：
/// <code>
/// &lt;Collapsible&gt;
///     &lt;CollapsibleTrigger&gt;
///         &lt;Button&gt;Show Content&lt;/Button&gt;
///     &lt;/CollapsibleTrigger&gt;
///     &lt;CollapsibleContent&gt;
///         &lt;p&gt;This content can be expanded or collapsed.&lt;/p&gt;
///     &lt;/CollapsibleContent&gt;
/// &lt;/Collapsible&gt;
/// </code>
///
/// 带内边距的内容：
/// <code>
/// &lt;CollapsibleContent Class="p-4 border-t"&gt;
///     &lt;div class="space-y-2"&gt;
///         &lt;p&gt;Paragraph 1&lt;/p&gt;
///         &lt;p&gt;Paragraph 2&lt;/p&gt;
///     &lt;/div&gt;
/// &lt;/CollapsibleContent&gt;
/// </code>
///
/// 带过渡动画的内容：
/// <code>
/// &lt;CollapsibleContent Class="transition-all duration-300 ease-in-out overflow-hidden"&gt;
///     &lt;div class="p-4"&gt;
///         &lt;p&gt;Content with smooth slide animation&lt;/p&gt;
///     &lt;/div&gt;
/// &lt;/CollapsibleContent&gt;
/// </code>
/// </example>
public partial class CollapsibleContent : ComponentBase
{
    /// <summary>
    /// 是否在关闭时也强制挂载内容。
    /// 为 true（默认）时可实现平滑的 CSS 动画；
    /// 为 false 时关闭即卸载内容（无动画，内存占用更低）。
    /// </summary>
    [Parameter]
    public bool ForceMount { get; set; } = true;

    /// <summary>
    /// 获取或设置要应用到内容容器元素的额外 CSS 类。
    /// </summary>
    /// <value>
    /// 包含一个或多个 CSS 类名的字符串，或为 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 使用此参数可修饰内容区域并添加动画。常用 Tailwind 工具类包括：
    /// <list type="bullet">
    /// <item>内边距：<c>p-4</c>、<c>px-6 py-4</c></item>
    /// <item>边框：<c>border-t</c>、<c>border-x</c></item>
    /// <item>背景：<c>bg-muted</c>、<c>bg-card</c></item>
    /// <item>动画：<c>animate-in slide-in-from-top</c></item>
    /// <item>溢出：<c>overflow-hidden</c>（实现平滑高度过渡）</item>
    /// </list>
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置折叠区域展开时渲染的内容。
    /// </summary>
    /// <value>
    /// 包含折叠内容的 <see cref="RenderFragment"/>，或为 <c>null</c>。
    /// </value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取网格容器的 CSS 类（用于动画）。
    /// </summary>
    /// <remarks>
    /// 这些类应用到外层容器以实现平滑的高度过渡；用户自定义类应用到内层包装器，
    /// 避免内边距影响网格折叠效果。
    /// </remarks>
    private string? CssClass => ClassNames.cn(Class);

    private static string GridCssClass =>
        "atom:grid atom:grid-rows-[0fr] atom:transition-[grid-template-rows] atom:duration-200 atom:ease-out atom:data-[state=open]:grid-rows-[1fr]";
}
