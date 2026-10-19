using Microsoft.AspNetCore.Components;
using AtomBlazor.Icons.Feather.Data;

namespace AtomBlazor.Icons.Feather;

/// <summary>
/// 用于渲染 Feather SVG 图标的 Blazor 组件。
/// Feather 图标为简约线条风格，画布 24x24、描边 2px。
/// </summary>
public partial class FeatherIcon : ComponentBase
{
    /// <summary>
    /// 要渲染的图标名称（大小写不敏感）。
    /// 示例："camera"、"home"、"user"
    /// </summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 图标尺寸（像素）。
    /// 默认为 24（Feather 图标按 24x24 设计）。
    /// </summary>
    [Parameter]
    public int Size { get; set; } = 24;

    /// <summary>
    /// 图标颜色，支持任意 CSS 颜色值。
    /// 默认为 "currentColor"（继承父级颜色）。
    /// 示例："red"、"#FF0000"、"var(--primary)"、"rgb(255, 0, 0)"
    /// </summary>
    [Parameter]
    public string Color { get; set; } = "currentColor";

    /// <summary>
    /// 图标描边宽度。
    /// 默认为 2（Feather 图标设计规范）。
    /// </summary>
    [Parameter]
    public double StrokeWidth { get; set; } = 2.0;

    /// <summary>
    /// 应用到图标的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 用于无障碍（屏幕阅读器）的 ARIA 标签。
    /// 建议在纯图标按钮上提供。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 应用到 SVG 元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 图标的 SVG 内容。
    /// 依据 Name 从 FeatherIconData 获取。
    /// 会剥离硬编码的 stroke 属性，以便 Color 参数生效。
    /// </summary>
    private string? SvgContent
    {
        get
        {
            var content = FeatherIconData.GetIcon(Name);
            if (content == null)
            {
                return null;
            }

            // 从 SVG 内容中移除 stroke="currentColor" 及 stroke="..." 属性，
            // 使外层 SVG 的 stroke（由 Color 参数驱动）能够生效
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"\s+stroke=""[^""]*""",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return content;
        }
    }

    /// <summary>
    /// 合并后的 CSS 类字符串。
    /// </summary>
    private string CssClass => string.IsNullOrEmpty(Class) ? string.Empty : Class;
}
