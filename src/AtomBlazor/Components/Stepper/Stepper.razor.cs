using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 展示一个人处于步骤序列中的哪个位置，并可选地展示活动步骤的内容。
/// 搭配 <see cref="Step"/> 子级组合使用。
/// </summary>
/// <remarks>
/// <para>
/// 步进器是一个纯粹的进度指示器：它不持有表单、不校验任何东西，也不渲染任何导航按钮。
/// 通过 <c>@bind-ActiveStep</c> 从你自己的控件驱动它，或者设置 <see cref="Clickable"/>
/// 让人靠点击在步骤间移动。若需要带逐步校验与内置"上一步/下一步"按钮的多步表单，
/// 请改用 <see cref="FormWizard"/>。
/// </para>
/// <para>
/// 特性：
/// - 水平与垂直两种布局
/// - 由位置推导的步骤状态，并可逐步覆写为错误或跳过
/// - 可选的步骤内容，只为活动步骤渲染
/// - 向屏幕阅读器播报步骤位置与状态
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Stepper @bind-ActiveStep="step" Clickable="true"&gt;
/// &lt;Step Title="Cart" /&gt;
/// &lt;Step Title="Address" /&gt;
/// &lt;Step Title="Payment" /&gt;
/// &lt;/Stepper&gt;
/// </code>
/// </example>
public partial class Stepper : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    private ElementReference root;
    private IJSObjectReference? orderModule;
    private DotNetObjectReference<Stepper>? selfRef;
    private bool disposed;

    /// <summary>将步骤指示器与当前子级在 DOM 中的顺序同步。</summary>
    [JSInvokable]
    public Task SynchronizeOrder(string[] ids) => InvokeAsync(() =>
    {
        if (disposed)
        {
            return;
        }
        var owners = stepOwners.Keys.ToDictionary(owner => owner.StepId, StringComparer.Ordinal);
        if (ids.Length != owners.Count || ids.Distinct(StringComparer.Ordinal).Count() != owners.Count
            || ids.Any(id => !owners.ContainsKey(id)))
        {
            return;
        }
        var ordered = ids.Select(id => owners[id]).ToList();
        if (ordered.Select((owner, index) => stepOwners[owner] == index).All(same => same))
        {
            return;
        }
        var infos = ordered.Select(owner => steps[stepOwners[owner]]).ToArray();
        steps.Clear();
        steps.AddRange(infos);
        for (var i = 0; i < ordered.Count; i++)
        {
            stepOwners[ordered[i]] = i;
            ordered[i].SetIndex(i);
        }
        StateHasChanged();
    });

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }
        try
        {
            orderModule = await ComponentModules.GetCoreAsync(JSRuntime);
            selfRef = DotNetObjectReference.Create(this);
            await orderModule.InvokeVoidAsync("stepper.initialize", root, selfRef);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or InvalidOperationException or TaskCanceledException or ObjectDisposedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (orderModule is not null)
        {
            try { await orderModule.InvokeVoidAsync("stepper.dispose", root); }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException) { }
        }
        selfRef?.Dispose();
        GC.SuppressFinalize(this);
    }

 // --- 注册（按实例为键，所以一次重渲染不会重复登记一个步骤） ---
    private readonly List<StepInfo> steps = [];
    private readonly Dictionary<Step, int> stepOwners = [];

    // --- 状态 ---
    private int activeStep;
    private bool initialized;
    private int lastRenderedStepCount = -1;

    /// <summary>
    /// 获取或设置活动步骤的从零开始的索引。支持通过 <c>@bind-ActiveStep</c> 双向绑定。
    /// 没有绑定它时，本值只是初始步骤，此后由组件自己持有
    /// 该值。
    /// </summary>
    [Parameter]
    public int ActiveStep { get; set; }

    /// <summary>
    /// 获取或设置在活动步骤改变时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<int> ActiveStepChanged { get; set; }

    /// <summary>
    /// 获取或设置指示器的布局。
    /// </summary>
    [Parameter]
    public StepperOrientation Orientation { get; set; } = StepperOrientation.Horizontal;

    /// <summary>
    /// 获取或设置一个步骤能否通过点击被激活。默认为 <c>false</c>，
    /// 此时指示器渲染为静态文本。
    /// </summary>
    [Parameter]
    public bool Clickable { get; set; }

    /// <summary>
    /// 获取或设置各个步骤及其内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置根元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置步骤指示列表的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? IndicatorClass { get; set; }

    /// <summary>
    /// 获取或设置步骤内容区域的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? ContentClass { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // --- 内部 API，由 Step 经由其级联参数使用 ---

    /// <summary>获取活动步骤的从零开始的索引。</summary>
    internal int ActiveStepIndex => EffectiveActiveStep;

    /// <summary>
    /// 在活动索引内保持于已注册范围内的取值。
    /// </summary>
    /// <remarks>
    /// 步骤在子内容渲染时才注册，那时 <c>OnParametersSet</c> 早已运行完、
    /// 其上方的指示器也早已渲染完。所以索引到达时无法钳制——
    /// 那一刻步骤数还是零——于是改为每次读取时
    /// 钳制。
    /// </remarks>
    private int EffectiveActiveStep => steps.Count == 0
        ? Math.Max(activeStep, 0)
        : Math.Clamp(activeStep, 0, steps.Count - 1);

    /// <summary>获取已注册步骤的数量。</summary>
    internal int StepCount => steps.Count;

    /// <summary>
    /// 注册一个步骤并返回其索引。以组件实例为键，因为
    /// <c>OnParametersSet</c> 在一个渲染周期内可能运行多次。
    /// </summary>
    internal int RegisterStep(Step owner, StepInfo info)
    {
        if (stepOwners.TryGetValue(owner, out var existing))
        {
            steps[existing] = info;
            return existing;
        }

        var index = steps.Count;
        steps.Add(info);
        stepOwners[owner] = index;
        return index;
    }

    /// <summary>注销一个已释放的步骤，并保持剩余索引连续。</summary>
    internal void UnregisterStep(Step owner)
    {
        if (!stepOwners.TryGetValue(owner, out var removed))
        {
            return;
        }

        steps.RemoveAt(removed);
        stepOwners.Remove(owner);

        foreach (var pair in stepOwners.Where(pair => pair.Value > removed).ToList())
        {
            stepOwners[pair.Key] = pair.Value - 1;
        }

    }

    /// <summary>获取给定索引处步骤的视觉状态。</summary>
    internal StepState GetStepState(int index)
    {
        if (index >= 0 && index < steps.Count && steps[index].State is { } explicitState)
        {
            return explicitState;
        }

        var active = EffectiveActiveStep;

        if (index == active)
        {
            return StepState.Active;
        }

        return index < active ? StepState.Completed : StepState.Pending;
    }

    /// <summary>给定索引处的步骤能否被点击激活。</summary>
    internal bool CanNavigateToStep(int index) =>
        Clickable
        && index >= 0
        && index < steps.Count
        && index != EffectiveActiveStep
        && !steps[index].Disabled;

    /// <summary>激活给定索引处的步骤，前提是它可以导航。</summary>
    internal async Task GoToStepAsync(int index)
    {
        if (!CanNavigateToStep(index))
        {
            return;
        }

        activeStep = index;

        if (ActiveStepChanged.HasDelegate)
        {
            await ActiveStepChanged.InvokeAsync(activeStep);
        }

        StateHasChanged();
    }

    // --- 生命周期 ---

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!initialized || ActiveStepChanged.HasDelegate)
        {
            activeStep = ActiveStep;
            initialized = true;
        }
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // 步骤在子内容渲染时才注册，而那发生在其上方指示器之后。
        // 再多渲染一次，让指示器与已存在的步骤对齐。
        if (steps.Count != lastRenderedStepCount)
        {
            lastRenderedStepCount = steps.Count;
            StateHasChanged();
        }
    }

    // --- 无障碍 ---

    private string StepAnnouncement(int index, StepState state)
    {
        var position = Localizer["Stepper.StepPosition", index + 1, steps.Count];

        var word = state switch
        {
            StepState.Completed => Localizer["Stepper.Completed"],
            StepState.Error => Localizer["Stepper.Error"],
            StepState.Skipped => Localizer["Stepper.Skipped"],
            _ => null
        };

        return word is null ? position : position + ", " + word;
    }

    // --- CSS ---

    private bool IsVertical => Orientation == StepperOrientation.Vertical;

    private bool HasStepContent => steps.Exists(step => step.HasContent);

    private string CssClass => ClassNames.cn(
        IsVertical ? "atom:flex atom:gap-8" : "atom:flex atom:flex-col",
        Class
    );

    private string IndicatorCssClass => ClassNames.cn(
        "atom:m-0 atom:list-none atom:p-0",
        IsVertical ? "atom:flex atom:shrink-0 atom:flex-col" : "atom:flex atom:w-full atom:items-start",
        IndicatorClass
    );

    private string ItemCssClass(bool isLast) => ClassNames.cn(
        "atom:flex",
        IsVertical ? "atom:flex-col" : "atom:items-start",
        !IsVertical && !isLast ? "atom:flex-1" : null
    );

    private string MarkerStaticCssClass => ClassNames.cn(
        "atom:flex",
        IsVertical ? "atom:flex-row atom:items-center atom:gap-3" : "atom:shrink-0 atom:flex-col atom:items-center atom:gap-2"
    );

    private string MarkerButtonCssClass => ClassNames.cn(
        MarkerStaticCssClass,
        "atom:cursor-pointer atom:rounded-md atom:outline-none",
        "atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2"
    );

    private static string MarkerCircleCssClass(StepState state) => ClassNames.cn(
        "atom:flex atom:h-8 atom:w-8 atom:shrink-0 atom:items-center atom:justify-center atom:rounded-full",
        "atom:border-2 atom:text-sm atom:font-medium atom:transition-colors",
        state switch
        {
            StepState.Active => "atom:border-primary atom:bg-primary atom:text-primary-foreground",
            StepState.Completed => "atom:border-primary atom:bg-primary atom:text-primary-foreground",
            StepState.Error => "atom:border-destructive atom:bg-destructive atom:text-destructive-foreground",
            StepState.Skipped => "atom:border-muted-foreground/50 atom:text-muted-foreground",
            _ => "atom:border-muted-foreground/25 atom:text-muted-foreground"
        }
    );

    private string LabelCssClass => ClassNames.cn(
        "atom:flex atom:flex-col",
        IsVertical ? "atom:items-start" : "atom:items-center"
    );

    private static string TitleCssClass(StepState state) => ClassNames.cn(
        "atom:whitespace-nowrap atom:text-sm atom:font-medium atom:transition-colors",
        state switch
        {
            StepState.Active => "atom:text-foreground",
            StepState.Error => "atom:text-destructive",
            _ => "atom:text-muted-foreground"
        }
    );

    private string ConnectorCssClass(StepState state) => ClassNames.cn(
        "atom:transition-colors",
        IsVertical ? "atom:ms-[15px] atom:h-8 atom:w-0.5" : "atom:mx-2 atom:mt-4 atom:h-0.5 atom:flex-1",
        state == StepState.Completed ? "atom:bg-primary" : "atom:bg-border"
    );

    private string ContentCssClass => ClassNames.cn(
        IsVertical ? "atom:min-w-0 atom:flex-1" : HasStepContent ? "atom:mt-6" : null,
        ContentClass
    );
}
