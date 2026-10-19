using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 无外观基元组件：为可展开/可折叠内容区提供折叠行为。
/// </summary>
/// <remarks>
/// <para>
/// Collapsible 组件是一枚无外观（不带样式）组件，负责折叠/展开这一交互模式的
/// 全部行为、状态管理与可访问性特性；它不提供任何默认样式，
/// 因而外观完全交由使用方掌控。
/// </para>
/// <para>
/// 这是一个由以下成员组成的复合组件模式：
/// <list type="bullet">
/// <item><see cref="CollapsiblePrimitive"/>——管理状态并向下提供上下文的容器</item>
/// <item><see cref="CollapsibleTrigger"/>——切换展开/收起状态的按钮</item>
/// <item><see cref="CollapsibleContent"/>——随之展开/收起的内容区</item>
/// </list>
/// </para>
/// <para>
/// 该组件支持受控与非受控两种模式：
/// <list type="bullet">
/// <item>受控：父级通过 Open 参数与 OpenChanged 回调管理状态</item>
/// <item>非受控：组件自行维护内部状态</item>
/// </list>
/// </para>
/// <para>
/// 内置的可访问性特性：
/// <list type="bullet">
/// <item>ARIA 属性（aria-expanded、aria-controls）</item>
/// <item>键盘支持（空格、回车）</item>
/// <item>焦点管理</item>
/// <item>语义化 HTML 结构</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 基础折叠面板（非受控）：
/// <code>
/// &lt;Collapsible&gt;
///     &lt;CollapsibleTrigger&gt;
///         Toggle
///     &lt;/CollapsibleTrigger&gt;
///     &lt;CollapsibleContent&gt;
///         This content can be expanded or collapsed.
///     &lt;/CollapsibleContent&gt;
/// &lt;/Collapsible&gt;
/// </code>
///
/// 受控折叠面板：
/// <code>
/// &lt;Collapsible @bind-Open="isOpen"&gt;
///     &lt;CollapsibleTrigger&gt;
///         @(isOpen ? "Collapse" : "Expand")
///     &lt;/CollapsibleTrigger&gt;
///     &lt;CollapsibleContent&gt;
///         Controlled content visibility.
///     &lt;/CollapsibleContent&gt;
/// &lt;/Collapsible&gt;
///
/// @@code {
///     private bool isOpen = false;
/// }
/// </code>
/// </example>
public partial class CollapsiblePrimitive : ComponentBase
{
    private CollapsibleContext context = new();
    private bool _parametersChanged;
    private bool _lastOpen;
    private bool _lastDisabled;

    /// <summary>
    /// 获取或设置一个值，指示折叠区域当前是否处于展开状态。
    /// </summary>
    /// <value>
    /// 如果折叠区域已展开（内容可见）则为 <c>true</c>；否则为 <c>false</c>。
    /// 默认值为 <c>false</c>。
    /// </value>
    /// <remarks>
    /// 支持通过 <c>@bind-Open</c> 语法进行双向绑定。
    /// </remarks>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>
    /// 获取或设置展开状态变化时调用的回调。
    /// </summary>
    /// <value>
    /// 接收新展开状态的 <see cref="EventCallback{Boolean}"/>，或为 <c>null</c>（未提供回调时）。
    /// </value>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>
    /// 获取或设置一个值，指示折叠区域是否被禁用。
    /// </summary>
    /// <value>
    /// 如果折叠区域被禁用且无法切换则为 <c>true</c>；否则为 <c>false</c>。
    /// 默认值为 <c>false</c>。
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置在折叠容器内渲染的子内容。
    /// </summary>
    /// <value>
    /// 包含子组件的 <see cref="RenderFragment"/>，或为 <c>null</c>。
    /// </value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到容器元素的额外属性。
    /// </summary>
    /// <value>
    /// 包含额外 HTML 属性的字典。
    /// </value>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 在首次渲染时初始化组件。
    /// </summary>
    protected override void OnInitialized()
    {
        context.Open = Open;
        context.Disabled = Disabled;
        context.Toggle = ToggleAsync;
        _lastOpen = Open;
        _lastDisabled = Disabled;
    }

    /// <summary>
    /// 在参数变化时更新上下文。
    /// </summary>
    protected override void OnParametersSet()
    {
        _parametersChanged = true;
        context.Open = Open;
        context.Disabled = Disabled;
    }

    /// <summary>
    /// 当 Open 与 Disabled 均未变化时跳过渲染，避免相邻折叠区域切换时
    /// 引发不必要的子树差异比对。
    /// </summary>
    protected override bool ShouldRender()
    {
        if (_parametersChanged)
        {
            _parametersChanged = false;
            _lastOpen = Open;
            _lastDisabled = Disabled;
            return true;
        }

        if (Open != _lastOpen || Disabled != _lastDisabled)
        {
            _lastOpen = Open;
            _lastDisabled = Disabled;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 切换折叠区域的展开/收起状态。
    /// </summary>
    /// <returns>表示异步操作的 <see cref="Task"/>。</returns>
    private async Task ToggleAsync()
    {
        if (Disabled)
        {
            return;
        }

        Open = !Open;
        context.Open = Open;

        if (OpenChanged.HasDelegate)
        {
            await OpenChanged.InvokeAsync(Open);
        }

        StateHasChanged();
    }
}
