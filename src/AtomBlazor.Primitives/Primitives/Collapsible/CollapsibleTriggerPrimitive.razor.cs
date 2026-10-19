using AtomBlazor.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor.Primitives;

/// <summary>
/// 无外观触发按钮组件：切换 Collapsible 的展开/收起状态。
/// </summary>
/// <remarks>
/// <para>
/// CollapsibleTriggerPrimitive 是一枚无外观基元，负责折叠切换的全部交互行为、
/// 键盘导航与可访问性特性；它不提供任何
/// 默认样式。
/// </para>
/// <para>
/// 本组件必须作为 <see cref="CollapsiblePrimitive"/> 组件的子级使用。
/// 它会自动通过 Blazor 的 CascadingParameter 接收折叠状态。
/// </para>
/// <para>
/// 内置的可访问性特性：
/// <list type="bullet">
/// <item>带恰当角色的语义化 button 元素</item>
/// <item>aria-expanded 属性反映当前状态</item>
/// <item>键盘支持（空格/回车）</item>
/// <item>禁用状态配套相应 ARIA 属性</item>
/// <item>data-state 属性供 CSS 挂样式钩子</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 基础无外观触发器：
/// <code>
/// &lt;Collapsible&gt;
///     &lt;CollapsibleTriggerPrimitive&gt;
///         Toggle Content
///     &lt;/CollapsibleTriggerPrimitive&gt;
///     &lt;CollapsibleContentPrimitive&gt;...&lt;/CollapsibleContentPrimitive&gt;
/// &lt;/Collapsible&gt;
/// </code>
///
/// 自定义类名的带样式触发器：
/// <code>
/// &lt;CollapsibleTriggerPrimitive class="flex items-center gap-2 px-4 py-2 hover:bg-accent"&gt;
///     &lt;svg&gt;...&lt;/svg&gt;
///     &lt;span&gt;Show More&lt;/span&gt;
/// &lt;/CollapsibleTriggerPrimitive&gt;
/// </code>
/// </example>
public partial class CollapsibleTriggerPrimitive : ComponentBase
{
    private static readonly AsChildTriggerDescription diagnostic = new("CollapsibleTriggerPrimitive", "切换此折叠区域的展开状态", "<button>");

    // 上一次渲染时级联出去的上下文，保留它以便首次渲染后
    // 检查是否有子组件消费。
    private TriggerContext? lastTriggerContext;

    protected override void OnInitialized()
    {
        if (Context == null)
        {
            throw new InvalidOperationException(
                "CollapsibleTriggerPrimitive 必须在 Collapsible 组件内使用。");
        }
    }

    /// <summary>
    /// 获取从父级 Collapsible 组件级联传来的折叠上下文。
    /// </summary>
    /// <value>
    /// 由父级提供的 <see cref="CollapsibleContext"/> 实例；若未嵌套在
    /// <see cref="CollapsiblePrimitive"/> 内则为 <c>null</c>。
    /// </value>
    [CascadingParameter]
    public CollapsibleContext? Context { get; set; }

    /// <summary>
    /// 获取或设置在触发器内渲染的内容。
    /// </summary>
    /// <value>
    /// 包含触发器内容的 <see cref="RenderFragment"/>，或为 <c>null</c>。
    /// </value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 为 true 时，触发器不渲染自己的按钮元素，
    /// 而是通过 TriggerContext 把触发行为传递给子组件。
    /// 子组件必须消费 TriggerContext 并挂载点击行为。
    /// </summary>
    [Parameter]
    public bool AsChild { get; set; } = false;

    /// <summary>
    /// 获取或设置要应用到按钮元素的额外属性。
    /// </summary>
    /// <value>
    /// 包含 class、style 等额外 HTML 属性的字典。
    /// </value>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// AsChild 为 true 时传递给子组件的上下文。
    /// </summary>
    private TriggerContext TriggerContext => lastTriggerContext = new()
    {
        IsOpen = Context?.Open ?? false,
        Toggle = () => Context?.Toggle?.Invoke()
    };

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            AsChildDiagnostics.WarnIfUnconsumed<CollapsibleTriggerPrimitive>(Services, diagnostic, AsChild, lastTriggerContext, AdditionalAttributes);
        }
    }

    /// <summary>
    /// 处理触发器上的点击事件以切换折叠状态。
    /// </summary>
    /// <param name="args">鼠标事件参数。</param>
    /// <returns>表示异步操作的 <see cref="Task"/>。</returns>
    private async Task HandleClick(MouseEventArgs args)
    {
        if (Context?.Toggle != null && !(Context.Disabled))
        {
            await Context.Toggle.Invoke();
        }
    }

    // 注意：没有 keydown 处理器。渲染出的是原生 <button>，浏览器在空格/回车按下时会
    // 派发 click——由 HandleClick 处理。此前额外追加 keydown 切换导致每次空格/回车
    // 切换两次（打开 + 关闭）。
}
