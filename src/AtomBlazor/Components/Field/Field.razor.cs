using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace AtomBlazor;

/// <summary>
/// 组织表单元素、支持多种布局方式的字段容器组件。
/// </summary>
/// <remarks>
/// <para>
/// Field 组件让开发者可以把标签、控件与帮助文本组合成无障碍的完整表单字段。
/// 它以 role="group" 渲染以保证语义标注正确，并支持垂直、水平与响应式方向。
/// </para>
/// <para>
/// 特性：
/// - 三种方向模式：垂直（默认）、水平、响应式
/// - 基于 CSS grid 与 flexbox 的灵活布局
/// - 通过 data-invalid 属性支持无效/错误态
/// - 正确的 ARIA role="group" 无障碍语义
/// - 与 FieldLabel、FieldContent、FieldDescription、FieldError 组件集成
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Field Orientation="FieldOrientation.Vertical"&gt;
///     &lt;FieldLabel&gt;Email&lt;/FieldLabel&gt;
///     &lt;FieldContent&gt;
///         &lt;Input Type="email" /&gt;
///         &lt;FieldDescription&gt;We'll never share your email.&lt;/FieldDescription&gt;
///     &lt;/FieldContent&gt;
/// &lt;/Field&gt;
/// </code>
/// </example>
public partial class Field : ComponentBase
{
    private static readonly string[] verticalClasses = new[]
    {
        // 全宽垂直堆叠（默认）
        "atom:flex-col atom:w-full"
    };

    private static readonly string[] horizontalClasses = new[]
    {
        // 行内布局、顶端对齐，全宽填充容器
        "atom:flex-row atom:items-start atom:w-full",
        // 复选框/单选框的对齐间距调整
        "atom:[&:has([data-slot=checkbox])]:items-center",
        "atom:[&:has([data-slot=radio])]:items-center",
        "atom:[&:has([data-slot=switch])]:items-center"
    };

    private static readonly string[] responsiveClasses = new[]
    {
        // 默认垂直，medium 断点转水平
        "atom:flex-col atom:w-full",
        "atom:@md:flex-row atom:@md:items-start",
        // medium 及以上时复选框/单选框的对齐间距调整
        "atom:@md:[&:has([data-slot=checkbox])]:items-center",
        "atom:@md:[&:has([data-slot=radio])]:items-center",
        "atom:@md:[&:has([data-slot=switch])]:items-center"
    };

    private static readonly string[] horizontalEndClasses = new[]
    {
        // 行反向布局：先内容后标签（标签在末/右侧），全宽填充容器
        "atom:flex-row-reverse atom:items-start atom:w-full",
        // 复选框/单选框的对齐间距调整
        "atom:[&:has([data-slot=checkbox])]:items-center",
        "atom:[&:has([data-slot=radio])]:items-center",
        "atom:[&:has([data-slot=switch])]:items-center"
    };

    private static readonly string[] verticalEndClasses = new[]
    {
        // 列反向布局：先内容后标签（标签在底部）
        "atom:flex-col-reverse atom:w-full"
    };

    /// <summary>
    /// 获取或设置字段布局的方向。
    /// </summary>
    /// <remarks>
    /// 控制布局方向与行为：
    /// - Vertical：标签堆叠在控件上方（默认，移动优先）
    /// - Horizontal：标签位于控件一侧并顶端对齐
    /// - Responsive：在 medium 断点自动从垂直切换为水平
    /// 默认值为 <see cref="FieldOrientation.Vertical"/>。
    /// </remarks>
    [Parameter]
    public FieldOrientation Orientation { get; set; } = FieldOrientation.Vertical;

    /// <summary>
    /// 获取或设置字段是否处于无效/错误态。
    /// </summary>
    /// <remarks>
    /// 为 true 时通过 data-invalid 属性应用错误样式，
    /// 以便针对校验错误进行条件化样式设计。默认值为 false。
    /// 该值同时以名为 FieldIsInvalid 的级联参数下发给内部控件。
    /// </remarks>
    [Parameter]
    public bool IsInvalid { get; set; }

    /// <summary>
    /// 获取或设置要应用到字段容器的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要在字段内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含 FieldLabel、FieldContent、FieldDescription、FieldError
    /// 等组件，组成完整的表单字段。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到字段元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取字段元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 组合了：
    /// - 基础 flex/grid 布局与 gap 间距
    /// - 方向相关类（垂直、水平或响应式）
    /// - 通过 data-invalid 属性选择器应用的错误态样式
    /// - 来自 Class 参数的自定义类
    /// 使用 cn() 工具进行智能类合并与 Tailwind 冲突消解。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 基础样式——带 gap 的 flex 容器
        "atom:flex atom:gap-2",

        // 方向相关样式
        Orientation switch
        {
            FieldOrientation.Vertical => string.Join(" ", verticalClasses),

            FieldOrientation.Horizontal => string.Join(" ", horizontalClasses),

            FieldOrientation.Responsive => string.Join(" ", responsiveClasses),

            FieldOrientation.HorizontalEnd => string.Join(" ", horizontalEndClasses),

            FieldOrientation.VerticalEnd => string.Join(" ", verticalEndClasses),

            _ => string.Empty
        },

        // 错误态样式
        "atom:data-[invalid=true]:text-destructive",

        // 自定义类（如提供）
        Class
    );
}

/// <summary>
/// 定义字段的方向选项。
/// </summary>
public enum FieldOrientation
{
    /// <summary>
    /// 标签堆叠在控件上方（移动优先，默认）。
    /// </summary>
    Vertical,

    /// <summary>
    /// 标签位于控件一侧，水平对齐。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 在 medium 断点自动从垂直切换为水平。
    /// 使用容器查询的 @md 前缀。
    /// </summary>
    Responsive,

    /// <summary>
    /// 标签位于控件的末端/右侧（flex-row-reverse）。
    /// 适用于复选框、开关、单选框等控件先行的场景。
    /// </summary>
    HorizontalEnd,

    /// <summary>
    /// 标签堆叠在控件下方（flex-col-reverse）。
    /// </summary>
    VerticalEnd
}
