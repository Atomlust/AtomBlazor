using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor;

/// <summary>
/// 一个紧凑的胶囊形元素，用于表示属性、过滤器或选择项，可被选中、移除，或两者兼有。
/// </summary>
/// <remarks>
/// <para>
/// 芯片与 <see cref="Badge"/> 的区别在于它是交互式的：携带选中态、移除按钮或点击回调。
/// Badge 只用于标注。
/// </para>
/// <para>
/// 特性：
/// - 五种变体、三种尺寸，各带选中形态
/// - 可选的移除按钮，携带本地化的可访问名称
/// - 选中态可选的对勾
/// - 选择既可本地驱动（<c>@bind-Selected</c>），也可由父级
///   <see cref="ChipSet{TValue}"/> 通过 <see cref="Value"/> 驱动
/// - 非交互时渲染为普通 span，静态芯片不会被读作按钮
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Chip Dismissible="true" OnDismiss="Remove"&gt;Design&lt;/Chip&gt;
///
/// &lt;Chip Selectable="true" @bind-Selected="showArchived"&gt;Archived&lt;/Chip&gt;
/// </code>
/// </example>
public partial class Chip : ComponentBase, IDisposable
{
    private bool? localSelected;

    /// <summary>
    /// 获取或设置视觉风格变体。回退到父级 <see cref="ChipSet{TValue}"/>，
    /// 再回退到 <see cref="ChipVariant.Default"/>。
    /// </summary>
    [Parameter]
    public ChipVariant? Variant { get; set; }

    /// <summary>
    /// 获取或设置芯片尺寸。回退到父级 <see cref="ChipSet{TValue}"/>，
    /// 再回退到 <see cref="ChipSize.Default"/>。
    /// </summary>
    [Parameter]
    public ChipSize? Size { get; set; }

    /// <summary>
    /// 获取或设置芯片内容，通常是简短标签。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置前导内容，通常是图标或头像。
    /// </summary>
    [Parameter]
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// 获取或设置该芯片向父级 <see cref="ChipSet{TValue}"/> 贡献的值。
    /// 集合内的芯片要参与选择就必须提供值。
    /// </summary>
    [Parameter]
    public object? Value { get; set; }

    /// <summary>
    /// 获取或设置芯片是否在点击时切换自身的选中状态。
    /// 在 <see cref="ChipSet{TValue}"/> 内不需要——集合自身驱动选择。
    /// </summary>
    [Parameter]
    public bool Selectable { get; set; }

    /// <summary>
    /// 获取或设置芯片是否选中。支持 <c>@bind-Selected</c> 双向绑定。
    /// 当父级 <see cref="ChipSet{TValue}"/> 持有选择时被忽略。
    /// </summary>
    [Parameter]
    public bool Selected { get; set; }

    /// <summary>
    /// 获取或设置在芯片自身选中状态变化时调用的回调。与 <c>@bind-Selected</c> 搭配使用。
    /// </summary>
    [Parameter]
    public EventCallback<bool> SelectedChanged { get; set; }

    /// <summary>
    /// 获取或设置选中时是否显示对勾。回退到父级 <see cref="ChipSet{TValue}"/>，再回退到 <c>false</c>。
    /// </summary>
    /// <remarks>
    /// 对勾出现时会让芯片变宽，导致换行的芯片行重排。请把它留给选中态需要经受
    /// 色觉障碍考验的集合。
    /// </remarks>
    [Parameter]
    public bool? ShowCheckMark { get; set; }

    /// <summary>
    /// 获取或设置芯片是否显示移除按钮。回退到父级 <see cref="ChipSet{TValue}"/>，再回退到 <c>false</c>。
    /// </summary>
    [Parameter]
    public bool? Dismissible { get; set; }

    /// <summary>
    /// 获取或设置在移除按钮被激活时调用的回调。把芯片从标记中移除是使用者的工作。
    /// 在 <see cref="ChipSet{TValue}"/> 内，集合自身的移除回调也会执行。
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnDismiss { get; set; }

    /// <summary>
    /// 获取或设置移除按钮的可访问名称。默认为本地化的 <c>Chip.Dismiss</c> 字符串。
    /// </summary>
    [Parameter]
    public string? DismissLabel { get; set; }

    /// <summary>
    /// 获取或设置芯片是否禁用。禁用的芯片不能选中也不能移除。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置在点击芯片主体时调用的回调。在任何选择变化之后运行，
    /// 并使芯片自身可交互。
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// 获取或设置应用到芯片上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [CascadingParameter]
    private ChipSetContext? SetContext { get; set; }

    // Localizer 由 Chip.razor 的 @inject ILocalizer Localizer 提供，
    // 与源项目一致（源为 @inject IBbLocalizer）。

    // --- 有效状态 ---

    private ChipVariant EffectiveVariant => Variant ?? SetContext?.Variant ?? ChipVariant.Default;

    private ChipSize EffectiveSize => Size ?? SetContext?.Size ?? ChipSize.Default;

    private bool EffectiveDismissible => Dismissible ?? SetContext?.Dismissible ?? false;

    private bool EffectiveShowCheckMark => ShowCheckMark ?? SetContext?.ShowCheckMark ?? false;

    private bool IsDisabled => Disabled || (SetContext?.Disabled ?? false);

    private string EffectiveDismissLabel => DismissLabel ?? Localizer["Chip.Dismiss"];

    /// <summary>父集合是否持有本芯片的选中状态。</summary>
    private bool IsSetDriven =>
        SetContext is { SelectionMode: not ChipSelectionMode.None } && Value is not null;

    private bool IsSelectable => IsSetDriven || Selectable || SelectedChanged.HasDelegate;

    private bool IsInteractive => IsSelectable || OnClick.HasDelegate;

    private bool IsSelected => IsSetDriven
        ? SetContext!.IsSelected(Value)
        : SelectedChanged.HasDelegate ? Selected : localSelected ?? Selected;

    private string StateAttribute => IsSelected ? "on" : "off";

    private string? PressedAttribute => IsSelectable ? (IsSelected ? "true" : "false") : null;

    // --- 生命周期 ---

    /// <inheritdoc />
    protected override void OnInitialized() => SetContext?.Register(this);

    /// <summary>
    /// 父集合改变选择后重渲染芯片。级联的上下文是固定的，
    /// 因此由集合通知芯片，而不是靠框架。
    /// </summary>
    internal void NotifySetStateChanged() => StateHasChanged();

    /// <inheritdoc />
    public void Dispose()
    {
        SetContext?.Unregister(this);
        GC.SuppressFinalize(this);
    }

    // --- 事件 ---

    private async Task HandleActivateAsync(MouseEventArgs args)
    {
        if (IsDisabled)
        {
            return;
        }

        if (IsSetDriven)
        {
            await SetContext!.ToggleAsync(Value);
        }
        else if (IsSelectable)
        {
            var next = !IsSelected;
            localSelected = next;

            if (SelectedChanged.HasDelegate)
            {
                await SelectedChanged.InvokeAsync(next);
            }
        }

        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(args);
        }
    }

    private async Task HandleDismissAsync(MouseEventArgs args)
    {
        if (IsDisabled)
        {
            return;
        }

        if (OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync(args);
        }

        if (SetContext is not null && Value is not null)
        {
            await SetContext.DismissAsync(Value);
        }
    }

    // --- CSS ---

    private string RootCssClass => ClassNames.cn(
        "atom:inline-flex atom:max-w-full atom:items-center atom:rounded-full atom:border atom:align-middle",
        "atom:font-medium atom:transition-colors",
        EffectiveSize switch
        {
            ChipSize.Small => "atom:h-6 atom:text-xs",
            ChipSize.Large => "atom:h-10 atom:text-sm",
            _ => "atom:h-8 atom:text-sm"
        },
        VariantCssClass,
        HoverCssClass,
        IsDisabled ? "atom:opacity-50" : null,
        Class
    );

    private string VariantCssClass => (EffectiveVariant, IsSelected) switch
    {
        (ChipVariant.Default, true) => "atom:border-transparent atom:bg-primary atom:text-primary-foreground",
        (ChipVariant.Default, false) => "atom:border-transparent atom:bg-secondary atom:text-secondary-foreground",
        (ChipVariant.Secondary, true) => "atom:border-primary/30 atom:bg-primary/15 atom:text-primary",
        (ChipVariant.Secondary, false) => "atom:border-transparent atom:bg-muted atom:text-muted-foreground",
        (ChipVariant.Outline, true) => "atom:border-primary atom:bg-accent atom:text-accent-foreground",
        (ChipVariant.Outline, false) => "atom:border-input atom:bg-transparent atom:text-foreground",
        (ChipVariant.Destructive, true) => "atom:border-transparent atom:bg-destructive atom:text-destructive-foreground",
        (ChipVariant.Destructive, false) => "atom:border-transparent atom:bg-destructive/10 atom:text-destructive",
        (ChipVariant.Soft, true) => "atom:border-transparent atom:bg-primary atom:text-primary-foreground",
        (ChipVariant.Soft, false) => "atom:border-transparent atom:bg-primary/10 atom:text-primary",
        _ => "atom:border-transparent atom:bg-secondary atom:text-secondary-foreground"
    };

    private string? HoverCssClass => !IsInteractive || IsDisabled
        ? null
        : (EffectiveVariant, IsSelected) switch
        {
            (ChipVariant.Default, true) => "atom:hover:bg-primary/90",
            (ChipVariant.Default, false) => "atom:hover:bg-secondary/80",
            (ChipVariant.Secondary, true) => "atom:hover:bg-primary/25",
            (ChipVariant.Secondary, false) => "atom:hover:bg-muted/70",
            (ChipVariant.Outline, true) => "atom:hover:bg-accent/80",
            (ChipVariant.Outline, false) => "atom:hover:bg-accent atom:hover:text-accent-foreground",
            (ChipVariant.Destructive, true) => "atom:hover:bg-destructive/90",
            (ChipVariant.Destructive, false) => "atom:hover:bg-destructive/20",
            (ChipVariant.Soft, true) => "atom:hover:bg-primary/90",
            (ChipVariant.Soft, false) => "atom:hover:bg-primary/20",
            _ => null
        };

    private string BodyCssClass => ClassNames.cn(
        "atom:inline-flex atom:min-w-0 atom:items-center atom:gap-1.5 atom:self-stretch atom:rounded-full",
        "atom:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        "atom:focus-visible:ring-offset-background",
        (EffectiveSize, EffectiveDismissible) switch
        {
            (ChipSize.Small, true) => "atom:ps-2 atom:pe-1",
            (ChipSize.Small, false) => "atom:px-2",
            (ChipSize.Large, true) => "atom:ps-4 atom:pe-2",
            (ChipSize.Large, false) => "atom:px-4",
            (_, true) => "atom:ps-3 atom:pe-1.5",
            (_, false) => "atom:px-3"
        },
        IsInteractive && !IsDisabled ? "atom:cursor-pointer" : null
    );

    private string DismissCssClass => ClassNames.cn(
        "atom:inline-flex atom:shrink-0 atom:items-center atom:justify-center atom:rounded-full atom:transition-colors",
        "atom:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        EffectiveSize switch
        {
            ChipSize.Small => "atom:me-1 atom:h-4 atom:w-4",
            ChipSize.Large => "atom:me-2 atom:h-6 atom:w-6",
            _ => "atom:me-1.5 atom:h-5 atom:w-5"
        },
        IsDisabled ? null : "atom:cursor-pointer atom:hover:bg-foreground/10"
    );

    private string IconCssClass => EffectiveSize switch
    {
        ChipSize.Small => "atom:h-3 atom:w-3",
        ChipSize.Large => "atom:h-4 atom:w-4",
        _ => "atom:h-3.5 atom:w-3.5"
    };

    private string CheckMarkCssClass => ClassNames.cn("atom:shrink-0", IconCssClass);

    private string DismissIconCssClass => IconCssClass;
}
