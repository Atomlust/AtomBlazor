using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

/// <summary>
/// 管理步骤导航、进度指示与分步校验的多步表单向导组件。
/// 与 <see cref="WizardStep"/> 子组件组合使用，定义向导中的每一步。
/// </summary>
/// <remarks>
/// <para>
/// 向导同时支持受控（<c>@bind-CurrentStep</c>）与非受控模式。
/// 放置在 <c>EditForm</c> 内即可通过每个步骤的
/// <see cref="WizardStep.FieldNames"/> 参数启用分步校验。
/// </para>
/// <para>
/// 特性：
/// - 横向与纵向两种步骤指示器布局
/// - 基于 EditContext 集成的分步校验
/// - 可选/可跳过步骤
/// - 内置或自定义导航按钮
/// - 步骤视觉状态（待处理、活动、已完成、校验失败）
/// </para>
/// </remarks>
public partial class FormWizard : ComponentBase
{
    // --- 注册（基于实例的去重） ---
    private readonly List<WizardStepInfo> steps = new();
    private readonly Dictionary<WizardStep, int> stepOwners = new();

    // --- 状态 ---
    private int currentStep;
    private int lastRenderedStepCount = -1;
    private readonly HashSet<int> visitedSteps = new() { 0 };
    private readonly Dictionary<int, bool> stepValidationState = new();
    private readonly HashSet<int> skippedSteps = new();

    // --- 参数 ---

    /// <summary>
    /// 获取或设置在向导内渲染的内容。应包含 <see cref="WizardStep"/> 组件。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置当前步骤的从零开始的索引。支持 <c>@bind-CurrentStep</c> 双向绑定。
    /// </summary>
    [Parameter]
    public int CurrentStep { get; set; }

    /// <summary>
    /// 获取或设置当前步骤变化时调用的事件回调。
    /// 与 <c>@bind-CurrentStep</c> 配合实现双向绑定。
    /// </summary>
    [Parameter]
    public EventCallback<int> CurrentStepChanged { get; set; }

    /// <summary>
    /// 获取或设置用户完成向导时（点击最后一步的完成按钮）调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback OnComplete { get; set; }

    /// <summary>
    /// 获取或设置活动步骤变化时调用的事件回调，携带新的步骤索引。
    /// 适用于导航时的埋点或副作用处理。
    /// </summary>
    [Parameter]
    public EventCallback<int> OnStepChanged { get; set; }

    /// <summary>
    /// 获取或设置步骤指示器的布局方向。
    /// <c>Horizontal</c>（默认）横向排布在顶部；
    /// <c>Vertical</c> 纵向排布在左侧。
    /// </summary>
    [Parameter]
    public WizardLayout Layout { get; set; } = WizardLayout.Horizontal;

    /// <summary>
    /// 获取或设置是否显示内置的 上一步/下一步/完成 导航按钮。
    /// 默认为 <c>true</c>。设为 <c>false</c> 可隐藏它们或改用 <see cref="NavigationTemplate"/>。
    /// </summary>
    [Parameter]
    public bool ShowNavigation { get; set; } = true;

    /// <summary>
    /// 获取或设置替换默认 上一步/下一步/完成 按钮的自定义导航模板。
    /// 可通过级联的 <c>FormWizard</c> 参数访问向导实例。
    /// </summary>
    [Parameter]
    public RenderFragment? NavigationTemplate { get; set; }

    /// <summary>
    /// 获取或设置用户是否可以点击步骤指示器中未来（未访问）的步骤以前进跳步。
    /// 默认为 <c>false</c>（用户只能点击已访问的步骤）。
    /// </summary>
    [Parameter]
    public bool AllowSkipAhead { get; set; }

    /// <summary>
    /// 获取或设置向后导航时已完成的步骤是否保留其完成/已访问状态。
    /// 为 <c>true</c>（默认）时步骤保持完成状态；
    /// 为 <c>false</c> 时向后导航会清除目标步骤之后所有步骤的完成与访问状态。
    /// </summary>
    [Parameter]
    public bool RetainStepState { get; set; } = true;

    /// <summary>
    /// 获取或设置向导根容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置步骤指示器 nav 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? IndicatorClass { get; set; }

    /// <summary>
    /// 获取或设置步骤内容区域的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? ContentClass { get; set; }

    /// <summary>
    /// 获取或设置导航按钮区域的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? NavigationClass { get; set; }

    /// <summary>
    /// 获取或设置“下一步”按钮的自定义标签。默认为“Next”。
    /// </summary>
    [Parameter]
    public string? NextLabel { get; set; }

    /// <summary>
    /// 获取或设置“上一步”按钮的自定义标签。默认为“Back”。
    /// </summary>
    [Parameter]
    public string? BackLabel { get; set; }

    /// <summary>
    /// 获取或设置最后一步显示的“完成”按钮的自定义标签。默认为“Complete”。
    /// </summary>
    [Parameter]
    public string? CompleteLabel { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [CascadingParameter]
    private EditContext? EditContext { get; set; }

    // --- 内部 API（由 WizardStep 经级联参数访问） ---

    /// <summary>
    /// 获取当前活动步骤的从零开始的索引。
    /// </summary>
    internal int ActiveStepIndex => currentStep;

    /// <summary>
    /// 获取已注册步骤的总数。
    /// </summary>
    internal int StepCount => steps.Count;

    /// <summary>
    /// 获取当前步骤是否为第一个可用步骤。
    /// </summary>
    internal bool IsFirstStep => FindPreviousEnabledStep(currentStep) == -1;

    /// <summary>
    /// 获取当前步骤是否为最后一个可用步骤。
    /// </summary>
    internal bool IsLastStep => FindNextEnabledStep(currentStep) == -1;

    /// <summary>
    /// 获取是否可以向后导航。
    /// </summary>
    internal bool CanGoBack => FindPreviousEnabledStep(currentStep) != -1;

    /// <summary>
    /// 获取是否可以向前导航。
    /// </summary>
    internal bool CanGoNext => FindNextEnabledStep(currentStep) != -1;

    // --- 步骤注册 ---

    /// <summary>
    /// 向向导注册一个步骤并返回其索引。
    /// 以组件实例为键防止 <c>OnParametersSet</c> 在每个渲染周期触发多次时产生重复条目。
    /// </summary>
    internal int RegisterStep(WizardStep owner, WizardStepInfo info)
    {
        if (stepOwners.TryGetValue(owner, out var existingIdx))
        {
            steps[existingIdx] = info;
            return existingIdx;
        }

        var idx = steps.Count;
        steps.Add(info);
        stepOwners[owner] = idx;
        return idx;
    }

    /// <summary>
    /// 在组件销毁时注销一个步骤。
    /// 重建索引以保证步骤列表连续。
    /// </summary>
    internal void UnregisterStep(WizardStep owner)
    {
        if (!stepOwners.TryGetValue(owner, out var removedIdx))
        {
            return;
        }

        steps.RemoveAt(removedIdx);
        stepOwners.Remove(owner);

        // 为被移除步骤之后的步骤重建索引
        foreach (var kvp in stepOwners)
        {
            if (kvp.Value > removedIdx)
            {
                stepOwners[kvp.Key] = kvp.Value - 1;
            }
        }

        // 清理引用了被移除索引的状态
        stepValidationState.Remove(removedIdx);
        skippedSteps.Remove(removedIdx);
        visitedSteps.Remove(removedIdx);

        // 将被移除索引之后的状态条目整体下移一位
        ShiftStateAfterRemoval(removedIdx);

        // 当前步骤越界时收敛到末步
        if (steps.Count > 0 && currentStep >= steps.Count)
        {
            currentStep = steps.Count - 1;
        }
    }

    private void ShiftStateAfterRemoval(int removedIdx)
    {
        var newValidation = new Dictionary<int, bool>();
        foreach (var kvp in stepValidationState)
        {
            newValidation[kvp.Key > removedIdx ? kvp.Key - 1 : kvp.Key] = kvp.Value;
        }
        stepValidationState.Clear();
        foreach (var kvp in newValidation)
        {
            stepValidationState[kvp.Key] = kvp.Value;
        }

        var newSkipped = new HashSet<int>();
        foreach (var idx in skippedSteps)
        {
            newSkipped.Add(idx > removedIdx ? idx - 1 : idx);
        }
        skippedSteps.Clear();
        foreach (var idx in newSkipped)
        {
            skippedSteps.Add(idx);
        }

        var newVisited = new HashSet<int>();
        foreach (var idx in visitedSteps)
        {
            newVisited.Add(idx > removedIdx ? idx - 1 : idx);
        }
        visitedSteps.Clear();
        foreach (var idx in newVisited)
        {
            visitedSteps.Add(idx);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // 同步受控状态
        if (CurrentStepChanged.HasDelegate && CurrentStep != currentStep)
        {
            currentStep = steps.Count > 0
                ? Math.Clamp(CurrentStep, 0, steps.Count - 1)
                : 0;
            visitedSteps.Add(currentStep);
        }
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // 步骤数量变化时重新渲染，使指示器反映当前步骤
        if (steps.Count != lastRenderedStepCount)
        {
            lastRenderedStepCount = steps.Count;
            StateHasChanged();
        }
    }

    // --- 导航 ---

    /// <summary>
    /// 校验当前步骤后导航到下一步。
    /// </summary>
    internal async Task GoToNextAsync()
    {
        if (!CanGoNext)
        {
            return;
        }

        if (!ValidateCurrentStep())
        {
            // BL0012：GoToNextAsync 由导航按钮 OnClick 接线，重渲染由框架自动完成。
            return;
        }

        stepValidationState[currentStep] = true;

        var nextIndex = FindNextEnabledStep(currentStep);
        if (nextIndex >= 0)
        {
            await SetCurrentStepAsync(nextIndex);
        }
    }

    /// <summary>
    /// 不经验校导航到上一步。
    /// </summary>
    internal async Task GoToBackAsync()
    {
        if (!CanGoBack)
        {
            return;
        }

        var prevIndex = FindPreviousEnabledStep(currentStep);
        if (prevIndex >= 0)
        {
            await SetCurrentStepAsync(prevIndex);
        }
    }

    /// <summary>
    /// 按索引直接导航到指定步骤。
    /// 仅当该步骤已访问或 <see cref="AllowSkipAhead"/> 为 true 时允许。
    /// </summary>
    internal async Task GoToStepAsync(int index)
    {
        if (index < 0 || index >= steps.Count || index == currentStep)
        {
            return;
        }

        if (steps[index].Disabled)
        {
            return;
        }

        if (!AllowSkipAhead && !visitedSteps.Contains(index))
        {
            return;
        }

        await SetCurrentStepAsync(index);
    }

    /// <summary>
    /// 跳过当前步骤（仅对可选步骤有效）并向前导航。
    /// </summary>
    internal async Task SkipCurrentStepAsync()
    {
        if (!CanGoNext)
        {
            return;
        }

        if (currentStep < steps.Count && steps[currentStep].IsOptional)
        {
            skippedSteps.Add(currentStep);
            stepValidationState.Remove(currentStep);
            var nextIndex = FindNextEnabledStep(currentStep);
            if (nextIndex >= 0)
            {
                await SetCurrentStepAsync(nextIndex);
            }
        }
    }

    /// <summary>
    /// 完成向导。校验当前步骤并调用 <see cref="OnComplete"/>。
    /// </summary>
    internal async Task CompleteAsync()
    {
        if (!ValidateCurrentStep())
        {
            // BL0012：CompleteAsync 由完成按钮 OnClick 接线，重渲染由框架自动完成。
            return;
        }

        stepValidationState[currentStep] = true;

        if (OnComplete.HasDelegate)
        {
            await OnComplete.InvokeAsync();
        }
    }

    // --- 校验 ---

    private bool ValidateCurrentStep()
    {
        if (currentStep >= steps.Count)
        {
            return true;
        }

        var step = steps[currentStep];

        // 检查基于 EditContext 的字段校验（仅当前步骤）
        if (step.FieldNames is { Length: > 0 } && EditContext is not null)
        {
            var hasErrors = false;

            foreach (var fieldName in step.FieldNames)
            {
                var fieldIdentifier = new FieldIdentifier(EditContext.Model, fieldName);
                EditContext.NotifyFieldChanged(fieldIdentifier);

                if (EditContext.GetValidationMessages(fieldIdentifier).Any())
                {
                    hasErrors = true;
                }
            }

            if (hasErrors)
            {
                stepValidationState[currentStep] = false;
                return false;
            }
        }

        // 检查自定义校验器
        if (step.Validator is not null && !step.Validator())
        {
            stepValidationState[currentStep] = false;
            return false;
        }

        return true;
    }

    // --- 步骤状态 ---

    /// <summary>
    /// 获取给定索引处步骤的视觉状态。
    /// </summary>
    internal WizardStepState GetStepState(int index)
    {
        if (index == currentStep)
        {
            return WizardStepState.Active;
        }

        if (steps.Count > index && steps[index].Disabled)
        {
            return WizardStepState.Pending;
        }

        if (skippedSteps.Contains(index))
        {
            return WizardStepState.Skipped;
        }

        if (stepValidationState.TryGetValue(index, out var isValid))
        {
            return isValid ? WizardStepState.Completed : WizardStepState.Invalid;
        }

        return WizardStepState.Pending;
    }

    /// <summary>
    /// 返回步骤指示器是否可点击导航。
    /// </summary>
    internal bool CanNavigateToStep(int index)
    {
        if (index == currentStep)
        {
            return false;
        }

        if (index < 0 || index >= steps.Count)
        {
            return false;
        }

        if (steps[index].Disabled)
        {
            return false;
        }

        return AllowSkipAhead || visitedSteps.Contains(index);
    }

    // --- 辅助方法 ---

    private async Task SetCurrentStepAsync(int index)
    {
        if (!RetainStepState && index < currentStep)
        {
            ClearForwardStepState(index);
        }

        currentStep = index;
        visitedSteps.Add(index);

        if (CurrentStepChanged.HasDelegate)
        {
            await CurrentStepChanged.InvokeAsync(currentStep);
        }

        if (OnStepChanged.HasDelegate)
        {
            await OnStepChanged.InvokeAsync(currentStep);
        }

        StateHasChanged();
    }

    private void ClearForwardStepState(int fromIndex)
    {
        for (var i = fromIndex + 1; i < steps.Count; i++)
        {
            visitedSteps.Remove(i);
            stepValidationState.Remove(i);
            skippedSteps.Remove(i);
            ResetStepModelFields(steps[i]);
        }
    }

    // 按字段名反射重置 EditForm 模型属性，宿主模型类型来自使用方，修剪器无法静态分析，
    // 其动态代码约束在本方法内抑制（仅触碰公开可写实例属性，失败时静默跳过）。
    [UnconditionalSuppressMessage("Trimming", "IL2075: 'this' 参数不满足 DynamicallyAccessedMemberTypes.PublicProperties",
        Justification = "仅重置宿主模型的公开可写实例属性，使用方模型随宿主程序集保留。")]
    [UnconditionalSuppressMessage("Trimming", "IL2072: 'type' 参数不满足 DynamicallyAccessedMemberTypes.PublicParameterlessConstructor",
        Justification = "仅对值类型取默认实例，引用类型置 null，不依赖动态构造。")]
    private void ResetStepModelFields(WizardStepInfo step)
    {
        if (EditContext is null || step.FieldNames is not { Length: > 0 })
        {
            return;
        }

        var model = EditContext.Model;
        var modelType = model.GetType();

        foreach (var fieldName in step.FieldNames)
        {
            var property = modelType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (property is { CanWrite: true })
            {
                var defaultValue = property.PropertyType.IsValueType
                    ? Activator.CreateInstance(property.PropertyType)
                    : null;
                property.SetValue(model, defaultValue);
            }
        }
    }

    private int FindNextEnabledStep(int fromIndex)
    {
        for (var i = fromIndex + 1; i < steps.Count; i++)
        {
            if (!steps[i].Disabled)
            {
                return i;
            }
        }
        return -1;
    }

    private int FindPreviousEnabledStep(int fromIndex)
    {
        for (var i = fromIndex - 1; i >= 0; i--)
        {
            if (!steps[i].Disabled)
            {
                return i;
            }
        }
        return -1;
    }

    // --- CSS ---

    private string CssClass => ClassNames.cn(
        Layout == WizardLayout.Vertical ? "atom:flex atom:gap-8" : "atom:flex atom:flex-col",
        Class
    );

    private string IndicatorCssClass => ClassNames.cn(
        Layout == WizardLayout.Horizontal
            ? "atom:flex atom:items-start atom:w-full"
            : "atom:flex atom:flex-col atom:shrink-0",
        IndicatorClass
    );

    private string ContentWrapperCssClass => ClassNames.cn(
        Layout == WizardLayout.Vertical ? "atom:flex-1 atom:min-w-0 atom:flex atom:flex-col" : null
    );

    private string ContentCssClass => ClassNames.cn(
        Layout == WizardLayout.Horizontal ? "atom:mt-6" : "atom:flex-1",
        ContentClass
    );

    private string NavigationCssClass => ClassNames.cn(
        "atom:flex atom:items-center atom:justify-between atom:pt-6 atom:pb-2",
        NavigationClass
    );

    private static string GetStepCircleClass(WizardStepState state) => ClassNames.cn(
        "atom:flex atom:h-8 atom:w-8 atom:shrink-0 atom:items-center atom:justify-center atom:rounded-full atom:border-2 atom:text-sm atom:font-medium atom:transition-colors",
        state switch
        {
            WizardStepState.Active => "atom:border-primary atom:bg-primary atom:text-primary-foreground",
            WizardStepState.Completed => "atom:border-primary atom:bg-primary atom:text-primary-foreground",
            WizardStepState.Invalid => "atom:border-destructive atom:bg-destructive atom:text-destructive-foreground",
            WizardStepState.Skipped => "atom:border-muted-foreground/50 atom:text-muted-foreground",
            _ => "atom:border-muted-foreground/25 atom:text-muted-foreground"
        }
    );

    private string GetStepButtonClass(int index) => ClassNames.cn(
        "atom:flex atom:group atom:cursor-default",
        // #459: 加圆角，使焦点环跟随步骤标记而非把标签一起框住。
        "atom:rounded-md atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        Layout == WizardLayout.Vertical
            ? "atom:flex-row atom:items-center atom:gap-2"
            : "atom:flex-col atom:items-center atom:shrink-0 atom:gap-2",
        CanNavigateToStep(index) ? "atom:cursor-pointer" : null
    );

    private static string GetStepTitleClass(WizardStepState state) => ClassNames.cn(
        "atom:text-sm atom:font-medium atom:transition-colors atom:whitespace-nowrap",
        state == WizardStepState.Active ? "atom:text-foreground" : "atom:text-muted-foreground"
    );

    private static string GetConnectorClass(WizardStepState state, WizardLayout layout)
    {
        if (layout == WizardLayout.Horizontal)
        {
            return ClassNames.cn(
                "atom:flex-1 atom:mx-2 atom:mt-4 atom:transition-colors",
                state == WizardStepState.Completed ? "atom:h-0.5 atom:bg-primary" : "atom:h-0.5 atom:bg-border"
            );
        }

        return ClassNames.cn(
            "atom:transition-colors atom:ms-[15px]",
            state == WizardStepState.Completed ? "atom:w-0.5 atom:h-8 atom:bg-primary" : "atom:w-0.5 atom:h-8 atom:bg-border"
        );
    }
}
