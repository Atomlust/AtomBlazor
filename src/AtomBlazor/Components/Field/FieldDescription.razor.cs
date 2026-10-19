using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为表单字段提供补充信息的帮助文本组件。
/// </summary>
/// <remarks>
/// <para>
/// FieldDescription 组件展示表单字段的补充信息，例如格式要求、示例或友好提示。
/// 它使用弱化文字颜色与较小字号，与主标签形成区分。
/// </para>
/// <para>
/// 特性：
/// - 面向帮助内容的较小、弱化文本样式
/// - 自动行平衡以改善可读性
/// - 在 FieldContent 中保持恰当间距
/// - 支持与输入控件的 aria-describedby 关联
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FieldContent&gt;
///     &lt;Input Type="email" aria-describedby="email-description" /&gt;
///     &lt;FieldDescription Id="email-description"&gt;
///         We'll never share your email with anyone else.
///     &lt;/FieldDescription&gt;
/// &lt;/FieldContent&gt;
/// </code>
/// </example>
public partial class FieldDescription : ComponentBase
{
    /// <summary>
    /// 获取或设置用于 aria-describedby 关联的 ID。
    /// </summary>
    /// <remarks>
    /// 设置后，关联输入控件的 aria-describedby 属性应引用该 ID，
    /// 以满足无障碍要求。
    /// </remarks>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置要应用到描述文本的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要在描述内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含解释关联表单字段用途或格式的帮助文本。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到描述元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取描述元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 使用 cn() 工具组合排版、颜色与自定义类，
    /// 并包含用于改善换行的 text-balance。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 排版——较小字号
        "atom:text-sm",
        // 颜色——帮助文本使用弱化色
        "atom:text-muted-foreground",
        // 行平衡以改善换行
        "atom:text-balance",
        // 自定义类（如提供）
        Class
    );
}
