using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 附件的操作控件，渲染为按钮。
/// </summary>
/// <remarks>
/// 复用 Button（交付）作为底层元素，默认取幽灵变体与小号图标尺寸，
/// 未匹配的 <c>OnClick</c> 等属性经 Button 透传到 button 元素。
/// </remarks>
public partial class AttachmentAction : ComponentBase
{
    /// <summary>
    /// 获取或设置操作按钮的变体。
    /// </summary>
    [Parameter]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Ghost;

    /// <summary>
    /// 获取或设置操作按钮的尺寸。
    /// </summary>
    [Parameter]
    public ButtonSize Size { get; set; } = ButtonSize.IconSmall;

    /// <summary>
    /// 获取或设置应用到操作按钮的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置操作内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，透传给底层按钮。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
}
