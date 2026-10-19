using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 用于 FieldContent 内部的标题元素。
/// </summary>
/// <remarks>
/// <para>
/// FieldTitle 组件为需要在 FieldContent 区域内放置标题的复杂字段布局
/// 提供带样式的标题，使用中等字重与一致排版形成视觉层次。
/// </para>
/// <para>
/// 特性：
/// - 中等字重带来视觉突出效果
/// - 与设计体系一致排版
/// - 与其他字段组件搭配时的恰当间距
/// - 适合分组控件或复杂字段布局
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FieldContent&gt;
///     &lt;FieldTitle&gt;Notification Preferences&lt;/FieldTitle&gt;
///     &lt;Checkbox&gt;Email notifications&lt;/Checkbox&gt;
///     &lt;Checkbox&gt;SMS notifications&lt;/Checkbox&gt;
/// &lt;/FieldContent&gt;
/// </code>
/// </example>
public partial class FieldTitle : ComponentBase
{
    /// <summary>
    /// 获取或设置要应用到标题的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要在标题内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含描述字段或控件组的文本。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到标题元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取标题元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 使用 cn() 工具组合排版与自定义类。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 排版——中等字重带来突出效果
        "atom:text-sm atom:font-medium atom:leading-none",
        // 自定义类（如提供）
        Class
    );
}
