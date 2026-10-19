using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 带无障碍属性的表单输入标签组件。
/// </summary>
/// <remarks>
/// <para>
/// FieldLabel 组件为表单控件提供带样式的 label 元素。
/// 它支持 for 属性与输入元素建立关联，并包含基于 peer 的错误态样式。
/// </para>
/// <para>
/// 特性：
/// - 支持 for 属性的语义化 label 元素
/// - 与设计体系一致的排版与间距
/// - 通过 peer 选择器实现的错误态样式
/// - 支持嵌套在 FieldContent 内的字段
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FieldLabel For="email-input"&gt;Email Address&lt;/FieldLabel&gt;
/// &lt;Input Id="email-input" Type="email" /&gt;
/// </code>
/// </example>
public partial class FieldLabel : ComponentBase
{
    /// <summary>
    /// 获取或设置此标签所关联表单控件的 ID。
    /// </summary>
    /// <remarks>
    /// 应与输入元素的 ID 一致以满足无障碍要求，
    /// 点击标签即可聚焦关联的输入控件。
    /// </remarks>
    [Parameter]
    public string? For { get; set; }

    /// <summary>
    /// 获取或设置要应用到标签的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要在标签内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含描述关联表单控件的文本，
    /// 也可以加入必填标识、提示等内容。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到标签元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取标签元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 使用 cn() 工具组合排版、间距、错误态与自定义类，
    /// 并包含与 aria-invalid 输入配合使用的基于 peer 的错误态样式。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 排版
        "atom:text-sm atom:font-medium atom:leading-none",
        // 防止在水平布局中被压缩
        "atom:shrink-0",
        // 通过 peer 选择器实现的错误态（输入携带 aria-invalid="true" 时）
        "atom:peer-aria-[invalid=true]:text-destructive",
        // 自定义类（如提供）
        Class
    );
}
