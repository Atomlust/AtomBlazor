using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Linq.Expressions;

namespace AtomBlazor;

/// <summary>
/// 遵循 shadcn/ui 设计体系的输入框组件。
/// </summary>
/// <remarks>
/// <para>
/// Input 组件提供可定制的表单输入框，支持多种输入类型与状态。
/// 内置 ARIA 属性，并与 Blazor 的数据绑定体系集成。
/// </para>
/// <para>
/// 特性：
/// - 多种输入类型（text、email、password、number、tel、url、file、search、date、time）
/// - 通过自定义伪元素选择器实现文件输入样式
/// - 通过 aria-invalid 属性呈现错误态
/// - 状态变化带平滑颜色过渡
/// - 支持禁用与必填状态
/// - 支持占位文本
/// - Value/ValueChanged 双向绑定
/// - 内置 ARIA 属性
/// - RTL（从右到左）支持
/// - 通过 CSS 变量兼容暗色模式
/// - JS 优先的事件处理，在 Blazor Server/Auto 下获得最佳性能
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Input Type="InputType.Text" @bind-Value="userName" Placeholder="Enter your name" /&gt;
///
/// &lt;Input Type="InputType.Email" Value="@email" ValueChanged="HandleEmailChange" Required="true" AriaInvalid="@hasError" /&gt;
/// </code>
/// </example>
public partial class Input : ComponentBase
{
    private ElementReference inputRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<Input>? dotNetRef;
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
    /// <remarks>
    /// 决定 HTML input 的 type 属性。
    /// 默认值为 <see cref="InputType.Text"/>。
    /// </remarks>
    [Parameter]
    public InputType Type { get; set; } = InputType.Text;

    /// <summary>
    /// 获取或设置输入框的当前值。
    /// </summary>
    /// <remarks>
    /// 支持通过 @bind-Value 语法进行双向绑定。
    /// </remarks>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置值变化时调用的回调。
    /// </summary>
    /// <remarks>
    /// 当 <see cref="UpdateTiming"/> 为 <see cref="UpdateTiming.OnChange"/>（默认）时
    /// 仅在失焦或回车时触发。需要逐键更新请使用 <see cref="UpdateTiming.Immediate"/>。
    /// </remarks>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置输入框为空时显示的占位文本。
    /// </summary>
    /// <remarks>
    /// 向用户提示应输入的内容。
    /// 不应以占位文本替代标签。
    /// </remarks>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置输入框是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用后：
    /// - 无法聚焦或编辑
    /// - 光标显示为 not-allowed
    /// - 降低不透明度以提供视觉反馈
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置输入框是否必填。
    /// </summary>
    /// <remarks>
    /// 为 true 时输出 HTML5 required 属性。
    /// 与表单校验及 :invalid CSS 伪类协同工作。
    /// </remarks>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置应用到输入框的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后
    /// 便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置输入框元素的 HTML id 属性。
    /// </summary>
    /// <remarks>
    /// 用于通过 label 的 for 属性把输入框与标签元素关联起来。
    /// 这对无障碍访问至关重要，也让点击标签即可聚焦输入框。
    /// </remarks>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置输入框的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 为屏幕阅读器提供可访问名称。
    /// 在没有可见标签元素时使用。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置描述该输入框的元素 ID。
    /// </summary>
    /// <remarks>
    /// 引用包含帮助文本或错误消息的元素 id
    /// 通过 aria-describedby 将描述性文本与输入框关联。
    /// </remarks>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置输入框的值是否无效。
    /// </summary>
    /// <remarks>
    /// 为 true 时输出 aria-invalid="true"。
    /// 应根据校验状态设置。
    /// </remarks>
    [Parameter]
    public bool? AriaInvalid { get; set; }

    /// <summary>
    /// 获取或设置应用到输入框元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置输入框元素的 HTML name 属性。
    /// </summary>
    /// <remarks>
    /// 位于 EditForm 内且未显式设置时，name 自动从
    /// ValueExpression（FieldIdentifier），以支持 SSR 表单回传。
    /// </remarks>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式。
    /// </summary>
    /// <remarks>
    /// 用于表单校验集成。提供后输入框会注册到 EditContext
    /// 并参与表单校验。
    /// </remarks>
    [Parameter]
    public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置 <see cref="ValueChanged"/> 的触发时机。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="UpdateTiming.Immediate"/> — 每次键入（经 requestAnimationFrame 批量合并）。</item>
    /// <item><see cref="UpdateTiming.OnChange"/> — 仅在失焦/回车时触发（默认）。</item>
    /// <item><see cref="UpdateTiming.Debounced"/> — 停止键入 <see cref="DebounceInterval"/> 毫秒后触发。</item>
    /// </list>
    /// </remarks>
    [Parameter]
    public UpdateTiming UpdateTiming { get; set; } = UpdateTiming.OnChange;

    /// <summary>
    /// 获取或设置 <see cref="UpdateTiming"/> 为 <see cref="UpdateTiming.Debounced"/> 时的防抖延迟（毫秒）。
    /// </summary>
    /// <remarks>
    /// <see cref="UpdateTiming"/> 不是 <see cref="UpdateTiming.Debounced"/> 时忽略。默认 500 毫秒。
    /// </remarks>
    [Parameter]
    public int DebounceInterval { get; set; } = 500;

    /// <summary>
    /// 获取有效的 name 属性值：位于 EditForm 内时回退为 FieldIdentifier 的名称。
    /// </summary>
    private string? EffectiveName => validation.GetEffectiveName(Name);

    /// <summary>
    /// 获取合并了手动 AriaInvalid 与 EditContext 校验结果的有效 aria-invalid 值。
    /// </summary>
    private string? EffectiveAriaInvalid => validation.GetEffectiveAriaInvalid(AriaInvalid, FieldIsInvalid);

    private string EffectiveId => Id ?? (generatedId ??= $"input-{Guid.NewGuid().ToString("N")[..8]}");

    /// <summary>
    /// 获取输入框元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础输入框样式（来自 shadcn/ui）
        "atom:flex atom:h-10 atom:w-full atom:rounded-md atom:border atom:border-input atom:bg-background atom:px-3 atom:py-2 atom:text-base",
        "atom:file:border-0 atom:file:bg-transparent atom:file:text-sm atom:file:font-medium atom:file:text-foreground",
        "atom:placeholder:text-muted-foreground",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        // aria-invalid 状态样式（destructive 错误色）
        "atom:aria-[invalid=true]:border-destructive",
        // 状态变化的平滑过渡
        "atom:transition-colors",
        // 中等屏幕及以上：更小字号
        "atom:md:text-sm",
        // 自定义类（如有）
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
