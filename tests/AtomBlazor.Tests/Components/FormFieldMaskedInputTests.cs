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
/// FormFieldMaskedInput 组合字段测试：在可选 EditContext 宿主下挂载
/// FormFieldMaskedInput（根 0，底座无弹层，无需 PortalHost），验证标签 for 实通输入
/// （Id=ControlId 直落底座 input）、aria-describedby 经正式参数通道落 input、键入经
/// oninput 派发清洗后按封装层三步沿回报未掩码值、掩码逐位拒非法字符（"999" 吃 "a1b"
/// 只留 "1"，再键纯字母整体清空）、预设切换 inputmode/占位并预格式化原始值、EditContext
/// 校验消息随键入消长并经 input 的 aria-invalid 显形（本家族与 Currency/Numeric 同为
/// 三件套透传档）、ErrorText 手动覆写切换 describedby、Disabled/Required/Name 落 input
/// （封装层与底座 MaskedInput 皆无 ReadOnly 通道，该侧不适用）、AriaLabel/InputClass/
/// splat 三族齐落同一 input 元素（落点形态与 Currency/Numeric 的"splat 独落容器"不同
/// 系底座根即裸 input 所致，据实断言）、缺省参数面。headless 键入沿照 MaskedInputTests
/// 先例经 renderer.DispatchAsync 派发 oninput/onfocus；焦点/失焦纯 JS 回写沿不测。
/// 快照含 formfield GUID 待审，先经正则归一化。
/// </summary>
public class FormFieldMaskedInputTests
{
    /// <summary>归一化 ControlId 随机 GUID（input id、label for 与 describedby 通道），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>");

    /// <summary>构造带基元服务的渲染器（底座 OnAfterRenderAsync 需 IJSRuntime；沿用 MaskedInputTests 先例）。</summary>
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

    /// <summary>底座键入沿：在 Dispatcher 上向渲染树派发 DOM 事件（MaskedInputTests 先例）。</summary>
    private static Task DispatchAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>校验沿测试模型：Phone 非 8005551212 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public string Phone { get; set; } = string.Empty;
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Phone 不为 8005551212 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Phone));

        void Validate()
        {
            messages.Clear();
            if (doc.Phone != "8005551212")
            {
                messages.Add(field, "号码必须为 8005551212");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        string? value = null,
        EventCallback<string> valueChanged = default,
        Expression<Func<string>>? valueExpression = null,
        string? name = null,
        MaskPreset? preset = null,
        string? mask = null,
        char? placeholderChar = null,
        bool? showMask = null,
        string? placeholder = null,
        bool? disabled = null,
        bool? required = null,
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
            parameters[nameof(FormFieldMaskedInput.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldMaskedInput.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldMaskedInput.ValueExpression)] = valueExpression;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldMaskedInput.Name)] = name;
        }
        if (preset is not null)
        {
            parameters[nameof(FormFieldMaskedInput.Preset)] = preset.Value;
        }
        if (mask is not null)
        {
            parameters[nameof(FormFieldMaskedInput.Mask)] = mask;
        }
        if (placeholderChar is not null)
        {
            parameters[nameof(FormFieldMaskedInput.PlaceholderChar)] = placeholderChar.Value;
        }
        if (showMask is not null)
        {
            parameters[nameof(FormFieldMaskedInput.ShowMask)] = showMask.Value;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldMaskedInput.Placeholder)] = placeholder;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldMaskedInput.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldMaskedInput.Required)] = required.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldMaskedInput.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldMaskedInput.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldMaskedInput.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldMaskedInput.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldMaskedInput.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldMaskedInput.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>取底座 input 开标签（本家族全树仅掩码输入一枚 input）。</summary>
    private static string InputTag(string markup) =>
        Regex.Match(markup, "<input[^>]*>").Value;

    /// <summary>取 Field 根容器开标签（role="group" 指纹；splat 不应落此）。</summary>
    private static string FieldRoot(string markup) =>
        Regex.Match(markup, """<div[^>]*role="group"[^>]*>""").Value;

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldMaskedInput>(FieldParams(
            label: "电话号码", helperText: "美国号码",
            value: "8005551212", preset: MaskPreset.Phone, name: "profile.phone"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应实通输入且describedby经正式参数落input()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldMaskedInput>(FieldParams(
            label: "电话号码", helperText: "美国号码"));
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
        var bare = FieldParams(label: "电话号码");
        bare[nameof(FormFieldMaskedInput.HelperText)] = null;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, bare));
        await SettleAsync(renderer);
        Assert.DoesNotContain("aria-describedby", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 键入沿与掩码清洗
    // ------------------------------------------------------------------

    [Fact]
    public async Task 键入应经封装层三步沿回报未掩码值()
    {
        var reports = new List<string>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldMaskedInput>(FieldParams(
            value: "", preset: MaskPreset.Phone,
            valueChanged: EventCallback.Factory.Create<string>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 底座 HandleInput 剥字面量与占位后逐位校验，满 4 位合法数字即上报。
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "(800) 5" });
        await SettleAsync(renderer);

        Assert.Equal(["8005"], reports);
        Assert.Equal("8005", field.Value); // HandleValueChanged 赋值步沿
        Assert.Contains(@"value=""(800) 5__-____""", renderer.Markup());
    }

    [Fact]
    public async Task 掩码应逐位拒非法字符且纯字母键入整体清空()
    {
        var reports = new List<string>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldMaskedInput>(FieldParams(
            value: "", mask: "999",
            valueChanged: EventCallback.Factory.Create<string>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // "999" 数字位：'a' 被位 0 拒、'1' 落位 0、'b' 被位 1 拒 → 清洗为 "1"。
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "a1b" });
        await SettleAsync(renderer);
        Assert.Equal(["1"], reports);
        Assert.Equal("1", field.Value);
        Assert.Contains(@"value=""1__""", renderer.Markup());

        // 再键纯字母：清洗后空串与现值 "1" 不等 → 回报 ""（清空沿）。
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "abc" });
        await SettleAsync(renderer);
        Assert.Equal(["1", ""], reports);
        Assert.Equal("", field.Value);
        Assert.Contains(@"value=""___""", renderer.Markup());
    }

    [Fact]
    public async Task 预设应切换输入方式占位并预格式化原始值()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldMaskedInput>(FieldParams(
            value: "8005551212", preset: MaskPreset.Phone));
        await SettleAsync(renderer);

        var input = InputTag(renderer.Markup());
        // Phone 预设目录：inputmode=tel、占位为掩码模板、未掩码值入树即预格式化。
        Assert.Contains(@"inputmode=""tel""", input);
        Assert.Contains(@"placeholder=""(___) ___-____""", input);
        Assert.Contains(@"value=""(800) 555-1212""", input);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随键入消长并经input的ariaInvalid显形()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        Expression<Func<string>> valueExpression = () => doc.Phone;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldMaskedInput>(0);
                builder.AddAttribute(1, nameof(FormFieldMaskedInput.Value), doc.Phone);
                builder.AddAttribute(2, nameof(FormFieldMaskedInput.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldMaskedInput.ValueChanged),
                    EventCallback.Factory.Create<string>(doc, value => doc.Phone = value));
                builder.AddAttribute(4, nameof(FormFieldMaskedInput.Label), (string?)"电话号码");
                builder.AddAttribute(5, nameof(FormFieldMaskedInput.Preset), MaskPreset.Phone);
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始空号非 8005551212：广播校验后错误经 FieldError 呈现；本家族三件套齐备
        // describedby 与 aria-invalid 双双落 input（与 Currency/Numeric 同档）。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("号码必须为 8005551212", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", InputTag(markup));
        Assert.Contains("aria-invalid=\"true\"", InputTag(markup));

        // 键入满位号码 → 底座清洗回报 + 外层三步沿 → 校验消解。
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "(800) 555-1212" });
        await SettleAsync(renderer);

        Assert.Equal("8005551212", doc.Phone);
        markup = renderer.Markup();
        Assert.DoesNotContain("号码必须为 8005551212", markup);
        Assert.DoesNotContain("aria-invalid", markup);
        Assert.Contains(@"value=""(800) 555-1212""", markup);
    }

    // ------------------------------------------------------------------
    // 错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应切换describedby并落input的ariaInvalid()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldMaskedInput>(FieldParams(
            label: "电话号码", helperText: "美国号码",
            errorText: "服务端驳回：号码格式错误"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：号码格式错误", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素。
        Assert.DoesNotContain("美国号码", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        Assert.Contains("aria-invalid=\"true\"", InputTag(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用必填与Name应落input元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldMaskedInput>(FieldParams(
            value: "8005551212", preset: MaskPreset.Phone,
            disabled: true, required: true, name: "profile.phone"));
        await SettleAsync(renderer);

        var input = InputTag(renderer.Markup());
        Assert.Contains(" disabled", input);
        Assert.Contains(" required", input);
        Assert.Contains("name=\"profile.phone\"", input);
        // 底座 MaskedInput 的禁用态类恒挂在 CssClass（Tailwind 变体类，浏览器态驱动）
 // 封装层与底座皆无 ReadOnly 参数——本家族 ReadOnly 侧不适用（同档登记）。
        Assert.Contains("atom:disabled:cursor-not-allowed", input);
        Assert.Contains("atom:disabled:opacity-50", input);

        // 缺省面：不传时属性整体缺席。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, FieldParams(
            value: "8005551212", preset: MaskPreset.Phone,
            disabled: false, required: false, name: "profile.phone")));
        await SettleAsync(renderer);
        input = InputTag(renderer.Markup());
        Assert.DoesNotContain(" disabled", input);
        Assert.DoesNotContain(" required", input);
    }

    [Fact]
    public async Task 无障碍名称加InputClass与splat应齐落同一input元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldMaskedInput>(FieldParams(
            ariaLabel: "电话号码（美国）", inputClass: "atom:custom-mask",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-masked" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // 落点实证：底座 MaskedInput 根即裸 input——AriaLabel、InputClass（经底座 Class
        // 并入 CssClass）与 splat（经底座 @attributes）三者齐落同一 input 元素
        // 与 Currency/Numeric 的"splat 独落容器 div、InputClass 落 input"形态不同。
        var input = InputTag(markup);
        Assert.Contains("aria-label=\"电话号码（美国）\"", input);
        Assert.Contains("atom:custom-mask", input);
        Assert.Contains("data-testid=\"field-masked\"", input);
        // Field 根仅收 Class，不吃 splat。
        Assert.DoesNotContain("data-testid", FieldRoot(markup));
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();

        // 缺省：Value=string.Empty、Preset=Custom、PlaceholderChar='_'、ShowMask=true、
        // Disabled/Required=false、其余引用面全 null；FormFieldBase 侧 Orientation=Vertical。
        var field = await renderer.MountAsync<FormFieldMaskedInput>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Equal(string.Empty, field.Value);
        Assert.Null(field.ValueExpression);
        Assert.Null(field.Name);
        Assert.Equal(MaskPreset.Custom, field.Preset);
        Assert.Null(field.Mask);
        Assert.Equal('_', field.PlaceholderChar);
        Assert.True(field.ShowMask);
        Assert.Null(field.Placeholder);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Null(field.InputClass);

        var markup = renderer.Markup();
        var input = InputTag(markup);
        Assert.Contains(@"inputmode=""text""", input); // Custom 预设目录输入方式
        Assert.DoesNotContain("aria-describedby", markup);
        Assert.DoesNotContain("aria-invalid", markup);
        Assert.DoesNotContain("data-slot=\"field-label\"", markup);
    }
}
