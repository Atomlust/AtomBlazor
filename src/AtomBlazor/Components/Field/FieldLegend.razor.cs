using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace AtomBlazor;

/// <summary>
/// 为字段集或字段组提供命名标注的 legend 元素。
/// </summary>
/// <remarks>
/// <para>
/// FieldLegend 组件为成组的表单控件提供标题/标签。
/// 它支持两种变体：语义化的 legend 元素（与 FieldSet 搭配）
/// 或带 role="group" 的 div（与 FieldGroup 搭配）。
/// </para>
/// <para>
/// 特性：
/// - 两种渲染模式：legend 元素或带 role="group" 的 div
/// - 一致的排版与间距
/// - 面向无障碍的 ARIA 支持
/// - 与 FieldSet、FieldGroup 集成
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FieldSet&gt;
///     &lt;FieldLegend Variant="FieldLegendVariant.Legend"&gt;
///         Shipping Address
///     &lt;/FieldLegend&gt;
///     &lt;Field&gt;...&lt;/Field&gt;
/// &lt;/FieldSet&gt;
/// </code>
/// </example>
public partial class FieldLegend : ComponentBase
{
    /// <summary>
    /// 获取或设置图例的变体。
    /// </summary>
    /// <remarks>
    /// 控制使用的 HTML 元素：
    /// - Legend：渲染语义化的 legend 元素（与 FieldSet 搭配）
    /// - Label：渲染带 role="group" 的 div（与 FieldGroup 搭配）
    /// 默认值为 <see cref="FieldLegendVariant.Legend"/>。
    /// </remarks>
    [Parameter]
    public FieldLegendVariant Variant { get; set; } = FieldLegendVariant.Legend;

    /// <summary>
    /// 获取或设置 Label 变体下的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 当变体为 Label 时为该分组提供可访问名称。
    /// 仅在 Variant 为 <see cref="FieldLegendVariant.Label"/> 时生效。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置要应用到图例的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要在图例内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含描述成组表单字段的文本。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到图例元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取图例元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 使用 cn() 工具组合排版、间距与自定义类。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 排版
        "atom:text-sm atom:font-medium atom:leading-none",
        // 间距
        "atom:mb-3",
        // 自定义类（如提供）
        Class
    );
}

/// <summary>
/// 定义字段图例的变体选项。
/// </summary>
public enum FieldLegendVariant
{
    /// <summary>
    /// 渲染语义化的 legend 元素（与 FieldSet 搭配使用）。
    /// </summary>
    Legend,

    /// <summary>
    /// 渲染带 role="group" 的 div（与 FieldGroup 搭配使用）。
    /// </summary>
    Label
}
