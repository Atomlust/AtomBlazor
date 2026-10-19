using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>
/// 支持自动类型转换的泛型输入组件，提供强类型双向绑定。
/// </summary>
/// <remarks>
/// <para>
/// 与只绑定 <c>string</c> 的 <see cref="Input"/> 不同，<c>InputField&lt;TValue&gt;</c>
/// 借助 <see cref="InputConverter{TValue}"/> 提供强类型双向绑定，支持任意已注册转换器的类型
/// 的格式化、解析、校验与错误处理。
/// </para>
/// <para>
/// 特性：
/// - 强类型双向绑定（int、decimal、DateTime、Guid、自定义类型等）
/// - 转换器体系：实例级、内置默认转换器逐级解析
/// - 通过 <see cref="Format"/> 参数支持显示格式
/// - 编辑/显示切换模式（聚焦时显示原始值，失焦后显示格式化值）
/// - 通过 <see cref="OnParseError"/> 与 <see cref="HasParseError"/> 上报解析错误
/// - 通过 <see cref="CurrentErrorKind"/> 与 <see cref="InputFieldErrorKind"/> 区分错误种类
/// - 通过 <see cref="OnErrorCleared"/> 通知错误已清除
/// - 失焦时保留非法文本（用户能看到自己输错了什么）
/// - 通过 <see cref="ValidationPattern"/> 在解析前做正则校验
/// - 通过 <see cref="Validation"/> 在解析后做值校验
/// - 完整 ARIA 属性支持，解析错误时自动输出 aria-invalid
/// - 与 <see cref="Input"/> 完全一致的视觉外观
/// - JS 优先的事件处理，在 Blazor Server/Auto 下获得最佳性能
/// </para>
/// </remarks>
/// <typeparam name="TValue">要绑定的值类型。无约束——值类型、引用类型与可空类型均可。</typeparam>
/// <example>
/// <code>
/// &lt;InputField TValue="int" @bind-Value="age" Placeholder="Enter age" /&gt;
///
/// &lt;InputField TValue="DateTime?" @bind-Value="birthDate" Format="yyyy-MM-dd" Type="InputType.Date" /&gt;
///
/// &lt;InputField TValue="decimal" @bind-Value="price" OnParseError="HandleError" /&gt;
/// </code>
/// </example>
public partial class InputField<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] TValue> : ComponentBase
{
    private ElementReference inputRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<InputField<TValue>>? dotNetRef;
    private readonly string instanceId = Guid.NewGuid().ToString("N");
    private string? generatedId;
    private bool jsInitialized;
    private bool disposed;
    private string editingValue = string.Empty;
    private bool isEditing;
    private bool hasParseError;
    private InputFieldErrorKind? currentErrorKind;
    private readonly InputValidationBehavior validation = new();
    private EditContext? subscribedEditContext;
    private InputConverter<TValue>? cachedDefaultConverter;

    /// <summary>
    /// 获取或设置当前的强类型值。
    /// </summary>
    /// <remarks>
    /// 支持通过 @bind-Value 语法进行双向绑定。
    /// 值经由转换器解析链在字符串与 <typeparamref name="TValue"/> 之间转换。
    /// </remarks>
    [Parameter]
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置强类型值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置本组件实例的可选自定义转换器。
    /// </summary>
    /// <remarks>
    /// 提供后，转换器的 <see cref="InputConverter{TValue}.GetFunc"/> 与
    /// <see cref="InputConverter{TValue}.SetFunc"/> 在解析链中拥有最高优先级。
    /// </remarks>
    [Parameter]
    public InputConverter<TValue>? Converter { get; set; }

    /// <summary>
    /// 获取或设置显示格式字符串。
    /// </summary>
    /// <remarks>
    /// 设置后使用 <see cref="IFormattable.ToString(string, IFormatProvider)"/> 进行显示格式化
    /// （如日期的 "yyyy-MM-dd"、数字的 "N2"）。Format 只影响显示
    /// 解析始终使用转换器的 <c>Get</c> 函数。
    /// </remarks>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// 获取或设置失焦时发生解析或校验错误的回调。
    /// </summary>
    /// <remarks>
    /// 当用户离开输入框时，值无法转换为 <typeparamref name="TValue"/> 或未通过校验则触发。
    /// 键入过程中的错误会被静默忽略。
    /// <see cref="InputParseException.ErrorKind"/> 属性指明具体的失败类型。
    /// </remarks>
    [Parameter]
    public EventCallback<InputParseException> OnParseError { get; set; }

    /// <summary>
    /// 获取或设置错误状态被清除时的回调。
    /// </summary>
    /// <remarks>
    /// 当输入框从错误态回到有效态时触发——无论用户键入了有效值还是清空了输入。
    /// 可用于清除父组件中的错误消息。
    /// </remarks>
    [Parameter]
    public EventCallback OnErrorCleared { get; set; }

    /// <summary>
    /// 获取输入框当前是否存在解析或校验错误。
    /// </summary>
    /// <remarks>
    /// 失焦时解析或校验失败置为 <c>true</c>；键入有效值后自动清除。
    /// 供使用者条件性地展示错误消息。
    /// </remarks>
    public bool HasParseError => hasParseError;

    /// <summary>
    /// 获取当前生效的错误种类，无错误时为 <c>null</c>。
    /// </summary>
    /// <remarks>
    /// 比 <see cref="HasParseError"/> 提供更细粒度的错误信息：
    /// 转换失败返回 <see cref="InputFieldErrorKind.Parse"/>
    /// 正则不匹配返回 <see cref="InputFieldErrorKind.PatternValidation"/>
    /// 解析后校验失败返回 <see cref="InputFieldErrorKind.ValueValidation"/>。
    /// </remarks>
    public InputFieldErrorKind? CurrentErrorKind => currentErrorKind;

    /// <summary>
    /// 获取或设置解析后的值校验函数。
    /// </summary>
    /// <remarks>
    /// 在成功解析后调用，用于校验输入的值。
    /// 值有效返回 <c>true</c>，拒绝该值返回 <c>false</c>。
    /// </remarks>
    [Parameter]
    public Func<TValue, bool>? Validation { get; set; }

    /// <summary>
    /// 获取或设置对原始字符串做解析前校验的正则表达式。
    /// </summary>
    /// <remarks>
    /// 设置后，原始输入字符串在解析前先与该模式匹配。
    /// 使用 <see cref="Regex.IsMatch(string, string)"/>。
    /// </remarks>
    [Parameter]
    public string? ValidationPattern { get; set; }

    /// <summary>
    /// 获取或设置输入类型。
    /// </summary>
    /// <remarks>
    /// 决定 HTML input 的 type 属性。默认 <see cref="InputType.Text"/>。
    /// </remarks>
    [Parameter]
    public InputType Type { get; set; } = InputType.Text;

    /// <summary>
    /// 获取或设置输入框为空时显示的占位文本。
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
    /// 获取或设置应用到输入框的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置输入框元素的 HTML id 属性。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置输入框的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置描述该输入框的元素 ID。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置输入框的值是否无效。
    /// </summary>
    /// <remarks>
    /// 为 true 时输出 aria-invalid="true"。与 <see cref="HasParseError"/> 合并考虑
    /// 任一为真即应用 destructive 边框 CSS。
    /// </remarks>
    [Parameter]
    public bool? AriaInvalid { get; set; }

    /// <summary>
    /// 获取或设置 <see cref="ValueChanged"/> 的触发时机。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="UpdateTiming.Immediate"/> — 每次键入（经 requestAnimationFrame 批量合并）。</item>
    /// <item><see cref="UpdateTiming.OnChange"/> — 仅在失焦时触发（默认）。</item>
    /// <item><see cref="UpdateTiming.Debounced"/> — 停止键入 <see cref="DebounceInterval"/> 毫秒后触发。</item>
    /// </list>
    /// </remarks>
    [Parameter]
    public UpdateTiming UpdateTiming { get; set; } = UpdateTiming.OnChange;

    /// <summary>
    /// 获取或设置 <see cref="UpdateTiming"/> 为 <see cref="UpdateTiming.Debounced"/> 时的防抖延迟（毫秒）。
    /// </summary>
    [Parameter]
    public int DebounceInterval { get; set; } = 500;

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// 由 <c>@bind-Value</c> 自动提供。
    /// </summary>
    [Parameter]
    public Expression<Func<TValue?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置 HTML name 属性。位于 EditForm 内且未显式设置时
    /// 自动从 <see cref="ValueExpression"/> 。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    private InputConverter<TValue> ResolvedConverter => Converter ?? (cachedDefaultConverter ??= new InputConverter<TValue>());

    private string? EffectiveName => validation.GetEffectiveName(Name);

    private string EffectiveId => Id ?? (generatedId ??= $"inputfield-{Guid.NewGuid().ToString("N")[..8]}");

    private bool? ComputedAriaInvalid => (AriaInvalid == true || hasParseError || validation.IsInvalid) ? true : AriaInvalid;

    private string DisplayValue
    {
        get
        {
            if (isEditing)
            {
                return editingValue;
            }

            // 保留非法文本，让用户看到自己输错了什么
            if (hasParseError)
            {
                return editingValue;
            }

            if (Value is null)
            {
                return string.Empty;
            }

            if (Format is not null)
            {
                return ResolvedConverter.SetWithFormat(Value, Format) ?? string.Empty;
            }

            return ResolvedConverter.Set(Value) ?? string.Empty;
        }
    }

    private string CssClass => ClassNames.cn(
        "atom:flex atom:h-10 atom:w-full atom:rounded-md atom:border atom:border-input atom:bg-background atom:px-3 atom:py-2 atom:text-base",
        "atom:file:border-0 atom:file:bg-transparent atom:file:text-sm atom:file:font-medium atom:file:text-foreground",
        "atom:placeholder:text-muted-foreground",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        "atom:aria-[invalid=true]:border-destructive",
        "atom:transition-colors",
        "atom:md:text-sm",
        Class
    );

    private string HtmlType => Type switch
    {
        InputType.Text => "text",
        InputType.Email => "email",
        InputType.Password => "password",
        InputType.Number => "number",
        InputType.Tel => "tel",
        InputType.Url => "url",
        InputType.Search => "search",
        InputType.Date => "date",
        InputType.Time => "time",
        InputType.File => "file",
        _ => "text"
    };

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (CascadedEditContext != subscribedEditContext)
        {
            subscribedEditContext?.OnValidationStateChanged -= OnValidationStateChanged;

            subscribedEditContext = CascadedEditContext;

            subscribedEditContext?.OnValidationStateChanged += OnValidationStateChanged;
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
                lastJsConfig = GetJsConfig();
                await jsModule.InvokeVoidAsync("textInput.initialize", inputRef, dotNetRef, instanceId, lastJsConfig);
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
            // 模块一直暴露着 updateConfig 却从未被调用，导致 UpdateTiming 与
            // DebounceInterval 被固定在输入框首次渲染时的取值上。
            var config = GetJsConfig();

            if (!config.Equals(lastJsConfig))
            {
                lastJsConfig = config;

                try
                {
                    await jsModule.InvokeVoidAsync("textInput.updateConfig", instanceId, config);
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
    }

    /// <summary>
    /// 上一次发给浏览器侧的配置，用于识别变化。命名 record 按值比较（结构相等）
    /// 这里正需要这种语义。
    /// </summary>
    private object? lastJsConfig;

    /// <summary>
    /// 依据当前参数构建发给 JS 的配置对象。
    /// </summary>
    private InputFieldJsConfig GetJsConfig() => new InputFieldJsConfig
    {
        mode = UpdateTiming switch
        {
            UpdateTiming.Immediate => "immediate",
            UpdateTiming.OnChange => "onchange",
            UpdateTiming.Debounced => "debounced",
            _ => "onchange"
        },
        debounceMs = DebounceInterval,
        notifyOnBlur = true
    };

    private void NotifyFieldChanged() => validation.NotifyFieldChanged();

    private void OnValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) =>
        StateHasChanged();

    /// <summary>
    /// 由 JavaScript 在键入过程中回调（仅 Immediate/Debounced 模式）。
    /// JS 侧已完成批量（rAF）或防抖（setTimeout）处理。
    /// </summary>
    [JSInvokable]
    public async Task JsOnInput(string? value)
    {
        if (disposed) { return; }

        var inputValue = value ?? string.Empty;
        editingValue = inputValue;
        isEditing = true;

        // 实时尝试解析；键入过程中的错误静默忽略
        try
        {
            if (string.IsNullOrEmpty(inputValue))
            {
                var defaultValue = default(TValue);
                if (!EqualityComparer<TValue?>.Default.Equals(Value, defaultValue))
                {
                    Value = defaultValue;
                    await ValueChanged.InvokeAsync(defaultValue);
                }

                await ClearErrorState();
                NotifyFieldChanged();
                StateHasChanged();
                return;
            }

            // 未设置模式时跳过正则沿：Regex.IsMatch(input, null) 会抛 ArgumentNullException
            // 使全部输入被判为解析失败（与源项目 的空值守卫一致）。
            if (ValidationPattern is not null && !Regex.IsMatch(inputValue, ValidationPattern))
            {
                StateHasChanged();
                return;
            }

            var parsed = ResolvedConverter.Get(inputValue);

            if (Validation is not null && !Validation(parsed))
            {
                StateHasChanged();
                return;
            }

            if (!EqualityComparer<TValue?>.Default.Equals(Value, parsed))
            {
                Value = parsed;
                await ValueChanged.InvokeAsync(parsed);
            }

            await ClearErrorState();
        }
        catch
        {
            // 键入过程中的解析错误静默忽略
        }

        NotifyFieldChanged();
        StateHasChanged();
    }

    /// <summary>
    /// 由 JavaScript 在失焦（所有模式）与回车（change 事件）时回调。
    /// 负责带错误上报的解析处理，并从编辑态切换回显示态。
    /// </summary>
    [JSInvokable]
    public async Task JsOnChange(string? value)
    {
        if (disposed) { return; }

        var inputValue = value ?? string.Empty;
        editingValue = inputValue;
        isEditing = false;

        if (string.IsNullOrEmpty(inputValue))
        {
            var defaultValue = default(TValue);
            if (!EqualityComparer<TValue?>.Default.Equals(Value, defaultValue))
            {
                Value = defaultValue;
                await ValueChanged.InvokeAsync(defaultValue);
            }

            await ClearErrorState();
            NotifyFieldChanged();
            StateHasChanged();
            return;
        }

        try
        {
            if (ValidationPattern is not null && !Regex.IsMatch(inputValue, ValidationPattern))
            {
                throw new InputFieldValidationException(InputFieldErrorKind.PatternValidation,
                    $"Input '{inputValue}' does not match validation pattern.");
            }

            var parsed = ResolvedConverter.Get(inputValue);

            if (Validation is not null && !Validation(parsed))
            {
                throw new InputFieldValidationException(InputFieldErrorKind.ValueValidation,
                    $"Value failed validation.");
            }

            if (!EqualityComparer<TValue?>.Default.Equals(Value, parsed))
            {
                Value = parsed;
                await ValueChanged.InvokeAsync(parsed);
            }

            await ClearErrorState();
        }
        catch (InputFieldValidationException ex)
        {
            await SetErrorState(ex.ErrorKind, ex);
        }
        catch (Exception ex)
        {
            await SetErrorState(InputFieldErrorKind.Parse, ex);
        }

        NotifyFieldChanged();
        StateHasChanged();
    }

    /// <summary>
    /// 处理聚焦事件。进入编辑态并载入未格式化的原始值。
    /// 保留为 Blazor 的 @onfocus 处理器：聚焦是一次性事件，无需 JS 化处理。
    /// </summary>
    private void HandleFocus(FocusEventArgs args)
    {
        if (hasParseError)
        {
            // 保留非法文本，便于用户修正
            isEditing = true;
            return;
        }

        if (Value is null)
        {
            editingValue = string.Empty;
        }
        else
        {
            // 编辑期间显示未格式化的值
            editingValue = ResolvedConverter.Set(Value) ?? string.Empty;
        }

        isEditing = true;
    }

    /// <summary>
    /// 清除错误状态；若从错误态回到有效态则触发 <see cref="OnErrorCleared"/>。
    /// </summary>
    private async Task ClearErrorState()
    {
        if (hasParseError)
        {
            hasParseError = false;
            currentErrorKind = null;

            if (OnErrorCleared.HasDelegate)
            {
                await OnErrorCleared.InvokeAsync();
            }
        }
    }

    /// <summary>
    /// 设置错误状态并触发 <see cref="OnParseError"/>。
    /// </summary>
    private async Task SetErrorState(InputFieldErrorKind errorKind, Exception ex)
    {
        hasParseError = true;
        currentErrorKind = errorKind;

        if (OnParseError.HasDelegate)
        {
            var parseException = new InputParseException(editingValue, typeof(TValue), errorKind, ex);
            await OnParseError.InvokeAsync(parseException);
        }
    }

    /// <summary>
    /// 获取底层输入框元素引用，供 JS interop 等场景使用。
    /// </summary>
    public ElementReference Element => inputRef;

    /// <summary>
    /// 让底层输入框元素获得焦点。
    /// </summary>
    public ValueTask FocusAsync() => inputRef.FocusAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        disposed = true;

        subscribedEditContext?.OnValidationStateChanged -= OnValidationStateChanged;

        if (jsModule != null && jsInitialized)
        {
            try
            {
                await jsModule.InvokeVoidAsync("textInput.dispose", instanceId);
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

/// <summary>
/// 内部异常：在 catch 块中区分校验失败与解析失败。
/// </summary>
/// <remarks>
/// 偏差说明：源项目将其嵌套于 &lt;T&gt; 内部；dotnet 11 RC2 裁剪分析器对
/// "泛型组件类型中嵌套 Exception 类"存在 IL2026 误报路径（宿主 OpenComponent 处报错）
/// 故提升到文件级 internal，行为与可见性（assembly 内私有）等价。
/// </remarks>
internal sealed class InputFieldValidationException : Exception
{
    public InputFieldErrorKind ErrorKind { get; }

    public InputFieldValidationException(InputFieldErrorKind errorKind, string message)
        : base(message)
    {
        ErrorKind = errorKind;
    }
}
