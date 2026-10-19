using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 带样式的触发器按钮组件，切换 Collapsible 的展开/收起状态。
/// </summary>
/// <remarks>
/// <para>
/// CollapsibleTrigger 组件是 CollapsibleTrigger 基元的样式封装，在应用 shadcn/ui
/// 样式的同时保留全部行为与无障碍能力。
/// </para>
/// <para>
/// 本组件必须作为 <see cref="Collapsible"/> 组件的子组件使用。
/// </para>
/// <para>
/// 无障碍特性：
/// <list type="bullet">
/// <item>语义化 button 元素与恰当的角色</item>
/// <item>aria-expanded 属性反映当前状态</item>
/// <item>键盘支持（空格/回车键）</item>
/// <item>禁用状态带相应 ARIA 属性</item>
/// <item>键盘导航的焦点管理</item>
/// </list>
/// </para>
/// <para>
/// <see cref="AsChild"/> 默认为 true，此时触发器不渲染自有元素，而是把行为交给读取它的
/// 子组件（例如 <c>Button</c>）。文本、图标或普通标记无法读取该上下文：这些场景应设置
/// <c>AsChild="false"</c>，让触发器渲染自己的 button 包裹它们。AsChild 模式下
/// <see cref="Class"/> 与额外属性会被忽略——因为没有元素可承载；当触发器内部没有任何
/// 内容消费该上下文时，开发环境会记录一条警告。
/// </para>
/// </remarks>
/// <example>
/// 以按钮作为触发器（默认用法）：
/// <code>
/// &lt;Collapsible&gt;
///     &lt;CollapsibleTrigger&gt;
///         &lt;Button Variant="ButtonVariant.Outline"&gt;Toggle Content&lt;/Button&gt;
///     &lt;/CollapsibleTrigger&gt;
///     &lt;CollapsibleContent&gt;...&lt;/CollapsibleContent&gt;
/// &lt;/Collapsible&gt;
/// </code>
///
/// 带图标的样式化触发器（需要自有按钮）：
/// <code>
/// &lt;CollapsibleTrigger AsChild="false" Class="flex items-center gap-2 px-4 py-2 hover:bg-accent"&gt;
///     &lt;LucideIcon Name="chevron-down" /&gt;
///     &lt;span&gt;Show More&lt;/span&gt;
/// &lt;/CollapsibleTrigger&gt;
/// </code>
/// </example>
public partial class CollapsibleTrigger : ComponentBase
{
    /// <summary>
    /// 获取或设置要应用到触发器按钮元素的额外 CSS 类。
    /// </summary>
    /// <value>
    /// 包含一个或多个 CSS 类名的字符串，或为 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 触发器样式化的常用 Tailwind 工具类：
    /// <list type="bullet">
    /// <item>弹性布局：<c>flex items-center gap-2</c></item>
    /// <item>内边距：<c>px-4 py-2</c></item>
    /// <item>悬停态：<c>hover:bg-accent</c></item>
    /// <item>过渡：<c>transition-all duration-200</c></item>
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
    /// 为 true 时，触发器不渲染自己的按钮元素，而是通过 TriggerContext 把触发行为
    /// 传递给子组件。适用于让自定义组件（如 Button）充当触发器的场景。
    /// </summary>
    [Parameter]
    public bool AsChild { get; set; } = true;

    /// <summary>
    /// 获取或设置在触发器内渲染的内容。
    /// </summary>
    /// <value>
    /// 包含触发器内容的 <see cref="RenderFragment"/>，或为 <c>null</c>。
    /// </value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取应用到触发器按钮元素的计算后 CSS 类字符串。
    /// </summary>
    /// <value>
    /// 包含全部待应用 CSS 类的字符串。
    /// </value>
    private string CssClass => ClassNames.cn(
        "atom:group",
        // #459：没有这一项时浏览器会绘制自有轮廓，与页面其他地方的 themed 环形焦点样式
        // 呈现为不同的焦点观感。
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        Class);
}
