using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为所有子 Tooltip 组件配置提示行为的提供者组件。
/// </summary>
/// <remarks>
/// <para>
/// TooltipProvider 遵循 shadcn/ui 设计体系，包裹应用的一段区域以提供共享的提示配置，
/// 并把 <see cref="TooltipProviderContext"/> 级联给所有后代 <see cref="Tooltip"/> 组件。
/// </para>
/// <para>
/// <strong>何时使用：</strong>
/// <list type="bullet">
/// <item>包裹整个应用根，提供默认提示行为</item>
/// <item>包裹特定区域，为该区域定制提示时序</item>
/// <item>嵌套提供者，在子树中覆盖配置</item>
/// </list>
/// </para>
/// <para>
/// <strong>使用 TooltipProvider 的好处：</strong>
/// <list type="bullet">
/// <item>全应用一致的提示延时</item>
/// <item>"提示发现模式"——首次交互之后的后续提示更快</item>
/// <item>集中配置——不必逐个修改提示即可改变行为</item>
/// <item>分区定制——不同 UI 区域可有不同时序</item>
/// </list>
/// </para>
/// <para>
/// <strong>说明：</strong>虽然推荐使用 TooltipProvider 以获得一致行为，
/// 但单个 <see cref="Tooltip"/> 组件在没有提供者时也能以默认延时值工作。
/// </para>
/// </remarks>
/// <example>
/// 包裹整个应用的基础用法：
/// <code>
/// &lt;TooltipProvider&gt;
///     &lt;Router AppAssembly="typeof(App).Assembly"&gt;
///         @* 带提示的应用内容 *@
///     &lt;/Router&gt;
/// &lt;/TooltipProvider&gt;
/// </code>
///
/// 为工具栏定制延时：
/// <code>
/// &lt;TooltipProvider DelayDuration="200" SkipDelayDuration="500"&gt;
///     &lt;Toolbar&gt;
///         &lt;Tooltip&gt;
///             &lt;TooltipTrigger&gt;&lt;Button&gt;保存&lt;/Button&gt;&lt;/TooltipTrigger&gt;
///             &lt;TooltipContent&gt;保存文档&lt;/TooltipContent&gt;
///         &lt;/Tooltip&gt;
///         @* 更多带提示的工具栏项 *@
///     &lt;/Toolbar&gt;
/// &lt;/TooltipProvider&gt;
/// </code>
///
/// 嵌套提供者以在不同分区使用不同时序：
/// <code>
/// &lt;TooltipProvider DelayDuration="700"&gt; @* 应用默认 *@
///     &lt;MainContent /&gt;
///
///     &lt;TooltipProvider DelayDuration="200"&gt; @* 工具栏用快一点 *@
///         &lt;Toolbar /&gt;
///     &lt;/TooltipProvider&gt;
/// &lt;/TooltipProvider&gt;
/// </code>
/// </example>
public partial class TooltipProvider : ComponentBase
{
    private TooltipProviderContext providerContext = new();

    /// <summary>
    /// 获取或设置显示提示前等待的毫秒数。
    /// </summary>
    /// <value>
    /// 延时毫秒数，默认 700。
    /// </value>
    /// <remarks>
    /// <para>
    /// 该延时作用于所有子 <see cref="Tooltip"/> 组件。用户悬停或聚焦触发器后，
    /// 提示会等待这段时间再出现。
    /// </para>
    /// <para>
    /// 推荐取值：
    /// <list type="bullet">
    /// <item><strong>0-200ms：</strong>极快（工具栏、高频访问的 UI）</item>
    /// <item><strong>300-500ms：</strong>快（交互元素）</item>
    /// <item><strong>600-800ms：</strong>标准（均衡，默认值）</item>
    /// <item><strong>900-1200ms：</strong>慢（详细信息）</item>
    /// </list>
    /// </para>
    /// <para>
    /// 单个提示无法覆盖此值——它们从最近的 TooltipProvider 祖先继承。
    /// </para>
    /// </remarks>
    [Parameter]
    public int DelayDuration { get; set; } = 700;

    /// <summary>
    /// 获取或设置在多长时间窗口内后续提示可跳过延时（毫秒）。
    /// </summary>
    /// <value>
    /// 跳过延时的时间窗口毫秒数，默认 300。
    /// </value>
    /// <remarks>
    /// <para>
    /// 用户看过一个提示之后，在该时间窗口内的后续提示会立即出现而不再等待，
    /// 从而形成流畅的"提示发现模式"体验。
    /// </para>
    /// <para>
    /// 推荐取值：
    /// <list type="bullet">
    /// <item><strong>0ms：</strong>关闭跳过行为</item>
    /// <item><strong>200-400ms：</strong>短窗口（紧邻成组的元素）</item>
    /// <item><strong>500-1000ms：</strong>标准窗口（从容探索）</item>
    /// <item><strong>1500-3000ms：</strong>长窗口（非常宽容）</item>
    /// </list>
    /// </para>
    /// <para>
    /// 设为 0 即完全关闭跳过延时特性：所有提示都会等待完整的 <see cref="DelayDuration"/>。
    /// </para>
    /// </remarks>
    [Parameter]
    public int SkipDelayDuration { get; set; } = 300;

    /// <summary>
    /// 获取或设置在提供者内渲染的子内容。
    /// </summary>
    /// <value>
    /// 包含子组件的 <see cref="RenderFragment"/>，或为 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 所有子 <see cref="Tooltip"/> 组件都会经 Blazor 的 CascadingValue 机制
    /// 从本提供者获得提示配置。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 在首次渲染前初始化提供者上下文。
    /// </summary>
    /// <remarks>
    /// 以配置的延时值创建 <see cref="TooltipProviderContext"/>，
    /// 该上下文随后被级联给所有后代组件。
    /// </remarks>
    protected override void OnInitialized() =>
        providerContext = new TooltipProviderContext(DelayDuration, SkipDelayDuration);

    /// <summary>
    /// 参数变化时更新提供者上下文。
    /// </summary>
    /// <remarks>
    /// 确保 <see cref="DelayDuration"/> 或 <see cref="SkipDelayDuration"/> 的改动
    /// 反映到已级联的上下文中，从而在运行时动态调整提示行为。
    /// </remarks>
    protected override void OnParametersSet()
    {
        providerContext.DelayDuration = DelayDuration;
        providerContext.SkipDelayDuration = SkipDelayDuration;
    }
}
