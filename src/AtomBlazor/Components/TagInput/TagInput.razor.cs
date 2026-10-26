using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 用于录入与管理字符串标签列表的内联标签输入组件。
/// </summary>
public partial class TagInput : ComponentBase, IAsyncDisposable
{
    [Inject] private ILocalizer Localizer { get; set; } = default!;

    // ── 基础设施 ────────────────────────────────────────────────────────
    private ElementReference _containerRef;
    private ElementReference _inputRef;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<TagInput>? _dotNetRef;
    private readonly string _instanceId = $"taginput-{Guid.NewGuid():N}";
    private bool _jsInitialized;
    private bool _disposed;

    // ── 状态 ────────────────────────────────────────────────────────────
    private string _inputText = string.Empty;
    private List<string> _currentTags = new();
    private bool _suggestionsOpen;
    private int _suggestionIndex = -1;
    private List<string> _filteredSuggestions = new();
    private CancellationTokenSource? _suggestionCts;
    private CancellationTokenSource? _blurCts;

    // ── ShouldRender 追踪 ───────────────────────────────────────────────
    private IReadOnlyList<string>? _lastTags;
    private string _lastInputText = string.Empty;
    private bool _lastSuggestionsOpen;
    private int _lastSuggestionIndex = -1;
    private bool _lastDisabled;
    private int _lastTagCount;

    // ── 移除处理器缓存 ──────────────────────────────────────────────────
    private readonly Dictionary<string, Func<Task>> _removeHandlerCache = new();

    // ── 表单集成 ────────────────────────────────────────────────────────
    private readonly InputValidationBehavior _validation = new();

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    [CascadingParameter(Name = "FieldIsInvalid")]
    private bool? FieldIsInvalid { get; set; }

    // ══════════════════════════════════════════════════════════════════
    // 参数
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取或设置标签列表。使用 <c>@bind-Tags</c> 进行双向绑定。
    /// </summary>
    [Parameter]
    public IReadOnlyList<string>? Tags { get; set; }

    /// <summary>
    /// 标签列表变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyList<string>?> TagsChanged { get; set; }

    /// <summary>
    /// 没有标签时显示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    private string ResolvedPlaceholder => Placeholder ?? Localizer["TagInput.Placeholder"];

    /// <summary>
    /// 允许的标签数量上限。
    /// </summary>
    [Parameter]
    public int MaxTags { get; set; } = int.MaxValue;

    /// <summary>
    /// 单个标签的最大字符长度。
    /// </summary>
    [Parameter]
    public int MaxTagLength { get; set; } = 50;

    /// <summary>
    /// 是否允许重复的标签值（不区分大小写比较）。
    /// </summary>
    [Parameter]
    public bool AllowDuplicates { get; set; }

    /// <summary>
    /// 用什么方式把键入的文本提交为标签。多个标志可用按位 OR 组合。
    /// 默认为 <see cref="TagInputTrigger.Enter"/> 与 <see cref="TagInputTrigger.Comma"/>。
    /// </summary>
    /// <remarks>
    /// 除 <see cref="TagInputTrigger.Blur"/> 外每个值都是一个按键。加上 <c>Blur</c>
    /// 可以在用户点击别处或 Tab 离开时也提交，避免写了一半的条目丢失。
    /// </remarks>
    [Parameter]
    public TagInputTrigger AddTrigger { get; set; } = TagInputTrigger.Enter | TagInputTrigger.Comma;

    /// <summary>
    /// 可选的校验函数。返回 false 表示拒绝该标签。
    /// </summary>
    [Parameter]
    public Func<string, bool>? Validate { get; set; }

    /// <summary>
    /// 标签被拒绝时（重复、校验失败、达到上限）调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnTagRejected { get; set; }

    /// <summary>
    /// 随用户键入显示的静态建议列表。
    /// </summary>
    [Parameter]
    public IEnumerable<string>? Suggestions { get; set; }

    /// <summary>
    /// 依据搜索词异步加载建议的函数。
    /// </summary>
    [Parameter]
    public Func<string, CancellationToken, Task<IEnumerable<string>>>? OnSearchSuggestions { get; set; }

    /// <summary>
    /// 建议搜索的防抖间隔（毫秒）。
    /// </summary>
    [Parameter]
    public int SuggestionDebounceMs { get; set; } = 300;

    /// <summary>
    /// 渲染标签的视觉变体。
    /// </summary>
    [Parameter]
    public TagInputVariant Variant { get; set; } = TagInputVariant.Default;

    /// <summary>
    /// 存在标签时是否显示"全部清除"按钮。
    /// </summary>
    [Parameter]
    public bool Clearable { get; set; }

    /// <summary>
    /// 组件是否被禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 渲染每个标签的可选自定义模板。上下文参数为标签文本。
    /// </summary>
    [Parameter]
    public RenderFragment<string>? TagTemplate { get; set; }

    /// <summary>
    /// Tags 属性的表达式，用于 EditForm 校验。
    /// </summary>
    [Parameter]
    public Expression<Func<IReadOnlyList<string>?>>? TagsExpression { get; set; }

    /// <summary>
    /// 标签输入容器的无障碍标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 追加到容器上的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的其他 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ══════════════════════════════════════════════════════════════════
    // 计算属性
    // ══════════════════════════════════════════════════════════════════

    private bool HasSuggestions => Suggestions is not null || OnSearchSuggestions is not null;

    private string? EffectivePlaceholder => _currentTags.Count == 0 ? ResolvedPlaceholder : null;

    private string? ActiveDescendantId =>
        _suggestionsOpen && _suggestionIndex >= 0
            ? $"{_instanceId}-suggestion-{_suggestionIndex}"
            : null;

    private BadgeVariant TagBadgeVariant => Variant switch
    {
        TagInputVariant.Outline => BadgeVariant.Outline,
        TagInputVariant.Secondary => BadgeVariant.Secondary,
        _ => BadgeVariant.Secondary
    };

    private bool IsInvalid => _validation.IsInvalid || FieldIsInvalid == true;

    // ══════════════════════════════════════════════════════════════════
    // 生命周期
    // ══════════════════════════════════════════════════════════════════

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (!ReferenceEquals(_lastTags, Tags))
        {
            _currentTags = Tags?.ToList() ?? new List<string>();
            _removeHandlerCache.Clear();
            _lastTags = Tags;
        }

        _validation.Update(CascadedEditContext, TagsExpression);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
 // 有意偏差（记录在案）：按实例直连 tag-input.js 模块路径
                // 本仓库统一改走共享 components-core.js barrel（camelCase 点分标识符）
                // 规则见 PrimitiveJsModuleTests。
                _jsModule = await ComponentModules.GetCoreAsync(JSRuntime);
                _dotNetRef = DotNetObjectReference.Create(this);
                await _jsModule.InvokeVoidAsync("tagInput.initialize",
                    _containerRef,
                    _inputRef,
                    _dotNetRef,
                    _instanceId,
                    new TagInputJsConfig { triggers = (int)AddTrigger });
                _jsInitialized = true;
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开时属于预期情况
            }
            catch (InvalidOperationException)
            {
                // 预渲染阶段 JS interop 不可用
            }
        }
    }

    protected override bool ShouldRender()
    {
        var changed =
            _lastInputText != _inputText ||
            _lastSuggestionsOpen != _suggestionsOpen ||
            _lastSuggestionIndex != _suggestionIndex ||
            _lastDisabled != Disabled ||
            _lastTagCount != _currentTags.Count;

        if (changed)
        {
            _lastInputText = _inputText;
            _lastSuggestionsOpen = _suggestionsOpen;
            _lastSuggestionIndex = _suggestionIndex;
            _lastDisabled = Disabled;
            _lastTagCount = _currentTags.Count;
            return true;
        }

        return false;
    }

    // ══════════════════════════════════════════════════════════════════
    // JSInvokable 回调
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 由 JS 在配置的触发键被按下时调用。
    /// </summary>
    [JSInvokable]
    public async Task JsTriggerAdd()
    {
        if (_disposed || Disabled)
        {
            return;
        }

        // 高亮中的建议项存在时，选中它而不是提交键入文本
        if (_suggestionsOpen && _suggestionIndex >= 0 && _suggestionIndex < _filteredSuggestions.Count)
        {
            await TryAddTag(_filteredSuggestions[_suggestionIndex]);
        }
        else
        {
            await TryAddTag(_inputText);
        }

        StateHasChanged();
    }

    /// <summary>
    /// 由 JS 在空输入框上按下 Backspace 时调用。
    /// </summary>
    [JSInvokable]
    public async Task JsBackspace()
    {
        if (_disposed || Disabled || _currentTags.Count == 0)
        {
            return;
        }

        var lastTag = _currentTags[^1];
        _currentTags.RemoveAt(_currentTags.Count - 1);
        _removeHandlerCache.Remove(lastTag);
        await NotifyTagsChanged();
        StateHasChanged();
    }

    /// <summary>
    /// 由 JS 在粘贴含分隔符的文本时调用。
    /// </summary>
    [JSInvokable]
    public async Task JsPasteText(string text)
    {
        if (_disposed || Disabled || string.IsNullOrEmpty(text))
        {
            return;
        }

        var delimiters = new List<char>();
        if (AddTrigger.HasFlag(TagInputTrigger.Comma))
        {
            delimiters.Add(',');
        }
        if (AddTrigger.HasFlag(TagInputTrigger.Semicolon))
        {
            delimiters.Add(';');
        }
        if (AddTrigger.HasFlag(TagInputTrigger.Space))
        {
            delimiters.Add(' ');
        }

        var parts = delimiters.Count > 0
            ? text.Split(delimiters.ToArray(), StringSplitOptions.RemoveEmptyEntries)
            : new[] { text };

        foreach (var part in parts)
        {
            await TryAddTag(part);
        }

        _inputText = string.Empty;
        StateHasChanged();
    }

    /// <summary>
    /// 由 JS 调用，导航到下一条建议。
    /// </summary>
    [JSInvokable]
    public void JsSuggestionNext()
    {
        if (_disposed || !_suggestionsOpen || _filteredSuggestions.Count == 0)
        {
            return;
        }

        _suggestionIndex = (_suggestionIndex + 1) % _filteredSuggestions.Count;
        StateHasChanged();
    }

    /// <summary>
    /// 由 JS 调用，导航到上一条建议。
    /// </summary>
    [JSInvokable]
    public void JsSuggestionPrev()
    {
        if (_disposed || !_suggestionsOpen || _filteredSuggestions.Count == 0)
        {
            return;
        }

        _suggestionIndex = _suggestionIndex <= 0
            ? _filteredSuggestions.Count - 1
            : _suggestionIndex - 1;
        StateHasChanged();
    }

    /// <summary>
    /// 由 JS 调用，关闭建议下拉列表。
    /// </summary>
    [JSInvokable]
    public void JsSuggestionClose()
    {
        if (_disposed)
        {
            return;
        }

        _suggestionsOpen = false;
        _suggestionIndex = -1;
        StateHasChanged();
    }

    // ══════════════════════════════════════════════════════════════════
    // 标签逻辑
    // ══════════════════════════════════════════════════════════════════

    private async Task TryAddTag(string rawText)
    {
        var text = rawText.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_currentTags.Count >= MaxTags)
        {
            await OnTagRejected.InvokeAsync(text);
            return;
        }

        if (text.Length > MaxTagLength)
        {
            await OnTagRejected.InvokeAsync(text);
            return;
        }

        if (!AllowDuplicates && _currentTags.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            await OnTagRejected.InvokeAsync(text);
            return;
        }

        if (Validate is not null && !Validate(text))
        {
            await OnTagRejected.InvokeAsync(text);
            return;
        }

        _currentTags.Add(text);
        _inputText = string.Empty;
        _suggestionsOpen = false;
        _suggestionIndex = -1;
        _removeHandlerCache.Clear();
        await NotifyTagsChanged();
    }

    private async Task RemoveTag(string tag)
    {
        _currentTags.Remove(tag);
        _removeHandlerCache.Remove(tag);
        await NotifyTagsChanged();
    }

    private async Task ClearAll()
    {
        _currentTags.Clear();
        _removeHandlerCache.Clear();
        _inputText = string.Empty;
        _suggestionsOpen = false;
        _suggestionIndex = -1;
        await NotifyTagsChanged();
    }

    private async Task NotifyTagsChanged()
    {
        IReadOnlyList<string>? newTags = _currentTags.Count > 0
            ? _currentTags.ToArray()
            : null;

        Tags = newTags;
        _lastTags = newTags;

        await TagsChanged.InvokeAsync(newTags);
        _validation.NotifyFieldChanged();
    }

    private Func<Task> GetRemoveHandler(string tag)
    {
        if (!_removeHandlerCache.TryGetValue(tag, out var handler))
        {
            handler = () => RemoveTag(tag);
            _removeHandlerCache[tag] = handler;
        }

        return handler;
    }

    // ══════════════════════════════════════════════════════════════════
    // 输入 / 焦点处理器
    // ══════════════════════════════════════════════════════════════════

    private async Task HandleInput(ChangeEventArgs e)
    {
        _inputText = e.Value?.ToString() ?? string.Empty;

        if (HasSuggestions && !string.IsNullOrWhiteSpace(_inputText))
        {
            await LoadSuggestionsAsync(_inputText);
        }
        else
        {
            _filteredSuggestions.Clear();
            _suggestionsOpen = false;
            _suggestionIndex = -1;
        }
    }

    private void HandleFocus()
    {
        _blurCts?.Cancel();
        _blurCts?.Dispose();
        _blurCts = null;

        if (HasSuggestions && _filteredSuggestions.Count > 0 && !string.IsNullOrWhiteSpace(_inputText))
        {
            _suggestionsOpen = true;
        }
    }

    /// <summary>
    /// 焦点真正离开时关闭建议列表；设置了 <see cref="TagInputTrigger.Blur"/> 时
    /// 把键入的文本提交为标签。
    /// </summary>
    /// <remarks>
    /// 提交搭上了推迟关闭建议列表的那同一个延迟的车，这正是它安全的原因。
    /// 点击建议项会在延迟到期前取消该 token，于是加入的是建议项本身
    /// 而其身后未提交的键入片段不会加入。焦点回到输入框同样会取消它。
    /// Tab 无需特判：当 Tab 也是触发方式时，它在按键瞬间就提交并清空文本
    /// 随后的失焦自然无事可做。
    /// </remarks>
    private async Task HandleBlur()
    {
        // 延迟关闭，让建议项上的 mousedown 先触发
        _blurCts?.Cancel();
        _blurCts?.Dispose();
        _blurCts = new CancellationTokenSource();
        var token = _blurCts.Token;

        try
        {
            await Task.Delay(150, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (AddTrigger.HasFlag(TagInputTrigger.Blur))
            {
                // 提交的是键入文本，而不是高亮中的建议项：离开输入框不是
                // 确认一条用户从未确认过的建议的方式。
                await TryAddTag(_inputText);
            }

            _suggestionsOpen = false;
            _suggestionIndex = -1;
            // BL0012（.NET 11）：事件处理器触发的重渲染由框架自动完成
            // 此处的显式 StateHasChanged 无需保留。
        }
        catch (OperationCanceledException)
        {
            // 焦点在超时前回到了输入框——保持建议列表打开
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // 建议
    // ══════════════════════════════════════════════════════════════════

    private async Task LoadSuggestionsAsync(string query)
    {
        _suggestionCts?.Cancel();
        _suggestionCts?.Dispose();
        _suggestionCts = new CancellationTokenSource();
        var token = _suggestionCts.Token;

        try
        {
            if (OnSearchSuggestions is not null)
            {
                await Task.Delay(SuggestionDebounceMs, token);
            }

            IEnumerable<string> results;

            if (OnSearchSuggestions is not null)
            {
                results = await OnSearchSuggestions(query, token);
            }
            else if (Suggestions is not null)
            {
                results = Suggestions.Where(s =>
                    s.Contains(query, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            _filteredSuggestions = results
                .Where(s => AllowDuplicates || !_currentTags.Contains(s, StringComparer.OrdinalIgnoreCase))
                .ToList();

            _suggestionsOpen = _filteredSuggestions.Count > 0;
            _suggestionIndex = -1;
            StateHasChanged();
        }
        catch (OperationCanceledException)
        {
            // 查询快速变化时属于预期情况
        }
    }

    private async Task SelectSuggestion(string suggestion)
    {
        _blurCts?.Cancel();
        await TryAddTag(suggestion);
        StateHasChanged();

        // 选中后让焦点回到输入框
        try
        {
            if (_jsModule is not null && _jsInitialized)
            {
                await _jsModule.InvokeVoidAsync("tagInput.focusInput", _instanceId);
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            // 电路断开时属于预期情况
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // CSS
    // ══════════════════════════════════════════════════════════════════

    private string ContainerCssClass => ClassNames.cn(
        "atom:flex atom:flex-wrap atom:items-center atom:gap-1.5 atom:min-h-10 atom:w-full atom:rounded-md",
        "atom:border atom:px-3 atom:py-2 atom:text-sm",
        "atom:transition-colors",
        "atom:focus-within:outline-none atom:focus-within:ring-2 atom:focus-within:ring-ring atom:focus-within:ring-offset-2",
        Variant switch
        {
            TagInputVariant.Secondary => "atom:border-input atom:bg-secondary/50",
            _ => "atom:border-input atom:bg-background"
        },
        IsInvalid ? "atom:border-destructive atom:focus-within:ring-destructive" : null,
        Disabled ? "atom:opacity-50 atom:cursor-not-allowed" : null,
        Class
    );

    private static string InputCssClass =>
        "atom:flex-1 atom:min-w-[120px] atom:bg-transparent atom:outline-none atom:placeholder:text-muted-foreground atom:disabled:cursor-not-allowed atom:text-sm";

    private static string SuggestionItemCssClass(bool isActive) => ClassNames.cn(
        "atom:flex atom:cursor-pointer atom:select-none atom:items-center atom:rounded-sm atom:px-2 atom:py-1.5 atom:text-sm",
        "atom:hover:bg-accent atom:hover:text-accent-foreground",
        isActive ? "atom:bg-accent atom:text-accent-foreground" : null
    );

    // ══════════════════════════════════════════════════════════════════
    // IAsyncDisposable
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取底层标签录入 input 元素引用，例如供 JS interop 使用。
    /// </summary>
    public ElementReference Element => _inputRef;

    /// <summary>
    /// 让焦点落到底层标签录入 input 元素上。
    /// </summary>
    public ValueTask FocusAsync() => _inputRef.FocusAsync();

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        GC.SuppressFinalize(this);

        _suggestionCts?.Cancel();
        _suggestionCts?.Dispose();
        _suggestionCts = null;

        _blurCts?.Cancel();
        _blurCts?.Dispose();
        _blurCts = null;

        if (_jsModule is not null && _jsInitialized)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("tagInput.dispose", _instanceId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开时属于预期情况
            }
            catch (InvalidOperationException)
            {
                // JS interop 不可用
            }

            _jsModule = null;
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }
}
