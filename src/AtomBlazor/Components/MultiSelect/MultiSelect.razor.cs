using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Linq.Expressions;
using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 允许多选的可搜索下拉多选组件。
/// </summary>
/// <typeparam name="TValue">所选值的类型。</typeparam>
public partial class MultiSelect<TValue> : ComponentBase, IAsyncDisposable
{
    /// <summary>为辅助技术标记该选择为必填。非空校验请交给模型验证。</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>控件的无障碍名称。</summary>
    [Parameter] public string? AriaLabel { get; set; }


    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    private FieldIdentifier _fieldIdentifier;
    private EditContext? _editContext;
    private IJSObjectReference? _multiSelectModule;
    private DotNetObjectReference<MultiSelect<TValue>>? _dotNetRef;
    private ElementReference _searchInputRef;
    private bool _jsSetupDone;

    // ShouldRender 追踪字段
    private bool _parametersChanged;
    private IEnumerable<SelectOption<TValue>>? _lastOptions;
    private IEnumerable<TValue>? _lastValues;
    private bool _lastIsOpen;
    private string _lastSearchQuery = string.Empty;
    private bool _lastDisabled;

    // 缓存事件处理器，避免每次渲染都分配委托
    // CS8714 抑制：作为字典键使用的 Option 值在实践中永不为 null
#pragma warning disable CS8714
    private readonly Dictionary<TValue, Func<Task>> _toggleHandlerCache = new(EqualityComparer<TValue>.Default);
    private readonly Dictionary<TValue, Func<Task>> _removeHandlerCache = new(EqualityComparer<TValue>.Default);
#pragma warning restore CS8714

    // 缓存 CSS 类字符串，避免每次渲染重复计算
    // TriggerCssClass 的缓存字段已移除——由 ClassNames.cn 承担

    /// <summary>
 /// 复合模式的条目文本登记表，用于展示文本查找与 EffectiveOptions。
    /// </summary>
#pragma warning disable CS8714 // TValue 可能可空，但字典键在实践中永不为 null
    private readonly Dictionary<TValue, string> _itemTextRegistry = new(EqualityComparer<TValue>.Default);

    /// <summary>
    /// 已选值展示文本的缓存。异步过滤期间 Options 数组换页后它仍然有效
    /// 于是所选条目不在当前 Options 页里时，徽标文本依旧正确。
    /// </summary>
    private readonly Dictionary<TValue, string> _displayTextCache = new(EqualityComparer<TValue>.Default);
#pragma warning restore CS8714

    /// <summary>
    /// 获取或设置从上级 EditForm 级联而来的 EditContext。
    /// </summary>
    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// 获取或设置多选下拉中展示的选项集合。
    /// 提供时走 Options 模式（数据驱动）；为 null 时走复合模式（ChildContent）。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<TValue>>? Options { get; set; }

    /// <summary>
    /// 获取或设置复合模式的子内容。
    /// 以 MultiSelectItem 组件作为子级来渲染富内容条目。
    /// 仅在 Options 为 null 时使用。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置当前选中的值集合。
    /// </summary>
    [Parameter]
    public IEnumerable<TValue>? Values { get; set; }

    /// <summary>
    /// 获取或设置选中值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<IEnumerable<TValue>?> ValuesChanged { get; set; }

    /// <summary>
    /// 获取或设置未选中任何条目时显示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置搜索输入框内的占位文本。
    /// </summary>
    [Parameter]
    public string? SearchPlaceholder { get; set; }

    /// <summary>
    /// 获取或设置无条目匹配搜索时显示的消息。
    /// </summary>
    [Parameter]
    public string? EmptyMessage { get; set; }

    /// <summary>
    /// 获取或设置搜索词变化时调用的回调。
    /// 用于服务端过滤或外部数据加载；设置后组件绕过内部文本过滤
    /// 由使用者完全控制所显示的条目。
    /// </summary>
    [Parameter]
    public EventCallback<string> SearchQueryChanged { get; set; }

    /// <summary>
    /// 获取或设置 Select All 选项的标签。
    /// </summary>
    [Parameter]
    public string? SelectAllLabel { get; set; }

    /// <summary>
    /// 获取或设置是否显示 Select All 选项。
    /// </summary>
    [Parameter]
    public bool ShowSelectAll { get; set; } = true;

    /// <summary>
    /// 获取或设置 Clear 按钮的标签。
    /// </summary>
    [Parameter]
    public string? ClearLabel { get; set; }

    /// <summary>
    /// 获取或设置 Close 按钮的标签。
    /// </summary>
    [Parameter]
    public string? CloseLabel { get; set; }

    private string EffectivePlaceholder => Placeholder ?? Localizer["MultiSelect.Placeholder"];
    private string EffectiveSearchPlaceholder => SearchPlaceholder ?? Localizer["MultiSelect.SearchPlaceholder"];
    private string EffectiveEmptyMessage => EmptyMessage ?? Localizer["MultiSelect.EmptyMessage"];
    private string EffectiveSelectAllLabel => SelectAllLabel ?? Localizer["MultiSelect.SelectAll"];
    private string EffectiveClearLabel => ClearLabel ?? Localizer["MultiSelect.Clear"];
    private string EffectiveCloseLabel => CloseLabel ?? Localizer["MultiSelect.Close"];

    /// <summary>
    /// 获取或设置追加到多选容器的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的其他 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置多选组件是否被禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置描述本多选组件的无障碍元素的 ID 列表。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置显示"+N more"之前最多渲染的标签数。
    /// </summary>
    [Parameter]
    public int MaxDisplayTags { get; set; } = 3;

    /// <summary>
    /// 为 <c>true</c> 时触发器保持单行固定高度：放不下的所选标签被裁切
    /// 而"+N more"指示与箭头图标保持钉住可见。
    /// 为 <c>false</c>（默认）时标签换行到额外行，触发器随之增高。
    /// </summary>
    [Parameter]
    public bool SingleLine { get; set; }

    /// <summary>
    /// 获取或设置弹层内容的宽度。
    /// MatchTriggerWidth 为 true 时忽略。
    /// </summary>
    [Parameter]
    public string PopoverWidth { get; set; } = "atom:w-[300px]";

    /// <summary>
    /// 获取或设置是否让下拉宽度跟随触发器元素宽度。
    /// 为 true 时忽略 PopoverWidth。
    /// </summary>
    [Parameter]
    public bool MatchTriggerWidth { get; set; } = false;

    /// <summary>
    /// 获取或设置下拉打开时应用于触发器的 CSS 类。
    /// 默认是 <c>"atom:bg-accent atom:text-accent-foreground"</c>。
    /// 设为 <c>null</c> 或空串可关闭激活态样式。
    /// </summary>
    [Parameter]
    public string? ActiveClass { get; set; } = "atom:bg-accent atom:text-accent-foreground";

    /// <summary>
    /// 获取或设置点击下拉之外是否关闭下拉。
    /// 为 true 时下拉关闭并清空搜索词。默认 true。
    /// </summary>
    [Parameter]
    public bool AutoClose { get; set; } = true;

    /// <summary>
    /// 获取或设置在用户滚动到下拉列表接近底部时调用的回调。
    /// 用于无限滚动场景加载追加条目。
    /// </summary>
    [Parameter]
    public EventCallback OnLoadMore { get; set; }

    /// <summary>
    /// 获取或设置是否正在加载追加条目。
    /// 为 true 时列表底部显示加载指示器，且 <see cref="OnLoadMore"/> 在加载完成前被抑制。
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    /// 获取或设置在全部条目加载完毕后显示于列表底部的消息。
    /// 仅在 <see cref="IsLoading"/> 为 false 时显示。设为 <c>null</c> 或空串则隐藏。
    /// </summary>
    [Parameter]
    public string? EndOfListMessage { get; set; }

    /// <summary>
    /// 获取或设置标识所绑定值的表达式，用于表单校验集成。
    /// </summary>
    [Parameter]
    public Expression<Func<IEnumerable<TValue>?>>? ValuesExpression { get; set; }

    private ElementReference _listboxScrollRef;
    private DotNetObjectReference<MultiSelect<TValue>>? _nearBottomRef;
    private int _nearBottomObserver;

    /// <summary>
    /// 跟踪弹层当前是否打开。
    /// </summary>
    private bool _isOpen { get; set; }

    /// <summary>
    /// 下一次关闭是否把焦点交还触发器。有意关闭（Escape、Close 按钮）为 true
    /// 点击外部为 false（焦点留在用户点到的地方）。绑定到弹层的 RestoreFocusOnClose。
    /// </summary>
    private bool _restoreFocusOnClose;

    /// <summary>
    /// 跟踪当前搜索词，用于过滤。
    /// </summary>
    private string _searchQuery = string.Empty;

    /// <summary>
    /// 获取本多选实例的唯一标识符。
    /// </summary>
    private string Id { get; set; } = $"multiselect-{Guid.NewGuid():N}";

    /// <summary>
    /// 获取当前所选值的列表。
    /// </summary>
    private List<TValue> SelectedValues => Values?.ToList() ?? new List<TValue>();

    /// <summary>
    /// 获取超出 MaxDisplayTags 的所选条目数。
    /// </summary>
    private int OverflowCount => Math.Max(0, SelectedValues.Count - MaxDisplayTags);

    /// <summary>
    /// 获取是否已有条目被选中。
    /// </summary>
    private bool HasSelectedItems => SelectedValues.Count > 0;

    /// <summary>
    /// 获取组件是否处于复合模式（ChildContent 携带 MultiSelectItem 子级）。
    /// </summary>
    private bool IsCompositionalMode => Options is null;

    /// <summary>
    /// 获取当前模式下的有效选项集合。
 /// Options 模式返回 Options；复合模式把已登记条目映射为 SelectOption。
    /// </summary>
    private IEnumerable<SelectOption<TValue>> EffectiveOptions =>
        Options ?? _itemTextRegistry.Select(kvp => new SelectOption<TValue>(kvp.Key, kvp.Value));

    /// <summary>
    /// 获取按当前搜索词过滤后的选项集合。
    /// </summary>
    private IEnumerable<SelectOption<TValue>> FilteredOptions => GetFilteredOptions();

    /// <summary>
    /// 获取过滤后是否还有可见选项。
    /// </summary>
    private bool HasFilteredItems => FilteredOptions.Any();

    /// <summary>
    /// 获取匹配当前搜索词的选项。
    /// 当 <see cref="SearchQueryChanged"/> 挂有委托时绕过内部过滤
    /// （由使用者在外部控制条目集合）。
    /// </summary>
    private IEnumerable<SelectOption<TValue>> GetFilteredOptions()
    {
        var options = EffectiveOptions;

        // 外部过滤生效时，信任使用者给出的条目列表
        if (SearchQueryChanged.HasDelegate)
        {
            return options;
        }

        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            return options;
        }

        return options.Where(o =>
            o.Text.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));
    }

    private async Task HandleSearchQueryChangedAsync()
    {
        if (SearchQueryChanged.HasDelegate)
        {
            await SearchQueryChanged.InvokeAsync(_searchQuery);
        }
    }

    /// <summary>
    /// 获取多选组件是否处于无效状态（用于校验）。
    /// </summary>
    private bool IsInvalid
    {
        get
        {
            if (_editContext != null && ValuesExpression != null && _fieldIdentifier.FieldName != null)
            {
                return _editContext.GetValidationMessages(_fieldIdentifier).Any();
            }
            return false;
        }
    }

    /// <summary>
    /// 校验参数合法性。
    /// </summary>
    protected override void OnParametersSet()
    {
        _parametersChanged = true;
        base.OnParametersSet();

        // 出于安全过滤掉 null 选项（仅 Options 模式）
        Options = Options?.Where(o => o != null);

        // Options 引用变化时清空处理器缓存，避免滞留的旧委托
        if (!ReferenceEquals(_lastOptions, Options))
        {
            _toggleHandlerCache.Clear();
        }

        // 若可用则初始化 EditContext 集成
        if (CascadedEditContext != null && ValuesExpression != null)
        {
            _editContext = CascadedEditContext;
            _fieldIdentifier = FieldIdentifier.Create(ValuesExpression);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 弹层打开时装配 JS
        if (_isOpen && !_jsSetupDone)
        {
            _jsSetupDone = true;

            try
            {
 // 有意偏差（记录在案）：按实例直连 multiselect.js 模块路径
                // 本仓库统一改走共享 components-core.js barrel（camelCase 点分标识符）
                // 规则见 PrimitiveJsModuleTests。
                _multiSelectModule = await ComponentModules.GetCoreAsync(JSRuntime);

                _dotNetRef = DotNetObjectReference.Create(this);

                await _multiSelectModule.InvokeVoidAsync(
                    "multiselect.setupMultiSelectInput",
                    _searchInputRef,
                    _dotNetRef,
                    $"{Id}-search",
                    $"{Id}-listbox");

                // 列表不加 @onscroll——理由见 CommandList 对应注释。由浏览器盯着它
                // 每次滚到底时回调一次。
                if (OnLoadMore.HasDelegate)
                {
                    _ = ObserveNearBottomAsync();
                }
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开时属于预期情况
                _jsSetupDone = false;
                _dotNetRef?.Dispose();
                _dotNetRef = null;
            }
            catch (InvalidOperationException)
            {
                // 预渲染阶段 JS interop 不可用
                _jsSetupDone = false;
                _dotNetRef?.Dispose();
                _dotNetRef = null;
            }
        }
        // 弹层关闭时清理 JS
        else if (!_isOpen && _jsSetupDone)
        {
            await CleanupJsAsync();
        }
    }

    private async Task CleanupJsAsync()
    {
        await UnobserveNearBottomAsync();

        if (_multiSelectModule != null)
        {
            try
            {
                await _multiSelectModule.InvokeVoidAsync("multiselect.removeMultiSelectInput", $"{Id}-search");
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 模块可能已释放或电路已断开
            }
        }
        _jsSetupDone = false;
    }

    /// <summary>
    /// 处理弹层打开状态的变化。
    /// 弹层关闭时复位焦点跟踪。
    /// </summary>
    /// <param name="isOpen">弹层现在是否打开。</param>
    private void HandleOpenChanged(bool isOpen)
    {
        _isOpen = isOpen;
        if (!isOpen)
        {
            // 在此留有空分支（焦点复位已移交弹层的 RestoreFocusOnClose），保持空壳。
        }
    }

    /// <summary>
    /// 打开下拉。
    /// </summary>
    private void Open()
    {
        if (Disabled)
        {
            return;
        }

        _restoreFocusOnClose = false; // 复位；有意关闭会重新登记
        _isOpen = true;
    }

    /// <summary>
    /// 以有意关闭的方式收起下拉（Escape、Close 按钮），把焦点交还触发器
    /// 让键盘导航从正确的位置继续。
    /// </summary>
    private Task Close() => CloseCore(restoreFocus: true);

    /// <summary>替换默认的 clear/close 页脚，置于滚动选项列表之外。</summary>
    [Parameter]
    public RenderFragment? FooterContent { get; set; }

    /// <summary>在渲染器调度线程上收起列表、清空搜索并把焦点交还触发器。</summary>
    public Task CloseAsync() => InvokeAsync(async () =>
    {
        if (!_isOpen) { return; }
        try
        {
            await Close();
        }
        finally
        {
            StateHasChanged();
        }
    });

    /// <summary>
    /// 关闭下拉。<paramref name="restoreFocus"/> 决定焦点是否交还触发器——
    /// 有意关闭为 true，点击外部为 false（焦点留在点到处）。
    /// </summary>
    private async Task CloseCore(bool restoreFocus)
    {
        _restoreFocusOnClose = restoreFocus;
        _isOpen = false;
        _searchQuery = string.Empty;

        // 通知使用者，便于其在下次打开前重载默认数据集
        if (SearchQueryChanged.HasDelegate)
        {
            await SearchQueryChanged.InvokeAsync(string.Empty);
        }
    }

    /// <summary>
    /// 依据 AutoClose 设置取点击外部的事件处理器。
    /// AutoClose 为 false 时返回空的 EventCallback。
    /// </summary>
    private EventCallback GetClickOutsideHandler()
    {
        return AutoClose
            ? EventCallback.Factory.Create(this, HandleClickOutside)
            : default;
    }

    /// <summary>
    /// 处理 AutoClose 生效时的点击外部事件。
    /// </summary>
    private async Task HandleClickOutside() => await CloseCore(restoreFocus: false);

    /// <summary>
    /// 处理选项切换（选中/取消）。
    /// </summary>
    private async Task HandleToggle(SelectOption<TValue> option)
    {
        var currentValues = SelectedValues;

        if (!currentValues.Remove(option.Value))
        {
            currentValues.Add(option.Value);
            // 缓存展示文本，异步过滤期间 Options 数组换页后它仍然有效
            if (option.Value is not null)
            {
                _displayTextCache[option.Value] = option.Text;
            }
        }
        else if (option.Value is not null)
        {
            _displayTextCache.Remove(option.Value);
        }

        await UpdateValues(currentValues);
    }

    /// <summary>
    /// 为指定选项取缓存过的切换处理器，避免委托分配。
    /// </summary>
    private Func<Task> GetToggleHandler(SelectOption<TValue> option)
    {
        var key = option.Value;
        if (!_toggleHandlerCache.TryGetValue(key, out var handler))
        {
            handler = () => HandleToggle(option);
            _toggleHandlerCache[key] = handler;
        }
        return handler;
    }

    /// <summary>
    /// 为指定值取缓存过的移除处理器，避免委托分配。
    /// </summary>
    private Func<Task> GetRemoveHandler(TValue value)
    {
        if (!_removeHandlerCache.TryGetValue(value, out var handler))
        {
            handler = () => RemoveValue(value);
            _removeHandlerCache[value] = handler;
        }
        return handler;
    }

    /// <summary>
    /// 处理 Select All 切换。只影响过滤后（可见）的条目。
    /// </summary>
    private async Task HandleSelectAllToggle()
    {
        var filteredOptions = FilteredOptions.ToList();
        var filteredValues = filteredOptions.Select(o => o.Value).ToList();
        var currentValues = SelectedValues;

        // 检查是否所有"过滤后"条目都已选中
        var allFilteredSelected = filteredValues.All(v => currentValues.Contains(v));

        if (allFilteredSelected)
        {
            // 只取消过滤后条目的选中
            currentValues.RemoveAll(v => filteredValues.Contains(v));
            foreach (var v in filteredValues.Where(v => v is not null))
            {
                _displayTextCache.Remove(v);
            }
        }
        else
        {
            // 选中全部过滤后条目（并入既有选择）
            foreach (var option in filteredOptions.Where(o => !currentValues.Contains(o.Value)))
            {
                currentValues.Add(option.Value);
                if (option.Value is not null)
                {
                    _displayTextCache[option.Value] = option.Text;
                }
            }
        }

        await UpdateValues(currentValues);
    }

    /// <summary>
    /// 从选中集合中移除一个值。
    /// </summary>
    private async Task RemoveValue(TValue value)
    {
        var currentValues = SelectedValues;
        currentValues.Remove(value);
        if (value is not null)
        {
            _displayTextCache.Remove(value);
        }
        await UpdateValues(currentValues);
    }

    /// <summary>
    /// 清空全部选中。
    /// </summary>
    private async Task ClearAll()
    {
        _displayTextCache.Clear();
        await UpdateValues(new List<TValue>());
    }

    /// <summary>
    /// 更新选中值并通知父组件。
    /// </summary>
    private async Task UpdateValues(List<TValue> values)
    {
        Values = values.Count > 0 ? values : null;
        await ValuesChanged.InvokeAsync(Values);

        // 通知 EditContext 字段已变化，触发校验
        if (_editContext != null && ValuesExpression != null && _fieldIdentifier.FieldName != null)
        {
            _editContext.NotifyFieldChanged(_fieldIdentifier);
        }
    }

    /// <summary>
    /// 检查某选项当前是否已选中。
    /// </summary>
    private bool IsSelected(SelectOption<TValue> option) =>
        SelectedValues.Contains(option.Value);

    /// <summary>
    /// 依据过滤后的选项取 Select All 复选框状态。
    /// </summary>
    private SelectAllState GetSelectAllState()
    {
        var filteredValues = new HashSet<TValue>(FilteredOptions.Select(o => o.Value), EqualityComparer<TValue>.Default);
        var selectedFilteredCount = SelectedValues.Count(v => filteredValues.Contains(v));

        if (selectedFilteredCount == 0)
        {
            return SelectAllState.None;
        }

        if (selectedFilteredCount == filteredValues.Count)
        {
            return SelectAllState.All;
        }

        return SelectAllState.Indeterminate;
    }

    /// <summary>
    /// 取某值的展示文本。
 /// 先查 Options，再查条目文本登记表（复合模式）。
    /// </summary>
    private string GetDisplayText(TValue value)
    {
        var option = Options?.FirstOrDefault(o => EqualityComparer<TValue>.Default.Equals(o.Value, value));
        if (option is not null)
        {
            return option.Text;
        }

        if (value is not null && _itemTextRegistry.TryGetValue(value, out var registeredText))
        {
            return registeredText;
        }

        // 兜底：条目被选中时缓存的展示文本。
        // 异步过滤期间 Options 数组换页后它仍然有效。
        if (value is not null && _displayTextCache.TryGetValue(value, out var cachedText))
        {
            return cachedText;
        }

        return value?.ToString() ?? "";
    }

    // JSInvokable 键盘导航回调
    [JSInvokable]
    public void HandleSpace()
    {
        // 空格切换复选框——条目点击已由 JS 处理完毕
        // 无需额外动作，下拉保持打开
    }

    [JSInvokable]
    public async Task HandleEnter()
    {
        // 回车在条目切换之后收起下拉
        await Close();
        StateHasChanged();
    }

    [JSInvokable]
    public async Task HandleEscape()
    {
        // Escape 收起下拉
        await Close();
        StateHasChanged();
    }

    private async Task ObserveNearBottomAsync()
    {
        try
        {
            var module = await PrimitiveModules.GetAsync(JSRuntime);
            _nearBottomRef ??= DotNetObjectReference.Create(this);
            _nearBottomObserver = await module.InvokeAsync<int>(
                "elementUtils.observeNearBottom", _listboxScrollRef, _nearBottomRef, nameof(JsOnNearBottom), 80.0);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // 电路已断、预渲染、或列表已拆除。加载更多只是锦上添花。
        }
    }

    private async Task UnobserveNearBottomAsync()
    {
        if (_nearBottomObserver == 0 || !PrimitiveModules.TryGetLoaded(JSRuntime, out var module))
        {
            return;
        }

        var id = _nearBottomObserver;
        _nearBottomObserver = 0;

        try
        {
            await module.InvokeVoidAsync("elementUtils.unobserveNearBottom", id);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            // 电路已断——监听器随文档一起消散了。
        }
    }

    /// <summary>
    /// 由 JavaScript 在列表滚动到接近底部时调用。
    /// </summary>
    [JSInvokable]
    public async Task JsOnNearBottom()
    {
        if (!OnLoadMore.HasDelegate || IsLoading)
        {
            return;
        }

        await OnLoadMore.InvokeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await CleanupJsAsync();

        _dotNetRef?.Dispose();
        _dotNetRef = null;
        _nearBottomRef?.Dispose();
        _nearBottomRef = null;
    }

    /// <summary>
    /// 依据跟踪到的状态变化决定组件是否重渲染。
    /// 这项优化削减了不必要的渲染周期。
    /// </summary>
    protected override bool ShouldRender()
    {
        if (_parametersChanged)
        {
            _parametersChanged = false;
            _lastOptions = Options;
            _lastValues = Values;
            _lastIsOpen = _isOpen;
            _lastSearchQuery = _searchQuery;
            _lastDisabled = Disabled;
            return true;
        }

        var optionsChanged = !ReferenceEquals(_lastOptions, Options);
        var valuesChanged = !ReferenceEquals(_lastValues, Values);
        var openChanged = _lastIsOpen != _isOpen;
        var searchChanged = _lastSearchQuery != _searchQuery;
        var disabledChanged = _lastDisabled != Disabled;

        if (optionsChanged || valuesChanged || openChanged || searchChanged || disabledChanged)
        {
            _lastOptions = Options;
            _lastValues = Values;
            _lastIsOpen = _isOpen;
            _lastSearchQuery = _searchQuery;
            _lastDisabled = Disabled;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 取多选容器的 CSS 类。
    /// </summary>
    private static string ContainerClass => "atom:relative";

    /// <summary>
    /// 取触发按钮的 CSS 类。
    /// 使用缓存避免每次渲染重复计算。
    /// </summary>
    private string TriggerCssClass => ClassNames.cn(
        "atom:inline-flex atom:items-center atom:justify-between atom:rounded-md atom:text-sm atom:font-medium",
        "atom:transition-colors atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:opacity-50 atom:disabled:pointer-events-none",
        "atom:border atom:border-input atom:bg-background atom:hover:bg-accent atom:hover:text-accent-foreground",
        _isOpen ? ActiveClass : null,
        // 单行模式用固定高度，防止换行的标签撑高触发器；默认用最小高度
        // 让触发器随换到额外行的标签增长。
        SingleLine ? "atom:h-10 atom:px-3 atom:py-1.5" : "atom:min-h-10 atom:px-3 atom:py-1.5",
        PopoverWidth,
        Class
    );

    /// <summary>
    /// 标签列表容器的 CSS。单行模式把放不下的标签裁切在一行里；默认让它们
    /// 换到额外的行。
    /// </summary>
    private string TagListClass => SingleLine
        ? "atom:flex atom:flex-nowrap atom:items-center atom:gap-1 atom:flex-1 atom:min-w-0 atom:overflow-hidden"
        : "atom:flex atom:flex-wrap atom:items-center atom:gap-1 atom:flex-1 atom:min-w-0";

    /// <summary>
    /// 每个所选标签徽标的 CSS。单行模式下标签保持原尺寸（放不下的被裁切）
    /// 而不是收缩挤入。
    /// </summary>
    private string TagBadgeClass => SingleLine ? "atom:gap-1 atom:shrink-0" : "atom:gap-1";

    /// <summary>
    /// 取标签移除按钮的 CSS 类。
    /// </summary>
    private static string TagRemoveButtonCssClass =>
        "atom:ms-0.5 atom:rounded-full atom:outline-none atom:hover:bg-secondary-foreground/20 atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring";

    /// <summary>
    /// 取下拉条目的 CSS 类。
    /// </summary>
    internal static string ItemCssClass =>
        "atom:relative atom:flex atom:cursor-pointer atom:select-none atom:items-center atom:gap-2 atom:rounded-sm atom:px-2 atom:py-1.5 atom:text-sm atom:outline-none " +
        "atom:data-[focused=true]:bg-accent atom:data-[focused=true]:text-accent-foreground " +
        "atom:data-[disabled=true]:pointer-events-none atom:data-[disabled=true]:opacity-50";

    /// <summary>
    /// 取复选框的 CSS 类。
    /// 借助 Checkbox 基元的 data-state 属性呈现 checked/unchecked/indeterminate 样式。
    /// </summary>
    internal static string CheckboxCssClass =>
        "atom:h-4 atom:w-4 atom:shrink-0 atom:rounded-sm atom:border atom:border-primary atom:flex atom:items-center atom:justify-center " +
        "atom:focus-visible:outline-none " +
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50 " +
        "atom:data-[state=checked]:bg-primary atom:data-[state=checked]:text-primary-foreground " +
        "atom:data-[state=indeterminate]:bg-primary atom:data-[state=indeterminate]:text-primary-foreground " +
        "atom:data-[state=unchecked]:bg-background";

    // ── 复合模式支持（供 MultiSelectItem 使用）─────────────────────

    /// <summary>
    /// 取当前搜索词，供条目可见性过滤。
    /// </summary>
    internal string SearchQuery => _searchQuery;

    /// <summary>
    /// 外部过滤是否生效（使用者经 SearchQueryChanged 处理搜索）。
    /// </summary>
    internal bool IsExternalFiltering => SearchQueryChanged.HasDelegate;

    /// <summary>
 /// 登记条目的值与展示文本，供徽标显示与 EffectiveOptions 使用。
    /// 由 MultiSelectItem 在初始化时调用。
    /// </summary>
    internal void RegisterItem(TValue value, string text)
    {
        if (value is not null)
        {
            _itemTextRegistry[value] = text;
        }
    }

    /// <summary>
 /// 从文本登记表注销条目的值。
    /// 由 MultiSelectItem 在释放时调用。
    /// </summary>
    internal void UnregisterItem(TValue value)
    {
        if (value is not null)
        {
            _itemTextRegistry.Remove(value);
        }
    }

    /// <summary>
    /// 检查某值当前是否已选中。
    /// 供 MultiSelectItem 渲染复选框状态。
    /// </summary>
    internal bool IsValueSelected(TValue value) =>
        SelectedValues.Contains(value);

    /// <summary>
    /// 切换某值的选中状态。
    /// 供 MultiSelectItem 在点击时调用。
    /// </summary>
    internal async Task ToggleItemValue(TValue value)
    {
        var currentValues = SelectedValues;

        if (!currentValues.Remove(value))
        {
            currentValues.Add(value);
            // 缓存展示文本，异步过滤期间条目重渲染后它仍然有效
            if (value is not null && _itemTextRegistry.TryGetValue(value, out var text))
            {
                _displayTextCache[value] = text;
            }
        }
        else if (value is not null)
        {
            _displayTextCache.Remove(value);
        }

        await UpdateValues(currentValues);
    }
}

/// <summary>
/// 表示 Select All 复选框的状态。
/// </summary>
public enum SelectAllState
{
    /// <summary>没有任何条目被选中。</summary>
    None,
    /// <summary>部分条目被选中。</summary>
    Indeterminate,
    /// <summary>全部条目被选中。</summary>
    All
}
