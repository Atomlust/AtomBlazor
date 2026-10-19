using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 用于展示校验错误的无障碍错误消息容器。
/// </summary>
/// <remarks>
/// <para>
/// FieldError 组件以恰当的样式与无障碍属性展示校验错误消息。
/// 既支持 Errors 集合渲染多条消息，也支持 ChildContent 插槽自定义错误渲染。
/// </para>
/// <para>
/// 特性：
/// - 基于集合、带项目符号的错误展示
/// - 通过 ChildContent 提供自定义错误内容
/// - 使用警示色（destructive）提升可见性
/// - 支持与输入控件的 aria-describedby 关联
/// - 仅在存在错误或内容时渲染
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FieldError Errors="@validationErrors" /&gt;
///
/// // 或使用自定义内容：
/// &lt;FieldError&gt;
///     &lt;span&gt;Password must be at least 8 characters&lt;/span&gt;
/// &lt;/FieldError&gt;
/// </code>
/// </example>
public partial class FieldError : ComponentBase
{
    /// <summary>
    /// 获取或设置要展示的错误消息集合。
    /// </summary>
    /// <remarks>
    /// 提供时，每条错误渲染为一个带项目符号的列表项。
    /// 若 Errors 与 ChildContent 同时提供，Errors 优先。
    /// 组件仅在存在错误（或 ChildContent 非空）时渲染。
    /// </remarks>
    [Parameter]
    public IEnumerable<string>? Errors { get; set; }

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
    /// 获取或设置要应用到错误容器的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要渲染的自定义错误内容。
    /// </summary>
    /// <remarks>
    /// 用于超出纯文本消息的自定义错误渲染。
    /// 若 Errors 与 ChildContent 同时提供，Errors 优先。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到错误元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取错误元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 使用 cn() 工具组合排版、警示色与自定义类。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 排版——较小字号
        "atom:text-sm",
        // 提升可见性的字重
        "atom:font-medium",
        // 错误使用警示色
        "atom:text-destructive",
        // 自定义类（如提供）
        Class
    );
}
