using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 声明一个可在 <see cref="Dock"/> 内停靠、加标签、浮动与关闭的面板。
/// 面板自身不渲染标记；其 <see cref="ChildContent"/> 由父 dock 提升到
/// 活动标签组中渲染。
/// </summary>
public partial class DockPanel : ComponentBase, IDisposable
{
    [CascadingParameter]
    private Dock? Dock { get; set; }

    /// <summary>
    /// 面板的稳定唯一标识。必须提供，dock 才能在布局变化
    /// （停靠、浮动、关闭与重开）过程中持续跟踪该面板。
    /// </summary>
    [Parameter, EditorRequired]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 标签页上展示的标题。
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 标签页标题之前渲染的可选图标。
    /// </summary>
    [Parameter]
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// dock 首次构建布局时面板被放置的区域。
    /// 共享同一区域的面板会被归入同一个标签组。
    /// </summary>
    [Parameter]
    public DockZone Region { get; set; } = DockZone.Center;

    /// <summary>
    /// 控制初始布局构建时面板之间的相对排序。
    /// 值越小，在同一区域内越靠前。
    /// </summary>
    [Parameter]
    public int Order { get; set; }

    /// <summary>
    /// 面板是否可由用户关闭。默认为 <c>true</c>。
    /// </summary>
    [Parameter]
    public bool Closable { get; set; } = true;

    /// <summary>
    /// 面板是否可被摘出为浮动窗口。默认为 <c>true</c>。
    /// 当 <see cref="Locked"/> 为 <c>true</c> 时该项被忽略。
    /// </summary>
    [Parameter]
    public bool CanFloat { get; set; } = true;

    /// <summary>
    /// 为 <c>true</c> 时面板被锁死在位：不可拖动移位、重排或重停靠，
    /// 不可摘出为浮动窗口，也不可关闭。适合工具箱这类常驻区域。
    /// 会覆盖 <see cref="Closable"/> 与 <see cref="CanFloat"/>。默认为 <c>false</c>。
    /// </summary>
    [Parameter]
    public bool Locked { get; set; }

    /// <summary>
    /// 面板最小宽度（像素）。调整浮动窗口大小时强制执行，
    /// 停靠时作为 CSS 约束施加。<c>null</c>（默认）表示不设下限。
    /// </summary>
    [Parameter]
    public int? MinWidth { get; set; }

    /// <summary>
    /// 面板最小高度（像素）。调整浮动窗口大小时强制执行，
    /// 停靠时作为 CSS 约束施加。<c>null</c>（默认）表示不设下限。
    /// </summary>
    [Parameter]
    public int? MinHeight { get; set; }

    /// <summary>
    /// 面板最大宽度（像素）。调整浮动窗口大小时强制执行，
    /// 停靠时作为 CSS 约束施加。<c>null</c>（默认）表示不设上限。
    /// </summary>
    [Parameter]
    public int? MaxWidth { get; set; }

    /// <summary>
    /// 面板最大高度（像素）。调整浮动窗口大小时强制执行，
    /// 停靠时作为 CSS 约束施加。<c>null</c>（默认）表示不设上限。
    /// </summary>
    [Parameter]
    public int? MaxHeight { get; set; }

    /// <summary>
    /// 面板作为活动标签时在其内部渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>用户是否可关闭本面板。锁定时为 <c>false</c>。</summary>
    internal bool CanClose => Closable && !Locked;

    /// <summary>用户是否可将本面板摘出为浮动窗口。锁定时为 <c>false</c>。</summary>
    internal bool CanDetach => CanFloat && !Locked;

    /// <summary>用户是否可拖动本面板以移位、重排或重停靠。锁定时为 <c>false</c>。</summary>
    internal bool CanMove => !Locked;

    private bool registered;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Dock is null)
        {
            throw new InvalidOperationException($"{nameof(DockPanel)} must be placed inside a {nameof(Dock)}.");
        }

        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException($"{nameof(DockPanel)} requires a non-empty {nameof(Id)}.");
        }

        Dock.RegisterPanel(this);
        registered = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (registered)
        {
            Dock?.UnregisterPanel(this);
            registered = false;
        }
    }
}
