using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace AtomBlazor;

/// <summary>
/// 把相关按钮在视觉上连接在一起的按钮组组件。
/// </summary>
/// <remarks>
/// <para>
/// ButtonGroup 组件按 shadcn/ui 设计体系把相关按钮以连体样式归组，
/// 支持水平与垂直两种排列方向，并自动处理焦点态、边框与圆角。
/// </para>
/// <para>
/// 特性：
/// - 水平与垂直方向
/// - 自动处理边框与圆角，形成连体外观
/// - 以 z-index 管理焦点态
/// - 正确的 ARIA role="group" 无障碍支持
/// - 支持嵌套按钮组（组间留间隙）
/// - 与 Button 组件无缝集成
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ButtonGroup&gt;
///     &lt;Button Variant="ButtonVariant.Outline"&gt;Archive&lt;/Button&gt;
///     &lt;Button Variant="ButtonVariant.Outline"&gt;Report&lt;/Button&gt;
/// &lt;/ButtonGroup&gt;
/// </code>
/// </example>
public partial class ButtonGroup : ComponentBase
{
    private static readonly string[] horizontalClasses =
    [
        // 去掉相邻按钮之间的边框与圆角
        "atom:[&>button:not(:first-child):not(:last-child)]:rounded-none",
        "atom:[&>button:not(:first-child)]:border-s-0",
        "atom:[&>button:first-child:not(:only-child)]:rounded-e-none",
        "atom:[&>button:last-child:not(:only-child)]:rounded-s-none",

        // 处理分隔符——与分隔符相邻的按钮保留其边框
        "atom:[&>button:has(+[data-slot=separator])]:rounded-e-none",
        "atom:[&>[data-slot=separator]+button]:rounded-s-none atom:[&>[data-slot=separator]+button]:!border-s atom:[&>[data-slot=separator]+button]:border-input",

        // 处理带间隙的嵌套按钮组
        "atom:[&>[data-slot=button-group]:not(:first-child)]:ms-2",

        // 焦点态 z-index（让获得焦点的按钮浮在其他按钮之上）
        "atom:[&>button:focus]:relative atom:[&>button:focus]:z-10"
    ];

    private static readonly string[] verticalClasses =
    [
        // 垂直布局
        "atom:flex-col",

        // 去掉相邻按钮之间的边框与圆角
        "atom:[&>button:not(:first-child):not(:last-child)]:rounded-none",
        "atom:[&>button:not(:first-child)]:border-t-0",
        "atom:[&>button:first-child:not(:only-child)]:rounded-b-none",
        "atom:[&>button:last-child:not(:only-child)]:rounded-t-none",

        // 处理分隔符——与分隔符相邻的按钮保留其边框
        "atom:[&>button:has(+[data-slot=separator])]:rounded-b-none",
        "atom:[&>[data-slot=separator]+button]:rounded-t-none atom:[&>[data-slot=separator]+button]:!border-t atom:[&>[data-slot=separator]+button]:border-input",

        // 处理带间隙的嵌套按钮组
        "atom:[&>[data-slot=button-group]:not(:first-child)]:mt-2",

        // 焦点态 z-index（让获得焦点的按钮浮在其他按钮之上）
        "atom:[&>button:focus]:relative atom:[&>button:focus]:z-10"
    ];

    /// <summary>
    /// 获取或设置按钮组的排列方向。
    /// </summary>
    /// <remarks>
    /// 控制按钮水平（默认）还是垂直排列。
    /// 默认值为 <see cref="ButtonGroupOrientation.Horizontal"/>。
    /// </remarks>
    [Parameter]
    public ButtonGroupOrientation Orientation { get; set; } = ButtonGroupOrientation.Horizontal;

    /// <summary>
    /// 获取或设置应用到按钮组上的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置按钮组的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 为使用 role="group" 的组提供名称，帮助辅助技术传达组的用途。
    /// 推荐用于执行特定功能的按钮组（如"文本格式化""邮件操作"）。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置渲染在按钮组内部的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含 Button 组件，也可包含嵌套的 ButtonGroup 以组成复杂布局。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到按钮组元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取按钮组元素计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 组合了：
    /// - 用于布局的基础 flex 样式
    /// - 方向专属样式（水平或垂直）
    /// - 连体按钮的边框与圆角去除
    /// - 焦点态 z-index 管理
    /// - 嵌套按钮组的间隙处理
    /// - 来自 Class 参数的自定义类
    /// 使用 cn() 工具做智能类名合并与 Tailwind 冲突消解。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 基础样式——带 role="group" 的 flex 容器
        "atom:isolate atom:inline-flex atom:items-stretch",

        // 方向专属样式
        Orientation switch
        {
            ButtonGroupOrientation.Horizontal => string.Join(" ", horizontalClasses),
            ButtonGroupOrientation.Vertical => string.Join(" ", verticalClasses),
            _ => string.Empty
        },

        // 自定义类（如有）
        Class
    );
}

/// <summary>
/// 定义按钮组的排列方向选项。
/// </summary>
public enum ButtonGroupOrientation
{
    /// <summary>
    /// 按钮水平排列（从左到右）。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 按钮垂直排列（从上到下）。
    /// </summary>
    Vertical
}
