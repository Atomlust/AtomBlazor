using Microsoft.AspNetCore.Components;
using AtomBlazor.Icons.Heroicons.Data;

namespace AtomBlazor.Icons.Heroicons;

/// <summary>
/// 用于渲染 Heroicons SVG 图标的 Blazor 组件。
/// 支持 4 种变体：Outline（24x24）、Solid（24x24）、Mini（20x20）与 Micro（16x16）。
/// </summary>
public partial class HeroIcon : ComponentBase
{
    /// <summary>
    /// 要渲染的图标名称（大小写不敏感）。
    /// 示例："camera"、"home"、"user"
    /// </summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 要渲染的图标变体。
    /// 默认为 Outline（24x24，线条风格）。
    /// </summary>
    [Parameter]
    public HeroIconVariant Variant { get; set; } = HeroIconVariant.Outline;

    /// <summary>
    /// 图标尺寸（像素）。
    /// 未指定时按变体的默认尺寸：
    /// - Outline：24px
    /// - Solid：24px
    /// - Mini：20px
    /// - Micro：16px
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
    /// 线条图标的描边宽度。
    /// 仅作用于 Outline 变体，对 Solid、Mini、Micro 无效。
    /// 默认为 1.5（Heroicons 线条图标设计规范）。
    /// </summary>
    [Parameter]
    public double? StrokeWidth { get; set; }

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
    /// 依据 Name 与 Variant 从 HeroIconData 获取。
    /// 会剥离硬编码的 stroke 与 fill 属性，以便 Color 参数生效。
    /// </summary>
    private string? SvgContent
    {
        get
        {
            var content = HeroIconData.GetIcon(Name, Variant);
            if (content == null)
            {
                return null;
            }

            // 从 SVG 内容中移除 stroke 与 fill 属性，
            // 使外层 SVG 的 stroke/fill（由 Color 参数驱动）能够生效
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"\s+(stroke|fill)=""[^""]*""",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return content;
        }
    }

    /// <summary>
    /// 依据变体或用户指定尺寸计算出的实际尺寸。
    /// </summary>
    private int ComputedSize
    {
        get
        {
            if (Size.HasValue)
            {
                return Size.Value;
            }

            return Variant switch
            {
                HeroIconVariant.Mini => 20,
                HeroIconVariant.Micro => 16,
                _ => 24  // Outline 与 Solid 默认为 24
            };
        }
    }

    /// <summary>
    /// 线条图标计算出的描边宽度。
    /// </summary>
    private double ComputedStrokeWidth => StrokeWidth ?? 1.5;

    /// <summary>
    /// 依据变体决定的 viewBox。
    /// </summary>
    private string ViewBox => Variant switch
    {
        HeroIconVariant.Mini => "0 0 20 20",
        HeroIconVariant.Micro => "0 0 16 16",
        _ => "0 0 24 24"  // Outline 与 Solid 使用 24x24 viewBox
    };

    /// <summary>
    /// 是否应用 stroke 属性（仅 Outline 变体）。
    /// </summary>
    private bool IsOutline => Variant == HeroIconVariant.Outline;

    /// <summary>
    /// 依据变体决定的 fill 属性值。
    /// </summary>
    private string Fill => IsOutline ? "none" : Color;

    /// <summary>
    /// stroke 属性值（仅 Outline 变体）。
    /// </summary>
    private string? Stroke => IsOutline ? Color : null;

    /// <summary>
    /// 合并后的 CSS 类字符串。
    /// </summary>
    private string CssClass => string.IsNullOrEmpty(Class) ? string.Empty : Class;
}
