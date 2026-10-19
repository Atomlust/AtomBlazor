using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace AtomBlazor;

/// <summary>
/// 专为在 ButtonGroup 内使用而设计的文本/标签组件。
/// </summary>
/// <remarks>
/// <para>
/// ButtonGroupText 组件为按钮组内的文本或标签提供带样式的容器，
/// 使用柔和背景与合适的内边距，把文本部分与可交互按钮在视觉上区分开。
/// </para>
/// <para>
/// 特性：
/// - 柔和背景，形成视觉区隔
/// - 与按钮一致的内外边距
/// - SVG 图标的自动尺寸适配
/// - 与 ButtonGroup 布局无缝集成
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ButtonGroup&gt;
///     &lt;ButtonGroupText&gt;Actions:&lt;/ButtonGroupText&gt;
///     &lt;Button Variant="ButtonVariant.Outline"&gt;Edit&lt;/Button&gt;
///     &lt;Button Variant="ButtonVariant.Outline"&gt;Delete&lt;/Button&gt;
/// &lt;/ButtonGroup&gt;
/// </code>
/// </example>
public partial class ButtonGroupText : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到文本容器上的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置渲染在文本容器内的内容。
    /// </summary>
    /// <remarks>
    /// 可以包含文本、图标或其他标记。图标会自动调整为与按钮组图标一致的尺寸。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到文本容器元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取文本容器元素计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 组合了：
    /// - 基础 flex 布局与内边距
    /// - 柔和背景样式
    /// - SVG 图标尺寸
    /// - 来自 Class 参数的自定义类
    /// 使用 cn() 工具做智能类名合并与 Tailwind 冲突消解。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 基础样式——带内边距的 inline-flex
        "atom:inline-flex atom:items-center atom:justify-center",
        "atom:px-3 atom:py-2",

        // 柔和背景样式
        "atom:bg-muted atom:text-muted-foreground",
        "atom:text-sm atom:font-medium",

        // 图标尺寸（SVG 元素）
        "atom:[&_svg]:size-4 atom:[&_svg]:shrink-0",

        // 自定义类（如有）
        Class
    );
}
