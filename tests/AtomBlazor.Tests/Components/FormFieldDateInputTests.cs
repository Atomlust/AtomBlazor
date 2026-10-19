using AtomBlazor.Primitives;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
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
/// FormFieldDateInput 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldDateInput（根 1），验证标签 for 实通首段输入（Id=ControlId 落文化段序首段）、
/// aria-describedby 经正式参数落组与每段、分段键入合成回填三步沿、EditContext 校验消长
/// （底座自身校验通知与外层 NotifyFieldChanged 双发沿）、ErrorText 手动覆写优先链且不落控件
/// aria-invalid（不传 IsInvalid 面，无效态实断据为 Field 根 data-invalid）、
/// ShowCalendar 缺省 false 与底座缺省 true 不对称、Required/ReadOnly/Disabled/Name 落分段与
/// 隐藏 ISO 输入、MinDate/MaxDate 拒值与 Culture 段序、AriaLabel/InputClass/splat 落根容器
/// 与缺省参数面。headless 口径：段间移焦属 JS 侧行为（底座 DateInputTests 已覆盖，本波不重复）
/// 快照含 formfield GUID、calendar GUID、popover-N 与 select-N 短 id，先经正则归一化
/// 分段断言钉死 en-US 文化（M/D/Y 段序）。
/// </summary>
public class FormFieldDateInputTests
{
    private static readonly CultureInfo enUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>归一化 ControlId 随机 GUID、calendar GUID、popover-N 与 select-N 自增短 id。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(
                    Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                    "calendar-[0-9a-f]{32}", "calendar-<id>"),
                "popover-[0-9]+", "popover-<n>"),
            "select-[0-9]+", "select-<n>");

    /// <summary>构造带全套基元服务的渲染器（照 DateInputTests 先例，日历弹层需要门户/定位/焦点服务）。</summary>
    private static ComponentTestRenderer CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<DropdownManagerService>();
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

    /// <summary>触发一次 DOM 事件（沿 DateInputTests 先例须在 Dispatcher 上派发）。</summary>
    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () => await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>向第 n 个分段输入派发 oninput 文本（en-US 段序 Month/Day/Year）。</summary>
    private static Task TypeAsync(ComponentTestRenderer renderer, int occurrence, string text) =>
        FireAsync(renderer, "oninput", new ChangeEventArgs { Value = text }, occurrence);

    /// <summary>按 data-atom-segment 抓取某分段 input 开标签内的属性值（DateInputTests 先例）。</summary>
    private static string? SegmentAttr(string markup, string segment, string attr)
    {
        var match = Regex.Match(markup, $"<input[^>]*data-atom-segment=\"{segment}\"[^>]*>");
        return match.Success ? Regex.Match(match.Value, @$"{attr}=""([^""]*)""").Groups[1] is { Length: > 0 } g ? g.Value : null : null;
    }

    /// <summary>校验沿测试模型：Date 非 2026-10-12 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public DateTime? Date { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Date 不为 2026-10-12 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Date));

        void Validate()
        {
            messages.Clear();
            if (doc.Date != new DateTime(2026, 10, 12))
            {
                messages.Add(field, "必须选择 2026-10-12");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        DateTime? value = null,
        EventCallback<DateTime?> valueChanged = default,
        Expression<Func<DateTime?>>? valueExpression = null,
        CultureInfo? culture = null,
        DateTime? minDate = null,
        DateTime? maxDate = null,
        bool? showCalendar = null,
        bool? readOnly = null,
        bool? disabled = null,
        bool? required = null,
        string? name = null,
        string? inputClass = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        FieldOrientation? orientation = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FormFieldDateInput.Culture)] = culture ?? enUs, // 测试钉死美式段序（缺省随机器文化，如此）
        };
        if (value is not null)
        {
            parameters[nameof(FormFieldDateInput.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldDateInput.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldDateInput.ValueExpression)] = valueExpression;
        }
        if (minDate is not null)
        {
            parameters[nameof(FormFieldDateInput.MinDate)] = minDate;
        }
        if (maxDate is not null)
        {
            parameters[nameof(FormFieldDateInput.MaxDate)] = maxDate;
        }
        if (showCalendar is not null)
        {
            parameters[nameof(FormFieldDateInput.ShowCalendar)] = showCalendar.Value;
        }
        if (readOnly is not null)
        {
            parameters[nameof(FormFieldDateInput.ReadOnly)] = readOnly.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldDateInput.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldDateInput.Required)] = required.Value;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldDateInput.Name)] = name;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldDateInput.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldDateInput.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldDateInput.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldDateInput.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldDateInput.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldDateInput.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateInput>(FieldParams(
            label: "出发日期", helperText: "按 月/日/年 键入",
            value: new DateTime(2026, 10, 12), name: "trip.date"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应实通首段输入且describedby落组与每段()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateInput>(FieldParams(
            label: "出发日期", helperText: "按 月/日/年 键入"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // FieldLabel For=ControlId 实通：en-US 首段 Month 的 id 即 ControlId。
        var id = SegmentAttr(markup, "Month", "id")!;
        Assert.Matches(@"^formfield-[0-9a-f]{32}$", id);
        Assert.Contains($"for=\"{id}\"", markup);
        // 其余段走 -month/-day/-year 后缀档（底座 Id 沿）。
        Assert.Matches(@"^formfield-[0-9a-f]{32}-year$", SegmentAttr(markup, "Year", "id")!);
        // describedby 经正式参数通道落根组与每段（Help 态指向描述元素）。
        Assert.Contains($"id=\"{id}-description\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-description""", markup);
    }

    // ------------------------------------------------------------------
    // 值回填沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 逐段键入完整日期应经三步沿回填并落隐藏ISO输入()
    {
        var reports = new List<DateTime?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldDateInput>(FieldParams(
            name: "trip.date",
            valueChanged: EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await TypeAsync(renderer, 0, "10"); // Month
        await TypeAsync(renderer, 1, "12"); // Day
        await SettleAsync(renderer);
        Assert.Empty(reports); // 尚缺年：合成为 null，null→null 无变化不回调

        await TypeAsync(renderer, 2, "2026"); // Year
        await SettleAsync(renderer);

        Assert.Equal(new DateTime(2026, 10, 12), Assert.Single(reports));
        Assert.Equal(new DateTime(2026, 10, 12), field.Value); // HandleValueChanged 赋值步沿
        Assert.Contains("""<input type="hidden" name="trip.date" value="2026-10-12">""", renderer.Markup());
    }

    [Fact]
    public async Task 不完整分段应置空值且底座本地无效不落Field根()
    {
        var reports = new List<DateTime?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldDateInput>(FieldParams(
            value: new DateTime(2026, 10, 12),
            valueChanged: EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await TypeAsync(renderer, 1, ""); // 清空 Day
        await SettleAsync(renderer);

        Assert.Equal([null], reports);
        Assert.Null(field.Value);
        var markup = renderer.Markup();
        Assert.Contains("aria-invalid=\"true\"", markup); // 底座本地错误置位
        // quirk：包装层不把自身 IsInvalid 传底座（无该参数通道）
        // 底座无效态也不回传包装层——Field 根无 data-invalid。
        Assert.DoesNotContain("data-invalid=\"true\"", markup);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随完整键入消长并经FieldError呈现()
    {
        var doc = new Doc();
        var reports = new List<DateTime?>();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        Expression<Func<DateTime?>> valueExpression = () => doc.Date;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldDateInput>(0);
                builder.AddAttribute(1, nameof(FormFieldDateInput.Culture), enUs);
                builder.AddAttribute(2, nameof(FormFieldDateInput.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldDateInput.ValueChanged),
                    EventCallback.Factory.Create<DateTime?>(doc, value =>
                    {
                        doc.Date = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(4, nameof(FormFieldDateInput.Label), (string?)"出发日期");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始空值：广播整体校验后错误经 FieldError 呈现，describedby 指向错误元素。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("必须选择 2026-10-12", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", markup);

        // 键入完整合法日期 → 外层三步沿 + 底座自身校验通知双发沿 → 校验消解。
        await TypeAsync(renderer, 0, "10");
        await TypeAsync(renderer, 1, "12");
        await TypeAsync(renderer, 2, "2026");
        await SettleAsync(renderer);

        Assert.Equal(new DateTime(2026, 10, 12), doc.Date);
        markup = renderer.Markup();
        Assert.DoesNotContain("必须选择 2026-10-12", markup);
        Assert.Contains("""value="2026-10-12">""", markup); // 隐藏 ISO 输入回填
    }

    // ------------------------------------------------------------------
    // 错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应显形并切换describedby且不落控件ariaInvalid()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateInput>(FieldParams(
            label: "出发日期", helperText: "按 月/日/年 键入",
            errorText: "服务端驳回：日期已过期"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：日期已过期", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素。
        Assert.DoesNotContain("按 月/日/年 键入", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        // quirk：DateInput 家族包装层不向底座传无效态（底座无 IsInvalid 参数）
        // ErrorText 手动覆写不落控件 aria-invalid（底座只认自身本地/字段校验错误）。
        Assert.DoesNotContain("aria-invalid", markup);
    }

    // ------------------------------------------------------------------
    // ShowCalendar 不对称缺省
    // ------------------------------------------------------------------

    [Fact]
    public async Task ShowCalendar应缺省关闭且置真时日历按钮与弹层闭合态入位()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(label: "出发日期");
        await renderer.MountAsync<FormFieldDateInput>(dict);
        await SettleAsync(renderer);

        // quirk：包装层缺省 false（紧凑表单行），底座缺省 true——默认无日历按钮。
        Assert.DoesNotContain("aria-label=\"Open calendar\"", renderer.Markup());

        // 置真透传：按钮入位且弹层关闭态（headless 口径断 aria-expanded）。
        dict[nameof(FormFieldDateInput.ShowCalendar)] = true;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Open calendar\"", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
        Assert.Matches(@"id=""popover-[0-9]+-trigger""", markup);
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 必填只读禁用与Name应落分段与隐藏输入()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateInput>(FieldParams(
            value: new DateTime(2026, 10, 12),
            required: true, disabled: true, name: "trip.date"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        foreach (var segment in new[] { "Month", "Day", "Year" })
        {
            var tag = Regex.Match(markup, $"<input[^>]*data-atom-segment=\"{segment}\"[^>]*>").Value;
            Assert.Contains(" disabled", tag);
            Assert.Contains(" required", tag);
        }

        Assert.Contains("atom:opacity-50 atom:cursor-not-allowed", markup); // 底座禁用叠类
        var hidden = Regex.Match(markup, """<input type="hidden"[^>]*>""").Value;
        Assert.Contains("name=\"trip.date\"", hidden);
        Assert.Contains(" disabled", hidden);
    }

    [Fact]
    public async Task 只读面应只落readonly属性而不叠禁用类()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateInput>(FieldParams(readOnly: true));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains(" readonly", Regex.Match(markup, """<input[^>]*data-atom-segment="Month"[^>]*>""").Value);
        Assert.DoesNotContain("atom:opacity-50", markup);
    }

    [Fact]
    public async Task 最小最大日期越界键入应拒值且文化面应改段序与分隔符()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var reports = new List<DateTime?>();
        var field = await renderer.MountAsync<FormFieldDateInput>(FieldParams(
            minDate: new DateTime(2026, 10, 1), maxDate: new DateTime(2026, 10, 18),
            valueChanged: EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 键入超 MaxDate 的完整日期 → 底座解析拒值为 null（透传生效；null→null 无变化零回调）。
        await TypeAsync(renderer, 0, "10");
        await TypeAsync(renderer, 1, "20");
        await TypeAsync(renderer, 2, "2026");
        await SettleAsync(renderer);
        Assert.Empty(reports);
        Assert.Null(field.Value);
        Assert.Contains("aria-invalid=\"true\"", renderer.Markup()); // 本地无效显形

        // de-DE：ShortDatePattern d.M.yyyy → Day/Month/Year 段序与 "." 分隔符。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            culture: CultureInfo.GetCultureInfo("de-DE"),
            value: new DateTime(2026, 10, 12))));
        await SettleAsync(renderer);
        var order = Regex.Matches(renderer.Markup(), "data-atom-segment=\"(\\w+)\"")
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["Day", "Month", "Year"], order);
        Assert.Equal(2, Regex.Count(renderer.Markup(), @">\.<"));
    }

    [Fact]
    public async Task 无障碍名称输入类与透传应落底座根容器()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateInput>(FieldParams(
            ariaLabel: "启程日子", inputClass: "atom:custom-date",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-dateinput" }));
        await SettleAsync(renderer);

        // 沿：AriaLabel 压过底座 Localizer["DateInput.Label"] 英文缺省。
        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"启程日子\"", markup);
        Assert.DoesNotContain("aria-label=\"Date\"", markup);
        Assert.Contains("data-testid=\"field-dateinput\"", markup);
        // InputClass 与 splat 皆透传给底座 DateInput 根 div（Field 根仅收 Class； L24-25）。
        var baseRoot = Regex.Matches(markup, """<div[^>]*role="group"[^>]*>""").Cast<Match>().Last().Value;
        Assert.Contains("atom:custom-date", baseRoot);
        Assert.Contains("data-testid=\"field-dateinput\"", baseRoot);
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、ShowCalendar 缺省 false
        // （与底座缺省 true 不对称）、开关全 false、引用面全 null（含 Culture——随当前文化）。
        var field = await renderer.MountAsync<FormFieldDateInput>(
            new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Value);
        Assert.Null(field.ValueExpression);
        Assert.Null(field.Culture);
        Assert.Null(field.MinDate);
        Assert.Null(field.MaxDate);
        Assert.False(field.ShowCalendar);
        Assert.False(field.ReadOnly);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Null(field.Name);
        Assert.Null(field.InputClass);
        // 未传 AriaLabel 时组名回落底座 Localizer 英文缺省词条（DateInputTests 先例）。
        Assert.DoesNotContain("aria-required=\"true\"", renderer.Markup());
        Assert.Contains("aria-label=\"Date\"", renderer.Markup());
        Assert.DoesNotContain("aria-label=\"Open calendar\"", renderer.Markup());
    }
}
