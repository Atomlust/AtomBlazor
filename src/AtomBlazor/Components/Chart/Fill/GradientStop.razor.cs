using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="LinearGradient"/> 内的一个颜色停靠点。
/// </summary>
/// <remarks>
/// <para>
/// 每个 GradientStop 指定渐变中某个偏移位置上的颜色。
/// 构建渐变时，各停靠点会按偏移自动排序。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;LinearGradient&gt;
///     &lt;GradientStop Offset="0" Color="var(--chart-1)" Opacity="0.8" /&gt;
///     &lt;GradientStop Offset="1" Color="var(--chart-1)" Opacity="0.1" /&gt;
/// &lt;/LinearGradient&gt;
/// </code>
/// </example>
public partial class GradientStop : ComponentBase
{
    [CascadingParameter]
    private LinearGradient? ParentGradient { get; set; }

    /// <summary>
    /// 获取或设置该停靠点在渐变中的位置（0 到 1）。
    /// </summary>
    /// <remarks>
    /// 0 表示渐变的起点，1 表示终点。
    /// </remarks>
    [Parameter]
    public double Offset { get; set; }

    /// <summary>
    /// 获取或设置该停靠点位置上的颜色。
    /// </summary>
    /// <remarks>
    /// 接受任何合法的 CSS 颜色值或 CSS 变量引用。
    /// </remarks>
    [Parameter]
    public string? Color { get; set; }

    /// <summary>
    /// 获取或设置该停靠点位置上的不透明度（0 到 1）。
    /// </summary>
    /// <remarks>
    /// 设置后，颜色会被包进 color-mix() 表达式。
    /// 为 null 时，颜色原样使用。
    /// </remarks>
    [Parameter]
    public double? Opacity { get; set; }

    /// <summary>
    /// 获取解析后的颜色字符串；若指定了不透明度则一并纳入。
    /// </summary>
    internal string ResolvedColor
    {
        get
        {
            var color = Color ?? "transparent";

            if (Opacity.HasValue && Opacity.Value < 1.0)
            {
                return $"color-mix(in srgb, {color} {(Opacity.Value * 100).ToString("F0", CultureInfo.InvariantCulture)}%, transparent)";
            }

            return color;
        }
    }

    protected override void OnInitialized() =>
        ParentGradient?.RegisterStop(this);
}
