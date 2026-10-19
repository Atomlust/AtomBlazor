using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 用于分组表单控件与描述信息的 flex 列容器。
/// </summary>
/// <remarks>
/// <para>
/// FieldContent 组件为 Field 内部的表单控件、描述与错误消息提供一致堆叠布局，
/// 使用带垂直堆叠与自动 gap 间距的 flexbox。
/// </para>
/// <para>
/// 特性：
/// - 带一致 gap 间距的 flex 列布局
/// - 子元素自动堆叠
/// - 与 Input、FieldDescription、FieldError 组件集成
/// - 默认全宽，适配响应式布局
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Field&gt;
///     &lt;FieldLabel&gt;Username&lt;/FieldLabel&gt;
///     &lt;FieldContent&gt;
///         &lt;Input Type="text" /&gt;
///         &lt;FieldDescription&gt;Choose a unique username&lt;/FieldDescription&gt;
///     &lt;/FieldContent&gt;
/// &lt;/Field&gt;
/// </code>
/// </example>
public partial class FieldContent : ComponentBase
{
    /// <summary>
    /// 获取或设置要应用到字段内容容器的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要在字段内容容器内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常先包含一个输入控件，随后是可选的
    /// FieldDescription 与 FieldError 组件。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到字段内容元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取字段内容元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 使用 cn() 工具组合 flex 布局、间距与自定义类。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // flex 列布局带 gap
        "atom:flex atom:flex-col atom:gap-1.5",
        // 全宽保证响应式表现，min-w-0 防止水平布局中溢出
        "atom:w-full atom:min-w-0",
        // 自定义类（如提供）
        Class
    );
}
