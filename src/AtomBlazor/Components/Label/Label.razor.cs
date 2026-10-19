using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为表单字段渲染语义化 HTML label 元素的标签组件。
/// </summary>
/// <remarks>
/// <para>
/// Label 组件遵循 shadcn/ui 设计体系，为表单控件提供无障碍标注。
/// 它通过 For 属性自动与表单字段建立关联，并借助 CSS peer 选择器支持禁用态样式。
/// </para>
/// <para>
/// 常见使用场景：
/// <list type="bullet">
/// <item>与表单控件显式关联的字段标签</item>
/// <item>必填字段标识</item>
/// <item>无障碍友好的复选框与单选框标签</item>
/// <item>可交互、可控制焦点的标签</item>
/// </list>
/// </para>
/// <para>
/// 组件利用 Tailwind CSS 的 peer-disabled 工具类，在其关联控件被禁用时自动为标签
/// 应用禁用样式，无需额外代码即可提供视觉反馈。
/// </para>
/// </remarks>
/// <example>
/// 带显式关联的基础标签：
/// <code>
/// &lt;Label For="email"&gt;Email Address&lt;/Label&gt;
/// &lt;Input Id="email" Type="InputType.Email" /&gt;
/// </code>
///
/// 必填字段标识：
/// <code>
/// &lt;Label For="username"&gt;
///     Username &lt;span class="atom:text-destructive"&gt;*&lt;/span&gt;
/// &lt;/Label&gt;
/// &lt;Input Id="username" Required /&gt;
/// </code>
///
/// 复选框标签：
/// <code>
/// &lt;div class="atom:flex atom:items-center atom:space-x-2"&gt;
///     &lt;Checkbox Id="terms" /&gt;
///     &lt;Label For="terms"&gt;Accept terms and conditions&lt;/Label&gt;
/// &lt;/div&gt;
/// </code>
/// </example>
public partial class Label : ComponentBase
{
    /// <summary>
    /// 获取或设置此标签所关联表单元素的 ID。
    /// </summary>
    /// <value>
    /// 目标表单控件的 ID 字符串，或 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 该参数映射到 HTML 的 <c>for</c> 属性（JSX 中为 htmlFor）。
    /// 设置后，点击标签即可聚焦或激活其关联的表单控件。
    /// <para>
    /// 最佳实践：
    /// <list type="bullet">
    /// <item>始终提供 For 值，实现标签与控件的显式关联</item>
    /// <item>确保 For 值与目标表单控件的 Id 一致</item>
    /// <item>使用能描述字段用途的有意义 ID</item>
    /// </list>
    /// </para>
    /// </remarks>
    [Parameter]
    public string? For { get; set; }

    /// <summary>
    /// 获取或设置要应用到标签元素上的额外 CSS 类。
    /// </summary>
    /// <value>
    /// 一个或多个 CSS 类名，或 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 使用此参数在默认样式之上自定义标签外观。
    /// 常用 Tailwind 工具类包括：
    /// <list type="bullet">
    /// <item>文本大小：<c>text-lg</c>、<c>text-sm</c></item>
    /// <item>字重：<c>font-bold</c>、<c>font-normal</c></item>
    /// <item>颜色：<c>text-muted-foreground</c>、<c>text-destructive</c></item>
    /// <item>间距：<c>mb-2</c>、<c>me-2</c></item>
    /// </list>
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素上的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置要在标签元素内部渲染的内容。
    /// </summary>
    /// <value>
    /// 包含标签内容的 <see cref="RenderFragment"/>，或 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 通常包含标签文本，也可以加入以下元素：
    /// <list type="bullet">
    /// <item>必填标识（星号、徽章）</item>
    /// <item>帮助文本或提示</item>
    /// <item>图标或视觉指示器</item>
    /// <item>用于局部文本样式化的嵌套 span</item>
    /// </list>
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取标签元素的最终 CSS 类串。
    /// </summary>
    /// <value>
    /// 要应用到标签元素上的全部 CSS 类组成的字符串。
    /// </value>
    /// <remarks>
    /// 类串包含：
    /// <list type="bullet">
    /// <item>基础文本样式：<c>text-sm font-medium leading-none</c></item>
    /// <item>禁用态样式：<c>peer-disabled:cursor-not-allowed peer-disabled:opacity-70</c></item>
    /// <item>自定义类：通过 <see cref="Class"/> 参数传入的类</item>
    /// </list>
    /// <para>
    /// peer-disabled 工具类在标签跟随一个带 <c>peer</c> 类的表单控件时生效。
    /// 该控件被禁用时，标签自动获得降低不透明度与 not-allowed 光标的样式。
    /// </para>
    /// </remarks>
    private string CssClass => ClassNames.cn(
        "atom:text-sm atom:font-medium atom:leading-none atom:peer-disabled:cursor-not-allowed atom:peer-disabled:opacity-70",
        Class
    );
}
