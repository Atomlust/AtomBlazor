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
/// FormFieldQuantityStepper 组合字段测试：在可选 EditContext 宿主下挂载
/// FormFieldQuantityStepper（根 0，底座无弹层，无需 PortalHost），验证标签 for 实通数值
/// 输入（Id=ControlId 经底座 NumericInput 落 input）、describedby 落底座 group 容器而非
/// input（input/容器分家形态，与 CurrencyInput 家族落点不同）、加减点击经封装层三步沿
/// 回填上报、Step 增减与 Max/Min 钳制且到界按钮 disabled 钉住、Min 处带 OnRemove 时减号
/// 转移除钮（Remove item 标签）并只回调移除、ReadOnly 通道实证（本家族封装层与底座
/// 均有该参数：切原生 readonly input、按钮全禁用）、EditContext 校验消息随数量修改消长、
/// ErrorText 手动覆写切容器 describedby、Disabled 落按钮与数值 input、AriaLabel 压
/// "Quantity" 本地化缺省且 Name 落 input、InputClass+splat 落底座 group 容器（非 Field 根）
/// 与缺省参数面。headless 口径沿用 QuantityStepperTests：onclick occurrence 0=减号、1=加号。
/// 快照含 formfield GUID，先经正则归一化。
/// </summary>
public class FormFieldQuantityStepperTests
{
    /// <summary>归一化 ControlId 随机 GUID（input id、label for 与 describedby 通道），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>");

    /// <summary>构造带基元服务的渲染器（底座 NumericInput 需 IJSRuntime；沿用 CurrencyInput 先例服务面）。</summary>
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

    /// <summary>触发一次 DOM 事件（须在 Dispatcher 上派发，沿用 QuantityStepperTests 先例）。</summary>
    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () => await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>校验沿测试模型：Qty 非 3 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public int Qty { get; set; } = 1;
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Qty 不为 3 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Qty));

        void Validate()
        {
            messages.Clear();
            if (doc.Qty != 3)
            {
                messages.Add(field, "数量必须为 3");
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
        int? min = null,
        int? max = null,
        int? step = null,
        bool? readOnly = null,
        bool? disabled = null,
        EventCallback onRemove = default,
        string? name = null,
        string? inputClass = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (value is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.Value)] = value.Value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldQuantityStepper.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.ValueExpression)] = valueExpression;
        }
        if (min is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.Min)] = min.Value;
        }
        if (max is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.Max)] = max.Value;
        }
        if (step is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.Step)] = step.Value;
        }
        if (readOnly is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.ReadOnly)] = readOnly.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.Disabled)] = disabled.Value;
        }
        if (onRemove.HasDelegate)
        {
            parameters[nameof(FormFieldQuantityStepper.OnRemove)] = onRemove;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.Name)] = name;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldQuantityStepper.AriaLabel)] = ariaLabel;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>取底座 group 容器开标签（atom:inline-flex 指纹区分 Field 根的 role="group" div）。</summary>
    private static string BaseGroup(string markup) =>
        Regex.Match(markup, """<div[^>]*atom:inline-flex[^>]*>""").Value;

    /// <summary>取数值侧 input 开标签（编辑态 NumericInput 与只读态原生 input 共用锚点）。</summary>
    private static string InputTag(string markup) =>
        Regex.Match(markup, "<input[^>]*>").Value;

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldQuantityStepper>(FieldParams(
            label: "购买数量", helperText: "每单最多十件", value: 2, min: 1, max: 10));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应实通数值输入且describedby落底座group容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldQuantityStepper>(FieldParams(
            label: "购买数量", helperText: "每单最多十件"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // FieldLabel For=ControlId 实通：底座 Id="@ControlId" 经 NumericInput 落 input id。
        var id = Regex.Match(markup, """<input[^>]*id="([^"]*)"[^>]*>""").Groups[1].Value;
        Assert.Matches(@"^formfield-[0-9a-f]{32}$", id);
        Assert.Contains($"for=\"{id}\"", markup);
        // 落点分家实证：describedby 经底座 AriaDescribedBy 落 group 容器，input 侧不吃。
        Assert.Contains($"aria-describedby=\"{id}-description\"", BaseGroup(markup));
        Assert.DoesNotContain("aria-describedby", InputTag(markup));
        Assert.Contains($"id=\"{id}-description\"", markup);

        // 显式 null 复位 HelperText：describedby 属性整体缺席。
        var bare = FieldParams(label: "购买数量");
        bare[nameof(FormFieldQuantityStepper.HelperText)] = null;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, bare));
        await SettleAsync(renderer);
        Assert.DoesNotContain("aria-describedby", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 步进沿与钳制
    // ------------------------------------------------------------------

    [Fact]
    public async Task 点击加减应经封装层三步沿回填并上报()
    {
        var reports = new List<int>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldQuantityStepper>(FieldParams(
            value: 5, min: 0,
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 先例序位：occurrence 0=减号、1=加号。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);
        Assert.Equal([6], reports);
        Assert.Equal(6, field.Value); // HandleValueChanged 赋值步沿
        Assert.Contains("value=\"6\"", renderer.Markup());

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await SettleAsync(renderer);
        Assert.Equal([6, 5], reports);
        Assert.Contains("value=\"5\"", renderer.Markup());
    }

    [Fact]
    public async Task 步长应透传增减并随上下限钳制钉住按钮()
    {
        var reports = new List<int>();
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldQuantityStepper>(FieldParams(
            value: 2, min: 1, max: 3, step: 2,
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 加号：2+2=4 越上限，钳到 3 上报。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);
        Assert.Equal([3], reports);

        // 已到上限：加号按钮 disabled 钉住，再点零新上报（钳后等值也被变化门控吞下）。
        Assert.Contains(@"aria-disabled=""true"" aria-label=""Increase quantity""", renderer.Markup());
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);
        Assert.Single(reports); // 仍只有首次钳制上报，钉住后零新沿

        // 减号：3-2=1 恰至下限上报；再减钳回 1 等值零上报，按钮转 disabled。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await SettleAsync(renderer);
        Assert.Equal([3, 1], reports);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await SettleAsync(renderer);
        Assert.Equal(2, reports.Count);
        Assert.Contains("value=\"1\"", InputTag(renderer.Markup()));
        Assert.Contains(@"aria-disabled=""true"" aria-label=""Decrease quantity""", renderer.Markup());
    }

    [Fact]
    public async Task 下限处带移除回调时减号应转移除钮并只回调移除()
    {
        var removeCount = 0;
        var reports = new List<int>();
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldQuantityStepper>(FieldParams(
            value: 1, min: 1,
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v)),
            onRemove: EventCallback.Factory.Create(new object(), () => removeCount++)));
        await SettleAsync(renderer);

        // CanRemove 形态：减号钮换 trash-2 图标与 Remove item 标签。
        var markup = renderer.Markup();
        Assert.Contains("Remove item", markup);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await SettleAsync(renderer);
        Assert.Equal(1, removeCount);
        Assert.Empty(reports); // 不步进、不上报值
        Assert.Contains("value=\"1\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // ReadOnly 通道实证
    // ------------------------------------------------------------------

    [Fact]
    public async Task 只读态应切原生readonly输入且按钮全禁用()
    {
        var reports = new List<int>();
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldQuantityStepper>(FieldParams(
            value: 3, min: 1, max: 9, readOnly: true, name: "order.qty",
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 通道实证：本家族封装层与底座均有 ReadOnly 参数（Switch/Cascader 家族则无）。
        var input = InputTag(renderer.Markup());
        Assert.Contains("readonly", input);
        Assert.Contains("name=\"order.qty\"", input);
        Assert.Contains("value=\"3\"", input);
        // 只读形态是原生 input，不是 NumericInput：spinbutton 角色整体缺席。
        Assert.DoesNotContain("spinbutton", renderer.Markup());

        // 两个按钮都吃 ReadOnly 禁用沿。
        Assert.Contains(@"aria-disabled=""true"" aria-label=""Decrease quantity""", renderer.Markup());
        Assert.Contains(@"aria-disabled=""true"" aria-label=""Increase quantity""", renderer.Markup());
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);
        Assert.Empty(reports);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随数量修改消长并切换容器describedby()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        Expression<Func<int>> valueExpression = () => doc.Qty;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldQuantityStepper>(0);
                builder.AddAttribute(1, nameof(FormFieldQuantityStepper.Value), doc.Qty);
                builder.AddAttribute(2, nameof(FormFieldQuantityStepper.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldQuantityStepper.ValueChanged),
                    EventCallback.Factory.Create<int>(doc, value => doc.Qty = value));
                builder.AddAttribute(4, nameof(FormFieldQuantityStepper.Label), (string?)"购买数量");
                builder.AddAttribute(5, nameof(FormFieldQuantityStepper.Min), 1);
                builder.AddAttribute(6, nameof(FormFieldQuantityStepper.Max), 9);
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始 Qty=1 非法：广播校验后错误经 FieldError 呈现；封装层无 AriaInvalid 通道
        //（即无该参数），describedby 经 DescribedById 切错误元素并落底座 group 容器。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("数量必须为 3", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", BaseGroup(markup));

        // 加号两次 1→2→3 → 封装层三步沿 + 底座自身字段通知 → 校验消解。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);

        Assert.Equal(3, doc.Qty);
        markup = renderer.Markup();
        Assert.DoesNotContain("数量必须为 3", markup);
        Assert.DoesNotContain("data-invalid", markup); // Field 根 IsInvalid 回落即整体撤下
    }

    // ------------------------------------------------------------------
    // 错误覆写与禁用
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应切换容器describedby并隐藏帮助()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldQuantityStepper>(FieldParams(
            label: "购买数量", helperText: "每单最多十件",
            errorText: "服务端驳回：超库存上限"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：超库存上限", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.DoesNotContain("每单最多十件", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
    }

    [Fact]
    public async Task 禁用态应落按钮与数值输入并拒绝步进()
    {
        var reports = new List<int>();
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldQuantityStepper>(FieldParams(
            value: 4, min: 1, disabled: true,
            valueChanged: EventCallback.Factory.Create<int>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        Assert.Contains(@"aria-disabled=""true"" aria-label=""Decrease quantity""", renderer.Markup());
        Assert.Contains(@"aria-disabled=""true"" aria-label=""Increase quantity""", renderer.Markup());
        Assert.Matches(@"<input[^>]*\sdisabled(\s|>)", renderer.Markup());

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);
        Assert.Empty(reports);
    }

    // ------------------------------------------------------------------
    // 无障碍与落点
    // ------------------------------------------------------------------

    [Fact]
    public async Task 无障碍名称应压本地化缺省且输入类与透传落底座group容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldQuantityStepper>(FieldParams(
            ariaLabel: "购买数量选择器", inputClass: "atom:custom-qs",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-qs" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var group = BaseGroup(markup);
        // 沿：AriaLabel 压过底座 Localizer["QuantityStepper.Label"]="Quantity" 缺省。
        Assert.Contains("aria-label=\"购买数量选择器\"", group);
        Assert.DoesNotContain("Quantity\"", markup);
        Assert.Contains("atom:custom-qs", group);
        Assert.Contains("data-testid=\"field-qs\"", group);
        // 编辑态数值侧同吃 EffectiveLabel；Field 根不吃 splat。
        Assert.Contains("aria-label=\"购买数量选择器\"", InputTag(markup));
        var fieldRoot = Regex.Match(markup, """<div[^>]*data-slot="field"[^>]*>""").Value;
        Assert.DoesNotContain("data-testid", fieldRoot);
    }

    // ------------------------------------------------------------------
    // 缺省参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();

        // 缺省：Value=0、Min=0、Max=null、Step=1、ReadOnly/Disabled=false、
        // OnRemove 无委托、Name/InputClass 等引用面全 null。
        var field = await renderer.MountAsync<FormFieldQuantityStepper>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Equal(0, field.Value);
        Assert.Equal(0, field.Min);
        Assert.Null(field.Max);
        Assert.Equal(1, field.Step);
        Assert.False(field.ReadOnly);
        Assert.False(field.Disabled);
        Assert.False(field.OnRemove.HasDelegate);
        Assert.False(field.ValueChanged.HasDelegate);
        Assert.Null(field.ValueExpression);
        Assert.Null(field.Name);
        Assert.Null(field.InputClass);

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Quantity\"", BaseGroup(markup)); // 本地化缺省名
        Assert.Contains("value=\"0\"", markup);
        // Value 0 <= Min 0 且无 OnRemove：减号 disabled 钉住；加号可用（Max=null 无上限）。
        Assert.Contains(@"aria-disabled=""true"" aria-label=""Decrease quantity""", markup);
        Assert.Contains(@"aria-disabled=""false"" aria-label=""Increase quantity""", markup);
    }
}
