#pragma warning disable BL0016 // 三处 interop 皆在 SafeJsAsync 的 try/catch 内（捕获 JSDisconnectedException/JSException/TaskCanceledException/ObjectDisposedException/InvalidOperationException），运行期已守卫；分析器不识别 lambda 间接调用而误报。照 Dock／ResizablePanelGroup 文件级抑制先例，避免逐点 pragma 打散与逐行对齐的行位
using System.Linq.Expressions;
using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>
/// 常驻可见的选项列表，没有触发器，也没有弹层。
/// </summary>
/// <remarks>
/// <para>
/// 适用于需要让选项常驻屏幕的场景——设置面板、穿梭列表、筛选窗格。
/// 若选项应在被需要之前保持隐藏，请使用 <c>Select</c> 或 <c>MultiSelect</c>
/// 它们是藏在触发器背后的同一套列表。
/// </para>
/// <para>
/// 整个列表是一个焦点停靠点，通过方向键移动活动选项
/// 并以 <c>aria-activedescendant</c> 上报。这正是它无需鼠标即可用的原因。
/// </para>
/// </remarks>
/// <typeparam name="TValue">选项值的类型。</typeparam>
public partial class ListBox<TValue> : ComponentBase, IAsyncDisposable
{
    private readonly string listId = $"atom-listbox-{Guid.NewGuid():N}";
    private List<SelectOption<TValue>> visibleOptions = [];
    private HashSet<TValue> selectedValues = new();
    private string searchQuery = string.Empty;
    private string lastSuppliedSearch = string.Empty;
    private int activeIndex = -1;
    private int anchorIndex = -1;
    private bool hasFocus;
    private ElementReference listElement;
    private IJSObjectReference? keyboardCleanup;
    private bool pendingScroll;

    private string typeahead = string.Empty;
    private DateTime typeaheadAt = DateTime.MinValue;

    private IJSObjectReference? jsModule;
    private bool disposed;

    private EditContext? editContext;
    private FieldIdentifier fieldIdentifier;

    /// <summary>
    /// 超过这一时长后，再次键入会重置而非追加搜索串
    /// 以免旧的搜索吞掉新的搜索。与原生列表允许的停留时间一致。
    /// </summary>
    private static readonly TimeSpan typeaheadWindow = TimeSpan.FromMilliseconds(800);

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// 获取或设置可供选择的选项。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<TValue>>? Options { get; set; }

    /// <summary>
    /// 获取或设置可选择一个还是多个选项。
    /// </summary>
    [Parameter]
    public ListBoxSelectionMode SelectionMode { get; set; } = ListBoxSelectionMode.Single;

    /// <summary>
    /// 获取或设置所选值，要求 <see cref="SelectionMode"/> 为
    /// <see cref="ListBoxSelectionMode.Single"/>。
    /// </summary>
    [Parameter]
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置在所选值变化时调用的回调。与
    /// <c>@bind-Value</c> 搭配使用。
    /// </summary>
    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于
    /// <c>EditForm</c> 内的校验。
    /// </summary>
    [Parameter]
    public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置所选值集合，要求 <see cref="SelectionMode"/> 为
    /// <see cref="ListBoxSelectionMode.Multiple"/>。
    /// </summary>
    [Parameter]
    public IEnumerable<TValue>? Values { get; set; }

    /// <summary>
    /// 获取或设置在所选值集合变化时调用的回调。与
    /// <c>@bind-Values</c> 搭配使用。
    /// </summary>
    [Parameter]
    public EventCallback<IEnumerable<TValue>?> ValuesChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值集合的表达式，用于
    /// <c>EditForm</c> 内的校验。
    /// </summary>
    [Parameter]
    public Expression<Func<IEnumerable<TValue>?>>? ValuesExpression { get; set; }

    /// <summary>
    /// 获取或设置显示在列表上方的标签。
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// 获取或设置是否在列表上方显示搜索框。
    /// </summary>
    [Parameter]
    public bool ShowSearch { get; set; }

    /// <summary>
    /// 获取或设置搜索框的占位文本。
    /// </summary>
    [Parameter]
    public string? SearchPlaceholder { get; set; }

    /// <summary>
    /// 获取或设置搜索框中的内容。与 <c>@bind-SearchText</c> 搭配使用。
    /// </summary>
    /// <remarks>
    /// 之所以可绑定，是为了让父组件能确切知道哪些选项在屏幕上可见，而不用去猜测——
    /// <c>PickList</c> 正需要这一点，才能只移动搜索后仍可见的项。
    /// </remarks>
    [Parameter]
    public string? SearchText { get; set; }

    /// <summary>
    /// 获取或设置在搜索框变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> SearchTextChanged { get; set; }

    /// <summary>
    /// 获取或设置是否显示全选复选框。仅适用于
    /// <see cref="ListBoxSelectionMode.Multiple"/>。
    /// </summary>
    /// <remarks>
    /// 它位于列表之上而非列表之内：一个用于选中其他选项的选项，会被朗读为普通选择项
    /// 之一，并占用方向键顺序中的一个位置。全选覆盖搜索框过滤后仍可见的项
    /// 这正是"先收窄、再全取"这一用法有价值的原因。
    /// </remarks>
    [Parameter]
    public bool ShowSelectAll { get; set; } = true;

    /// <summary>
    /// 获取或设置全选复选框的标签。
    /// </summary>
    [Parameter]
    public string? SelectAllLabel { get; set; }

    /// <summary>
    /// 获取或设置无可选项时显示的消息。
    /// </summary>
    [Parameter]
    public string? EmptyMessage { get; set; }

    /// <summary>
    /// 获取或设置将个别选项标记为不可用的谓词。
    /// </summary>
    [Parameter]
    public Func<SelectOption<TValue>, bool>? OptionDisabled { get; set; }

    /// <summary>
    /// 获取或设置选项内容的模板。无论怎样都会绘制选择指示器。
    /// </summary>
    [Parameter]
    public RenderFragment<SelectOption<TValue>>? ItemTemplate { get; set; }

    /// <summary>
    /// 获取或设置以 CSS 长度表示的滚动区域高度。默认为 <c>16rem</c>。
    /// </summary>
    [Parameter]
    public string Height { get; set; } = "16rem";

    /// <summary>
    /// 获取或设置是否禁用整个列表。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置列表的可访问名称，用于没有 <see cref="Label"/> 的场合。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置根元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsMultiple => SelectionMode == ListBoxSelectionMode.Multiple;

    private string EffectiveSearchPlaceholder => SearchPlaceholder ?? Localizer["ListBox.SearchPlaceholder"];

    private string EffectiveSelectAllLabel => SelectAllLabel ?? Localizer["ListBox.SelectAll"];

    private string EffectiveEmptyMessage => EmptyMessage
        ?? (searchQuery.Length > 0 ? Localizer["ListBox.NoMatches"] : Localizer["ListBox.Empty"]);

    private string SelectionSummary => Localizer["ListBox.SelectedCount", selectedValues.Count];

    private string? ActiveDescendantId =>
        hasFocus && activeIndex >= 0 && activeIndex < visibleOptions.Count ? OptionId(activeIndex) : null;

    private string OptionId(int index) => $"{listId}-option-{index}";

    private string RootClass => ClassNames.cn(
        "atom:w-full",
        Disabled ? "atom:opacity-60" : null,
        Class);

    private const string frameClass =
        "atom:rounded-md atom:border atom:border-input atom:bg-background atom:overflow-hidden";

    private const string checkboxClass =
        "atom:h-4 atom:w-4 atom:shrink-0 atom:rounded-sm atom:border atom:border-primary atom:flex atom:items-center atom:justify-center " +
        "atom:focus-visible:outline-none atom:disabled:cursor-not-allowed atom:disabled:opacity-50 " +
        "atom:data-[state=checked]:bg-primary atom:data-[state=checked]:text-primary-foreground " +
        "atom:data-[state=indeterminate]:bg-primary atom:data-[state=indeterminate]:text-primary-foreground " +
        "atom:data-[state=unchecked]:bg-background";

    private string IndicatorClass(bool selected) => IsMultiple
        ? ClassNames.cn(
            "atom:h-4 atom:w-4 atom:shrink-0 atom:rounded-sm atom:border atom:border-primary atom:flex atom:items-center atom:justify-center",
            selected ? "atom:bg-primary atom:text-primary-foreground" : "atom:bg-background")
        : "atom:h-4 atom:w-4 atom:shrink-0 atom:flex atom:items-center atom:justify-center";

    private static string OptionClass(bool optionDisabled) => ClassNames.cn(
        "atom:relative atom:flex atom:cursor-pointer atom:select-none atom:items-center atom:gap-2 atom:rounded-sm atom:px-2 atom:py-1.5 atom:text-sm atom:outline-none",
        "atom:data-[active=true]:bg-accent atom:data-[active=true]:text-accent-foreground",
        "atom:data-[selected=true]:font-medium",
        optionDisabled ? "atom:pointer-events-none atom:opacity-50" : "atom:hover:bg-accent/50");

    private enum SelectAllKind
    {
        None,
        Indeterminate,
        All
    }

    private SelectAllKind SelectAllState
    {
        get
        {
            var selectable = visibleOptions.Where(o => !IsOptionDisabled(o)).ToList();
            if (selectable.Count == 0)
            {
                return SelectAllKind.None;
            }

            var chosen = selectable.Count(o => IsSelected(o.Value));
            return chosen == 0 ? SelectAllKind.None
                : chosen == selectable.Count ? SelectAllKind.All
                : SelectAllKind.Indeterminate;
        }
    }

    protected override void OnParametersSet()
    {
        // Adopt the supplied term only when it is a new one. Comparing against searchQuery instead
        // would undo the person's typing on the next render, because their text is exactly what the
        // parameter does not yet hold.
        var supplied = SearchText ?? string.Empty;
        if (supplied != lastSuppliedSearch)
        {
            lastSuppliedSearch = supplied;
            searchQuery = supplied;
        }

        selectedValues = IsMultiple
            ? [.. Values ?? []]
            : Value is null ? [] : [Value];

        RebuildVisibleOptions();

        if (CascadedEditContext is not null)
        {
            if (IsMultiple && ValuesExpression is not null)
            {
                editContext = CascadedEditContext;
                fieldIdentifier = FieldIdentifier.Create(ValuesExpression);
            }
            else if (!IsMultiple && ValueExpression is not null)
            {
                editContext = CascadedEditContext;
                fieldIdentifier = FieldIdentifier.Create(ValueExpression);
            }
        }
    }

    private void RebuildVisibleOptions()
    {
        var all = Options?.Where(o => o is not null) ?? [];
        visibleOptions = [.. all.Where(o => ListBoxSearch.Matches(o.Text, searchQuery))];

        if (activeIndex >= visibleOptions.Count)
        {
            activeIndex = visibleOptions.Count - 1;
        }
    }

    private bool IsSelected(TValue value) => selectedValues.Contains(value);

    private bool IsOptionDisabled(SelectOption<TValue> option) => OptionDisabled?.Invoke(option) ?? false;

    // ── Pointer ────────────────────────────────────────────────────────

    private async Task HandleOptionClickAsync(int index, MouseEventArgs args)
    {
        if (Disabled || index < 0 || index >= visibleOptions.Count)
        {
            return;
        }

        var option = visibleOptions[index];
        if (IsOptionDisabled(option))
        {
            return;
        }

        activeIndex = index;

        if (IsMultiple && args.ShiftKey && anchorIndex >= 0)
        {
            await SelectRangeAsync(anchorIndex, index);
            return;
        }

        anchorIndex = index;

        // Ctrl or Cmd keeps a multi-select additive without needing the checkbox, which is what a
        // list behaves like everywhere else. Without a modifier a plain click still toggles,
        // because the checkboxes say the list is additive.
        await ActivateAsync(option);
    }

    // ── Keyboard ───────────────────────────────────────────────────────

    private async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        if (Disabled)
        {
            return;
        }

        switch (args.Key)
        {
            case "ArrowDown":
                await MoveAsync(1, args.ShiftKey);
                return;
            case "ArrowUp":
                await MoveAsync(-1, args.ShiftKey);
                return;
            case "Home":
                await MoveToAsync(FirstEnabled(0, 1), args.ShiftKey);
                return;
            case "End":
                await MoveToAsync(FirstEnabled(visibleOptions.Count - 1, -1), args.ShiftKey);
                return;
            case "PageDown":
                await MoveAsync(10, args.ShiftKey);
                return;
            case "PageUp":
                await MoveAsync(-10, args.ShiftKey);
                return;
            case " ":
            case "Enter":
                if (activeIndex >= 0 && activeIndex < visibleOptions.Count)
                {
                    anchorIndex = activeIndex;
                    await ActivateAsync(visibleOptions[activeIndex]);
                }
                return;
            case "a" or "A" when IsMultiple && (args.CtrlKey || args.MetaKey):
                await SelectAllAsync();
                return;
        }

        // A single printable character jumps to the next option starting with it.
        if (args.Key.Length == 1 && !args.CtrlKey && !args.MetaKey && !args.AltKey)
        {
            await TypeaheadAsync(args.Key);
            return;
        }
    }

    private async Task HandleSearchInputAsync(ChangeEventArgs args)
    {
        searchQuery = args.Value?.ToString() ?? string.Empty;
        RebuildVisibleOptions();
        // The old active index refers to a list that no longer exists.
        activeIndex = visibleOptions.Count > 0 ? FirstEnabled(0, 1) : -1;
        anchorIndex = -1;
        // lastSuppliedSearch deliberately untouched: it records what the parent last supplied, not
        // what was typed. Setting it here would make the next parameter pass look like a change and
        // wipe the typing on a list box whose SearchText nobody is binding.
        await SearchTextChanged.InvokeAsync(searchQuery);
    }

    private async Task HandleSearchKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key is not ("ArrowDown" or "Enter"))
        {
            return;
        }

        // Moving down out of the search box hands the arrows to the list, which is where they mean
        // something. Focus has to actually move: aria-activedescendant on an input the list does
        // not own would point at options the screen reader cannot reach.
        activeIndex = visibleOptions.Count > 0 ? FirstEnabled(0, 1) : -1;
        await FocusListAsync();
    }

    private async Task TypeaheadAsync(string key)
    {
        var now = DateTime.UtcNow;
        var expired = now - typeaheadAt > typeaheadWindow;

        // Pressing one letter over and over walks through the options starting with it, rather
        // than searching for "ccc" and finding nothing. Anything else spells out a prefix.
        var cycling = !expired && typeahead.Length > 0 && typeahead.All(c => c == key[0]);

        typeahead = expired ? key : typeahead + key;
        typeaheadAt = now;

        var needle = cycling ? key : typeahead;
        var start = activeIndex < 0 ? 0 : activeIndex;
        // A fresh search may match where the caret already is; a cycle must move off it.
        var from = cycling || needle.Length == 1 ? start + 1 : start;

        for (var offset = 0; offset < visibleOptions.Count; offset++)
        {
            var index = (((from + offset) % visibleOptions.Count) + visibleOptions.Count) % visibleOptions.Count;
            var option = visibleOptions[index];
            if (IsOptionDisabled(option) || !option.Text.StartsWith(needle, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            await MoveToAsync(index, extend: false);
            return;
        }
    }

    private async Task MoveAsync(int delta, bool extend)
    {
        if (visibleOptions.Count == 0)
        {
            return;
        }

        var start = activeIndex < 0 ? (delta > 0 ? -1 : visibleOptions.Count) : activeIndex;
        var step = Math.Sign(delta);
        var target = Math.Clamp(start + delta, 0, visibleOptions.Count - 1);

        // Land on something choosable rather than stopping dead on a disabled row.
        target = FirstEnabled(target, step == 0 ? 1 : step);
        await MoveToAsync(target, extend);
    }

    private int FirstEnabled(int from, int step)
    {
        for (var index = Math.Clamp(from, 0, Math.Max(visibleOptions.Count - 1, 0));
             index >= 0 && index < visibleOptions.Count;
             index += step)
        {
            if (!IsOptionDisabled(visibleOptions[index]))
            {
                return index;
            }
        }

        // Nothing in that direction; keep whatever was active rather than jumping somewhere else.
        return activeIndex;
    }

    private async Task MoveToAsync(int index, bool extend)
    {
        if (index < 0 || index >= visibleOptions.Count)
        {
            return;
        }

        activeIndex = index;
        pendingScroll = true;

        if (IsMultiple)
        {
            if (extend)
            {
                if (anchorIndex < 0)
                {
                    anchorIndex = index;
                }
                await SelectRangeAsync(anchorIndex, index);
                return;
            }

            anchorIndex = index;
            return;
        }

        // A single-select list selects as it moves, the way a native one does.
        anchorIndex = index;
        await CommitSingleAsync(visibleOptions[index].Value);
    }

    // ── Selection ──────────────────────────────────────────────────────

    private async Task ActivateAsync(SelectOption<TValue> option)
    {
        if (IsMultiple)
        {
            if (!selectedValues.Add(option.Value))
            {
                selectedValues.Remove(option.Value);
            }
            await CommitMultipleAsync();
            return;
        }

        await CommitSingleAsync(option.Value);
    }

    private async Task SelectRangeAsync(int from, int to)
    {
        var start = Math.Min(from, to);
        var end = Math.Max(from, to);

        for (var index = start; index <= end && index < visibleOptions.Count; index++)
        {
            var option = visibleOptions[index];
            if (!IsOptionDisabled(option))
            {
                selectedValues.Add(option.Value);
            }
        }

        await CommitMultipleAsync();
    }

    private async Task ToggleSelectAllAsync()
    {
        if (SelectAllState == SelectAllKind.All)
        {
            foreach (var option in visibleOptions.Where(o => !IsOptionDisabled(o)))
            {
                selectedValues.Remove(option.Value);
            }
            await CommitMultipleAsync();
            return;
        }

        await SelectAllAsync();
    }

    private async Task SelectAllAsync()
    {
        // Only what the search has left visible. Taking hidden options too would mean the box
        // quietly selecting things the person cannot see.
        foreach (var option in visibleOptions.Where(o => !IsOptionDisabled(o)))
        {
            selectedValues.Add(option.Value);
        }

        await CommitMultipleAsync();
    }

    private async Task CommitSingleAsync(TValue value)
    {
        selectedValues = [value];
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyEditContext();
    }

    private async Task CommitMultipleAsync()
    {
        var values = selectedValues.Count > 0 ? selectedValues.ToList() : null;
        Values = values;
        await ValuesChanged.InvokeAsync(values);
        NotifyEditContext();
    }

    private void NotifyEditContext()
    {
        if (editContext is not null && fieldIdentifier.FieldName is not null)
        {
            editContext.NotifyFieldChanged(fieldIdentifier);
        }
    }

    // ── Focus and scrolling ────────────────────────────────────────────

    private void HandleFocus()
    {
        hasFocus = true;
        if (activeIndex < 0 && visibleOptions.Count > 0)
        {
            // Open on the selection when there is one, so arrowing continues from where the person
            // left off rather than from the top.
            var selected = visibleOptions.FindIndex(o => IsSelected(o.Value));
            activeIndex = selected >= 0 ? selected : FirstEnabled(0, 1);
            pendingScroll = true;
        }
    }

    private void HandleBlur() => hasFocus = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (keyboardCleanup is null && !disposed)
        {
            await SafeJsAsync(async module => keyboardCleanup = await module.InvokeAsync<IJSObjectReference>(
                "keyboardNav.setupListBox", listElement));
        }

        if (!pendingScroll || disposed)
        {
            return;
        }

        pendingScroll = false;
        if (activeIndex < 0 || activeIndex >= visibleOptions.Count)
        {
            return;
        }

        await SafeJsAsync(module => module.InvokeVoidAsync(
            "elementUtils.scrollIntoView", OptionId(activeIndex), "nearest", "instant").AsTask());
    }

    private async Task FocusListAsync() =>
        await SafeJsAsync(module => module.InvokeVoidAsync("elementUtils.focusElement", listId).AsTask());

    private async Task SafeJsAsync(Func<IJSObjectReference, Task> action)
    {
        try
        {
            jsModule ??= await PrimitiveModules.GetAsync(JS);
            if (!disposed)
            {
                await action(jsModule);
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // Expected during circuit disconnect or prerendering
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        disposed = true;
        if (keyboardCleanup is not null)
        {
            try
            {
                await keyboardCleanup.InvokeVoidAsync("dispose");
                await keyboardCleanup.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // The browser may have gone away before the component unmounted.
            }
        }
    }
}
