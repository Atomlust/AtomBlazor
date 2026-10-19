using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Linq.Expressions;

namespace AtomBlazor;

/// <summary>
/// 为在 InputGroup 内使用而优化的文本域组件。
/// </summary>
/// <remarks>
/// <para>
/// InputGroupTextarea 是与 InputGroup 容器无缝配合的专用文本域。
/// 它去掉了独立样式，因为视觉容器、边框与聚焦管理均由父级提供。
/// </para>
/// <para>
/// 特性：
/// - 透明背景，集成更无缝
/// - 无边框无聚焦环（由父级负责）
/// - 高度随内容弹性变化
/// - 缩放调节选项
/// - 自动标记以便父级探测
/// - JS 优先的事件处理，在 Blazor Server/Auto 下获得最佳性能
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;InputGroup&gt;
/// &lt;InputGroupTextarea Rows="4" Placeholder="Write a comment..." /&gt;
/// &lt;InputGroupAddon Align="InputGroupAlign.BlockEnd"&gt;
/// &lt;InputGroupText&gt;0 / 280&lt;/InputGroupText&gt;
/// &lt;/InputGroupAddon&gt;
/// &lt;/InputGroup&gt;
/// </code>
/// </example>
public partial class InputGroupTextarea : ComponentBase
{
    private ElementReference inputRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<InputGroupTextarea>? dotNetRef;
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
    /// 获取或设置文本域的当前值。
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置可见文本行数。
    /// </summary>
    /// <remarks>
    /// 默认 3 行。若启用缩放，文本域可增长超过该行数。
    /// </remarks>
    [Parameter]
    public int Rows { get; set; } = 3;

    /// <summary>
    /// 获取或设置占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置文本域是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置文本域是否必填。
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
    /// 获取或设置文本域的值是否无效。
    /// </summary>
    [Parameter]
    public bool? AriaInvalid { get; set; }

    /// <summary>
    /// 获取或设置文本域元素的 HTML name 属性。
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

    private string EffectiveId => Id ?? (generatedId ??= $"textarea-{Guid.NewGuid().ToString("N")[..8]}");

    /// <summary>
    /// 获取或设置额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取文本域元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础样式——组上下文保持最简
        "atom:flex-1 atom:bg-transparent atom:px-3 atom:py-2 atom:text-base atom:min-h-[60px]",
        "atom:border-0 atom:rounded-none", // 无边框无圆角，无缝融合
        "atom:placeholder:text-muted-foreground",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        "atom:resize-none", // 禁止手动缩放，外观更干净
        // 中等屏幕及以上：更小字号
        "atom:md:text-sm",
        // 自定义类
        Class
    );

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
            // DebounceInterval 被固定在文本域首次渲染时的取值上。
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
    /// 由 JavaScript 在失焦时回调（所有模式）。
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
    /// 获取底层文本域元素引用，供 JS interop 等场景使用。
    /// </summary>
    public ElementReference Element => inputRef;

    /// <summary>
    /// 让底层文本域元素获得焦点。
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
