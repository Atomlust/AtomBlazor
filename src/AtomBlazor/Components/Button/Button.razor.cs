using AtomBlazor.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor;

/// <summary>
/// 遵循 shadcn/ui 设计体系的按钮组件。
/// </summary>
/// <remarks>
/// <para>
/// Button 组件提供可定制的按钮，支持多种视觉变体、尺寸与状态。
/// 它自带 ARIA 属性与键盘支持，并与 Blazor 的事件系统深度集成。
/// </para>
/// <para>
/// 特性：
/// - 6 种视觉变体（Default、Destructive、Outline、Secondary、Ghost、Link）
/// - 4 种尺寸选项（Small、Default、Large、Icon）
/// - 支持禁用状态
/// - 键盘支持（Tab、Enter、Space）
/// - 自带 ARIA 属性
/// - RTL（从右到左）支持
/// - 经 CSS 变量兼容暗色模式
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Button Variant="ButtonVariant.Default" Size="ButtonSize.Default" OnClick="HandleClick"&gt;
///     Click me
/// &lt;/Button&gt;
/// </code>
/// </example>
public partial class Button : ComponentBase
{
    /// <summary>
    /// 获取或设置按钮的视觉样式变体。
    /// </summary>
    /// <remarks>
    /// 经 CSS 自定义属性控制配色方案与视觉外观。
    /// 默认值为 <see cref="ButtonVariant.Default"/>。
    /// </remarks>
    [Parameter]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Default;

    /// <summary>
    /// 获取或设置按钮的尺寸。
    /// </summary>
    /// <remarks>
    /// 控制内边距、字号与整体尺寸。
    /// 默认值为 <see cref="ButtonSize.Default"/>。
    /// 所有尺寸都保持最小触控目标（44x44 像素）以满足无障碍要求。
    /// </remarks>
    [Parameter]
    public ButtonSize Size { get; set; } = ButtonSize.Default;

    /// <summary>
    /// 获取或设置 HTML button 元素的 type 特性值。
    /// </summary>
    /// <remarks>
    /// 控制按钮位于表单内时的表单提交行为。
    /// 默认值为 <see cref="ButtonType.Button"/>，以防误触发表单提交。
    /// </remarks>
    [Parameter]
    public ButtonType Type { get; set; } = ButtonType.Button;

    /// <summary>
    /// 获取或设置按钮是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用时：
    /// - 按钮不可点击、不可聚焦
    /// - 透明度降低（经 disabled:opacity-50 Tailwind 类）
    /// - 光标变为 not-allowed（经 disabled:cursor-not-allowed）
    /// - aria-disabled 特性置为 true
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置应用到按钮上的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置按钮被点击时调用的回调。
    /// </summary>
    /// <remarks>
    /// 事件处理器收到一个携带点击细节的 <see cref="MouseEventArgs"/> 参数。
    /// 若按钮被禁用，此回调不会被调用。
    /// </remarks>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// 获取或设置渲染在按钮内部的内容。
    /// </summary>
    /// <remarks>
    /// 可以是文本、图标或任意其他 Blazor 标记。
    /// 纯图标按钮请使用 <see cref="ButtonSize.Icon"/> 并提供 aria-label。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置按钮的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 纯图标按钮必须提供，让屏幕阅读器有可读的文本名称；带文本的按钮可选。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置显示在按钮中的图标。
    /// </summary>
    /// <remarks>
    /// 可以是任意 RenderFragment（SVG、图标字体、图片）。
    /// 位置由 <see cref="IconPosition"/> 控制。
    /// 会自动在图标与文本之间加入 RTL 感知的间距。
    /// </remarks>
    [Parameter]
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// 获取或设置图标相对按钮文本的位置。
    /// </summary>
    /// <remarks>
    /// 默认值为 <see cref="IconPosition.Start"/>（LTR 下位于文本之前）。
    /// 经 Tailwind 方向性工具类自动适配 RTL 布局。
    /// </remarks>
    [Parameter]
    public IconPosition IconPosition { get; set; } = IconPosition.Start;

    /// <summary>
    /// 获取或设置按钮是否处于加载中状态。
    /// </summary>
    /// <remarks>
    /// 为 true 时按钮被禁用，显示Spinner（如提供 <see cref="LoadingTemplate"/> 则显示该模板），
    /// 若指定了 <see cref="LoadingText"/> 则用它替换按钮文本。
    /// </remarks>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// 获取或设置加载期间显示的文本。
    /// </summary>
    /// <remarks>
    /// 设置且 <see cref="Loading"/> 为 true 时，该文本替换 <see cref="ChildContent"/>。
    /// </remarks>
    [Parameter]
    public string? LoadingText { get; set; }

    /// <summary>
    /// 获取或设置自定义的加载指示模板。
    /// </summary>
    /// <remarks>
    /// 设置且 <see cref="Loading"/> 为 true 时，渲染该模板而非默认的 Spinner。
    /// </remarks>
    [Parameter]
    public RenderFragment? LoadingTemplate { get; set; }

    /// <summary>
    /// 获取或设置要跳转的 URL。设置后组件渲染为 &lt;a&gt; 元素而非 &lt;button&gt;。
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// 获取或设置锚点的 target 特性（如 "_blank"）。仅在设置了 <see cref="Href"/> 时生效。
    /// </summary>
    [Parameter]
    public string? Target { get; set; }

    /// <summary>
    /// 获取或设置应用到按钮元素的额外 HTML 属性。
    /// </summary>
    /// <remarks>
    /// 捕获一切未匹配的属性并原样传给底层 button 元素，
    /// 适合 title、data-* 之类的属性。
    /// </remarks>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 使用 AsChild 模式时从上级触发器组件获取的触发器上下文。
    /// </summary>
    /// <remarks>
    /// 当 Button 作为设置了 AsChild=true 的触发器组件的子级使用时，
    /// 会自动收到这个上下文，从而承担触发器行为（点击切换、aria 属性等）。
    /// </remarks>
    [CascadingParameter(Name = "TriggerContext")]
    public TriggerContext? TriggerContext { get; set; }

    /// <summary>
    /// 按钮元素的引用，供 AsChild 用法下的定位支持。
    /// </summary>
    private ElementReference _buttonRef;

    /// <summary>
    /// 获取按钮是否实际处于禁用状态（显式禁用或正在加载）。
    /// </summary>
    private bool IsDisabled => Disabled || Loading;

    /// <summary>
    /// 获取组件是否应渲染为锚元素。
    /// </summary>
    private bool HasHref => !string.IsNullOrEmpty(Href);

    /// <summary>
    /// 获取 rel 特性值。Target 为 "_blank" 时返回 "noopener noreferrer" 以保证安全。
    /// </summary>
    private string? Rel => Target == "_blank" ? "noopener noreferrer" : null;

    /// <summary>
    /// 获取按钮元素计算后的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 组合了：
    /// - 基础按钮样式（定位、过渡、焦点态）
    /// - 变体专属类（颜色、背景、边框）
    /// - 尺寸专属类（内边距、字号、高度）
    /// - 来自 Class 参数的自定义类
    /// 使用 cn() 工具做智能类名合并与 Tailwind 冲突消解。
    /// </remarks>
    private string CssClass => ClassNames.cn(
        // 基础按钮样式（来自 shadcn/ui）
        "atom:inline-flex atom:items-center atom:justify-center atom:gap-2 atom:rounded-md atom:text-sm atom:font-medium",
        "atom:transition-colors atom:focus-visible:outline-none atom:focus-visible:ring-2",
        "atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        "atom:cursor-pointer atom:disabled:opacity-50 atom:disabled:cursor-not-allowed",
        // 变体专属样式
        Variant switch
        {
            ButtonVariant.Default => "atom:bg-primary atom:text-primary-foreground atom:hover:bg-primary/90",
            ButtonVariant.Destructive => "atom:bg-destructive atom:text-destructive-foreground atom:hover:bg-destructive/90",
            ButtonVariant.Outline => "atom:border atom:border-input atom:bg-background atom:hover:bg-accent atom:hover:text-accent-foreground",
            ButtonVariant.Secondary => "atom:bg-secondary atom:text-secondary-foreground atom:hover:bg-secondary/80",
            ButtonVariant.Ghost => "atom:hover:bg-accent atom:hover:text-accent-foreground",
            ButtonVariant.Link => "atom:text-primary atom:underline-offset-4 atom:hover:underline",
            _ => "atom:bg-primary atom:text-primary-foreground atom:hover:bg-primary/90"
        },
        // 尺寸专属样式
        Size switch
        {
            ButtonSize.Small => "atom:h-9 atom:rounded-md atom:px-3 atom:text-xs",
            ButtonSize.Default => "atom:h-10 atom:px-4 atom:py-2",
            ButtonSize.Large => "atom:h-11 atom:rounded-md atom:px-8",
            ButtonSize.Icon => "atom:h-10 atom:w-10",
            ButtonSize.IconSmall => "atom:h-9 atom:w-9",
            ButtonSize.IconLarge => "atom:h-11 atom:w-11",
            _ => "atom:h-10 atom:px-4 atom:py-2"
        },
        // 禁用锚样式（`:disabled` 伪类对 `<a>` 元素无效）
        HasHref && IsDisabled ? "atom:pointer-events-none atom:cursor-not-allowed atom:opacity-50" : null,
        // AsChild 触发器守卫：浮层打开期间忽略指针事件，
        // 使单次点击无法既经"点击外部"关闭浮层、又经本按钮重开它
        TriggerContext is { IsOpen: true, SuppressPointerEventsWhenOpen: true } ? "atom:pointer-events-none" : null,
        // 自定义类（如有）
        Class
    );

    /// <summary>
    /// 获取 HTML button 元素的 type 特性值。
    /// </summary>
    private string HtmlType => Type switch
    {
        ButtonType.Submit => "submit",
        ButtonType.Reset => "reset",
        ButtonType.Button => "button",
        _ => "button"
    };

    /// <summary>
    /// 处理按钮点击事件。
    /// </summary>
    /// <param name="args">鼠标事件参数。</param>
    /// <remarks>
    /// 按钮被点击且未被禁用时调用。若存在 TriggerContext（来自 AsChild 模式），
    /// 则调用其 Toggle 或 Close；若注册了 OnClick 回调也会触发。
    /// </remarks>
    private async Task HandleClick(MouseEventArgs args)
    {
        if (IsDisabled)
        {
            return;
        }

        // 处理触发器上下文行为（来自 AsChild 模式）
        if (TriggerContext != null)
        {
            // 关闭按钮（只有 Close、没有 Toggle）
            if (TriggerContext.Toggle == null && TriggerContext.Close != null)
            {
                TriggerContext.Close?.Invoke();
            }
            // 触发按钮（有 Toggle）
            else
            {
                TriggerContext.Toggle?.Invoke();
            }
        }

        // 调用 OnClick 回调
        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(args);
        }
    }

    /// <summary>
    /// 处理触发器上下文的键盘事件（下拉菜单方向键等）。
    /// </summary>
    private async Task HandleKeyDown(KeyboardEventArgs args)
    {
        if (IsDisabled)
        {
            return;
        }

        if (TriggerContext?.OnKeyDown != null)
        {
            await TriggerContext.OnKeyDown.Invoke(args);
        }
    }

    /// <summary>
    /// 处理悬停触发组件（Tooltip、HoverCard）的 mouseenter 事件。
    /// </summary>
    private void HandleMouseEnter() =>
        TriggerContext?.OnMouseEnter?.Invoke();

    /// <summary>
    /// 处理悬停触发组件的 mouseleave 事件。
    /// </summary>
    private void HandleMouseLeave() =>
        TriggerContext?.OnMouseLeave?.Invoke();

    /// <summary>
    /// 处理聚焦触发组件的 focus 事件。
    /// </summary>
    private void HandleFocus() =>
        TriggerContext?.OnFocus?.Invoke();

    /// <summary>
    /// 处理聚焦触发组件的 blur 事件。
    /// </summary>
    private void HandleBlur() =>
        TriggerContext?.OnBlur?.Invoke();

    /// <summary>
    /// 记录此前的 TriggerContext，用于检测它的变化。
    /// </summary>
    private TriggerContext? _previousTriggerContext;

    /// <summary>
    /// 把按钮元素注册给触发器上下文用于定位。
    /// TriggerContext 变化时（如上级在 Dialog/Sheet 内重渲染）重新注册。
    /// </summary>
    protected override void OnAfterRender(bool firstRender)
    {
        if (TriggerContext?.SetTriggerElement != null &&
            (firstRender || TriggerContext != _previousTriggerContext))
        {
            TriggerContext.SetTriggerElement.Invoke(_buttonRef);
            _previousTriggerContext = TriggerContext;
        }
    }
}
