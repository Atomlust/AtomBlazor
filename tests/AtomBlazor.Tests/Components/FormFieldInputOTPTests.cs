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
/// FormFieldInputOTP 组合字段测试：在可选 EditContext 宿主下挂载
/// FormFieldInputOTP（根 0，底座无弹层，无需 PortalHost），验证标签独立呈现
/// quirk：FieldLabel 不带 For，底座亦无 Id/AriaDescribedBy 正式参数通道，describedby
/// 与 for 全树不落控件）、数位键入经封装层三步沿逐位回报且满位触发 OnComplete 一次、
/// JS 粘贴沿 OnPasteJs 过滤非法字符并填满即完成（InputOTPTests 直调先例）、非法字符
/// 被字符模式拒收零上报、MaskInput 星号遮蔽已填数位、Name 落隐藏回传项且数位无 name、
/// 分隔符随组宽摆放可关可换自定义内容、EditContext 校验消息随粘贴消长且仅在 Field 根
/// data-invalid 显形（控件级 aria-invalid 无通道，与 TimePicker 家族同档 quirk）、
/// ErrorText 手动覆写落 FieldError、Disabled/Required 落每枚数位 input（封装层与
/// 底座 InputOTP 皆无 ReadOnly 通道，该侧不适用）、AriaLabel 逐位铺开且 InputClass 落
/// 数位 input、splat 独落底座容器 div（以 items-center+gap-2 容器类组合指纹定位——Field
/// 根类亦含单 atom:gap-2，落点形态与 MaskedInput 家族齐落 input 不同，据实断）、缺省参数面。headless 限制照底座先例：
/// 纯焦点移动沿不测。快照含 formfield GUID 与 otp 随机短 id，先经正则归一化。
/// </summary>
public class FormFieldInputOTPTests
{
    /// <summary>归一化 ControlId 随机 GUID 与底座自生成的 otp 短 id，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
            "otp-[0-9a-f]{8}", "otp-<id>");

    /// <summary>构造带基元服务的渲染器（底座 OnAfterRenderAsync 需 IJSRuntime；沿用 InputOTPTests 先例）。</summary>
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

    /// <summary>底座键入沿：在 Dispatcher 上向渲染树派发 DOM 事件（occurrence 即数位索引）。</summary>
    private static Task DispatchAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>底座 JS 回调沿：直接在 Dispatcher 上调用 [JSInvokable] 入口并等待落定（InputOTPTests 先例）。</summary>
    private static Task InvokeBaseAsync(
        ComponentTestRenderer renderer, Func<InputOTP, Task> callback) =>
        renderer.Dispatcher.InvokeAsync(async () => await callback(renderer.FindComponent<InputOTP>()));

    /// <summary>校验沿测试模型：Code 非 1234 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public string? Code { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Code 不为 1234 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Code));

        void Validate()
        {
            messages.Clear();
            if (doc.Code != "1234")
            {
                messages.Add(field, "验证码必须为 1234");
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
        Expression<Func<string?>>? valueExpression = null,
        string? name = null,
        int? length = null,
        EventCallback<string> onComplete = default,
        bool? disabled = null,
        bool? required = null,
        bool? showSeparator = null,
        int? groupSize = null,
        RenderFragment? separator = null,
        InputOTPSize? size = null,
        bool? maskInput = null,
        InputOTPInputMode? inputMode = null,
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
            parameters[nameof(FormFieldInputOTP.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldInputOTP.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldInputOTP.ValueExpression)] = valueExpression;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldInputOTP.Name)] = name;
        }
        if (length is not null)
        {
            parameters[nameof(FormFieldInputOTP.Length)] = length.Value;
        }
        if (onComplete.HasDelegate)
        {
            parameters[nameof(FormFieldInputOTP.OnComplete)] = onComplete;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldInputOTP.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldInputOTP.Required)] = required.Value;
        }
        if (showSeparator is not null)
        {
            parameters[nameof(FormFieldInputOTP.ShowSeparator)] = showSeparator.Value;
        }
        if (groupSize is not null)
        {
            parameters[nameof(FormFieldInputOTP.GroupSize)] = groupSize.Value;
        }
        if (separator is not null)
        {
            parameters[nameof(FormFieldInputOTP.Separator)] = separator;
        }
        if (size is not null)
        {
            parameters[nameof(FormFieldInputOTP.Size)] = size.Value;
        }
        if (maskInput is not null)
        {
            parameters[nameof(FormFieldInputOTP.MaskInput)] = maskInput.Value;
        }
        if (inputMode is not null)
        {
            parameters[nameof(FormFieldInputOTP.InputMode)] = inputMode.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldInputOTP.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldInputOTP.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldInputOTP.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldInputOTP.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldInputOTP.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldInputOTP.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>取底座 InputOTP 容器 div 开标签（容器类带 items-center+gap-2 组合指纹——
    /// Field 根类亦含 atom:gap-2，单原子指纹会误中；Field 根仅收 Class）。</summary>
    private static string BaseRoot(string markup) =>
        Regex.Match(markup, """<div[^>]*atom:items-center atom:gap-2[^>]*>""").Value;

    /// <summary>取 Field 根容器开标签（role="group" 指纹）。</summary>
    private static string FieldRoot(string markup) =>
        Regex.Match(markup, """<div[^>]*role="group"[^>]*>""").Value;

    /// <summary>取第 index 枚数位 input 开标签（跳过头部可能存在的隐藏回传项）。</summary>
    private static string DigitTag(string markup, int index)
    {
        var matches = Regex.Matches(markup, "<input[^>]*>");
        var digit = matches.Cast<Match>().Where(m => !m.Value.Contains("type=\"hidden\"")).ToList();
        return digit[index].Value;
    }

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            label: "验证码", helperText: "四位数字验证码",
            value: "12", length: 4, name: "auth.code"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应独立呈现且全树无for与describedby通道()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            label: "验证码", helperText: "四位数字验证码"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"field-label\"", markup);
        Assert.Contains(">验证码</label>", markup);
        Assert.DoesNotContain("<label for=", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        // 描述元素仍自带 id（仅供错误态外部锚定，控件侧无人引用）；本家族无 for 通道
        // ControlId 全树仅以 -description 后缀形态出现。
        var id = Regex.Match(markup, "formfield-[0-9a-f]{32}").Value;
        Assert.Contains($"id=\"{id}-description\"", markup);
    }

    // ------------------------------------------------------------------
    // 键入/粘贴沿与字符模式
    // ------------------------------------------------------------------

    [Fact]
    public async Task 键入数位应经封装层逐位回报且满位触发完成一次()
    {
        var reports = new List<string>();
        var completed = new List<string>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            length: 4,
            valueChanged: EventCallback.Factory.Create<string>(new object(), v => reports.Add(v)),
            onComplete: EventCallback.Factory.Create<string>(new object(), v => completed.Add(v))));
        await SettleAsync(renderer);

        // occurrence 即数位索引；底座逐位拼接当前全值回报（空格位并入为空段）。
        for (var i = 0; i < 4; i++)
        {
            await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = ((char)('1' + i)).ToString() }, occurrence: i);
            await SettleAsync(renderer);
        }

        Assert.Equal(["1", "12", "123", "1234"], reports);
        Assert.Equal(["1234"], completed); // 满位仅触发一次
        Assert.Equal("1234", field.Value); // HandleValueChanged 赋值步沿
        Assert.Contains(@"value=""4""", renderer.Markup());
    }

    [Fact]
    public async Task 粘贴应过滤非法字符填满即完成并经封装层回报()
    {
        var reports = new List<string>();
        var completed = new List<string>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            length: 4,
            valueChanged: EventCallback.Factory.Create<string>(new object(), v => reports.Add(v)),
            onComplete: EventCallback.Factory.Create<string>(new object(), v => completed.Add(v))));
        await SettleAsync(renderer);

        // 底座 [JSInvokable] 粘贴沿直调（InputOTPTests 先例）：Numbers 模式滤除字母。
        await InvokeBaseAsync(renderer, otp => otp.OnPasteJs("12ab34"));
        await SettleAsync(renderer);

        Assert.Equal(["1234"], reports);
        Assert.Equal(["1234"], completed);
        Assert.Equal("1234", field.Value);
        Assert.Contains(@"value=""3""", renderer.Markup());
    }

    [Fact]
    public async Task 非法字符应被数字模式拒收零上报()
    {
        var reports = new List<string>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            length: 4,
            valueChanged: EventCallback.Factory.Create<string>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // Numbers 模式：字母首字符直接被底座 HandleInput 早退，不填位不上报。
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "a" });
        await SettleAsync(renderer);
        Assert.Empty(reports);
        Assert.Null(field.Value);

        // 合法数字仍走沿。
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "7" });
        await SettleAsync(renderer);
        Assert.Equal(["7"], reports);
    }

    [Fact]
    public async Task 掩码开关应用星号遮蔽已填数位()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            value: "12", length: 4, maskInput: true));
        await SettleAsync(renderer);

        // 已填两格显星号、未填两格仍空值。
        Assert.Equal(2, Regex.Count(renderer.Markup(), "value=\"\\*\""));
        Assert.DoesNotContain(@"value=""1""", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 结构与分组
    // ------------------------------------------------------------------

    [Fact]
    public async Task Name应落隐藏回传项且数位input不带name()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            value: "12", length: 4, name: "auth.code"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // 渲染实测：自闭合 ` />` 落为 `></input>` 形态。
        Assert.Contains("""<input type="hidden" name="auth.code" value="12">""", markup);
        // 底座形态：name 仅回传项持有，各数位 input 无 name。
        Assert.Equal(0, Regex.Count(markup, "<input[^>]*name[^>]*type=\"text\""));
    }

    [Fact]
    public async Task 分隔符应随组宽摆放可关可换自定义内容()
    {
        var renderer = CreateRenderer();

        // 缺省 Length=6、GroupSize=3：仅第三段后一枚分隔符（第六段为末位不摆）。
        await renderer.MountAsync<FormFieldInputOTP>(new Dictionary<string, object?>());
        await SettleAsync(renderer);
        Assert.Equal(1, Regex.Count(renderer.Markup(), "text-muted-foreground"));
        Assert.Contains("<span>-</span>", renderer.Markup());

        // 关闭开关：分隔符整体撤下。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, FieldParams(
            showSeparator: false)));
        await SettleAsync(renderer);
        Assert.Equal(0, Regex.Count(renderer.Markup(), "text-muted-foreground"));

        // 自定义内容：换 "•"（未列参数保持关分隔现值，先重新开启）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, FieldParams(
            length: 4, groupSize: 2, showSeparator: true,
            separator: builder => builder.AddContent(0, "•"))));
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("•", markup);
        Assert.DoesNotContain("<span>-</span>", markup);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随粘贴消长且仅Field根dataInvalid显形()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        Expression<Func<string?>> valueExpression = () => doc.Code;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldInputOTP>(0);
                builder.AddAttribute(1, nameof(FormFieldInputOTP.Value), doc.Code);
                builder.AddAttribute(2, nameof(FormFieldInputOTP.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldInputOTP.ValueChanged),
                    EventCallback.Factory.Create<string?>(doc, value => doc.Code = value));
                builder.AddAttribute(4, nameof(FormFieldInputOTP.Label), (string?)"验证码");
                builder.AddAttribute(5, nameof(FormFieldInputOTP.Length), 4);
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始无效：错误经 FieldError 呈现；本家族底座无 AriaInvalid/AriaDescribedBy 通道
        // 控件级 aria-invalid 与 describedby 不落（仅 Field 根 data-invalid 显形
        // 与 TimePicker 家族同档 quirk，区别于 Currency/Numeric/Masked 三件套档）。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("验证码必须为 1234", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", FieldRoot(markup));
        Assert.DoesNotContain("aria-invalid", markup);
        Assert.DoesNotContain("aria-describedby", markup);

        // 粘贴满位合法验证码 → 底座通知 + 外层三步沿 → 校验消解。
        await InvokeBaseAsync(renderer, otp => otp.OnPasteJs("1234"));
        await SettleAsync(renderer);

        Assert.Equal("1234", doc.Code);
        markup = renderer.Markup();
        Assert.DoesNotContain("验证码必须为 1234", markup);
        Assert.DoesNotContain("data-invalid", markup);
    }

    [Fact]
    public async Task 错误文本手动覆写应落FieldError而控件保持静音()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            label: "验证码", helperText: "四位数字验证码", length: 4,
            errorText: "服务端驳回：验证码已过期"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：验证码已过期", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        // 帮助文本让位错误消息；无通道故 describedby/aria-invalid 仍全树缺席。
        Assert.DoesNotContain("四位数字验证码", markup);
        Assert.DoesNotContain("aria-describedby", renderer.Markup());
        Assert.DoesNotContain("aria-invalid", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用必填应逐位落每枚数位input()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            value: "12", length: 4, disabled: true, required: true));
        await SettleAsync(renderer);

        // 底座形态：Disabled/Required 逐位铺开（4 枚数位 input 全带）。
        Assert.Equal(4, Regex.Count(renderer.Markup(), "<input[^>]* disabled"));
        Assert.Equal(4, Regex.Count(renderer.Markup(), "<input[^>]* required"));
        // 封装层与底座 InputOTP 皆无 ReadOnly 参数——本家族 ReadOnly 侧不适用
 //（同档登记，非偏差）。
    }

    [Fact]
    public async Task 无障碍名称与InputClass逐位落input而透传落底座容器div()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldInputOTP>(FieldParams(
            length: 4, ariaLabel: "验证码", inputClass: "atom:custom-otp",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-otp" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // 落点实证：AriaLabel 经底座逐位 input 的 aria-label 铺开；InputClass 进每枚
        // 数位 ComputedInputClass；splat 经底座 @attributes 独落容器 div（items-center+
        // gap-2 组合类指纹，Field 根类亦含裸 gap-2 故单原子不可作锚），与 MaskedInput 家族
        // "齐落裸 input"形态各异；Field 根仅收 Class。
        Assert.Equal(4, Regex.Count(markup, "aria-label=\"验证码\""));
        Assert.Equal(4, Regex.Count(markup, "atom:custom-otp"));
        Assert.Contains("data-testid=\"field-otp\"", BaseRoot(markup));
        Assert.DoesNotContain("data-testid", DigitTag(markup, 0));
        Assert.DoesNotContain("data-testid", FieldRoot(markup));
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();

        // 缺省：Length=6、GroupSize=3、ShowSeparator=true、Size=Default、
        // InputMode=Numbers、MaskInput=false、开关面全 false、引用面全 null
        // FormFieldBase 侧 Orientation=Vertical。
        var field = await renderer.MountAsync<FormFieldInputOTP>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Value);
        Assert.Null(field.ValueExpression);
        Assert.Null(field.Name);
        Assert.Equal(6, field.Length);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.True(field.ShowSeparator);
        Assert.Equal(3, field.GroupSize);
        Assert.Null(field.Separator);
        Assert.Equal(InputOTPSize.Default, field.Size);
        Assert.False(field.MaskInput);
        Assert.Equal(InputOTPInputMode.Numbers, field.InputMode);
        Assert.Null(field.InputClass);
        Assert.False(field.OnComplete.HasDelegate);

        var markup = renderer.Markup();
        Assert.Equal(6, Regex.Count(markup, "<input")); // 六段且无 Name 时零隐藏回传项
        Assert.DoesNotContain("type=\"hidden\"", markup);
        Assert.Contains(@"inputmode=""numeric""", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }
}
