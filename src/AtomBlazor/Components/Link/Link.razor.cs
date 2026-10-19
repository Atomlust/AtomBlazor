using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor;

/// <summary>
/// 用于正文文本内联链接的带样式锚点。
/// </summary>
/// <remarks>
/// <para>
/// 这是 Button 组件的内联对应物。采用 Link 变体（<see cref="ButtonVariant.Link"/>）的按钮看起来像链接，
/// 却保留按钮的高度和内边距，会打断段落的行文节奏；本组件渲染的是贴合文字基线的裸锚点。
/// 动作请交给按钮，导航请使用本组件。
/// </para>
/// <para>
/// 特性：
/// - 四种着色方案，其中一种继承周围文字颜色
/// - 下划线可选始终、悬停时或从不显示
/// - 焦点环跟随文字跨行延伸
/// - 在新标签页打开的链接自动获得 <c>rel="noopener noreferrer"</c>
/// - 可选的外部链接图标，附带屏幕阅读器提示
/// </para>
/// <para>
/// 下划线是唯一能经受色觉障碍阅读的提示，因此在正文中应当避免使用
/// <see cref="LinkUnderline.None"/>。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Read the &lt;Link Href="/guides/rtl"&gt;right-to-left guide&lt;/Link&gt; first.
///
/// &lt;Link Href="https://example.com" Target="_blank" ShowExternalIcon="true"&gt;Example&lt;/Link&gt;
/// </code>
/// </example>
public partial class Link : ComponentBase
{
    /// <summary>
    /// 获取或设置链接的目标 URL。
    /// </summary>
    [Parameter, EditorRequired]
    public string? Href { get; set; }

    /// <summary>
    /// 获取或设置锚点的 <c>target</c>，例如 <c>_blank</c>。
    /// </summary>
    [Parameter]
    public string? Target { get; set; }

    /// <summary>
    /// 获取或设置锚点的 <c>rel</c>。未设置且 <see cref="Target"/> 为 <c>_blank</c> 时，
    /// 默认取 <c>noopener noreferrer</c>，阻止被打开的页面反向操作本页。
    /// </summary>
    [Parameter]
    public string? Rel { get; set; }

    /// <summary>
    /// 获取或设置着色方案。
    /// </summary>
    [Parameter]
    public LinkVariant Variant { get; set; } = LinkVariant.Default;

    /// <summary>
    /// 获取或设置下划线的显示时机。
    /// </summary>
    [Parameter]
    public LinkUnderline Underline { get; set; } = LinkUnderline.Hover;

    /// <summary>
    /// 获取或设置是否在文字后追加外部链接图标。图标带有屏幕阅读器提示，
    /// 使目标被朗读出来而非仅仅画出来。
    /// </summary>
    [Parameter]
    public bool ShowExternalIcon { get; set; }

    /// <summary>
    /// 获取或设置链接是否禁用。禁用的链接会去掉 <c>href</c>，
    /// 因此既无法跳转也无法用键盘聚焦。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置链接被点击时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// 获取或设置链接文字。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到锚点的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到锚点的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    /// <summary>
    /// 打开到别处的链接会自动获得 <c>noopener noreferrer</c>，
    /// 除非使用者自己设置了 <c>rel</c>。
    /// </summary>
    private string? EffectiveRel => Rel ?? (Target == "_blank" ? "noopener noreferrer" : null);

    private async Task HandleClickAsync(MouseEventArgs args)
    {
        if (Disabled)
        {
            return;
        }

        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(args);
        }
    }

    private string CssClass => ClassNames.cn(
        // decoration-from-font 让下划线避开字母下伸部；焦点环沿文字而非方框绘制，
        // 于是句中换行的链接看起来仍然是一个完整的链接。
        "atom:inline atom:items-baseline atom:gap-1 atom:rounded-sm atom:underline-offset-4",
        "atom:decoration-from-font atom:transition-colors",
        "atom:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        Variant switch
        {
            LinkVariant.Inherit => "atom:text-inherit",
            LinkVariant.Muted => "atom:text-muted-foreground atom:hover:text-foreground",
            LinkVariant.Destructive => "atom:text-destructive atom:hover:text-destructive/80",
            _ => "atom:text-primary atom:hover:text-primary/80"
        },
        Underline switch
        {
            LinkUnderline.Always => "atom:underline",
            LinkUnderline.None => "atom:no-underline",
            _ => "atom:no-underline atom:hover:underline atom:focus-visible:underline"
        },
        Disabled ? "atom:pointer-events-none atom:cursor-not-allowed atom:opacity-50" : "atom:cursor-pointer",
        Class
    );
}
