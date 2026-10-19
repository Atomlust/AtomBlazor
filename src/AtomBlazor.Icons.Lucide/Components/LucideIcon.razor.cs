using Microsoft.AspNetCore.Components;
using AtomBlazor.Icons.Lucide.Data;

namespace AtomBlazor.Icons.Lucide;

/// <summary>
/// 以内联 SVG 渲染一个 Lucide 图标。
/// 提供熟悉的组件式 API，便于在 Blazor 应用中集成。
/// </summary>
public partial class LucideIcon : ComponentBase
{
    /// <summary>
    /// 要渲染的图标名称（如 "camera"、"home"、"user"）。
    /// 名称大小写不敏感。
    /// </summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 图标尺寸（像素）。默认 24。
    /// </summary>
    [Parameter]
    public int Size { get; set; } = 24;

    /// <summary>
    /// 图标颜色。默认 "currentColor"（继承父级颜色）。
    /// 可接受任何合法 CSS 颜色值（十六进制、rgb、命名色、CSS 变量等）。
    /// </summary>
    [Parameter]
    public string Color { get; set; } = "currentColor";

    /// <summary>
    /// 图标路径的描边宽度。默认 2.0。
    /// Lucide 图标按 2px 描边设计。
    /// </summary>
    [Parameter]
    public double StrokeWidth { get; set; } = 2.0;

    /// <summary>
    /// 图标填充色。默认 "none"（仅描边不填充）。
    /// 可设为 "currentColor" 继承父级，或任何合法 CSS 颜色值。
    /// </summary>
    [Parameter]
    public string Fill { get; set; } = "none";

    /// <summary>
    /// 附加到 SVG 元素上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 无障碍 ARIA 标签。纯图标按钮建议使用。
    /// 未指定时以图标名作为 aria-label。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 附加的 SVG 属性（捕获未匹配的参数字典）。
    /// 可传入任意合法 SVG 属性，如 data-*、style 等。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 从 <see cref="LucideIconData"/> 取图标的 SVG 内容；
    /// 未找到返回 null。
    /// 同时剔除数据中硬编码的 stroke/fill 属性，让组件参数生效。
    /// </summary>
    private string? SvgContent
    {
        get
        {
            var content = LucideIconData.GetIcon(Name);
            if (content == null)
            {
                return null;
            }

            // 去掉内层的 stroke="..." 属性——
            // 让外层 SVG 的 stroke（来自 Color 参数）生效
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"\s+stroke=""[^""]*""",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // 去掉内层的 fill="..." 属性——
            // 让外层 SVG 的 fill（来自 Fill 参数）生效
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"\s+fill=""[^""]*""",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return content;
        }
    }

    /// <summary>
    /// 组装 SVG 元素的 CSS 类串：
    /// 基础类 "lucide-icon" 与任何自定义类合并。
    /// </summary>
    private string CssClass
    {
        get
        {
            var classes = new List<string> { "lucide-icon" };

            if (!string.IsNullOrWhiteSpace(Class))
            {
                classes.Add(Class);
            }

            return string.Join(" ", classes);
        }
    }
}
