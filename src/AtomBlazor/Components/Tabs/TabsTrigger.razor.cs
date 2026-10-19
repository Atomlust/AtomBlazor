#pragma warning disable BL0012 // 与一致保留四处显式 StateHasChanged：重命名编辑器的开合须在按键当场落定，不押注于事件管线末尾的自动重渲染（照 CopyText/ExitPrompt 先例；采文件级抑制以免逐点 pragma 打散与逐行对齐的行位）
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>
/// <see cref="TabsList"/> 中的一个标签。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Closable"/>、<see cref="Renamable"/> 与 <see cref="Reorderable"/> 默认全部为
/// <see langword="null"/>，并回落到列表所说的值。在这里设一个，即可就这一个标签覆写列表——
/// 最常见的理由是一个无人可以关闭的固定标签。
/// </para>
/// <para>
/// 三者都不往任何东西里写入。它们各自通过回调发出请求
/// 调用方再改动自己的集合，理由与 Gantt 相同：
/// 只有调用方知道顺序与名字实际存在哪里。
/// </para>
/// </remarks>
public partial class TabsTrigger : IDisposable
{
    private bool isRenaming;
    private string renameText = string.Empty;
    private string renameStartedFrom = string.Empty;
    private bool focusRenameInput;
    private ElementReference renameInput;

    /// <summary>
    /// 标识本标签页的值。
    /// 用于与 TabsContent 组件配对。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 本标签的显示文本。
    /// </summary>
    /// <remarks>
    /// 用于响应式 Select 兜底、作为重命名开始时的初始文本
    /// 以及在未提供 <see cref="ChildContent"/> 时作为标签自己的内容。
    /// 回落到 <see cref="Value"/>。
    /// 开着 <see cref="Renamable"/> 的标签希望把本项设上——重命名标记不是一件能做到的事。
    /// </remarks>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// 本标签触发器是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 本标签是否可以关闭。为 null 时跟随列表。
    /// </summary>
    [Parameter]
    public bool? Closable { get; set; }

    /// <summary>
    /// 本标签是否可以就地重命名。为 null 时跟随列表。
    /// </summary>
    [Parameter]
    public bool? Renamable { get; set; }

    /// <summary>
    /// 本标签是否可以拖到新位置。为 null 时跟随列表。
    /// </summary>
    [Parameter]
    public bool? Reorderable { get; set; }

    /// <summary>
    /// 在标签触发器内渲染的子内容。
    /// </summary>
    /// <remarks>留空时标签显示 <see cref="Label"/>。</remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 要应用到标签触发器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 父级 Components 层的 TabsList。
    /// </summary>
    [CascadingParameter]
    public TabsList? ComponentsList { get; set; }

    /// <summary>获取标签所显示的文本，也是重命名开始时的文本。</summary>
    internal string EffectiveLabel => Label ?? Value;

    private bool IsClosable => (Closable ?? ComponentsList?.Closable ?? false) && ComponentsList?.HasCloseHandler == true;

    private bool IsRenamable => (Renamable ?? ComponentsList?.Renamable ?? false) && ComponentsList?.HasRenameHandler == true;

    private bool IsReorderable => (Reorderable ?? ComponentsList?.Reorderable ?? false) && ComponentsList?.HasMoveHandler == true;

    /// <inheritdoc />
    protected override void OnParametersSet() =>
        ComponentsList?.RegisterResponsiveTrigger(Value, EffectiveLabel);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!focusRenameInput)
        {
            return;
        }

        focusRenameInput = false;

        try
        {
            await renameInput.FocusAsync();
            // 选中整个名字，因为重命名通常是替换它而不是编辑它。
            await (ComponentsList?.SelectRenameTextAsync(renameInput) ?? Task.CompletedTask);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            // 电路断开或元素已经消失，忽略。
        }
        catch (InvalidOperationException)
        {
            // 没有可供聚焦穿越的 JavaScript——预渲染，或根本没有浏览器的宿主。
            // 编辑器仍然开着且可用，只是不会自己取得焦点。
        }
    }

    private string CssClass => ClassNames.cn(
        "atom:inline-flex atom:items-center atom:justify-center atom:whitespace-nowrap atom:rounded-sm atom:px-3 atom:py-1.5",
        "atom:text-sm atom:font-medium atom:ring-offset-background atom:transition-all",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        "atom:disabled:pointer-events-none atom:disabled:opacity-50",
        "atom:data-[state=active]:bg-background atom:data-[state=active]:text-foreground atom:data-[state=active]:shadow-sm",
        // 关闭图标坐在标签内部，所以标签需要在末端一侧为它留出空间。
        IsClosable ? "atom:gap-1 atom:pe-1.5" : null,
        IsReorderable ? "atom:cursor-grab atom:active:cursor-grabbing" : null,
        Class
    );

    // 是一个 span 而不是 button：role="tab" 不得包含另一个可交互元素。
    // 它带 aria-hidden，因为 Delete 已经让屏幕阅读器得到同一个动作，而且是在标签本身上
    // 播报，而非作为其内部的第二个停靠点。
    private static string CloseCssClass => ClassNames.cn(
        "atom:inline-flex atom:size-4 atom:shrink-0 atom:cursor-pointer atom:items-center atom:justify-center",
        "atom:rounded-xs atom:opacity-60 atom:transition-opacity",
        "atom:hover:bg-muted-foreground/20 atom:hover:opacity-100"
    );

    // 尺寸与被它取代的标签一致，好让编辑框打开时列表不跳动。
    private string RenameCssClass => ClassNames.cn(
        "atom:inline-flex atom:h-full atom:rounded-sm atom:bg-background atom:px-3 atom:py-1.5",
        "atom:text-sm atom:font-medium atom:text-foreground atom:shadow-sm",
        "atom:outline-none atom:ring-2 atom:ring-ring",
        // 宽度足以放下一个短表名而又不撑开列表。更长的名字在框内滚动
        // 而不是在输入过程中把其他所有标签挤得移位。
        "atom:w-32 atom:max-w-full",
        Class
    );

    private async Task HandleDoubleClickAsync()
    {
        if (IsRenamable)
        {
            await BeginRenameAsync();
        }
    }

    private Task BeginRenameAsync()
    {
        if (!IsRenamable || Disabled || isRenaming)
        {
            return Task.CompletedTask;
        }

        isRenaming = true;
        renameStartedFrom = EffectiveLabel;
        renameText = renameStartedFrom;
        focusRenameInput = true;
        StateHasChanged();

        return Task.CompletedTask;
    }

    private async Task HandleRenameKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await CommitRenameAsync();
            return;
        }

        if (args.Key == "Escape")
        {
            // 先清空，以免重渲染随后触发的 blur 提交已输入的内容。
            isRenaming = false;
            renameText = string.Empty;
            StateHasChanged();
        }
    }

    private async Task CommitRenameAsync()
    {
        if (!isRenaming)
        {
            return;
        }

        isRenaming = false;
        var typed = renameText.Trim();
        renameText = string.Empty;

        // 空盒子是一次取消，而不是请求一个无名的标签。
        if (typed.Length == 0 || string.Equals(typed, renameStartedFrom, StringComparison.Ordinal))
        {
            StateHasChanged();
            return;
        }

        var refused = ComponentsList is not null
            && await ComponentsList.RequestRenameAsync(Value, renameStartedFrom, typed);

        if (refused)
        {
            // 重新打开，且已输入的内容仍在其中。因为一个字符冲突就要重打整个长名字
            // 正是那种让人从此不再重命名的小折磨。
            isRenaming = true;
            renameText = typed;
            focusRenameInput = true;
        }

        StateHasChanged();
    }

    private Task HandleCloseClickAsync() => RequestCloseAsync();

    private Task RequestCloseAsync() =>
        IsClosable && !Disabled
            ? ComponentsList?.RequestCloseAsync(Value) ?? Task.CompletedTask
            : Task.CompletedTask;

    private Task HandleKeyboardMoveAsync(int delta) =>
        IsReorderable && !Disabled
            ? ComponentsList?.RequestMoveAsync(Value, delta) ?? Task.CompletedTask
            : Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        ComponentsList?.UnregisterResponsiveTrigger(Value);
        GC.SuppressFinalize(this);
    }
}
