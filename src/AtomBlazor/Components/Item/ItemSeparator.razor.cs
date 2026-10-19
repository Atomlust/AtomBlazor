using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 用于在列表中分隔条目的水平分隔元素。
/// </summary>
/// <remarks>
/// ItemSeparator 复用 Separator 组件，并附加特定外边距工具类，
/// 在条目之间形成视觉间隔。
/// </remarks>
public partial class ItemSeparator : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到分隔符上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取分隔符计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:my-1",
        Class
    );
}
