using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 归组 <see cref="Chip"/> 子元素并持有它们的选择，使一排芯片可以充当单选或多选过滤器。
/// </summary>
/// <typeparam name="TValue">每个芯片携带的值的类型。</typeparam>
/// <remarks>
/// <para>
/// 每个参与的芯片都要提供 <see cref="Chip.Value"/>。集合用
/// <see cref="EqualityComparer{T}.Default"/> 匹配值，所以 record 或基元类型无需额外配置
/// 即可工作，而类类型需要实现值相等才能匹配。
/// </para>
/// <para>
/// 当相应的变化回调被接上时，选择即为受控模式（<see cref="ChipSelectionMode.Single"/>
/// 用 <c>@bind-Value</c>，<see cref="ChipSelectionMode.Multiple"/> 用 <c>@bind-Values</c>）。
/// 没有绑定时，集合自行保持选择，并把 <see cref="Value"/> 或 <see cref="Values"/>
/// 当作初始状态。
/// </para>
/// <para>
/// 集合还为芯片提供默认的变体、尺寸、移除按钮与对勾。芯片自己声明的优先。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ChipSet TValue="string" SelectionMode="ChipSelectionMode.Multiple" @bind-Values="tags"&gt;
///     &lt;Chip Value="@("design")"&gt;Design&lt;/Chip&gt;
///     &lt;Chip Value="@("build")"&gt;Build&lt;/Chip&gt;
/// &lt;/ChipSet&gt;
/// </code>
/// </example>
public partial class ChipSet<TValue> : ComponentBase
{
    private readonly ChipSetContext setContext = new();
    private TValue? currentValue;
    private List<TValue> currentValues = [];
    private bool initialized;

    /// <summary>
    /// 获取或设置一次可选中多少芯片。默认为 <see cref="ChipSelectionMode.Single"/>。
    /// 仅展示的芯片行请使用 <see cref="ChipSelectionMode.None"/>。
    /// </summary>
    [Parameter]
    public ChipSelectionMode SelectionMode { get; set; } = ChipSelectionMode.Single;

    /// <summary>
    /// 获取或设置 <see cref="ChipSelectionMode.Single"/> 模式下的选中值。
    /// 支持 <c>@bind-Value</c> 双向绑定。
    /// </summary>
    [Parameter]
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置在选中值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置 <see cref="ChipSelectionMode.Multiple"/> 模式下的选中值集合。
    /// 支持 <c>@bind-Values</c> 双向绑定。
    /// </summary>
    [Parameter]
    public List<TValue>? Values { get; set; }

    /// <summary>
    /// 获取或设置在选中值集合变化时调用的回调。回调收到的是新列表，
    /// 因此绑定的字段会被整体替换而不是原地修改。
    /// </summary>
    [Parameter]
    public EventCallback<List<TValue>> ValuesChanged { get; set; }

    /// <summary>
    /// 获取或设置集合是否始终保持至少一个芯片被选中。为 <c>true</c> 时，
    /// 点击最后一个选中芯片不会产生任何效果。请提供初始选择。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置集合内芯片的默认变体。
    /// </summary>
    [Parameter]
    public ChipVariant Variant { get; set; } = ChipVariant.Default;

    /// <summary>
    /// 获取或设置集合内芯片的默认尺寸。
    /// </summary>
    [Parameter]
    public ChipSize Size { get; set; } = ChipSize.Default;

    /// <summary>
    /// 获取或设置集合内芯片是否默认显示移除按钮。
    /// </summary>
    [Parameter]
    public bool Dismissible { get; set; }

    /// <summary>
    /// 获取或设置集合内选中芯片是否默认显示对勾。
    /// </summary>
    [Parameter]
    public bool ShowCheckMark { get; set; }

    /// <summary>
    /// 获取或设置是否禁用集合内的所有芯片。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置芯片是否换行到更多行。为 <c>false</c> 时集合改为横向滚动。
    /// 默认为 <c>true</c>。
    /// </summary>
    [Parameter]
    public bool Wrap { get; set; } = true;

    /// <summary>
    /// 获取或设置当集合内芯片被移除时调用的回调。被移除的值同时会从选择中剔除。
    /// 把芯片从标记中移除是使用者的工作。
    /// </summary>
    [Parameter]
    public EventCallback<TValue> OnDismiss { get; set; }

    /// <summary>
    /// 获取或设置要渲染的芯片。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到集合上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        setContext.IsSelected = IsSelectedCore;
        setContext.ToggleAsync = ToggleCoreAsync;
        setContext.DismissAsync = DismissCoreAsync;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // 相应的回调被接上时即为受控；否则传入参数只作初始选择，此后由集合持有。
        if (!initialized || ValueChanged.HasDelegate)
        {
            currentValue = Value;
        }

        if (!initialized || ValuesChanged.HasDelegate)
        {
            currentValues = Values is null ? [] : [.. Values];
        }

        initialized = true;

        setContext.Variant = Variant;
        setContext.Size = Size;
        setContext.Dismissible = Dismissible;
        setContext.ShowCheckMark = ShowCheckMark;
        setContext.Disabled = Disabled;
        setContext.SelectionMode = SelectionMode;
    }

    // --- 选择 ---

    private bool IsSelectedCore(object? value)
    {
        if (SelectionMode == ChipSelectionMode.None || value is not TValue typed)
        {
            return false;
        }

        return SelectionMode == ChipSelectionMode.Single
            ? EqualityComparer<TValue>.Default.Equals(currentValue, typed)
            : currentValues.Contains(typed);
    }

    private async Task ToggleCoreAsync(object? value)
    {
        if (SelectionMode == ChipSelectionMode.None || Disabled || value is not TValue typed)
        {
            return;
        }

        if (SelectionMode == ChipSelectionMode.Single)
        {
            var wasSelected = EqualityComparer<TValue>.Default.Equals(currentValue, typed);

            if (wasSelected && Required)
            {
                return;
            }

            currentValue = wasSelected ? default : typed;

            if (ValueChanged.HasDelegate)
            {
                await ValueChanged.InvokeAsync(currentValue);
            }
        }
        else
        {
            var next = new List<TValue>(currentValues);

            if (next.Contains(typed))
            {
                if (Required && next.Count == 1)
                {
                    return;
                }

                next.Remove(typed);
            }
            else
            {
                next.Add(typed);
            }

            currentValues = next;

            if (ValuesChanged.HasDelegate)
            {
                await ValuesChanged.InvokeAsync(next);
            }
        }

        setContext.NotifyStateChanged();
        StateHasChanged();
    }

    private async Task DismissCoreAsync(object? value)
    {
        if (Disabled || (Required && IsSelectedCore(value)
            && (SelectionMode == ChipSelectionMode.Single || currentValues.Count == 1)))
        {
            return;
        }
        if (value is not TValue typed)
        {
            return;
        }

        var changed = false;

        if (SelectionMode == ChipSelectionMode.Single
            && EqualityComparer<TValue>.Default.Equals(currentValue, typed))
        {
            currentValue = default;
            changed = true;

            if (ValueChanged.HasDelegate)
            {
                await ValueChanged.InvokeAsync(currentValue);
            }
        }
        else if (SelectionMode == ChipSelectionMode.Multiple && currentValues.Contains(typed))
        {
            var next = new List<TValue>(currentValues);
            next.Remove(typed);
            currentValues = next;
            changed = true;

            if (ValuesChanged.HasDelegate)
            {
                await ValuesChanged.InvokeAsync(next);
            }
        }

        if (OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync(typed);
        }

        if (changed)
        {
            setContext.NotifyStateChanged();
            StateHasChanged();
        }
    }

    // --- CSS ---

    private string CssClass => ClassNames.cn(
        "atom:flex atom:items-center atom:gap-2",
        Wrap ? "atom:flex-wrap" : "atom:overflow-x-auto atom:[&>*]:shrink-0",
        Class
    );
}
