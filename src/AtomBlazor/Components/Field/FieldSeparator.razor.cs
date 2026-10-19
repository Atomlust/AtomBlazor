using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 用于在字段组内分隔各部分的视觉分割线。
/// </summary>
/// <remarks>
/// <para>
/// FieldSeparator 组件提供一条水平线，在 FieldGroup 或 FieldSet 内
/// 视觉上分隔字段区块，使用来自设计体系的柔和边框色。
/// </para>
/// <para>
/// 特性：
/// - 水平分隔线
/// - 来自主题的一致边框色
/// - 适合视觉分隔的间距
/// - 自成一体，无子内容
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FieldGroup&gt;
///     &lt;Field&gt;...&lt;/Field&gt;
///     &lt;FieldSeparator /&gt;
///     &lt;Field&gt;...&lt;/Field&gt;
/// &lt;/FieldGroup&gt;
/// </code>
/// </example>
public partial class FieldSeparator : ComponentBase
{
    /// <summary>
    /// 获取或设置要应用到分隔线的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到分隔线元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取分隔线元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 使用 cn() 工具组合边框样式与自定义类，
    /// 生成一条带柔和边框色的水平线。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 全宽
        "atom:w-full",
        // 水平边框
        "atom:border-t atom:border-border",
        // 垂直间距
        "atom:my-2",
        // 自定义类（如提供）
        Class
    );
}
