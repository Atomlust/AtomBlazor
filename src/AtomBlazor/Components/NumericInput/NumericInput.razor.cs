using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;

namespace AtomBlazor;

/// <summary>
/// 泛型数字输入组件，支持 int、double、decimal、float、long 与 short。
/// </summary>
/// <typeparam name="TValue">数字类型（必须实现 INumber&lt;TValue&gt;）。</typeparam>
public partial class NumericInput<TValue> : ComponentBase where TValue : struct, INumber<TValue>
{
    private ElementReference inputRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<NumericInput<TValue>>? dotNetRef;
    private readonly string instanceId = Guid.NewGuid().ToString("N");
    private string? generatedId;
    private bool jsInitialized;
    private bool lastWheelStepEnabled;
    private bool disposed;
    private string editingValue = string.Empty;
    private bool isEditing;
    private readonly InputValidationBehavior validation = new();

    /// <summary>
    /// 获取或设置从上级 EditForm 级联而来的 EditContext。
    /// </summary>
    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    [CascadingParameter(Name = "FieldIsInvalid")]
    private bool? FieldIsInvalid { get; set; }

    /// <summary>
    /// 获取或设置当前值。
    /// </summary>
    [Parameter]
    public TValue Value { get; set; }

    /// <summary>
    /// 获取或设置值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置允许的最小值。
    /// </summary>
    [Parameter]
    public TValue? Min { get; set; }

    /// <summary>
    /// 获取或设置允许的最大值。
    /// </summary>
    [Parameter]
    public TValue? Max { get; set; }

    /// <summary>
    /// 获取或设置方向键/按钮增减的步长。
    /// </summary>
    [Parameter]
    public TValue? Step { get; set; }

    /// <summary>
    /// 获取或设置显示的小数位数（用于 decimal/double/float 类型）。
    /// </summary>
    [Parameter]
    public int? DecimalPlaces { get; set; }

    /// <summary>
    /// 获取或设置是否允许负值。
    /// </summary>
    [Parameter]
    public bool AllowNegative { get; set; } = true;

    /// <summary>
    /// 获取或设置是否显示增减按钮。
    /// </summary>
    [Parameter]
    public bool ShowButtons { get; set; }

    /// <summary>
    /// 获取或设置占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置输入框是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置输入框是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置附加的 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 特性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置 HTML id 特性。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置描述本输入框的元素 ID。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置输入值是否无效。
    /// </summary>
    [Parameter]
    public bool? AriaInvalid { get; set; }

    /// <summary>
    /// 获取或设置 input 元素的 HTML name 特性。
    /// </summary>
    /// <remarks>
    /// 位于 EditForm 内且未显式设置时，name 自动取自 ValueExpression
    /// （FieldIdentifier），以支持 SSR 表单回传。
    /// </remarks>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式。
    /// </summary>
    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取合并了手动 AriaInvalid 与 EditContext 校验结果的有效 aria-invalid 值。
    /// </summary>
    private string? EffectiveAriaInvalid => validation.GetEffectiveAriaInvalid(AriaInvalid, FieldIsInvalid);

    /// <summary>
    /// 获取有效的 name 特性：位于 EditForm 内时回退为 FieldIdentifier 的名称。
    /// </summary>
    private string? EffectiveName => validation.GetEffectiveName(Name);

    private string EffectiveId => Id ?? (generatedId ??= $"numeric-{Guid.NewGuid().ToString("N")[..8]}");

    /// <summary>
    /// 获取或设置值的显示格式字符串。
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// 获取或设置是否禁用防抖。为 false（默认）时，<see cref="ValueChanged"/> 在键入过程中被防抖。
    /// </summary>
    [Parameter]
    public bool DisableDebounce { get; set; }

    /// <summary>
    /// 获取或设置防抖间隔（毫秒）。默认为 500 毫秒。
    /// </summary>
    [Parameter]
    public int DebounceInterval { get; set; } = 500;

    /// <summary>
    /// 获取或设置是否在输入框获得焦点时用鼠标滚轮增减值。
    /// 默认为 <c>false</c>：增减必须对 wheel 事件调用 <c>preventDefault</c>
    /// 这会把滚动从页面手里抢走——长表单一旦指针经过获得焦点的输入框就会停止滚动
    /// 还顺带悄悄改掉一个数字。只有在值得付出这一交换的场景（例如紧凑的纯数字编辑器）
    /// 才应启用。
    /// </summary>
    [Parameter]
    public bool EnableWheelStep { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        validation.Update(CascadedEditContext, ValueExpression);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                jsModule = await ComponentModules.GetCoreAsync(JSRuntime);
                dotNetRef = DotNetObjectReference.Create(this);
                lastWheelStepEnabled = EnableWheelStep;
                lastJsConfig = GetJsConfig();
                await jsModule.InvokeVoidAsync("numericInput.initialize", inputRef, dotNetRef, instanceId, lastJsConfig);
                jsInitialized = true;
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
        else if (jsInitialized && jsModule != null)
        {
            // 当影响浏览器侧行为的参数在首次渲染后发生变化时，让 JS 侧与之保持同步。
            // 模块一直暴露着 updateConfig 却从未被调用，导致小数、符号与防抖规则
            // 被固定在输入框首次渲染时的取值上。
            var config = GetJsConfig();

            if (!config.Equals(lastJsConfig))
            {
                lastJsConfig = config;

                try
                {
                    await jsModule.InvokeVoidAsync("numericInput.updateConfig", instanceId, config);
                }
                catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
                {
                    // 电路断开时属于预期情况
                }
                catch (InvalidOperationException)
                {
                    // JS interop 不可用
                }
            }

            if (lastWheelStepEnabled == EnableWheelStep)
            {
                return;
            }

            // 滚轮增减跟随参数在首次渲染后的变化保持同步
            lastWheelStepEnabled = EnableWheelStep;

            try
            {
                await jsModule.InvokeVoidAsync("numericInput.setWheelStepEnabled", instanceId, EnableWheelStep);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开时属于预期情况
            }
            catch (InvalidOperationException)
            {
                // JS interop 不可用
            }
        }
    }

    /// <summary>
    /// 上一次发给浏览器侧的配置，用于识别变化。命名 record 按值比较（结构相等）
    /// 这里正需要这种语义。
    /// </summary>
    private object? lastJsConfig;

    /// <summary>
    /// 依据当前参数构建发给 JS 的配置对象。
    /// </summary>
    private NumericInputJsConfig GetJsConfig() => new NumericInputJsConfig
    {
        disableDebounce = DisableDebounce,
        debounceMs = DebounceInterval,
        stepKeys = new[] { "ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End" },
        allowDecimal = IsFloatingPoint,
        allowNegative = AllowNegative,
        decimalSeparator = CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator,
        enableWheelStep = EnableWheelStep
    };

    private void NotifyFieldChanged() => validation.NotifyFieldChanged();

    private TValue StepValue => Step ?? TValue.One;

    private bool IsAtMax => Max.HasValue && Value >= Max.Value;
    private bool IsAtMin => Min.HasValue && Value <= Min.Value;

    private string DisplayValue
    {
        get
        {
            if (isEditing)
            {
                return editingValue;
            }

            if (Format != null)
            {
                return string.Format(CultureInfo.InvariantCulture, $"{{0:{Format}}}", Value);
            }

            if (DecimalPlaces.HasValue && IsFloatingPoint)
            {
                return string.Format(CultureInfo.InvariantCulture, $"{{0:F{DecimalPlaces.Value}}}", Value);
            }

            return Value.ToString() ?? string.Empty;
        }
    }

    private static bool IsFloatingPoint =>
        typeof(TValue) == typeof(double) ||
        typeof(TValue) == typeof(float) ||
        typeof(TValue) == typeof(decimal);

    private static string InputMode => IsFloatingPoint ? "decimal" : "numeric";

    private string ContainerClass => ClassNames.cn(
        // 用 items-stretch 而不是 items-center：步进列的高度由所在行反推，而非自带高度
        // 这样它就能跟随 input 实际解析出的高度。
        "atom:flex atom:items-stretch",
        ShowButtons ? "atom:rounded-md" : null
    );

    private string CssClass => ClassNames.cn(
        "atom:flex atom:h-10 atom:w-full atom:border atom:border-input atom:bg-background atom:px-3 atom:py-2 atom:text-base",
        "atom:placeholder:text-muted-foreground",
        ShowButtons ? "atom:rounded-s-md" : "atom:rounded-md",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        "atom:aria-[invalid=true]:border-destructive",
        "atom:transition-colors",
        "atom:md:text-sm",
        ShowButtons ? "atom:pe-8 atom:border-e-0" : null,
        Class
    );

    /// <summary>
    /// 增减按钮的 CSS 类。
    /// </summary>
    /// <remarks>
    /// 每个按钮用 <c>flex-1</c> 占据步进列的一半高度，而不是固定高度。以前是 <c>h-5</c>
    /// 两个按钮加起来恰好等于 input 默认的 <c>h-10</c>——所以在有人改动 input 高度之前它们
    /// 看起来是对齐的；而本组件的高度完全通过 <see cref="Class"/> 来调，因为没有 size 参数。
    /// 传入 <c>h-8</c> 的消费者会得到一个 40px 的步进列贴在 32px 输入框旁边，两端都探出去
    /// 圆角也不再与输入框的边框相接。<c>min-h-0</c> 允许按钮收缩到低于图标的固有高度。
    /// </remarks>
    private static string ButtonClass => ClassNames.cn(
        "atom:flex atom:flex-1 atom:min-h-0 atom:items-center atom:justify-center atom:w-8 atom:border atom:border-input atom:bg-background",
        "atom:hover:bg-accent atom:hover:text-accent-foreground",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        "atom:first:border-b-0",
        "atom:first:rounded-se-md atom:last:rounded-ee-md",
        "atom:transition-colors"
    );

    /// <summary>
    /// 由 JavaScript 在键入过程中回调。JS 侧已完成防抖（若启用）。
    /// </summary>
    [JSInvokable]
    public async Task JsOnInput(string? value)
    {
        if (disposed) { return; }

        var inputValue = value ?? string.Empty;
        editingValue = inputValue;
        isEditing = true;

        if (TryParseValue(inputValue, out var parsedValue))
        {
            var clampedValue = ClampValue(parsedValue);
            if (!clampedValue.Equals(Value))
            {
                Value = clampedValue;
                await ValueChanged.InvokeAsync(clampedValue);
                NotifyFieldChanged();
            }
        }

        StateHasChanged();
    }

    /// <summary>
    /// 由 JavaScript 在失焦时回调。提交当前值。
    /// </summary>
    [JSInvokable]
    public async Task JsOnBlur(string? value)
    {
        if (disposed) { return; }

        var inputValue = value ?? string.Empty;
        isEditing = false;

        if (TryParseValue(inputValue, out var parsedValue))
        {
            var clampedValue = ClampValue(parsedValue);
            if (!clampedValue.Equals(Value))
            {
                Value = clampedValue;
            }
            // 失焦时总是触发一次，确保父级状态同步
            await ValueChanged.InvokeAsync(clampedValue);
            NotifyFieldChanged();
        }

        StateHasChanged();
    }

    /// <summary>
    /// 由 JavaScript 在获得焦点时回调。
    /// </summary>
    [JSInvokable]
    public void JsOnFocus()
    {
        if (disposed) { return; }

        editingValue = Value.ToString() ?? string.Empty;
        isEditing = true;
        StateHasChanged();
    }

    /// <summary>
    /// 由 JavaScript 在按下步进按键时回调（ArrowUp/Down、PageUp/Down、Home/End）。
    /// JS 侧已调用 preventDefault()。
    /// </summary>
    [JSInvokable]
    public async Task JsOnKeyDown(string key)
    {
        if (disposed || Disabled) { return; }

        switch (key)
        {
            case "ArrowUp":
                await Increment();
                break;
            case "ArrowDown":
                await Decrement();
                break;
            case "PageUp":
                await IncrementBy(TValue.CreateChecked(10) * StepValue);
                break;
            case "PageDown":
                await DecrementBy(TValue.CreateChecked(10) * StepValue);
                break;
            case "Home":
                if (Min.HasValue)
                {
                    await SetValue(Min.Value);
                }
                break;
            case "End":
                if (Max.HasValue)
                {
                    await SetValue(Max.Value);
                }
                break;
        }

        StateHasChanged();
    }

    private async Task Increment()
    {
        if (Disabled || IsAtMax)
        {
            return;
        }

        await SetValue(Offset(StepValue));
    }

    private async Task Decrement()
    {
        if (Disabled || IsAtMin)
        {
            return;
        }

        await SetValue(Offset(-StepValue));
    }

    private async Task IncrementBy(TValue amount)
    {
        if (Disabled)
        {
            return;
        }

        await SetValue(Offset(amount));
    }

    private async Task DecrementBy(TValue amount)
    {
        if (Disabled)
        {
            return;
        }

        await SetValue(Offset(-amount));
    }

    /// <summary>
    /// 在当前值上加上 <paramref name="amount"/>，不会环绕到类型取值范围的另一端。
    /// </summary>
    /// <remarks>
    /// 这里的加法以前不做溢出检查：已在 <see cref="int.MaxValue"/> 上的 <c>int</c>
    /// 按一下 ArrowUp 会得出一个很大的负数——而 <c>ClampValue</c> 看到低于最小值的数
    /// 又把它抬回 <c>Min</c>。一次按键就让字段从最大值变成最小值。现在溢出时取值
    /// 钉在配置的边界上；没有边界时原地不动。
    /// </remarks>
    private TValue Offset(TValue amount)
    {
        try
        {
            return checked(Value + amount);
        }
        catch (OverflowException)
        {
            if (amount > TValue.Zero)
            {
                return Max ?? Value;
            }

            return Min ?? Value;
        }
    }

    private async Task SetValue(TValue value)
    {
        var clampedValue = ClampValue(value);

        if (!clampedValue.Equals(Value))
        {
            Value = clampedValue;
            editingValue = clampedValue.ToString() ?? string.Empty;
            await ValueChanged.InvokeAsync(clampedValue);
            NotifyFieldChanged();
        }
    }

    private TValue ClampValue(TValue value)
    {
        if (!AllowNegative && value < TValue.Zero)
        {
            value = TValue.Zero;
        }

        if (Min.HasValue && value < Min.Value)
        {
            value = Min.Value;
        }

        if (Max.HasValue && value > Max.Value)
        {
            value = Max.Value;
        }

        return value;
    }

    private bool TryParseValue(string? input, out TValue result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            result = TValue.Zero;
            return true;
        }

        // 去掉千分位分隔符并归一化小数点分隔符
        input = input.Replace(",", "").Trim();

        // 处理负号
        if (!AllowNegative && input.StartsWith('-'))
        {
            return false;
        }

        return TValue.TryParse(input, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// 获取底层 input 元素引用，供 JS interop 等场景使用。
    /// </summary>
    public ElementReference Element => inputRef;

    /// <summary>
    /// 让焦点落到底层 input 元素上。
    /// </summary>
    public ValueTask FocusAsync() => inputRef.FocusAsync();

    public async ValueTask DisposeAsync()
    {
        disposed = true;

        if (jsModule != null && jsInitialized)
        {
            try
            {
                await jsModule.InvokeVoidAsync("numericInput.dispose", instanceId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开时属于预期情况
            }
            catch (InvalidOperationException)
            {
                // JS interop 不可用
            }
        }

        dotNetRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
