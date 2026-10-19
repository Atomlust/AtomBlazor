using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 用于分组相关表单控件的语义化 fieldset 容器。
/// </summary>
/// <remarks>
/// <para>
/// FieldSet 组件渲染原生 HTML fieldset 元素，为相关表单字段提供
/// 语义化分组，保持键盘导航与辅助技术的正确无障碍行为。
/// </para>
/// <para>
/// 特性：
/// - 语义化 HTML fieldset 元素
/// - 与 FieldLegend 集成实现分组命名
/// - 面向分组控件的正确无障碍语义
/// - 基于 Tailwind CSS 的灵活样式
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FieldSet&gt;
///     &lt;FieldLegend&gt;Personal Information&lt;/FieldLegend&gt;
///     &lt;Field&gt;...&lt;/Field&gt;
///     &lt;Field&gt;...&lt;/Field&gt;
/// &lt;/FieldSet&gt;
/// </code>
/// </example>
public partial class FieldSet : ComponentBase
{
    /// <summary>
    /// 获取或设置要应用到字段集的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类会与组件基础类合并，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要在字段集内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常先是一个 FieldLegend，随后是多个 Field 组件，
    /// 组成分组表单控件。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到字段集元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取字段集元素的最终 CSS 类。
    /// </summary>
    /// <remarks>
    /// 使用 cn() 工具合并基础样式与自定义类，
    /// 移除浏览器默认样式并应用一致间距。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 移除浏览器默认样式
        "atom:border-0 atom:p-0 atom:m-0",
        // 分组字段的间距
        "atom:space-y-4",
        // 自定义类（如提供）
        Class
    );
}
