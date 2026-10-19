using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace AtomBlazor;

/// <summary>
/// 用于堆叠多个 Field 组件的布局容器。
/// </summary>
/// <remarks>
/// <para>
/// FieldGroup 组件为多个相关字段提供一致的间距与布局。
/// 它支持垂直、水平与响应式三种方向，自动处理 gap 间距并支持容器查询。
/// </para>
/// <para>
/// 特性：
/// - 三种方向模式：垂直（默认）、水平、响应式
/// - 字段之间自动的 gap 间距
/// - 响应式布局的容器查询支持（@container/field-group）
/// - 与 Field 组件集成
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FieldGroup Orientation="FieldGroupOrientation.Responsive"&gt;
///     &lt;Field&gt;
///         &lt;FieldLabel&gt;First Name&lt;/FieldLabel&gt;
///         &lt;Input /&gt;
///     &lt;/Field&gt;
///     &lt;Field&gt;
///         &lt;FieldLabel&gt;Last Name&lt;/FieldLabel&gt;
///         &lt;Input /&gt;
///     &lt;/Field&gt;
/// &lt;/FieldGroup&gt;
/// </code>
/// </example>
public partial class FieldGroup : ComponentBase
{
    private static readonly string[] responsiveClasses = new[]
    {
        // 默认垂直
        "atom:flex-col",
        // 容器查询设置，实现响应式行为
        "atom:@container/field-group",
        // 容器宽度达到 medium 时转为水平
        "atom:@md:flex-row atom:@md:flex-wrap"
    };

    /// <summary>
    /// 获取或设置字段组布局的方向。
    /// </summary>
    /// <remarks>
    /// 控制布局方向：
    /// - Vertical：字段垂直堆叠（默认）
    /// - Horizontal：字段水平排列并支持换行
    /// - Responsive：使用容器查询按可用空间自适应布局
    /// 默认值为 <see cref="FieldGroupOrientation.Vertical"/>。
    /// </remarks>
    [Parameter]
    public FieldGroupOrientation Orientation { get; set; } = FieldGroupOrientation.Vertical;

    /// <summary>
    /// 获取或设置要应用到字段组的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要在字段组内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含多个 Field 组件。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到字段组元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取字段组元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 使用 cn() 工具组合布局样式、间距与自定义类。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 基础 flex 容器
        "atom:flex atom:gap-4",

        // 方向相关样式
        Orientation switch
        {
            FieldGroupOrientation.Vertical => "atom:flex-col",

            FieldGroupOrientation.Horizontal => "atom:flex-row atom:flex-wrap",

            FieldGroupOrientation.Responsive => string.Join(" ", responsiveClasses),

            _ => string.Empty
        },

        // 自定义类（如提供）
        Class
    );
}

/// <summary>
/// 定义字段组的方向选项。
/// </summary>
public enum FieldGroupOrientation
{
    /// <summary>
    /// 字段垂直堆叠（默认）。
    /// </summary>
    Vertical,

    /// <summary>
    /// 字段水平排列并支持换行。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 使用容器查询自动适配布局。
    /// </summary>
    Responsive
}
