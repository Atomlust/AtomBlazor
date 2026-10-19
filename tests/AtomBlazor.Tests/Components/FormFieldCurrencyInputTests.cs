using AtomBlazor.Primitives;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FormFieldCurrencyInput 组合字段测试：在可选 EditContext 宿主下挂载
/// FormFieldCurrencyInput（根 0，底座无弹层，无需 PortalHost），验证标签 for 实通输入
/// （Id=ControlId 直落底座 input）、aria-describedby 经正式参数通道落 input、失焦键入
/// 经封装层三步沿回填上报、Max 钳制与 AllowNegative=false 拒负、币种代码控符号位与文化
/// 呈现（EUR 逗号小数、SEK 后置符号）、方向键按币种最小单位步进且随 Max 钉住、EditContext
/// 校验消息随金额修改消长并经 input 的 aria-invalid 显形（本家族有 AriaInvalid 通道
/// 与 DateInput 家族 quirk 不同）、ErrorText 手动覆写切换 describedby、Disabled/Required/Name
/// 落 input 并叠容器置位类（三家族封装层均无 ReadOnly 通道，底座亦无该参数）、
/// AriaLabel 压币种英文名缺省、InputClass+splat 落底座根容器（非 Field 根）与缺省参数面。
/// 底座键入口沿用 CurrencyInputTests headless 先例：直接调用 [JSInvokable] 面
/// （JsOnBlur/JsOnKeyDown），不测纯焦点沿。快照含 formfield GUID 与 3 字节伪 verified 待审
/// 先经正则归一化。
/// </summary>
public class FormFieldCurrencyInputTests
{
    /// <summary>归一化 ControlId 随机 GUID（input id、label for 与 describedby 通道），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>");

    /// <summary>构造带基元服务的渲染器（底座 OnAfterRenderAsync 需 IJSRuntime；沿用 CurrencyInputTests 先例）。</summary>
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

    /// <summary>底座 JS 回调沿：直接在 Dispatcher 上调用 [JSInvokable] 入口并等待落定（CurrencyInputTests 先例）。</summary>
    private static Task InvokeBaseAsync(
        ComponentTestRenderer renderer, Func<CurrencyInput, Task> callback) =>
        renderer.Dispatcher.InvokeAsync(async () => await callback(renderer.FindComponent<CurrencyInput>()));

    /// <summary>校验沿测试模型：Amount 非 1234.50 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Amount 不为 1234.50 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Amount));

        void Validate()
        {
            messages.Clear();
            if (doc.Amount != 1234.50m)
            {
                messages.Add(field, "金额必须为 1234.50");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        decimal? value = null,
        EventCallback<decimal> valueChanged = default,
        Expression<Func<decimal>>? valueExpression = null,
        string? name = null,
        string? currencyCode = null,
        bool? showSymbol = null,
        decimal? min = null,
        decimal? max = null,
        bool? allowNegative = null,
        bool? useThousandSeparator = null,
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
            parameters[nameof(FormFieldCurrencyInput.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldCurrencyInput.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.ValueExpression)] = valueExpression;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.Name)] = name;
        }
        if (currencyCode is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.CurrencyCode)] = currencyCode;
        }
        if (showSymbol is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.ShowSymbol)] = showSymbol.Value;
        }
        if (min is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.Min)] = min;
        }
        if (max is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.Max)] = max;
        }
        if (allowNegative is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.AllowNegative)] = allowNegative.Value;
        }
        if (useThousandSeparator is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.UseThousandSeparator)] = useThousandSeparator.Value;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.Placeholder)] = placeholder;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.Required)] = required.Value;
        }
        if (disableDebounce is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.DisableDebounce)] = disableDebounce.Value;
        }
        if (debounceInterval is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.DebounceInterval)] = debounceInterval.Value;
        }
        if (enableWheelStep is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.EnableWheelStep)] = enableWheelStep.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldCurrencyInput.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>取底座 CurrencyInput 根容器开标签（其容器类带 overflow-hidden 指纹；Field 根仅收 Class）。</summary>
    private static string BaseRoot(string markup) =>
        Regex.Match(markup, """<div[^>]*atom:overflow-hidden[^>]*>""").Value;

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

        await renderer.MountAsync<FormFieldCurrencyInput>(FieldParams(
            label: "报销金额", helperText: "美元含税",
            value: 1234.5m, name: "order.amount"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应实通输入且describedby经正式参数落input()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCurrencyInput>(FieldParams(
            label: "报销金额", helperText: "美元含税"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // FieldLabel For=ControlId 实通：底座 Id="@ControlId" 直落 input id。
        var id = Regex.Match(markup, """<input[^>]*id="([^"]*)"[^>]*>""").Groups[1].Value;
        Assert.Matches(@"^formfield-[0-9a-f]{32}$", id);
        Assert.Contains($"for=\"{id}\"", markup);
        // 帮助态 describedby 指向描述元素；无帮助无错误时该属性整体缺席。
        Assert.Contains($"aria-describedby=\"{id}-description\"", InputTag(markup));
        Assert.Contains($"id=\"{id}-description\"", markup);

        // 显式 null 复位 HelperText：参数视图中未出现的参数保持组件当前值。
        var bare = FieldParams(label: "报销金额");
        bare[nameof(FormFieldCurrencyInput.HelperText)] = null;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, bare));
        await SettleAsync(renderer);
        Assert.DoesNotContain("aria-describedby", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 值回填沿与边界
    // ------------------------------------------------------------------

    [Fact]
    public async Task 失焦键入金额应经三步沿回填上报并格式化呈现()
    {
        var reports = new List<decimal>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldCurrencyInput>(FieldParams(
            value: 0m,
            valueChanged: EventCallback.Factory.Create<decimal>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 失焦沿总是上报一次（底座 JsOnBlur 语义），符号与千分位在解析前剥除。
        await InvokeBaseAsync(renderer, i => i.JsOnBlur("$1,234.56"));
        await SettleAsync(renderer);

        Assert.Equal([1234.56m], reports);
        Assert.Equal(1234.56m, field.Value); // HandleValueChanged 赋值步沿
        Assert.Contains(@"value=""1,234.56""", renderer.Markup());
    }

    [Fact]
    public async Task 越界金额应钳到上限且禁负时负值应被拒绝()
    {
        var reports = new List<decimal>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldCurrencyInput>(FieldParams(
            value: 5m, max: 10m, allowNegative: false,
            valueChanged: EventCallback.Factory.Create<decimal>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await InvokeBaseAsync(renderer, i => i.JsOnBlur("500"));
        await SettleAsync(renderer);
        Assert.Equal([10m], reports); // Max 透传生效：钳到上限
        Assert.Equal(10m, field.Value);

        reports.Clear();
        await InvokeBaseAsync(renderer, i => i.JsOnBlur("-3"));
        await SettleAsync(renderer);
        Assert.Empty(reports); // AllowNegative=false：解析被拒，零回调值不动
        Assert.Equal(10m, field.Value);
    }

    [Fact]
    public async Task 方向键应按币种最小单位步进并随上限钉住()
    {
        var reports = new List<decimal>();
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCurrencyInput>(FieldParams(
            value: 1m, max: 1.02m,
            valueChanged: EventCallback.Factory.Create<decimal>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await InvokeBaseAsync(renderer, i => i.JsOnKeyDown("ArrowUp"));
        await InvokeBaseAsync(renderer, i => i.JsOnKeyDown("ArrowUp"));
        await SettleAsync(renderer);
        Assert.Equal([1.01m, 1.02m], reports); // 美元 2 位小数的最小单位步长

        // 已到上限：钳后与当前值等，SetValue 变化门控吞掉事件，零新上报。
        await InvokeBaseAsync(renderer, i => i.JsOnKeyDown("ArrowUp"));
        await SettleAsync(renderer);
        Assert.Equal(2, reports.Count);
        Assert.Contains(@"value=""1.02""", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 币种格式面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 币种代码应控符号位置与文化呈现而关符号撤下span()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCurrencyInput>(FieldParams(
            currencyCode: "EUR", value: 1234.5m));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Amount in Euro\"", markup);
        Assert.Contains(@"value=""1.234,50""", markup); // de-DE：句点千分位、逗号小数

        // SEK 后置符号族：span 落在 input 之后。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, FieldParams(
            currencyCode: "SEK", value: 99m)));
        await SettleAsync(renderer);
        markup = renderer.Markup();
        Assert.True(markup.IndexOf("<span", StringComparison.Ordinal) >
                    markup.IndexOf("<input", StringComparison.Ordinal));
        Assert.Contains(@"value=""99,00""", markup);

        // 关闭符号：span 整体撤下。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, FieldParams(
            currencyCode: "SEK", value: 99m, showSymbol: false)));
        await SettleAsync(renderer);
        Assert.DoesNotContain("<span", renderer.Markup());

        // 关闭千分位：切 F 格式（回 USD 显式复位币种，未列参数保持现值）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, FieldParams(
            currencyCode: "USD", value: 1234.5m, useThousandSeparator: false)));
        await SettleAsync(renderer);
        Assert.Contains(@"value=""1234.50""", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随金额修改消长并经input的ariaInvalid显形()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        Expression<Func<decimal>> valueExpression = () => doc.Amount;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldCurrencyInput>(0);
                builder.AddAttribute(1, nameof(FormFieldCurrencyInput.Value), doc.Amount);
                builder.AddAttribute(2, nameof(FormFieldCurrencyInput.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldCurrencyInput.ValueChanged),
                    EventCallback.Factory.Create<decimal>(doc, value => doc.Amount = value));
                builder.AddAttribute(4, nameof(FormFieldCurrencyInput.Label), (string?)"报销金额");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始 0 非 1234.50：广播校验后错误经 FieldError 呈现；本家族有 AriaInvalid 通道
        // describedby 与 aria-invalid 双双落 input（与 DateInput 家族不落控件的 quirk 不同）。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("金额必须为 1234.50", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", InputTag(markup));
        Assert.Contains("aria-invalid=\"true\"", InputTag(markup));

        // 失焦键入合法金额 → 外层三步沿 + 底座自身校验通知 → 校验消解。
        await InvokeBaseAsync(renderer, i => i.JsOnBlur("1234.50"));
        await SettleAsync(renderer);

        Assert.Equal(1234.50m, doc.Amount);
        markup = renderer.Markup();
        Assert.DoesNotContain("金额必须为 1234.50", markup);
        Assert.DoesNotContain("aria-invalid", markup);
        Assert.Contains(@"value=""1,234.50""", markup);
    }

    // ------------------------------------------------------------------
    // 错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应切换describedby并落input的ariaInvalid()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCurrencyInput>(FieldParams(
            label: "报销金额", helperText: "美元含税",
            errorText: "服务端驳回：金额超限"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：金额超限", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素。
        Assert.DoesNotContain("美元含税", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        Assert.Contains("aria-invalid=\"true\"", InputTag(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用必填与Name应落input并叠容器置位类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCurrencyInput>(FieldParams(
            value: 10m, disabled: true, required: true, name: "order.amount"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var input = InputTag(markup);
        Assert.Contains(" disabled", input);
        Assert.Contains(" required", input);
        Assert.Contains("name=\"order.amount\"", input);
        // 形态：容器 div 叠 atom:opacity-50、input 叠 disabled:cursor-not-allowed
        //（本家族与 Numeric 家族均无 ReadOnly 通道——封装层与底座皆无该参数）。
        Assert.Contains("atom:opacity-50", BaseRoot(markup));
        Assert.Contains("atom:disabled:cursor-not-allowed", input);

        // 必填缺省面：不传时属性整体缺席。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, FieldParams(
            value: 10m, disabled: false, required: false)));
        await SettleAsync(renderer);
        input = InputTag(renderer.Markup());
        Assert.DoesNotContain("required", input);
        Assert.DoesNotContain("atom:opacity-50", BaseRoot(renderer.Markup()));
    }

    [Fact]
    public async Task 无障碍名称应压币种英文名且输入类与透传落底座根容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldCurrencyInput>(FieldParams(
            ariaLabel: "报销金额（美元）", inputClass: "atom:custom-cur",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-currency" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // 沿：AriaLabel 压过底座 $"Amount in {Currency.Name}" 英文缺省。
        Assert.Contains("aria-label=\"报销金额（美元）\"", markup);
        Assert.DoesNotContain("Amount in", markup);

        // 落点：InputClass 经 Class 参数进底座 CssClass 落 input 元素（CurrencyInput
        // 容器类不吃 Class）；splat 经 @attributes 落底座根容器 div（Field 根仅收 Class
        // L30-31——本底座根无 role="group"，以容器类指纹定位）。
        Assert.Contains("atom:custom-cur", InputTag(markup));
        var baseRoot = BaseRoot(markup);
        Assert.Contains("data-testid=\"field-currency\"", baseRoot);
        var fieldRoot = Regex.Match(markup, """<div[^>]*role="group"[^>]*>""").Value;
        Assert.DoesNotContain("data-testid", fieldRoot);
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、CurrencyCode=USD、
        // 三开关全 true（ShowSymbol/AllowNegative/UseThousandSeparator）、DisableDebounce=false、
        // DebounceInterval=500、EnableWheelStep=false、开关面全 false、引用面全 null。
        var field = await renderer.MountAsync<FormFieldCurrencyInput>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Equal(0m, field.Value);
        Assert.Null(field.ValueExpression);
        Assert.Null(field.Name);
        Assert.Equal("USD", field.CurrencyCode);
        Assert.True(field.ShowSymbol);
        Assert.Null(field.Min);
        Assert.Null(field.Max);
        Assert.True(field.AllowNegative);
        Assert.True(field.UseThousandSeparator);
        Assert.Null(field.Placeholder);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.False(field.DisableDebounce);
        Assert.Equal(500, field.DebounceInterval);
        Assert.False(field.EnableWheelStep);
        Assert.Null(field.InputClass);

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Amount in US Dollar\"", markup); // 币种英文名缺省
        Assert.Contains(@"value=""0.00""", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }
}
