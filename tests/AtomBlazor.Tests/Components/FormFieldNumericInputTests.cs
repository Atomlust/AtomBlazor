using AtomBlazor.Primitives;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FormFieldNumericInput 组合字段测试：在可选 EditContext 宿主下挂载
/// FormFieldNumericInput&lt;int&gt;/&lt;decimal&gt;（根 0，底座无弹层，无需 PortalHost）
/// 验证标签 for 实通输入（Id=ControlId 直落底座 input）、aria-describedby 经正式参数落 input、
/// 键入即时上报并钳到 Max、清空回落零、禁负拒值、方向键随 Step 步进且 Home/End 锚边界、
/// 增减按钮 spinbutton 角色与点击步进沿（上限吞事件）、EditContext 校验消息随数量修改消长
/// 并经 input 的 aria-invalid 显形（本家族有 AriaInvalid 通道，与 DateInput 家族 quirk 不同）、
/// ErrorText 手动覆写切换 describedby、Disabled/Required/Name 落 input 且联动按钮禁用
/// （三家族封装层与底座均无 ReadOnly 通道；且 Numeric 容器不叠 opacity-50
/// 禁用形态与 Currency 家族不同）、Format/DecimalPlaces 控显示面（decimal 实例）、
/// AriaLabel+InputClass 落 input、splat 落底座根容器（非 Field 根）与缺省参数面。
/// 底座键入/步进沿用 NumericInputTests headless 先例：直接调用 [JSInvokable] 面
/// （JsOnInput/JsOnBlur/JsOnKeyDown），按钮沿按 onclick 出现序派发。快照含 formfield GUID
/// 先经正则归一化。
/// </summary>
public class FormFieldNumericInputTests
{
    /// <summary>归一化 ControlId 随机 GUID（input id、label for 与 describedby 通道），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>");

    /// <summary>构造带基元服务的渲染器（底座 OnAfterRenderAsync 需 IJSRuntime；沿用 NumericInputTests 先例）。</summary>
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>触发一次 DOM 事件（沿 NumericInputTests 先例须在 Dispatcher 上派发）。</summary>
    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () => await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>底座 JS 回调沿：直接在 Dispatcher 上调用 [JSInvokable] 入口并等待落定（NumericInputTests 先例）。</summary>
    private static Task InvokeBaseAsync<TValue>(
        ComponentTestRenderer renderer, Func<NumericInput<TValue>, Task> callback)
        where TValue : struct, System.Numerics.INumber<TValue> =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await callback(renderer.FindComponent<NumericInput<TValue>>()));

    /// <summary>校验沿测试模型：Quantity 非 42 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public int Quantity { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Quantity 不为 42 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Quantity));

        void Validate()
        {
            messages.Clear();
            if (doc.Quantity != 42)
            {
                messages.Add(field, "数量必须为 42");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        int? value = null,
        EventCallback<int> valueChanged = default,
        Expression<Func<int>>? valueExpression = null,
        string? name = null,
        int? min = null,
        int? max = null,
        int? step = null,
        bool? allowNegative = null,
        bool? showButtons = null,
        string? placeholder = null,
        bool? disabled = null,
        bool? required = null,
        bool? disableDebounce = null,
        int? debounceInterval = null,
        bool? enableWheelStep = null,
        string? inputClass = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        FieldOrientation? orientation = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (value is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldNumericInput<int>.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.ValueExpression)] = valueExpression;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Name)] = name;
        }
        if (min is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Min)] = min;
        }
        if (max is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Max)] = max;
        }
        if (step is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Step)] = step;
        }
        if (allowNegative is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.AllowNegative)] = allowNegative.Value;
        }
        if (showButtons is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.ShowButtons)] = showButtons.Value;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Placeholder)] = placeholder;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Required)] = required.Value;
        }
        if (disableDebounce is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.DisableDebounce)] = disableDebounce.Value;
        }
        if (debounceInterval is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.DebounceInterval)] = debounceInterval.Value;
        }
        if (enableWheelStep is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.EnableWheelStep)] = enableWheelStep.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldNumericInput<int>.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>取底座 NumericInput 根容器开标签（其容器类带 items-stretch 指纹；Field 根仅收 Class）。</summary>
    private static string BaseRoot(string markup) =>
        Regex.Match(markup, """<div[^>]*atom:items-stretch[^>]*>""").Value;

    /// <summary>取底座 input 开标签。</summary>
    private static string InputTag(string markup) =>
        Regex.Match(markup, "<input[^>]*>").Value;

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            label: "订购数量", helperText: "1 到 100 之间",
            value: 42, name: "order.qty", max: 100, showButtons: true));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应实通输入且describedby经正式参数落input()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            label: "订购数量", helperText: "1 到 100 之间", showButtons: true));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // FieldLabel For=ControlId 实通：底座 Id="@ControlId" 直落 input id。
        var id = Regex.Match(markup, """<input[^>]*id="([^"]*)"[^>]*>""").Groups[1].Value;
        Assert.Matches(@"^formfield-[0-9a-f]{32}$", id);
        Assert.Contains($"for=\"{id}\"", markup);
        Assert.Contains($"aria-describedby=\"{id}-description\"", InputTag(markup));
        // ShowButtons 透传：input 挂 spinbutton 角色。
        Assert.Contains("role=\"spinbutton\"", InputTag(markup));

        // 显式 null 复位 HelperText：参数视图中未出现的参数保持组件当前值。
        var bare = FieldParams(label: "订购数量", showButtons: true);
        bare[nameof(FormFieldNumericInput<int>.HelperText)] = null;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, bare));
        await SettleAsync(renderer);
        Assert.DoesNotContain("aria-describedby", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 值回填沿与边界
    // ------------------------------------------------------------------

    [Fact]
    public async Task 键入超上限应即时钳制上报并保留编辑原文()
    {
        var reports = new List<int>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            value: 1, max: 50,
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await InvokeBaseAsync<int>(renderer, i => i.JsOnInput("999"));
        await SettleAsync(renderer);

        Assert.Equal([50], reports); // Max 透传生效：键入即时钳到上限
        Assert.Equal(50, field.Value); // HandleValueChanged 赋值步沿
        Assert.Contains(@"value=""999""", renderer.Markup()); // 编辑态保留用户原始文本
    }

    [Fact]
    public async Task 清空输入应回落为零并经三步沿上报()
    {
        var reports = new List<int>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            value: 9,
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await InvokeBaseAsync<int>(renderer, i => i.JsOnBlur(""));
        await SettleAsync(renderer);

        Assert.Equal([0], reports);
        Assert.Equal(0, field.Value);
    }

    [Fact]
    public async Task 禁负时负值应被拒绝而正值照常()
    {
        var reports = new List<int>();
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            value: 7, allowNegative: false,
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await InvokeBaseAsync<int>(renderer, i => i.JsOnBlur("-5"));
        await SettleAsync(renderer);
        Assert.Empty(reports); // AllowNegative=false：解析被拒，零回调值不动

        await InvokeBaseAsync<int>(renderer, i => i.JsOnBlur("5"));
        await SettleAsync(renderer);
        Assert.Equal([5], reports);
    }

    [Fact]
    public async Task 方向键应随步长进退且HomeEnd锚定边界()
    {
        var reports = new List<int>();
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            value: 10, min: 0, max: 100, step: 5,
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await InvokeBaseAsync<int>(renderer, i => i.JsOnKeyDown("ArrowUp")); // Step=5 透传 → 15
        await InvokeBaseAsync<int>(renderer, i => i.JsOnKeyDown("Home"));    // 锚 Min → 0
        await InvokeBaseAsync<int>(renderer, i => i.JsOnKeyDown("End"));     // 锚 Max → 100
        await SettleAsync(renderer);

        Assert.Equal([15, 0, 100], reports);
    }

    [Fact]
    public async Task 增减按钮应渲染spinbutton钮面且点击沿经封装层上报()
    {
        var reports = new List<int>();
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            value: 2, max: 2, showButtons: true,
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Increase value\"", markup); // Localizer 词条缺省
        Assert.Contains("aria-label=\"Decrease value\"", markup);
        Assert.Contains("disabled aria-label=\"Increase value\"", markup); // 已在上限：加号禁用

        // onclick 出现序：0 加号、1 减号。加号在上限处吞步进，零上报。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await SettleAsync(renderer);
        Assert.Empty(reports);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1); // 减号 2 → 1
        await SettleAsync(renderer);
        Assert.Equal([1], reports);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随数量修改消长并经input的ariaInvalid显形()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        Expression<Func<int>> valueExpression = () => doc.Quantity;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldNumericInput<int>>(0);
                builder.AddAttribute(1, nameof(FormFieldNumericInput<int>.Value), doc.Quantity);
                builder.AddAttribute(2, nameof(FormFieldNumericInput<int>.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldNumericInput<int>.ValueChanged),
                    EventCallback.Factory.Create<int>(doc, value => doc.Quantity = value));
                builder.AddAttribute(4, nameof(FormFieldNumericInput<int>.Label), (string?)"订购数量");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始 0 非 42：广播校验后错误经 FieldError 呈现；本家族有 AriaInvalid 通道
        // describedby 与 aria-invalid 双双落 input（与 DateInput 家族不落控件的 quirk 不同）。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("数量必须为 42", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", InputTag(markup));
        Assert.Contains("aria-invalid=\"true\"", InputTag(markup));

        // 键入合法数量 → 外层三步沿 + 底座自身校验通知 → 校验消解。
        await InvokeBaseAsync<int>(renderer, i => i.JsOnInput("42"));
        await SettleAsync(renderer);

        Assert.Equal(42, doc.Quantity);
        markup = renderer.Markup();
        Assert.DoesNotContain("数量必须为 42", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }

    // ------------------------------------------------------------------
    // 错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应切换describedby并落input的ariaInvalid()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            label: "订购数量", helperText: "1 到 100 之间",
            errorText: "服务端驳回：库存不足"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：库存不足", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素。
        Assert.DoesNotContain("1 到 100 之间", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        Assert.Contains("aria-invalid=\"true\"", InputTag(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用必填与Name应落input且联动按钮禁用()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            value: 10, disabled: true, required: true, name: "order.qty", showButtons: true));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var input = InputTag(markup);
        Assert.Contains(" disabled", input);
        Assert.Contains(" required", input);
        Assert.Contains("name=\"order.qty\"", input);
        // 形态：禁用联动两枚步进按钮（Disabled || IsAt*）；Numeric 容器不吃
        // opacity-50 叠类（与 Currency 家族不同），且本家族无 ReadOnly 通道。
        Assert.Equal(2, Regex.Count(markup, "<button[^>]* disabled"));
        Assert.DoesNotContain("atom:opacity-50", BaseRoot(markup));
        Assert.Contains("atom:disabled:cursor-not-allowed", input);

        // 显式 false 复位：不传时属性整体缺席。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, FieldParams(
            value: 10, disabled: false, required: false, showButtons: true)));
        await SettleAsync(renderer);
        input = InputTag(renderer.Markup());
        Assert.DoesNotContain("required", input);
    }

    [Fact]
    public async Task Format与DecimalPlaces应控显示面且Format优先()
    {
        var renderer = CreateRenderer();

        var dict = new Dictionary<string, object?>
        {
            [nameof(FormFieldNumericInput<decimal>.Value)] = 42.5m,
            [nameof(FormFieldNumericInput<decimal>.DecimalPlaces)] = 2,
        };
        await renderer.MountAsync<FormFieldNumericInput<decimal>>(dict);
        await SettleAsync(renderer);

        Assert.Contains(@"value=""42.50""", renderer.Markup()); // F2 沿

        // Format 非空优先于 DecimalPlaces（不变文化 {0:0.000}）。
        dict[nameof(FormFieldNumericInput<decimal>.Format)] = "0.000";
        dict[nameof(FormFieldNumericInput<decimal>.DecimalPlaces)] = null;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, dict));
        await SettleAsync(renderer);
        Assert.Contains(@"value=""42.500""", renderer.Markup());
    }

    [Fact]
    public async Task 无障碍名称与输入类应落input且透传落底座根容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldNumericInput<int>>(FieldParams(
            ariaLabel: "订购数量", inputClass: "atom:custom-num",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-numeric" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // 落点：AriaLabel 直落 input；InputClass 经 Class 参数进 CssClass 落 input
        // splat 经 @attributes 落底座根容器 div（Field 根仅收 Class
        // L33-34——底座根无 role="group"，以容器类指纹定位）。
        Assert.Contains("aria-label=\"订购数量\"", InputTag(markup));
        Assert.Contains("atom:custom-num", InputTag(markup));
        Assert.Contains("data-testid=\"field-numeric\"", BaseRoot(markup));
        var fieldRoot = Regex.Match(markup, """<div[^>]*role="group"[^>]*>""").Value;
        Assert.DoesNotContain("data-testid", fieldRoot);
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、AllowNegative=true、
        // ShowButtons=false、DisableDebounce=false、DebounceInterval=500、EnableWheelStep=false、
        // 开关面全 false、引用面全 null（Value=default(TValue)=0）。
        var field = await renderer.MountAsync<FormFieldNumericInput<int>>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Equal(0, field.Value);
        Assert.Null(field.ValueExpression);
        Assert.Null(field.Name);
        Assert.Null(field.Min);
        Assert.Null(field.Max);
        Assert.Null(field.Step);
        Assert.Null(field.DecimalPlaces);
        Assert.True(field.AllowNegative);
        Assert.False(field.ShowButtons);
        Assert.Null(field.Placeholder);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Null(field.Format);
        Assert.False(field.DisableDebounce);
        Assert.Equal(500, field.DebounceInterval);
        Assert.False(field.EnableWheelStep);
        Assert.Null(field.InputClass);

        var markup = renderer.Markup();
        Assert.DoesNotContain("role=\"spinbutton\"", markup); // ShowButtons 缺省 false
        Assert.DoesNotContain("Increase value", markup);      // 无步进钮面
        Assert.Contains(@"value=""0""", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }
}
