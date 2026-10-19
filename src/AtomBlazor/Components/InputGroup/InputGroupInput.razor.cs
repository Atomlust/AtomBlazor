using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Linq.Expressions;

namespace AtomBlazor;

/// <summary>
/// 为在 InputGroup 内使用而优化的输入框组件。
/// </summary>
/// <remarks>
/// <para>
/// InputGroupInput 是 Input 组件的专用变体，与 InputGroup 容器无缝配合。
/// 它去掉了独立输入框的样式（边框、背景、聚焦环）
/// 因为这些均由父级 InputGroup 提供。
/// </para>
/// <para>
/// 特性：
/// - 透明背景，集成更无缝
/// - 无边框无聚焦环（由父级负责）
/// - 弹性宽度填满可用空间
/// - 与标准 Input 参数完全兼容
/// - 自动标记以便父级探测（data-slot 属性）
/// - JS 优先的事件处理，在 Blazor Server/Auto 下获得最佳性能
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;InputGroup&gt;
/// &lt;InputGroupInput Type="InputType.Email" Placeholder="Enter email" /&gt;
/// &lt;/InputGroup&gt;
/// </code>
/// </example>
public partial class InputGroupInput : ComponentBase
{
    private ElementReference inputRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<InputGroupInput>? dotNetRef;
    private readonly string instanceId = Guid.NewGuid().ToString("N");
    private string? generatedId;
    private bool jsInitialized;
    private bool disposed;
    private readonly InputValidationBehavior validation = new();

    /// <summary>
    /// 获取或设置从父级 EditForm 级联而来的 EditContext。
    /// </summary>
    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    [CascadingParameter(Name = "FieldIsInvalid")]
    private bool? FieldIsInvalid { get; set; }

    /// <summary>
    /// 获取或设置输入类型。
    /// </summary>
    [Parameter]
    public InputType Type { get; set; } = InputType.Text;

    /// <summary>
    /// 获取或设置输入框的当前值。
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

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
    /// 获取或设置额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置 HTML id 属性。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置 aria-describedby 属性。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置输入框的值是否无效。
    /// </summary>
    [Parameter]
    public bool? AriaInvalid { get; set; }

    /// <summary>
    /// 获取或设置输入框元素的 HTML name 属性。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式。
    /// </summary>
    [Parameter]
    public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置 <see cref="ValueChanged"/> 的触发时机。
    /// </summary>
    [Parameter]
    public UpdateTiming UpdateTiming { get; set; } = UpdateTiming.OnChange;

    /// <summary>
    /// 获取或设置 <see cref="UpdateTiming"/> 为 <see cref="UpdateTiming.Debounced"/> 时的防抖延迟（毫秒）。
    /// </summary>
    [Parameter]
    public int DebounceInterval { get; set; } = 500;

    private string? EffectiveAriaInvalid => validation.GetEffectiveAriaInvalid(AriaInvalid, FieldIsInvalid);

    /// <summary>
    /// 获取有效的 name 属性值：位于 EditForm 内时回退为 FieldIdentifier 的名称。
    /// </summary>
    private string? EffectiveName => validation.GetEffectiveName(Name);

    private string EffectiveId => Id ?? (generatedId ??= $"input-{Guid.NewGuid().ToString("N")[..8]}");

    /// <summary>
    /// 获取或设置额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 首次渲染后携带输入框 ElementReference 的回调。
    /// 供聚焦、事件绑定等 JS interop 场景使用。
    /// </summary>
    [Parameter]
    public Action<ElementReference>? OnInputRef { get; set; }

    /// <summary>
    /// 获取输入框元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础样式——组上下文保持最简
        "atom:flex-1 atom:bg-transparent atom:px-3 atom:py-2 atom:text-base",
        "atom:border-0 atom:rounded-none", // 无边框无圆角，无缝融合
        "atom:placeholder:text-muted-foreground",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        // 文件输入样式
        "atom:file:border-0 atom:file:bg-transparent atom:file:text-sm atom:file:font-medium atom:file:text-foreground",
        // 中等屏幕及以上：更小字号
        "atom:md:text-sm",
        // 自定义类
        Class
    );

    /// <summary>
    /// 获取 HTML input 的 type 属性值。
    /// </summary>
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

        validation.Update(CascadedEditContext, ValueExpression);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            OnInputRef?.Invoke(inputRef);

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
    private TextInputJsConfig GetJsConfig() => new TextInputJsConfig
    {
        mode = UpdateTiming switch
        {
            UpdateTiming.Immediate => "immediate",
            UpdateTiming.OnChange => "onchange",
            UpdateTiming.Debounced => "debounced",
            _ => "onchange"
        },
        debounceMs = DebounceInterval
    };

    /// <summary>
    /// 由 JavaScript 在键入过程中回调（仅 Immediate/Debounced 模式）。
    /// </summary>
    [JSInvokable]
    public async Task JsOnInput(string? value)
    {
        if (disposed) { return; }

        if (string.IsNullOrWhiteSpace(value))
        {
            value = null;
        }

        Value = value;

        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(value);
        }

        validation.NotifyFieldChanged();

        StateHasChanged();
    }

    /// <summary>
    /// 由 JavaScript 在失焦/回车时回调（所有模式）。
    /// </summary>
    [JSInvokable]
    public async Task JsOnChange(string? value)
    {
        if (disposed) { return; }

        if (string.IsNullOrWhiteSpace(value))
        {
            value = null;
        }

        Value = value;

        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(value);
        }

        validation.NotifyFieldChanged();

        StateHasChanged();
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
