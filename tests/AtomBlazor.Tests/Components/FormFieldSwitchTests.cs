using AtomBlazor.Primitives;
using System.Linq.Expressions;
using System.Reflection;
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
/// FormFieldSwitch 组合字段测试：在可选 EditContext 宿主下挂载
/// FormFieldSwitch（根 0，底座无弹层，无需 PortalHost），验证标签 for 实通开关按钮
/// （Id=ControlId 经未匹配属性抵达按钮元素并按源样张保留大写 "Id"，沿用 SwitchTests 的
/// 大写属性锚点口径）、aria-describedby 经正式参数通道落按钮、点击拨动经封装层两步沿
/// 回填上报（Checked+CheckedChanged+NotifyFieldChanged）、Size 变体透传落按钮、
/// EditContext 校验消息随拨动消长并经按钮的 aria-invalid 显形（底座自算沿：本家族封装层
/// 无 AriaInvalid 通道，与 CurrencyInput 家族显式传入形态不同）、ErrorText 手动覆写仅
/// 切换 describedby（无 EditContext 时控件侧不落 aria-invalid）、Disabled 落按钮
/// disabled/aria-disabled/tabindex=-1 并拒绝拨动、AriaLabel 落按钮且 InputClass+splat
/// 同落按钮（Field 根不吃 splat）、底座 Switch 无本地化缺省名称沿（不传即 aria-label
/// 整体缺席）、ReadOnly 通道不适用（封装层与底座皆无该参数——反射实证）与缺省参数面。
/// 快照含 formfield GUID，先经正则归一化。
/// </summary>
public class FormFieldSwitchTests
{
    /// <summary>归一化 ControlId 随机 GUID（按钮 Id、label for 与 describedby 通道），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>");

    /// <summary>构造带基元服务的渲染器（底座无 JS 依赖，服务面照 CurrencyInput 先例补齐）。</summary>
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

    /// <summary>触发一次 DOM 事件（须在 Dispatcher 上派发，沿用 SwitchTests 先例）。</summary>
    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () => await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>校验沿测试模型：Enabled 非 true 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Enabled 不为 true 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Enabled));

        void Validate()
        {
            messages.Clear();
            if (!doc.Enabled)
            {
                messages.Add(field, "开关必须打开");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        bool? isChecked = null,
        EventCallback<bool> checkedChanged = default,
        Expression<Func<bool>>? checkedExpression = null,
        SwitchSize? size = null,
        bool? disabled = null,
        string? inputClass = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        FieldOrientation? orientation = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (isChecked is not null)
        {
            parameters[nameof(FormFieldSwitch.Checked)] = isChecked.Value;
        }
        if (checkedChanged.HasDelegate)
        {
            parameters[nameof(FormFieldSwitch.CheckedChanged)] = checkedChanged;
        }
        if (checkedExpression is not null)
        {
            parameters[nameof(FormFieldSwitch.CheckedExpression)] = checkedExpression;
        }
        if (size is not null)
        {
            parameters[nameof(FormFieldSwitch.Size)] = size.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldSwitch.Disabled)] = disabled.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldSwitch.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldSwitch.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldSwitch.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldSwitch.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldSwitch.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldSwitch.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>取底座开关按钮开标签（role="switch" 指纹；Field 根为 data-slot="field" div）。</summary>
    private static string SwitchTag(string markup) =>
        Regex.Match(markup, """<button[^>]*role="switch"[^>]*>""").Value;

    /// <summary>取开关按钮大写 Id 属性值（原语无 Id 参数，未匹配属性按源样张保留大写 "Id"——SwitchTests 先例）。</summary>
    private static string ButtonId(string markup) =>
        Regex.Match(markup, "Id=\"([^\"]+)\"").Groups[1].Value;

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldSwitch>(FieldParams(
            label: "启用通知", helperText: "发送邮件提醒", isChecked: true));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应实通开关按钮且describedby经正式参数落按钮()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldSwitch>(FieldParams(
            label: "启用通知", helperText: "发送邮件提醒"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // FieldLabel For=ControlId 实通：底座 Id="@ControlId" 直落开关按钮。
        var id = ButtonId(markup);
        Assert.Matches(@"^formfield-[0-9a-f]{32}$", id);
        Assert.Contains($"for=\"{id}\"", markup);
        // 帮助态 describedby 指向描述元素并落按钮；无帮助无错误时该属性整体缺席。
        Assert.Contains($"aria-describedby=\"{id}-description\"", SwitchTag(markup));
        Assert.Contains($"id=\"{id}-description\"", markup);

        // 显式 null 复位 HelperText：参数视图中未出现的参数保持组件当前值。
        var bare = FieldParams(label: "启用通知");
        bare[nameof(FormFieldSwitch.HelperText)] = null;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, bare));
        await SettleAsync(renderer);
        Assert.DoesNotContain("aria-describedby", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 拨动沿与参数透传
    // ------------------------------------------------------------------

    [Fact]
    public async Task 点击开关应经封装层两步沿回填并上报()
    {
        var reports = new List<bool>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldSwitch>(FieldParams(
            isChecked: false,
            checkedChanged: EventCallback.Factory.Create<bool>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 关闭态唯一 onclick 携带者就是开关按钮（occurrence 0）。
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        Assert.Equal([true], reports);
        Assert.True(field.Checked); // HandleCheckedChanged 赋值步沿
        var button = SwitchTag(renderer.Markup());
        Assert.Contains("aria-checked=\"true\"", button);
        Assert.Contains("data-state=\"checked\"", button);

        // 再拨回：受控参数视图未重放，底座自身 Checked 翻转沿。
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        Assert.Equal([true, false], reports);
        Assert.Contains("aria-checked=\"false\"", SwitchTag(renderer.Markup()));
    }

    [Fact]
    public async Task 尺寸变体应透传落开关按钮类面()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldSwitch>(FieldParams(size: SwitchSize.Small));
        await SettleAsync(renderer);
        var button = SwitchTag(renderer.Markup());
        Assert.Contains("atom:h-5", button);
        Assert.Contains("atom:w-9", button);

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(
            0, FieldParams(size: SwitchSize.Large)));
        await SettleAsync(renderer);
        button = SwitchTag(renderer.Markup());
        Assert.Contains("atom:h-7", button);
        Assert.Contains("atom:w-14", button);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随拨动消长并经按钮的ariaInvalid显形()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        Expression<Func<bool>> checkedExpression = () => doc.Enabled;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldSwitch>(0);
                builder.AddAttribute(1, nameof(FormFieldSwitch.Checked), doc.Enabled);
                builder.AddAttribute(2, nameof(FormFieldSwitch.CheckedExpression), checkedExpression);
                builder.AddAttribute(3, nameof(FormFieldSwitch.CheckedChanged),
                    EventCallback.Factory.Create<bool>(doc, value => doc.Enabled = value));
                builder.AddAttribute(4, nameof(FormFieldSwitch.Label), (string?)"启用通知");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始 false：广播校验后错误经 FieldError 呈现；封装层无 AriaInvalid 通道
        // 按钮的 aria-invalid 由底座自算（EditContext+CheckedExpression 级联沿）
        // describedby 经 DescribedById 切到错误元素并落按钮。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("开关必须打开", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", SwitchTag(markup));
        Assert.Contains("aria-invalid=\"true\"", SwitchTag(markup));
        // 底座 quirk：拿到 CheckedExpression 即落 aria-required="true"。
        Assert.Contains("aria-required=\"true\"", SwitchTag(markup));

        // 点击拨到 true → 封装层 NotifyFieldChanged + 底座自通知 → 校验消解。
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        Assert.True(doc.Enabled);
        markup = renderer.Markup();
        Assert.DoesNotContain("开关必须打开", markup);
        Assert.DoesNotContain("aria-invalid", markup);
        Assert.Contains("aria-checked=\"true\"", SwitchTag(markup));
    }

    // ------------------------------------------------------------------
    // 错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应切换describedby且控件侧不落ariaInvalid()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldSwitch>(FieldParams(
            label: "启用通知", helperText: "发送邮件提醒",
            errorText: "服务端驳回：账户不允许"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：账户不允许", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素。
        Assert.DoesNotContain("发送邮件提醒", markup);
        // 归一化残注：Scrub 注入的 "<id>" 含 '>'，会截断属性级标签正则——直打全量 markup。
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        // 通道实证：封装层不吃 AriaInvalid 参数（即无该通道），手动错误态且
        // 无 EditContext 时按钮的 aria-invalid 整体缺席——与 CurrencyInput 家族形态不同。
        Assert.DoesNotContain("aria-invalid", markup);
    }

    // ------------------------------------------------------------------
    // 禁用与无障碍
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用态应落按钮disabled并拒绝拨动()
    {
        var reports = new List<bool>();
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldSwitch>(FieldParams(
            isChecked: true, disabled: true,
            checkedChanged: EventCallback.Factory.Create<bool>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        var button = SwitchTag(renderer.Markup());
        Assert.Contains(" disabled", button);
        Assert.Contains("aria-disabled=\"true\"", button);
        Assert.Contains("tabindex=\"-1\"", button);

        // headless 仍可直发 onclick；底座 HandleClick 的 !Disabled 门吞下，零上报。
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        Assert.Empty(reports);
        Assert.Contains("aria-checked=\"true\"", SwitchTag(renderer.Markup()));
    }

    [Fact]
    public async Task 无障碍名称输入类与透传应同落开关按钮()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldSwitch>(FieldParams(
            ariaLabel: "启用通知开关", inputClass: "atom:custom-sw",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-switch" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var button = SwitchTag(markup);
        Assert.Contains("aria-label=\"启用通知开关\"", button);
        Assert.Contains("atom:custom-sw", button);
        Assert.Contains("data-testid=\"field-switch\"", button);
        // Field 根不吃 splat（splat 经 @attributes 只进底座开关）。
        var fieldRoot = Regex.Match(markup, """<div[^>]*data-slot="field"[^>]*>""").Value;
        Assert.DoesNotContain("data-testid", fieldRoot);
    }

    // ------------------------------------------------------------------
    // 缺省面与通道实证
    // ------------------------------------------------------------------

    [Fact]
    public async Task 缺省应无本地化名称且按钮保持未拨动态()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldSwitch>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var button = SwitchTag(markup);
        // 底座 Switch 无本地化缺省名称沿：不传 AriaLabel 即 aria-label 整体缺席。
        Assert.DoesNotContain("aria-label", button);
        Assert.Contains("aria-checked=\"false\"", button);
        Assert.Contains("data-state=\"unchecked\"", button);
        // 无 CheckedExpression：底座 quirk 的 aria-required 亦缺席。
        Assert.DoesNotContain("aria-required", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        Assert.DoesNotContain("<label", markup);
    }

    [Fact]
    public async Task 缺省参数面应与一致且ReadOnly通道不适用()
    {
        var renderer = CreateRenderer();

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、Checked=false、
        // Size=Medium、Disabled=false、引用面全 null。
        var field = await renderer.MountAsync<FormFieldSwitch>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.False(field.Checked);
        Assert.Null(field.CheckedExpression);
        Assert.Equal(SwitchSize.Medium, field.Size);
        Assert.False(field.Disabled);
        Assert.Null(field.InputClass);
        Assert.False(field.CheckedChanged.HasDelegate);

 // 通道逐家族实证（/ 纪律）：本家族封装层与底座 Switch 均无 ReadOnly 参数。
        Assert.Null(typeof(FormFieldSwitch).GetProperty("ReadOnly", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(Switch).GetProperty("ReadOnly", BindingFlags.Public | BindingFlags.Instance));
    }
}
