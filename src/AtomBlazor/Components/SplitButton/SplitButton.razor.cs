using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor;

/// <summary>
/// 拆分按钮：主按钮与下拉触发按钮并排组合，右侧箭头展开菜单内容。
/// </summary>
public partial class SplitButton : ComponentBase
{
    /// <summary>
    /// 获取或设置主按钮的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置下拉菜单内的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? DropdownContent { get; set; }

    /// <summary>
    /// 获取或设置按钮变体。
    /// </summary>
    [Parameter]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Default;

    /// <summary>
    /// 获取或设置按钮尺寸。
    /// </summary>
    [Parameter]
    public ButtonSize Size { get; set; } = ButtonSize.Default;

    /// <summary>
    /// 获取或设置按钮是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置主按钮的点击回调。
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// 获取或设置主按钮的图标。
    /// </summary>
    [Parameter]
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// 获取或设置图标相对文本的位置。
    /// </summary>
    [Parameter]
    public IconPosition IconPosition { get; set; } = IconPosition.Start;

    /// <summary>
    /// 获取或设置主按钮的无障碍标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置下拉触发按钮的无障碍标签，缺省为 "More options"。
    /// </summary>
    [Parameter]
    public string? DropdownAriaLabel { get; set; }

    /// <summary>
    /// 获取或设置应用于根元素的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用于根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.cn(
        "atom:inline-flex",
        Class
    );

    private static string PrimaryButtonClass => ClassNames.cn(
        "atom:!rounded-e-none atom:border-e-0 atom:focus-visible:z-10"
    );

    private string DropdownButtonClass => ClassNames.cn(
        "atom:!rounded-s-none atom:!px-2 atom:focus-visible:z-10",
        Variant == ButtonVariant.Outline ? "atom:border-s" : "atom:border-s atom:border-s-primary-foreground/20"
    );

    private ButtonSize DropdownButtonSize => Size switch
    {
        ButtonSize.Small => ButtonSize.Small,
        ButtonSize.Large => ButtonSize.Large,
        _ => ButtonSize.Default
    };
}
