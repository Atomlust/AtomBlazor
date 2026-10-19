using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 带样式的容器组件，提供可折叠功能与可展开/收起的内容区域。
/// </summary>
/// <remarks>
/// <para>
/// Collapsible 组件是 Collapsible 基元的样式封装，遵循 shadcn/ui 设计体系，
/// 实现披露（disclosure）模式并带 ARIA 属性。它在默认应用 shadcn 样式的同时，
/// 保留基元的全部行为与无障碍能力。
/// </para>
/// <para>
/// 本组件为复合组件模式，由以下部分组成：
/// <list type="bullet">
/// <item><see cref="Collapsible"/> - 管理状态的样式化容器</item>
/// <item><see cref="CollapsibleTrigger"/> - 切换展开/收起状态的样式化按钮</item>
/// <item><see cref="CollapsibleContent"/> - 展开/收起的内容区域</item>
/// </list>
/// </para>
/// <para>
/// 常见用例包括：
/// <list type="bullet">
/// <item>手风琴式 FAQ 分区</item>
/// <item>可展开的导航菜单</item>
/// <item>可折叠的表单分区</item>
/// <item>带子项的侧栏菜单项</item>
/// </list>
/// </para>
/// <para>
/// 组件支持受控与非受控两种模式：
/// <list type="bullet">
/// <item>受控：父级通过 Open 参数与 OpenChanged 回调管理状态</item>
/// <item>非受控：组件自行管理内部状态</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 基础折叠（非受控）：
/// <code>
/// &lt;Collapsible&gt;
///     &lt;CollapsibleTrigger&gt;
///         &lt;Button&gt;Toggle&lt;/Button&gt;
///     &lt;/CollapsibleTrigger&gt;
///     &lt;CollapsibleContent&gt;
///         &lt;p&gt;This content can be expanded or collapsed.&lt;/p&gt;
///     &lt;/CollapsibleContent&gt;
/// &lt;/Collapsible&gt;
/// </code>
///
/// 带自定义状态的受控折叠：
/// <code>
/// &lt;Collapsible Open="@isOpen" OpenChanged="@((bool open) => isOpen = open)"&gt;
///     &lt;CollapsibleTrigger&gt;
///         &lt;Button&gt;@(isOpen ? "Collapse" : "Expand")&lt;/Button&gt;
///     &lt;/CollapsibleTrigger&gt;
///     &lt;CollapsibleContent&gt;
///         &lt;p&gt;Controlled content visibility.&lt;/p&gt;
///     &lt;/CollapsibleContent&gt;
/// &lt;/Collapsible&gt;
///
/// @@code {
///     private bool isOpen = false;
/// }
/// </code>
///
/// 禁用的折叠：
/// <code>
/// &lt;Collapsible Disabled&gt;
///     &lt;CollapsibleTrigger&gt;
///         &lt;Button&gt;Cannot Toggle&lt;/Button&gt;
///     &lt;/CollapsibleTrigger&gt;
///     &lt;CollapsibleContent&gt;
///         &lt;p&gt;This collapsible cannot be toggled.&lt;/p&gt;
///     &lt;/CollapsibleContent&gt;
/// &lt;/Collapsible&gt;
/// </code>
/// </example>
public partial class Collapsible : ComponentBase
{
    private bool _isOpen;

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
    /// 获取或设置非受控模式下的默认展开状态。
    /// </summary>
    /// <value>
    /// 如果折叠区域默认应为展开状态则为 <c>true</c>；否则为 <c>false</c>。
    /// 默认值为 <c>false</c>。
    /// </value>
    [Parameter]
    public bool DefaultOpen { get; set; }

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
    /// 获取或设置要应用到容器元素的额外 CSS 类。
    /// </summary>
    /// <value>
    /// 包含一个或多个 CSS 类名的字符串，或为 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 使用此参数可自定义容器外观与布局。常用 Tailwind 工具类包括：
    /// <list type="bullet">
    /// <item>边框：<c>border rounded-lg</c></item>
    /// <item>内边距：<c>p-4</c>、<c>px-6</c></item>
    /// <item>背景：<c>bg-muted</c>、<c>bg-card</c></item>
    /// <item>间距：<c>space-y-2</c>、<c>mb-4</c></item>
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
    /// 获取或设置在折叠容器内渲染的子内容。
    /// </summary>
    /// <value>
    /// 包含子组件的 <see cref="RenderFragment"/>，或为 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 通常包含：
    /// <list type="bullet">
    /// <item><see cref="CollapsibleTrigger"/> - 按钮/触发器元素</item>
    /// <item><see cref="CollapsibleContent"/> - 可展开的内容区域</item>
    /// </list>
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() =>
        // 非受控时以 DefaultOpen 初始化
        _isOpen = OpenChanged.HasDelegate ? Open : DefaultOpen;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // 受控（带回调）时与 Open 参数同步
        if (OpenChanged.HasDelegate)
        {
            _isOpen = Open;
        }
    }

    private string? CssClass => ClassNames.cn(Class);

    private async Task HandleOpenChanged(bool newValue)
    {
        _isOpen = newValue;

        if (OpenChanged.HasDelegate)
        {
            await OpenChanged.InvokeAsync(newValue);
        }
    }
}
