using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Linq.Expressions;

namespace AtomBlazor;

/// <summary>
/// 支持预设与自定义掩码模式的掩码输入组件。
/// </summary>
public partial class MaskedInput : ComponentBase, IAsyncDisposable
{
    private static readonly string[] guardSuppressedEvents = ["input"];

    private ElementReference _inputRef;
    private MaskProcessor? _processor;
    private string _displayValue = string.Empty;
    private IJSObjectReference? _jsModule;
    private bool _jsModuleLoaded;
    private IJSObjectReference? _guardModule;
    private readonly string _guardId = Guid.NewGuid().ToString("N");
    private string? _generatedId;
    private readonly InputValidationBehavior validation = new();

    /// <summary>
    /// 获取或设置从上级 EditForm 级联而来的 EditContext。
    /// </summary>
    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    [CascadingParameter(Name = "FieldIsInvalid")]
    private bool? FieldIsInvalid { get; set; }

    /// <summary>
    /// 获取或设置未掩码的值（不带格式化的原始输入）。
    /// </summary>
    [Parameter]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置未掩码值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置掩码预设。
    /// </summary>
    [Parameter]
    public MaskPreset Preset { get; set; } = MaskPreset.Custom;

    /// <summary>
    /// 获取或设置自定义掩码模式（当 Preset 为 Custom 时）。
    /// 掩码字符：9 = 数字，A = 字母，* = 字母或数字。
    /// </summary>
    [Parameter]
    public string? Mask { get; set; }

    /// <summary>
    /// 获取或设置未填充位置的占位字符。
    /// </summary>
    [Parameter]
    public char PlaceholderChar { get; set; } = '_';

    /// <summary>
    /// 获取或设置是否显示带占位字符的掩码。
    /// </summary>
    [Parameter]
    public bool ShowMask { get; set; } = true;

    /// <summary>
    /// 获取或设置占位文本（为空且未聚焦时显示）。
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
    public Expression<Func<string>>? ValueExpression { get; set; }

    private string? EffectiveAriaInvalid => validation.GetEffectiveAriaInvalid(AriaInvalid, FieldIsInvalid);

    /// <summary>
    /// 获取有效的 name 特性：位于 EditForm 内时回退为 FieldIdentifier 的名称。
    /// </summary>
    private string? EffectiveName => validation.GetEffectiveName(Name);

    private string EffectiveId => Id ?? (_generatedId ??= $"masked-{Guid.NewGuid().ToString("N")[..8]}");

    /// <summary>
    /// 获取掩码后（格式化）的值。
    /// </summary>
    public string MaskedValue => _displayValue;

    /// <summary>
    /// 获取未掩码的值。
    /// </summary>
    public string UnmaskedValue => Processor.GetUnmaskedValue(_displayValue);

    private MaskProcessor Processor
    {
        get
        {
            var effectiveMask = GetEffectiveMask();
            if (_processor == null || _processor.Mask != effectiveMask || _processor.PlaceholderChar != PlaceholderChar)
            {
                _processor = new MaskProcessor(effectiveMask, PlaceholderChar);
            }
            return _processor;
        }
    }

    private string GetEffectiveMask()
    {
        if (Preset != MaskPreset.Custom)
        {
            return MaskDefinitions.GetPattern(Preset);
        }

        return Mask ?? string.Empty;
    }

    private string InputMode => MaskDefinitions.GetInputMode(Preset);

    private int MaskLength => GetEffectiveMask().Length;

    private string EffectivePlaceholder
    {
        get
        {
            if (!string.IsNullOrEmpty(Placeholder))
            {
                return Placeholder;
            }

            if (Preset != MaskPreset.Custom)
            {
                return MaskDefinitions.GetPlaceholder(Preset);
            }

            return Processor.GetEmptyMask();
        }
    }

    protected override void OnInitialized() =>
        UpdateDisplayValue();

    protected override void OnParametersSet()
    {
        UpdateDisplayValue();

        validation.Update(CascadedEditContext, ValueExpression);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
 // 有意偏差（沿 numeric-input 先例）：逐实例直连
                // masked-input.js，本仓库经 PrimitiveJsModuleTests/ComponentModules 扫描约束
                // 改走共享 core bundle，调用点使用 maskedInput. 命名空间标识符。
                _jsModule = await ComponentModules.GetCoreAsync(JSRuntime);
                _jsModuleLoaded = true;

                // 应用掩码意味着改写元素的 value，而这会重置进行中的输入法组合。
                // 组合期间抑制 input 事件，能把整个 HandleInput 流程——掩码处理也包括
                // 在内——推迟到输入法上屏之后再执行。
                _guardModule = await ComponentModules.GetCoreAsync(JSRuntime);
                await _guardModule.InvokeVoidAsync(
                    "compositionGuard.attach", _inputRef, _guardId, new CompositionGuardJsConfig { suppress = guardSuppressedEvents });
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

    private void UpdateDisplayValue() =>
        _displayValue = Processor.ApplyMask(Value);

    private void HandleInput(ChangeEventArgs args)
    {
        var inputValue = args.Value?.ToString() ?? string.Empty;
        var mask = GetEffectiveMask();

        // 统计掩码中可编辑位置的最大数量
        var maxEditablePositions = 0;
        foreach (var c in mask)
        {
            if (MaskProcessor.IsMaskChar(c))
            {
                maxEditablePositions++;
            }
        }

        // 只从输入中提取合法字符（仅限字母与数字）
        var rawInput = new System.Text.StringBuilder();

        foreach (var c in inputValue)
        {
            // 跳过占位字符
            if (c == PlaceholderChar)
            {
                continue;
            }

            // 只收集字母数字字符
            if (char.IsLetterOrDigit(c))
            {
                rawInput.Append(c);
            }

            // 字符数够了就停止
            if (rawInput.Length >= maxEditablePositions)
            {
                break;
            }
        }

        var rawInputStr = rawInput.ToString();

        // 将每个字符与对应掩码位置逐一校验
        var validatedInput = new System.Text.StringBuilder();
        var rawIndex = 0;
        var maskIndex = 0;

        while (rawIndex < rawInputStr.Length && maskIndex < mask.Length)
        {
            // 找到下一个可编辑的掩码位置
            while (maskIndex < mask.Length && !MaskProcessor.IsMaskChar(mask[maskIndex]))
            {
                maskIndex++;
            }

            if (maskIndex >= mask.Length)
            {
                break;
            }

            var maskChar = mask[maskIndex];
            var inputChar = rawInputStr[rawIndex];

            var isValid = maskChar switch
            {
                '9' => char.IsDigit(inputChar),
                'A' => char.IsLetter(inputChar),
                '*' => char.IsLetterOrDigit(inputChar),
                _ => false
            };

            if (isValid)
            {
                validatedInput.Append(inputChar);
                maskIndex++;
            }
            rawIndex++;
        }

        var validatedStr = validatedInput.ToString();

        // 应用掩码得到显示值
        var newDisplayValue = Processor.ApplyMask(validatedStr);

        // 计算光标位置
        var cursorPosition = CalculateCursorPosition(validatedStr.Length);

        // 更新状态
        _displayValue = newDisplayValue;

        // 更新绑定的 Value
        var unmasked = Processor.GetUnmaskedValue(_displayValue);
        if (unmasked != Value)
        {
            Value = unmasked;
            _ = ValueChanged.InvokeAsync(unmasked);

            validation.NotifyFieldChanged();
        }

        // 借由 JS 原子性地设置值与光标，避免闪烁
        _ = SetInputValueAsync(newDisplayValue, cursorPosition);
    }

    private async Task SetInputValueAsync(string value, int cursorPosition)
    {
        if (_jsModuleLoaded && _jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("maskedInput.setInputValue", _inputRef, value, cursorPosition);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开时属于预期情况
            }
        }
    }

    private int CalculateCursorPosition(int rawInputLength)
    {
        var mask = GetEffectiveMask();
        var position = 0;
        var inputCount = 0;

        for (var i = 0; i < mask.Length && inputCount < rawInputLength; i++)
        {
            position = i + 1;
            if (MaskProcessor.IsMaskChar(mask[i]))
            {
                inputCount++;
            }
        }

        // 若已填入若干字符，跳过紧随其后的字面量
        if (rawInputLength > 0 && position < mask.Length)
        {
            while (position < mask.Length && !MaskProcessor.IsMaskChar(mask[position]))
            {
                position++;
            }
        }

        return position;
    }

    private async Task HandleFocus(FocusEventArgs args)
    {
        // 值为空时显示掩码
        if (string.IsNullOrEmpty(Value) && ShowMask)
        {
            _displayValue = Processor.GetEmptyMask();

            // 借由 JS 设置值并把光标定位到起始处
            if (_jsModuleLoaded && _jsModule != null)
            {
                try
                {
                    // 找到第一个可编辑位置
                    var firstEditablePos = Processor.GetNextEditablePosition(0);
                    if (firstEditablePos < 0)
                    {
                        firstEditablePos = 0;
                    }

                    await _jsModule.InvokeVoidAsync("maskedInput.setInputValue", _inputRef, _displayValue, firstEditablePos);
                }
                catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
                {
                    // 电路断开时属于预期情况
                }
            }
            else
            {
                // BL0012（.NET 11）：事件处理器触发的重渲染由框架自动完成
                // 此处的显式 StateHasChanged 无需保留。
            }
        }
    }

    private void HandleBlur(FocusEventArgs args)
    {
        // 值为空且不显示掩码时清空显示
        if (string.IsNullOrEmpty(Value) && !ShowMask)
        {
            _displayValue = string.Empty;
            // BL0012（.NET 11）：事件处理器触发的重渲染由框架自动完成，无需显式 StateHasChanged。
        }
    }

    /// <summary>
    /// 获取底层 input 元素引用，供 JS interop 等场景使用。
    /// </summary>
    public ElementReference Element => _inputRef;

    /// <summary>
    /// 让焦点落到底层 input 元素上。
    /// </summary>
    public ValueTask FocusAsync() => _inputRef.FocusAsync();

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (_guardModule != null)
        {
            try
            {
                await _guardModule.InvokeVoidAsync("compositionGuard.detach", _guardId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开时属于预期情况
            }
        }
    }

    private string CssClass => ClassNames.cn(
        "atom:flex atom:h-10 atom:w-full atom:rounded-md atom:border atom:border-input atom:bg-background atom:px-3 atom:py-2 atom:text-base",
        "atom:placeholder:text-muted-foreground",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        "atom:aria-[invalid=true]:border-destructive",
        "atom:transition-colors",
        "atom:md:text-sm",
        "atom:font-mono atom:tracking-wider",
        Class
    );
}
