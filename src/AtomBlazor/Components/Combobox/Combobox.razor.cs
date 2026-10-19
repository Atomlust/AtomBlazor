using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Linq.Expressions;
using AtomBlazor.Primitives;

namespace AtomBlazor;

/// <summary>
/// 可搜索的组合框组件，让用户从选项列表中过滤并选择一项。
/// 支持两种模式：Options 模式（数据驱动）与复合模式（ChildContent 内放 ComboboxItem 子组件）。
/// </summary>
/// <typeparam name="TValue">选中值的类型。</typeparam>
/// <remarks>
/// <para>
/// Combobox 组件把自动完成功能与下拉界面结合在一起。它内部组合了 Popover、Command
/// 与 Button 组件，提供可搜索的选择体验。
/// </para>
/// <para>
/// 特性：
/// - 泛型类型支持，绑定灵活
/// - @bind-Value 双向绑定
/// - 搜索/过滤功能（不区分大小写）
/// - 单选，带切换（toggle）行为
/// - 选中项显示对勾图标
/// - 无匹配时的空状态
/// - 键盘导航支持
/// - 无障碍：ARIA 属性、键盘支持
/// - 双模式：简单列表用 Options，富渲染用 ChildContent + ComboboxItem
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;!-- Options 模式 --&gt;
/// &lt;Combobox TValue="string"
///           Options="frameworkOptions"
///           @bind-Value="selectedFramework"
///           Placeholder="Select framework..."
///           SearchPlaceholder="Search framework..."
///           EmptyMessage="No framework found." /&gt;
///
/// &lt;!-- 复合模式 --&gt;
/// &lt;Combobox TValue="string" @bind-Value="selectedFramework"
///           Placeholder="Select framework..." SearchPlaceholder="Search..."&gt;
///     &lt;ComboboxItem TValue="string" Value="blazor" Text="Blazor"&gt;Blazor&lt;/ComboboxItem&gt;
/// &lt;/Combobox&gt;
/// </code>
/// </example>
public partial class Combobox<TValue> : ComponentBase
{
    /// <summary>控件的无障碍名称。</summary>
    [Parameter] public string? AriaLabel { get; set; }


    [Inject] private ILocalizer Localizer { get; set; } = default!;

    private FieldIdentifier _fieldIdentifier;
    private EditContext? _editContext;
    private static readonly Func<CommandItemMetadata, string, bool> passthroughFilter = (_, _) => true;
    private Func<CommandItemMetadata, string, bool>? _filterFunction;
    private string? _selectedDisplayTextCache;

    // ShouldRender 追踪字段
    private bool _parametersChanged;
    private bool _lastIsOpen;
    private IEnumerable<SelectOption<TValue>>? _lastOptions;
    private TValue? _lastValue;
    private bool _lastDisabled;
    private string _lastSearchQuery = string.Empty;

    // ShouldRender 的旁路：触发器需要为当前选中项拾取一个刚刚注册来的显示文本。
    // ComboboxItem 子组件会通过隐藏注册通道在初始渲染时注册自己的文本，但那发生得比
    // 触发器的首次渲染更晚——所以 RegisterItem 到达时"跳过渲染"的状态早已落定，
    // 必须强制一次后续渲染。
    private bool _triggerTextDirty;

    protected override bool ShouldRender()
    {
        if (_parametersChanged || _triggerTextDirty)
        {
            _parametersChanged = false;
            _triggerTextDirty = false;
            _lastIsOpen = _isOpen;
            _lastValue = Value;
            _lastDisabled = Disabled;
            _lastSearchQuery = SearchQuery;
            _lastOptions = Options;
            return true;
        }

        if (_isOpen != _lastIsOpen
            || !EqualityComparer<TValue>.Default.Equals(Value, _lastValue)
            || Disabled != _lastDisabled
            || SearchQuery != _lastSearchQuery
            || !ReferenceEquals(Options, _lastOptions))
        {
            _lastIsOpen = _isOpen;
            _lastValue = Value;
            _lastDisabled = Disabled;
            _lastSearchQuery = SearchQuery;
            _lastOptions = Options;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取或设置从父级 EditForm 级联而来的 EditContext。
    /// </summary>
    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// 获取或设置组合框中展示的选项集合。
    /// 提供时走 Options 模式（数据驱动）；为 null 时走复合模式（ChildContent）。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<TValue>>? Options { get; set; }

    /// <summary>
    /// 获取或设置复合模式的子内容。
    /// 把 ComboboxItem 组件作为子级以获得富渲染能力。
    /// 仅在 Options 为 null 时使用。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置当前选中的值。
    /// </summary>
    [Parameter]
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置选中值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式。
    /// </summary>
    [Parameter]
    public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置未选中任何项时按钮上显示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置在复合模式下已通过 <see cref="Value"/> 预选值、
    /// 但尚无匹配的 <c>ComboboxItem</c> 完成注册时，触发器显示的文本。
    /// </summary>
    /// <remarks>
    /// 复合项现在会在初始渲染时（经由隐藏注册通道）注册自己的文本，所以常见情形下
    /// 触发器无需本参数即可解析预绑定的 <see cref="Value"/>。仅当首次渲染的标记里
    /// 确实不存在匹配项时才设置它——例如选中值异步加载、之后才加入——让触发器在此期间
    /// 也能显示标题。Options 模式永远不需要它；它从 <see cref="Options"/> 集合同步解析文本。
    /// 已注册项的文本总是压过这个提示，所以重新注册（如父级更新了 Text）仍会纠正过期标题。
    /// </remarks>
    [Parameter]
    public string? SelectedItemText { get; set; }

    /// <summary>
    /// 获取或设置搜索输入框的占位文本。
    /// </summary>
    [Parameter]
    public string? SearchPlaceholder { get; set; }

    /// <summary>
    /// 获取或设置没有条目匹配搜索时显示的消息。
    /// </summary>
    [Parameter]
    public string? EmptyMessage { get; set; }

    private string EffectivePlaceholder => Placeholder ?? Localizer["Combobox.Placeholder"];
    private string EffectiveSearchPlaceholder => SearchPlaceholder ?? Localizer["Combobox.SearchPlaceholder"];
    private string EffectiveEmptyMessage => EmptyMessage ?? Localizer["Combobox.EmptyMessage"];

    /// <summary>
    /// 获取或设置当前搜索查询文本。
    /// 与 <see cref="SearchQueryChanged"/> 配合实现双向绑定，以响应过滤变化，
    /// 例如服务端过滤或加载额外数据。
    /// </summary>
    /// <remarks>
    /// 弹层关闭时，搜索查询被重置为 <see cref="string.Empty"/> 并调用
    /// <see cref="SearchQueryChanged"/>，让消费方为下一次打开重载默认数据集
    /// （例如初始 top-N 结果）。
    /// </remarks>
    [Parameter]
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置搜索查询变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> SearchQueryChanged { get; set; }

    /// <summary>
    /// 获取或设置追加到组合框容器上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置组合框是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置组合框是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置描述本组合框（无障碍用途）的元素 id 列表。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置弹层内容的宽度。
    /// </summary>
    /// <remarks>
    /// 默认 "w-[200px]"。可用 Tailwind 类覆盖。MatchTriggerWidth 为 true 时忽略。
    /// </remarks>
    [Parameter]
    public string PopoverWidth { get; set; } = "atom:w-[200px]";

    /// <summary>
    /// 获取或设置是否让下拉宽度与触发元素宽度一致。
    /// 为 true 时忽略 PopoverWidth。
    /// </summary>
    [Parameter]
    public bool MatchTriggerWidth { get; set; }

    /// <summary>
    /// 获取或设置在用户滚动到下拉列表底部附近时调用的回调。
    /// 用于无限滚动场景下加载额外条目（例如对大数据集分页）。
    /// </summary>
    /// <remarks>
    /// 通常与 <see cref="IsLoading"/> 搭配使用，防止重复请求，
    /// 并在拉取更多条目期间显示加载指示器。
    /// </remarks>
    [Parameter]
    public EventCallback OnLoadMore { get; set; }

    /// <summary>
    /// 获取或设置是否正在加载额外条目。
    /// 为 true 时在下拉列表底部显示加载指示器，并抑制 <see cref="OnLoadMore"/> 直到加载完成。
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    /// 获取或设置在所有条目加载完毕后显示于下拉列表底部的消息。
    /// 仅在 <see cref="IsLoading"/> 为 false 时显示。设为 <c>null</c> 或空字符串则隐藏。
    /// </summary>
    [Parameter]
    public string? EndOfListMessage { get; set; }

    /// <summary>
    /// 获取或设置下拉打开时应用到触发器的 CSS 类。
    /// 默认 <c>"bg-accent text-accent-foreground"</c>。
    /// 设为 <c>null</c> 或空字符串可禁用激活样式。
    /// </summary>
    [Parameter]
    public string? ActiveClass { get; set; } = "atom:bg-accent atom:text-accent-foreground";

    /// <summary>
    /// 追踪弹层当前是否打开。
    /// </summary>
    private bool _isOpen { get; set; }

    /// <summary>
    /// 对 CommandInput 的引用，用于焦点管理。
    /// </summary>
    // 搜索输入框的 id，同时交给 CommandInput 与弹层的 AutoFocusId，让输入框在
    // 揭示弹层的那次调用里就被聚焦。它过去是在弹层的 ready 回调里聚焦的——内容渲染
    // 完成后再一趟往返、然后睡 50 毫秒、再为 FocusAsync 跑一趟往返——每次打开都付这些。
    private readonly string _searchInputId = $"combobox-search-{Guid.NewGuid():N}";

    /// <summary>
    /// 追踪本次打开是否已完成聚焦。
    /// </summary>

    /// <summary>
    /// 下一次受控关闭是否应把焦点还给触发器。选择项（有意关闭）时置 true，打开时复位。
    /// 绑定到弹层的 RestoreFocusOnClose。
    /// </summary>
    private bool _restoreFocusOnClose;

    /// <summary>
    /// 复合模式下查找触发器显示文本的项文本注册表。
    /// </summary>
#pragma warning disable CS8714 // TValue 可能为 null，但字典键在实践中非空
    private readonly Dictionary<TValue, string> _itemTextRegistry = new(EqualityComparer<TValue>.Default);
#pragma warning restore CS8714

    /// <summary>
    /// 获取本组合框实例的唯一标识。
    /// </summary>
    private string Id { get; set; } = $"combobox-{Guid.NewGuid():N}";

    /// <summary>
    /// 获取弹层当前是否打开。
    /// </summary>
    private bool GetIsOpen() => _isOpen;

    /// <summary>
    /// 校验必需参数。
    /// </summary>
    protected override void OnParametersSet()
    {
        _parametersChanged = true;
        base.OnParametersSet();

        // 出于安全过滤掉 null 选项（仅 Options 模式）
        Options = Options?.Where(o => o != null);

        // 当消费方通过 SearchQueryChanged 在外部处理过滤时，
        // 绕过 Command 的内部文本过滤器，避免双重过滤与竞态。
        _filterFunction = SearchQueryChanged.HasDelegate ? passthroughFilter : null;

        if (CascadedEditContext != null && ValueExpression != null)
        {
            _editContext = CascadedEditContext;
            _fieldIdentifier = FieldIdentifier.Create(ValueExpression);
        }
    }

    /// <summary>
    /// 获取当前选中项的显示文本。解析顺序：
    /// Options（Options 模式）→ 已注册项（复合模式）→
    /// 调用方提供的 <see cref="SelectedItemText"/>（标记里还没有匹配项时的兜底）→
    /// 上一次用户选择缓存的文本 → 占位符。
    /// </summary>
    private string SelectedDisplayText
    {
        get
        {
            if (Value is null)
            {
                return EffectivePlaceholder;
            }

            // Options 模式：从 Options 集合同步查找。
            var selectedOption = Options?.FirstOrDefault(o => EqualityComparer<TValue>.Default.Equals(o.Value, Value));
            if (selectedOption is not null)
            {
                return selectedOption.Text;
            }

            // 复合模式：已注册项压过调用方提供的提示，这样重新注册
            // （如父级更新了 Text）就能纠正过期标题。
            if (_itemTextRegistry.GetValueOrDefault(Value) is { } registryText)
            {
                return registryText;
            }

            // 调用方提供的初始文本——当选中的值在标记里还没有匹配项时的兜底
            // （例如选中项是异步加载的）。
            if (!string.IsNullOrEmpty(SelectedItemText))
            {
                return SelectedItemText;
            }

            // 最后兜底：缓存自上一次用户选择的文本，它能在异步过滤过程中
            // 挺过 Options 数组的变化（例如选中项被过滤出了当前结果）。
            return _selectedDisplayTextCache ?? EffectivePlaceholder;
        }
    }

    /// <summary>
    /// 处理弹层打开状态变化。弹层关闭时重置焦点追踪与搜索查询。
    /// </summary>
    /// <param name="isOpen">弹层现在是否打开。</param>
    private async Task HandleOpenChanged(bool isOpen)
    {
        _isOpen = isOpen;
        if (isOpen)
        {
            // 默认【不】恢复焦点；只有选择项关闭会显式开启（见 HandleSelect）。
            // 这让点击外部关闭把焦点留在用户点击的位置。
            _restoreFocusOnClose = false;
        }
        if (!isOpen)
        {
            // 重置搜索查询并通知消费方，让它为下一次打开重载默认数据集
            // （例如初始 top-N 结果）——但仅在确实有搜索内容需要清空时。
            //
            // 它过去在每次关闭都触发，包括用户根本没键入任何东西的、压倒性常见的那种关闭。
            // 对把一个通知理解为"重载你的第一页"的分页列表（这也正是该通知的本意），代价很严重：
            // 滚进七页、从最后一页挑一项，关闭就把七页全丢了。重开显示第一页，选中的项已不在
            // 列表里，于是重开也滚不到它。重载引发的额外渲染落在关闭动画中途，这正是下拉闪烁
            // 的来源。
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                SearchQuery = string.Empty;
                await SearchQueryChanged.InvokeAsync(string.Empty);
            }
        }
    }

    /// <summary>
    /// 处理来自内部 Command 组件的搜索查询变化。
    /// </summary>
    private async Task HandleSearchQueryChanged(string query)
    {
        SearchQuery = query;
        await SearchQueryChanged.InvokeAsync(query);
    }

    /// <summary>
    /// 处理条目选择，带切换（toggle）行为。
    /// </summary>
    /// <param name="option">被选中的选项。</param>
    private async Task HandleSelect(SelectOption<TValue> option)
    {
        // 切换行为：已选中则取消选中
        var newValue = EqualityComparer<TValue>.Default.Equals(Value, option.Value) ? default : option.Value;

        // 缓存显示文本，让它在异步 Options 变化（选中项可能不再在当前结果里）中存活。
        _selectedDisplayTextCache = newValue is not null ? option.Text : null;

        Value = newValue;
        await ValueChanged.InvokeAsync(newValue);

        if (_editContext != null && ValueExpression != null && _fieldIdentifier.FieldName != null)
        {
            _editContext.NotifyFieldChanged(_fieldIdentifier);
        }

        // 选择后关闭弹层——这是一次有意的关闭，把焦点还给触发器（弹层内容会卸载；
        // 没有这一步，焦点丢失到 <body>，下一次 Tab 会从文档顶部重新开始）。
        _restoreFocusOnClose = true;
        _isOpen = false;
    }

    /// <summary>
    /// 检查某个选项当前是否被选中。
    /// </summary>
    /// <param name="option">要检查的选项。</param>
    /// <returns>选项被选中时为 true，否则为 false。</returns>
    private bool IsSelected(SelectOption<TValue> option) => EqualityComparer<TValue>.Default.Equals(Value, option.Value);

    /// <summary>
    /// 获取组合框容器的 CSS 类。
    /// </summary>
    private static string ContainerClass => "atom:relative";

    /// <summary>
    /// 获取按钮元素的 CSS 类（样式对齐 ButtonVariant.Outline）。
    /// </summary>
    private string ButtonCssClass => ClassNames.cn(
        "atom:inline-flex atom:items-center atom:justify-between atom:rounded-md atom:text-sm atom:font-medium",
        "atom:transition-colors atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:opacity-50 atom:disabled:pointer-events-none",
        "atom:border atom:border-input atom:bg-background atom:hover:bg-accent atom:hover:text-accent-foreground",
        _isOpen ? ActiveClass : null,
        "atom:h-10 atom:px-3",
        string.IsNullOrWhiteSpace(Class) ? PopoverWidth : null,
        Class
    );

    // ── 复合模式支持（供 ComboboxItem 使用）──────────────────────────────

    /// <summary>
    /// 注册一个项的值与显示文本，供触发器查找显示文本。
    /// 由 ComboboxItem 在初始化与参数级联时调用。
    /// </summary>
    internal void RegisterItem(TValue value, string text)
    {
        if (value is null)
        {
            return;
        }

        // 仅在文本真正变化时才置脏——没有这道守卫，OnParametersSet 侧的 RegisterItem
        // 会在每次父级级联时用相同文本重复注册，从而排队一次渲染。
        var textChanged = !_itemTextRegistry.TryGetValue(value, out var existing)
            || !string.Equals(existing, text, StringComparison.Ordinal);

        _itemTextRegistry[value] = text;

        // 如果被注册的项正是当前选中项且其显示文本确实变了（覆盖首挂载与 Text 更新），
        // 重新渲染，让触发器拾取新标题。注册发生得比触发器的首次渲染更晚（隐藏注册
        // 通道在同一初始渲染周期里更晚才挂载子级），所以没有这一步触发器会停在占位符上。
        if (textChanged && EqualityComparer<TValue>.Default.Equals(value, Value))
        {
            _triggerTextDirty = true;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 从一个项的值注销文本注册。由 ComboboxItem 在释放时调用。
    /// </summary>
    internal void UnregisterItem(TValue value)
    {
        if (value is not null)
        {
            _itemTextRegistry.Remove(value);
        }
    }

    /// <summary>
    /// 检查某个值当前是否被选中。供 ComboboxItem 渲染对勾图标使用。
    /// </summary>
    internal bool IsValueSelected(TValue? value) =>
        EqualityComparer<TValue>.Default.Equals(Value, value);

    /// <summary>
    /// 处理来自 ComboboxItem 子组件的项选择。
    /// 构造一个 SelectOption 并委托给 HandleSelect。
    /// </summary>
    internal async Task HandleItemSelected(TValue value, string text) =>
        await HandleSelect(new SelectOption<TValue>(value, text));
}
