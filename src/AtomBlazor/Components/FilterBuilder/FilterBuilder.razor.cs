using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Filtering;

namespace AtomBlazor;

/// <summary>
/// 用于构建数据筛选表达式的容器组件。
/// 管理由筛选条件与分组构成、带 AND/OR 逻辑的树。
/// 支持通过 <c>@bind-Filter</c> 双向绑定。
/// </summary>
public partial class FilterBuilder : ComponentBase, IDisposable
{
    [Inject] private ILocalizer Localizer { get; set; } = default!;

    private FilterBuilderContext? context;
    private Timer? debounceTimer;
    private int debounceVersion;
    private bool disposed;
    private FilterDefinition? draftFilter;
    private FilterDefinition? lastFilter;
    private string? selectedPresetId;

    /// <summary>可复用的筛选定义，使用前会被克隆。</summary>
    [Parameter] public IReadOnlyList<FilterPreset> Presets { get; set; } = [];
    /// <summary>预设以按钮还是下拉框呈现。</summary>
    [Parameter] public FilterPresetDisplay PresetDisplay { get; set; }
    /// <summary>字段选择使用可搜索的 Combobox。</summary>
    [Parameter] public bool SearchableFields { get; set; }
    /// <summary>可选的自定义值编辑器，以字段名为键。</summary>
    [Parameter] public IReadOnlyDictionary<string, RenderFragment<FilterValueEditorContext>>? ValueEditors { get; set; }

    /// <summary>将克隆出的预设载入活动草稿。显式应用模式的构建器仍需点击 Apply。</summary>
    public Task ApplyPresetAsync(string id) => InvokeAsync(async () =>
    {
        var preset = Presets.FirstOrDefault(p => p.Id == id)
            ?? throw new ArgumentException("No filter preset has the requested ID.", nameof(id));
        if (MaxConditions.HasValue && preset.Filter.TotalConditionCount > MaxConditions.Value)
        {
            throw new InvalidOperationException("The preset exceeds MaxConditions.");
        }
        if (FilterDepth(preset.Filter) > MaxDepth) { throw new InvalidOperationException("The preset exceeds MaxDepth."); }
        debounceTimer?.Dispose();
        debounceVersion++;
        var copy = preset.Filter.Clone();
        var target = draftFilter ?? Filter;
        target.Conditions = copy.Conditions;
        target.Groups = copy.Groups;
        target.Operator = copy.Operator;
        selectedPresetId = id;
        if (!ShowApplyButton) { await NotifyFilterChanged(); }
        StateHasChanged();
    });

    private Task SelectPresetAsync(string? id) => id is null ? Task.CompletedTask : ApplyPresetAsync(id);
    private static int FilterDepth(FilterDefinition filter) => filter.Groups.Count == 0 ? 0 : 1 + filter.Groups.Max(FilterDepth);

    /// <summary>
    /// 获取或设置当前筛选状态。支持通过 <c>@bind-Filter</c> 双向绑定。
    /// </summary>
    [Parameter]
    public FilterDefinition Filter { get; set; } = new();

    /// <summary>
    /// 获取或设置筛选改变时调用的回调（双向绑定）。
    /// </summary>
    [Parameter]
    public EventCallback<FilterDefinition> FilterChanged { get; set; }

    /// <summary>
    /// 获取或设置可供筛选的字段。
    /// </summary>
    [Parameter]
    public IEnumerable<FilterField> Fields { get; set; } = Enumerable.Empty<FilterField>();

    /// <summary>
    /// 获取或设置筛选改变时调用的回调（用于应用筛选之类的副作用）。
    /// </summary>
    [Parameter]
    public EventCallback<FilterDefinition> OnFilterChanged { get; set; }

    /// <summary>
    /// 获取或设置筛选组的最大嵌套深度。默认为 3。
    /// </summary>
    [Parameter]
    public int MaxDepth { get; set; } = 3;

    /// <summary>
    /// 获取或设置允许的最大条件总数。为 null 表示不限。
    /// </summary>
    [Parameter]
    public int? MaxConditions { get; set; }

    /// <summary>
    /// 获取或设置新分组的默认逻辑运算符。默认为 <see cref="LogicalOperator.And"/>。
    /// </summary>
    [Parameter]
    public LogicalOperator DefaultOperator { get; set; } = LogicalOperator.And;

    /// <summary>
    /// 获取或设置是否显示显式的 Apply 按钮，而不是在变化时自动应用。
    /// 默认为 false（带防抖的自动应用）。
    /// </summary>
    [Parameter]
    public bool ShowApplyButton { get; set; }

    /// <summary>
    /// 获取或设置 Apply 按钮的文本。默认为 "Apply Filter"。
    /// 仅在 <see cref="ShowApplyButton"/> 为 true 时使用。
    /// </summary>
    [Parameter]
    public string? ApplyButtonText { get; set; }

    /// <summary>
    /// 获取或设置 Clear 按钮的文本。默认为 "Clear"。
    /// 仅在 <see cref="ShowApplyButton"/> 为 true 时使用。
    /// </summary>
    [Parameter]
    public string? ClearButtonText { get; set; }

    /// <summary>
    /// 获取或设置是否使用紧凑布局，以便嵌入行内/工具栏。
    /// </summary>
    [Parameter]
    public bool Compact { get; set; }

    /// <summary>
    /// 获取或设置应用到容器的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    protected override void OnParametersSet()
    {
        // When ShowApplyButton is true, work on a draft copy so the bound Filter
        // is not mutated until the user explicitly clicks Apply.
        // Re-clone when the Filter parameter reference changes (e.g., loading a saved filter).
        var filterChanged = !ReferenceEquals(lastFilter, Filter);
        lastFilter = Filter;

        if (ShowApplyButton && (draftFilter == null || filterChanged))
        {
            draftFilter = Filter.Clone();
        }
        else if (!ShowApplyButton)
        {
            draftFilter = null;
        }

        var activeFilter = draftFilter ?? Filter;

        context = new FilterBuilderContext
        {
            Fields = Fields,
            MaxDepth = MaxDepth,
            MaxConditions = MaxConditions,
            DefaultOperator = DefaultOperator,
            Compact = Compact,
            SearchableFields = SearchableFields,
            ValueEditors = ValueEditors,
            RootFilter = activeFilter,
            NotifyChanged = OnFilterTreeChanged
        };
    }

    private void OnFilterTreeChanged()
    {
        selectedPresetId = null;
        if (ShowApplyButton)
        {
            StateHasChanged();
            return;
        }

        debounceTimer?.Dispose();
        var capturedVersion = ++debounceVersion;
        debounceTimer = new Timer(
            _ => _ = HandleDebouncedChangeAsync(capturedVersion),
            null,
            300,
            Timeout.Infinite);
    }

    private async Task HandleDebouncedChangeAsync(int capturedVersion)
    {
        try
        {
            if (disposed || capturedVersion != debounceVersion)
            {
                return;
            }
            await InvokeAsync(async () =>
            {
                if (capturedVersion != debounceVersion)
                {
                    return;
                }
                await NotifyFilterChanged();
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // Component was disposed between timer fire and InvokeAsync
        }
    }

    private async Task HandleApply()
    {
        if (draftFilter != null)
        {
            // Deep-clone draft state into Filter so the two remain independent.
            // Without cloning, subsequent draft edits would mutate Filter directly.
            var snapshot = draftFilter.Clone();
            Filter.Conditions = snapshot.Conditions;
            Filter.Groups = snapshot.Groups;
            Filter.Operator = snapshot.Operator;
        }
        await NotifyFilterChanged();
    }

    private async Task HandleClear()
    {
        selectedPresetId = null;
        var activeFilter = draftFilter ?? Filter;
        activeFilter.Conditions.Clear();
        activeFilter.Groups.Clear();
        activeFilter.Operator = DefaultOperator;

        if (draftFilter != null)
        {
            // Also clear the bound Filter on explicit Clear
            Filter.Conditions.Clear();
            Filter.Groups.Clear();
            Filter.Operator = DefaultOperator;
        }
        await NotifyFilterChanged();
    }

    private async Task NotifyFilterChanged()
    {
        Filter.Version++;

        if (FilterChanged.HasDelegate)
        {
            await FilterChanged.InvokeAsync(Filter);
        }
        if (OnFilterChanged.HasDelegate)
        {
            await OnFilterChanged.InvokeAsync(Filter);
        }
    }

    private string ContainerCssClass => ClassNames.cn(
        "atom:space-y-3",
        Compact ? "atom:text-sm" : null,
        Class
    );

    private static string ActionsCssClass => "atom:flex atom:items-center atom:gap-2 atom:pt-2";

    public void Dispose()
    {
        if (!disposed)
        {
            debounceTimer?.Dispose();
            disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
