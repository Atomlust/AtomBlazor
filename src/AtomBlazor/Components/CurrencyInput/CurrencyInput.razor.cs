using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Globalization;
using System.Linq.Expressions;

namespace AtomBlazor;

/// <summary>
/// 具备区域感知格式化能力的币种输入组件。
/// </summary>
public partial class CurrencyInput : ComponentBase
{
    private ElementReference inputRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<CurrencyInput>? dotNetRef;
    private readonly string instanceId = Guid.NewGuid().ToString("N");
    private string? generatedId;
    private bool jsInitialized;
    private bool lastWheelStepEnabled;
    private bool disposed;
    private string editingValue = string.Empty;
    private bool isEditing;
    private CurrencyDefinition? currency;
    private CultureInfo? cultureInfo;
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
    public decimal Value { get; set; }

    /// <summary>
    /// 获取或设置值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<decimal> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置 ISO 4217 币种代码。
    /// </summary>
    [Parameter]
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>
    /// 获取或设置是否显示币种符号。
    /// </summary>
    [Parameter]
    public bool ShowSymbol { get; set; } = true;

    /// <summary>
    /// 获取或设置允许的最小值。
    /// </summary>
    [Parameter]
    public decimal? Min { get; set; }

    /// <summary>
    /// 获取或设置允许的最大值。
    /// </summary>
    [Parameter]
    public decimal? Max { get; set; }

    /// <summary>
    /// 获取或设置是否允许负值。
    /// </summary>
    [Parameter]
    public bool AllowNegative { get; set; } = true;

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
    public Expression<Func<decimal>>? ValueExpression { get; set; }

    private string? EffectiveAriaInvalid => validation.GetEffectiveAriaInvalid(AriaInvalid, FieldIsInvalid);

    /// <summary>
    /// 获取有效的 name 特性：位于 EditForm 内时回退为 FieldIdentifier 的名称。
    /// </summary>
    private string? EffectiveName => validation.GetEffectiveName(Name);

    private string EffectiveId => Id ?? (generatedId ??= $"currency-{Guid.NewGuid().ToString("N")[..8]}");

    /// <summary>
    /// 获取或设置显示模式是否使用千分位分隔符。
    /// </summary>
    [Parameter]
    public bool UseThousandSeparator { get; set; } = true;

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
    /// 还顺带悄悄改掉一个金额。只有在值得付出这一交换的场景（例如紧凑的纯数字编辑器）
    /// 才应启用。
    /// </summary>
    [Parameter]
    public bool EnableWheelStep { get; set; }

    private CurrencyDefinition Currency => currency ??= CurrencyCatalog.GetCurrency(CurrencyCode);

    private CultureInfo CultureInfo
    {
        get
        {
            if (cultureInfo == null || cultureInfo.Name != Currency.CultureName)
            {
                try
                {
                    cultureInfo = new CultureInfo(Currency.CultureName);
                }
                catch
                {
                    cultureInfo = CultureInfo.InvariantCulture;
                }
            }
            return cultureInfo;
        }
    }

    protected override void OnParametersSet()
    {
        // 币种代码变化时重置币种缓存
        if (currency != null && !string.Equals(currency.Code, CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            currency = null;
            cultureInfo = null;
        }

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
    /// <remarks>
    /// 交给 JS 清洗器的小数分隔符必须与 <see cref="TryParseValue"/> 读回时使用的同一个——
    /// 也就是<em>币种</em>区域的那个，见 <see cref="CultureInfo"/>。此前这里用宿主环境区域
    /// 两者不一致时读写就会互相错位；而在 Blazor Server 上宿主环境区域是服务器的区域
    /// 而非用户的区域，对所有人来说它们都不一致。
    /// </remarks>
    private NumericInputJsConfig GetJsConfig() => new NumericInputJsConfig
    {
        disableDebounce = DisableDebounce,
        debounceMs = DebounceInterval,
        stepKeys = new[] { "ArrowUp", "ArrowDown" },
        allowDecimal = true,
        allowNegative = AllowNegative,
        decimalSeparator = CultureInfo.NumberFormat.NumberDecimalSeparator,
        enableWheelStep = EnableWheelStep
    };

    private void NotifyFieldChanged() => validation.NotifyFieldChanged();

    private string DisplayValue
    {
        get
        {
            if (isEditing)
            {
                return editingValue;
            }

            return FormatCurrency(Value);
        }
    }

    private string FormatCurrency(decimal value)
    {
        var format = UseThousandSeparator ? "N" : "F";
        return value.ToString($"{format}{Currency.DecimalPlaces}", CultureInfo);
    }

    private string ContainerClass => ClassNames.cn(
        "atom:flex atom:items-center atom:rounded-md atom:overflow-hidden",
        Disabled ? "atom:opacity-50" : null
    );

    private string CssClass => ClassNames.cn(
        "atom:flex atom:h-10 atom:w-full atom:border atom:border-input atom:bg-background atom:px-3 atom:py-2 atom:text-base",
        "atom:placeholder:text-muted-foreground",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        "atom:aria-[invalid=true]:border-destructive",
        "atom:transition-colors",
        "atom:md:text-sm",
        "atom:text-end atom:tabular-nums",
        ShowSymbol && Currency.SymbolBefore ? "atom:border-s-0" : "atom:rounded-s-md",
        ShowSymbol && !Currency.SymbolBefore ? "atom:border-e-0" : "atom:rounded-e-md",
        Class
    );

    private string SymbolClass => ClassNames.cn(
        "atom:flex atom:h-10 atom:items-center atom:justify-center atom:px-3 atom:border atom:border-input atom:bg-muted atom:text-muted-foreground atom:text-sm",
        Currency.SymbolBefore ? "atom:border-e-0" : "atom:border-s-0",
        Disabled ? "atom:opacity-50" : null
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
            if (clampedValue != Value)
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
            if (clampedValue != Value)
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

        // 显示不带千分位的裸数字，便于编辑。这里要经币种区域格式化，而不是不变区域：
        // 失焦时 TryParseValue 会用同一区域读回这个字符串，若把不变区域的 "999.99"
        // 交给 de-DE 解析，句点会被当作千分位剥掉，回来就变成 99999。
        editingValue = Value.ToString($"F{Currency.DecimalPlaces}", CultureInfo);
        isEditing = true;
        StateHasChanged();
    }

    /// <summary>
    /// 由 JavaScript 在按下步进按键时回调（ArrowUp/Down）。
    /// JS 侧已调用 preventDefault()。
    /// </summary>
    [JSInvokable]
    public async Task JsOnKeyDown(string key)
    {
        if (disposed || Disabled) { return; }

        var step = (decimal)Math.Pow(10, -Currency.DecimalPlaces);

        switch (key)
        {
            case "ArrowUp":
                await SetValue(Value + step);
                break;
            case "ArrowDown":
                await SetValue(Value - step);
                break;
        }

        StateHasChanged();
    }

    private async Task SetValue(decimal value)
    {
        var clampedValue = ClampValue(value);

        if (clampedValue != Value)
        {
            Value = clampedValue;
            editingValue = clampedValue.ToString($"F{Currency.DecimalPlaces}", CultureInfo);
            await ValueChanged.InvokeAsync(clampedValue);
            NotifyFieldChanged();
        }
    }

    private decimal ClampValue(decimal value)
    {
        // 舍入到币种的小数位数
        value = Math.Round(value, Currency.DecimalPlaces);

        if (!AllowNegative && value < 0)
        {
            value = 0;
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

    private bool TryParseValue(string? input, out decimal result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        // 去掉币种符号、千分位与空白
        input = input
            .Replace(Currency.Symbol, "")
            .Replace(" ", "")
            .Trim();

        // 处理区域特定的小数分隔符
        var decimalSeparator = CultureInfo.NumberFormat.NumberDecimalSeparator;
        var groupSeparator = CultureInfo.NumberFormat.NumberGroupSeparator;

        // 去掉千分位分隔符
        input = input.Replace(groupSeparator, "");

        // 把小数分隔符归一化为不变区域形式
        if (decimalSeparator != ".")
        {
            input = input.Replace(decimalSeparator, ".");
        }

        // 处理负号
        if (!AllowNegative && input.StartsWith('-'))
        {
            return false;
        }

        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
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
