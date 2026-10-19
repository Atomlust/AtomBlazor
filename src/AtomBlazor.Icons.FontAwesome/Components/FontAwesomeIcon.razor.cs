using Microsoft.AspNetCore.Components;
using AtomBlazor.Icons.FontAwesome.Data;

namespace AtomBlazor.Icons.FontAwesome;

/// <summary>
/// 用于渲染 Font Awesome Free SVG 图标的 Blazor 组件。
/// 支持 3 种变体：Solid、Regular 与 Brands。
/// </summary>
public partial class FontAwesomeIcon : ComponentBase
{
    /// <summary>
    /// 要渲染的图标名称（大小写不敏感，kebab-case）。
    /// 示例："camera"、"user"、"github"
    /// </summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 要渲染的图标变体。
    /// 默认为 Solid。
    /// </summary>
    [Parameter]
    public FontAwesomeIconVariant Variant { get; set; } = FontAwesomeIconVariant.Solid;

    /// <summary>
    /// 图标尺寸（像素，作用于宽度）。
    /// 高度按比例缩放以保持宽高比（尤其是 Brands 图标并非正方形）。
    /// 默认为 16px。
    /// </summary>
    [Parameter]
    public int? Size { get; set; }

    /// <summary>
    /// 图标颜色，支持任意 CSS 颜色值。
    /// 默认为 "currentColor"（继承父级颜色）。
    /// 示例："red"、"#FF0000"、"var(--primary)"、"rgb(255, 0, 0)"
    /// </summary>
    [Parameter]
    public string Color { get; set; } = "currentColor";

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

    private const int defaultSize = 16;

    /// <summary>
    /// 当前 Name 与 Variant 对应的图标条目（主体 + 固有宽高）。
    /// </summary>
    private FontAwesomeIconEntry? IconEntry => FontAwesomeIconData.GetIcon(Name, Variant);

    /// <summary>
    /// 剥离硬编码 fill/stroke 属性后的图标 SVG 主体，
    /// 使外层 &lt;svg&gt; 元素的 fill（由 Color 参数驱动）能够生效。
    /// </summary>
    private string SvgBody => IconEntry is null
        ? string.Empty
        : System.Text.RegularExpressions.Regex.Replace(
            IconEntry.Body,
            @"\s+(stroke|fill)=""[^""]*""",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// 计算出的宽度（像素）。
    /// </summary>
    private int ComputedSize => Size ?? defaultSize;

    /// <summary>
    /// 计算出的高度（像素），按比例缩放以保持图标的固有宽高比。
    /// </summary>
    private int ComputedHeight
    {
        get
        {
            if (IconEntry is null || IconEntry.Width == 0)
            {
                return ComputedSize;
            }

            return (int)Math.Round(ComputedSize * ((double)IconEntry.Height / IconEntry.Width));
        }
    }

    /// <summary>
    /// 由图标固有宽高推导出的 viewBox。
    /// </summary>
    private string ViewBox => IconEntry is null
        ? "0 0 512 512"
        : $"0 0 {IconEntry.Width} {IconEntry.Height}";

    /// <summary>
    /// 合并后的 CSS 类字符串。
    /// </summary>
    private string CssClass => string.IsNullOrEmpty(Class) ? string.Empty : Class;
}
