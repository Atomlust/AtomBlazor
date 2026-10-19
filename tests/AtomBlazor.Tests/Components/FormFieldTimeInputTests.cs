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
/// FormFieldTimeInput 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldTimeInput（根 1），验证标签 for 实通小时段输入（Id=ControlId 落 Hour 段，Minute 走
/// -minute 后缀档）、aria-describedby 经正式参数落组与每段、分段键入合成回填沿（Format=Hour24
/// 绕开 12 小时制下界干扰，沿 TimeInputTests 先例）、EditContext 校验消长（底座自身校验通知与
/// 外层 NotifyFieldChanged 双发沿）、ErrorText 手动覆写优先链且不落控件 aria-invalid（不传
/// IsInvalid 面，无效态实断据为 Field 根 data-invalid）、ShowPicker 缺省 false 与底座缺省 true
/// 不对称、Format/ShowSeconds/MinuteStep/Culture 段形透传、Required/ReadOnly/Disabled/Name 落
/// 分段与隐藏 HH:MM:SS 输入、MinTime/MaxTime 拒值与 de-DE 二十四小时制面、AriaLabel/InputClass/
/// splat 落底座根容器与缺省参数面。headless 口径：段间移焦与弹层开合属 JS 侧行为（底座
/// TimeInputTests 已覆盖，本波只断关闭态入位）；快照含 formfield GUID、popover-N、select-N 与
/// input 短 id，先经正则归一化；分段断言钉死 en-US 文化。
/// </summary>
public class FormFieldTimeInputTests
{
    private static readonly CultureInfo enUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>归一化 ControlId 随机 GUID、popover-N、select-N 与 input 随机短 id。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(
                    Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                    "popover-[0-9]+", "popover-<n>"),
                "select-[0-9]+", "select-<n>"),
            "input-[0-9a-f]{8}", "input-<id>");

    /// <summary>构造带全套基元服务的渲染器（照 TimeInputTests 先例，时钟弹层需要门户/定位/焦点服务）。</summary>
    private static ComponentTestRenderer CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
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

    /// <summary>触发一次 DOM 事件（沿 TimeInputTests 先例须在 Dispatcher 上派发）。</summary>
    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () => await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>向第 n 个分段输入派发 oninput 文本（段序 Hour/Minute[/Second]）。</summary>
    private static Task TypeAsync(ComponentTestRenderer renderer, int occurrence, string text) =>
        FireAsync(renderer, "oninput", new ChangeEventArgs { Value = text }, occurrence);

    /// <summary>按 data-atom-segment 抓取某分段 input 开标签内的属性值（TimeInputTests 先例）。</summary>
    private static string? SegmentAttr(string markup, string segment, string attr)
    {
        var match = Regex.Match(markup, $"<input[^>]*data-atom-segment=\"{segment}\"[^>]*>");
        return match.Success ? Regex.Match(match.Value, @$"{attr}=""([^""]*)""").Groups[1] is { Length: > 0 } g ? g.Value : null : null;
    }

    /// <summary>校验沿测试模型：Time 非 09:30 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public TimeSpan? Time { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Time 不为 09:30 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Time));

        void Validate()
        {
            messages.Clear();
            if (doc.Time != new TimeSpan(9, 30, 0))
            {
                messages.Add(field, "必须选择 09:30");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        TimeSpan? value = null,
        EventCallback<TimeSpan?> valueChanged = default,
        Expression<Func<TimeSpan?>>? valueExpression = null,
        CultureInfo? culture = null,
        TimeFormat? format = null,
        TimeSpan? minTime = null,
        TimeSpan? maxTime = null,
        int? minuteStep = null,
        bool? showSeconds = null,
        bool? showPicker = null,
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
            [nameof(FormFieldTimeInput.Culture)] = culture ?? enUs, // 测试钉死美式段序（缺省随机器文化，如此）
        };
        if (value is not null)
        {
            parameters[nameof(FormFieldTimeInput.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldTimeInput.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldTimeInput.ValueExpression)] = valueExpression;
        }
        if (format is not null)
        {
            parameters[nameof(FormFieldTimeInput.Format)] = format.Value;
        }
        if (minTime is not null)
        {
            parameters[nameof(FormFieldTimeInput.MinTime)] = minTime;
        }
        if (maxTime is not null)
        {
            parameters[nameof(FormFieldTimeInput.MaxTime)] = maxTime;
        }
        if (minuteStep is not null)
        {
            parameters[nameof(FormFieldTimeInput.MinuteStep)] = minuteStep.Value;
        }
        if (showSeconds is not null)
        {
            parameters[nameof(FormFieldTimeInput.ShowSeconds)] = showSeconds.Value;
        }
        if (showPicker is not null)
        {
            parameters[nameof(FormFieldTimeInput.ShowPicker)] = showPicker.Value;
        }
        if (readOnly is not null)
        {
            parameters[nameof(FormFieldTimeInput.ReadOnly)] = readOnly.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldTimeInput.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldTimeInput.Required)] = required.Value;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldTimeInput.Name)] = name;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldTimeInput.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldTimeInput.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldTimeInput.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldTimeInput.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldTimeInput.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldTimeInput.Orientation)] = orientation.Value;
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

        await renderer.MountAsync<FormFieldTimeInput>(FieldParams(
            label: "会议时刻", helperText: "按 时/分 键入",
            value: new TimeSpan(14, 30, 0), name: "meeting.time"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应实通小时段输入且describedby落组与每段()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimeInput>(FieldParams(
            label: "会议时刻", helperText: "按 时/分 键入"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // FieldLabel For=ControlId 实通：底座 Id 只落 Hour 段（TimeInput.razor L10）。
        var id = SegmentAttr(markup, "Hour", "id")!;
        Assert.Matches(@"^formfield-[0-9a-f]{32}$", id);
        Assert.Contains($"for=\"{id}\"", markup);
        // 其余段走 -minute 后缀档（底座 Id 沿）。
        Assert.Matches(@"^formfield-[0-9a-f]{32}-minute$", SegmentAttr(markup, "Minute", "id")!);
        // describedby 经正式参数通道落根组与每段（Help 态指向描述元素）。
        Assert.Contains($"id=\"{id}-description\"", markup);
        var describedByPattern = @"aria-describedby=""formfield-[0-9a-f]{32}-description""";
        Assert.Matches(describedByPattern, markup);
        // 根组 + 两输入段 + AM/PM 选择器触发器皆承接（en-US 缺省十二小时制有选择器）。
        Assert.True(Regex.Count(markup, describedByPattern) >= 3);
    }

    // ------------------------------------------------------------------
    // 值回填沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 逐段键入完整时刻应经沿回填并落隐藏输入()
    {
        var reports = new List<TimeSpan?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldTimeInput>(FieldParams(
            name: "meeting.time", format: TimeFormat.Hour24, // 绕开 12 小时制下界 1 的干扰（TimeInputTests 先例）
            valueChanged: EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await TypeAsync(renderer, 0, "09"); // Hour
        await SettleAsync(renderer);
        Assert.Empty(reports); // 尚缺分：合成为 null，null→null 无变化不回调

        await TypeAsync(renderer, 1, "30"); // Minute
        await SettleAsync(renderer);

        Assert.Equal(new TimeSpan(9, 30, 0), Assert.Single(reports));
        Assert.Equal(new TimeSpan(9, 30, 0), field.Value); // HandleValueChanged 赋值步沿
        Assert.Contains(
            $"""<input type="hidden" name="meeting.time" value="{new TimeSpan(9, 30, 0).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}">""",
            renderer.Markup());
    }

    [Fact]
    public async Task 不完整分段应置空值且底座本地无效不落Field根()
    {
        var reports = new List<TimeSpan?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldTimeInput>(FieldParams(
            value: new TimeSpan(14, 30, 0),
            valueChanged: EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await TypeAsync(renderer, 1, ""); // 清空 Minute
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
        var reports = new List<TimeSpan?>();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        Expression<Func<TimeSpan?>> valueExpression = () => doc.Time;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldTimeInput>(0);
                builder.AddAttribute(1, nameof(FormFieldTimeInput.Culture), enUs);
                builder.AddAttribute(2, nameof(FormFieldTimeInput.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldTimeInput.ValueChanged),
                    EventCallback.Factory.Create<TimeSpan?>(doc, value =>
                    {
                        doc.Time = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(4, nameof(FormFieldTimeInput.Format), TimeFormat.Hour24);
                builder.AddAttribute(5, nameof(FormFieldTimeInput.Label), (string?)"会议时刻");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始空值：广播整体校验后错误经 FieldError 呈现，describedby 指向错误元素。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("必须选择 09:30", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", markup);

        // 键入完整合法时刻 → 外层沿 + 底座自身校验通知双发沿 → 校验消解。
        await TypeAsync(renderer, 0, "09");
        await TypeAsync(renderer, 1, "30");
        await SettleAsync(renderer);

        Assert.Equal(new TimeSpan(9, 30, 0), doc.Time);
        markup = renderer.Markup();
        Assert.DoesNotContain("必须选择 09:30", markup);
        Assert.DoesNotContain("Enter a complete, valid time", markup); // 底座本地错误亦随完整键入清除
        Assert.Contains("""value="09:30:00">""", markup); // 隐藏输入回填
    }

    // ------------------------------------------------------------------
    // 错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应显形并切换describedby且不落控件ariaInvalid()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimeInput>(FieldParams(
            label: "会议时刻", helperText: "按 时/分 键入",
            errorText: "服务端驳回：会议时刻已过期"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：会议时刻已过期", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素。
        Assert.DoesNotContain("按 时/分 键入", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        // quirk：TimeInput 家族包装层不向底座传无效态（底座无 IsInvalid 参数）
        // ErrorText 手动覆写不落控件 aria-invalid（底座只认自身本地/字段校验错误）。
        Assert.DoesNotContain("aria-invalid", markup);
    }

    // ------------------------------------------------------------------
    // ShowPicker 不对称缺省
    // ------------------------------------------------------------------

    [Fact]
    public async Task ShowPicker应缺省关闭且置真时时钟按钮与弹层闭合态入位()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(label: "会议时刻");
        await renderer.MountAsync<FormFieldTimeInput>(dict);
        await SettleAsync(renderer);

        // quirk：包装层缺省 false（紧凑表单行），底座缺省 true——默认无时钟按钮。
        Assert.DoesNotContain("aria-label=\"Open time picker\"", renderer.Markup());

        // 置真透传：按钮入位且弹层关闭态（headless 口径断 aria-expanded）。
        dict[nameof(FormFieldTimeInput.ShowPicker)] = true;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Open time picker\"", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
        Assert.Matches(@"id=""popover-[0-9]+-trigger""", markup);
    }

    // ------------------------------------------------------------------
    // 段形透传
    // ------------------------------------------------------------------

    [Fact]
    public async Task Format覆写ShowSeconds与MinuteStep应切换段形与步进面()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(value: new TimeSpan(14, 30, 45), showSeconds: true);
        await renderer.MountAsync<FormFieldTimeInput>(dict);
        await SettleAsync(renderer);

        // en-US ShortTimePattern "h:mm tt" → 缺省十二小时制：02:30 PM + Second 段。
        var markup = renderer.Markup();
        var order = Regex.Matches(markup, "data-atom-segment=\"(\\w+)\"")
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["Hour", "Minute", "Second"], order);
        Assert.Equal("02", SegmentAttr(markup, "Hour", "value"));
        Assert.Equal("45", SegmentAttr(markup, "Second", "value"));
        Assert.Equal("1", SegmentAttr(markup, "Hour", "data-min")); // Hour12：时下界 1
        Assert.Equal("12", SegmentAttr(markup, "Hour", "data-max"));
        Assert.Contains("aria-label=\"AM or PM\">PM", markup);

        // Format=Hour24 覆写 + MinuteStep=15 透传：14 时、界 0-23、时段选择器移除、分钟段步进落位。
        dict[nameof(FormFieldTimeInput.Format)] = TimeFormat.Hour24;
        dict[nameof(FormFieldTimeInput.MinuteStep)] = 15;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        markup = renderer.Markup();
        Assert.Equal("14", SegmentAttr(markup, "Hour", "value"));
        Assert.Equal("0", SegmentAttr(markup, "Hour", "data-min"));
        Assert.Equal("23", SegmentAttr(markup, "Hour", "data-max"));
        Assert.DoesNotContain("aria-label=\"AM or PM\"", markup);
        Assert.Equal("45", SegmentAttr(markup, "Second", "value")); // 秒段保持
        Assert.Equal("15", SegmentAttr(markup, "Minute", "data-step")); // 步进透传沿（段形过滤属底座 TimeInputTests 覆盖）
        Assert.Equal("1", SegmentAttr(markup, "Hour", "data-step"));     // 非分钟段恒 1
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 必填只读禁用与Name应落分段与隐藏输入()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimeInput>(FieldParams(
            value: new TimeSpan(14, 30, 0),
            required: true, disabled: true, name: "meeting.time"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        foreach (var segment in new[] { "Hour", "Minute" })
        {
            var tag = Regex.Match(markup, $"<input[^>]*data-atom-segment=\"{segment}\"[^>]*>").Value;
            Assert.Contains(" disabled", tag);
            Assert.Contains(" required", tag);
        }

        Assert.Contains("atom:opacity-50 atom:cursor-not-allowed", markup); // 底座禁用叠类
        var hidden = Regex.Match(markup, """<input type="hidden"[^>]*>""").Value;
        Assert.Contains("name=\"meeting.time\"", hidden);
        Assert.Contains(" disabled", hidden);
    }

    [Fact]
    public async Task 只读面应只落readonly属性而不叠禁用类()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimeInput>(FieldParams(readOnly: true));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains(" readonly", Regex.Match(markup, """<input[^>]*data-atom-segment="Hour"[^>]*>""").Value);
        // 单写 atom:opacity-50 会误中 AM/PM SelectTrigger 的 V 形箭头 svg 类，改断整对（TimeInputTests 先例）。
        Assert.DoesNotContain("atom:opacity-50 atom:cursor-not-allowed", markup);
    }

    [Fact]
    public async Task 最小最大时刻越界键入应拒值且文化面应切换时分制()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var reports = new List<TimeSpan?>();
        var field = await renderer.MountAsync<FormFieldTimeInput>(FieldParams(
            minTime: new TimeSpan(9, 0, 0), maxTime: new TimeSpan(18, 0, 0), format: TimeFormat.Hour24,
            valueChanged: EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 键入超 MaxTime 的完整时刻 → 底座解析拒值为 null（透传生效；null→null 无变化零回调）。
        await TypeAsync(renderer, 0, "20");
        await TypeAsync(renderer, 1, "00");
        await SettleAsync(renderer);
        Assert.Empty(reports);
        Assert.Null(field.Value);
        Assert.Contains("aria-invalid=\"true\"", renderer.Markup()); // 本地无效显形

        // de-DE：ShortTimePattern "HH:mm" → 二十四小时制面，无时段选择器。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            culture: CultureInfo.GetCultureInfo("de-DE"),
            value: new TimeSpan(14, 30, 0))));
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Equal(["Hour", "Minute"], Regex.Matches(markup, "data-atom-segment=\"(\\w+)\"")
            .Select(m => m.Groups[1].Value).ToList());
        Assert.Equal("14", SegmentAttr(markup, "Hour", "value"));
        Assert.DoesNotContain("aria-label=\"AM or PM\"", markup);
        Assert.Equal(1, Regex.Count(markup, ">:<")); // 两段一时分分隔符
    }

    [Fact]
    public async Task 无障碍名称输入类与透传应落底座根容器()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimeInput>(FieldParams(
            ariaLabel: "启程时刻", inputClass: "atom:custom-time",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-timeinput" }));
        await SettleAsync(renderer);

        // 沿：AriaLabel 压过底座 Localizer["TimeInput.Label"] 英文缺省。
        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"启程时刻\"", markup);
        Assert.DoesNotContain("aria-label=\"Time\"", markup);
        Assert.Contains("data-testid=\"field-timeinput\"", markup);
        // InputClass 与 splat 皆透传给底座 TimeInput 根 div（Field 根仅收 Class； L27-28）。
        var baseRoot = Regex.Matches(markup, """<div[^>]*role="group"[^>]*>""").Cast<Match>().Last().Value;
        Assert.Contains("atom:custom-time", baseRoot);
        Assert.Contains("data-testid=\"field-timeinput\"", baseRoot);
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、ShowPicker 缺省 false
        // （与底座缺省 true 不对称）、MinuteStep=1、开关全 false、引用面全 null（含 Culture——随当前文化）。
        var field = await renderer.MountAsync<FormFieldTimeInput>(
            new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Value);
        Assert.Null(field.ValueExpression);
        Assert.Null(field.Culture);
        Assert.Null(field.Format);
        Assert.Null(field.MinTime);
        Assert.Null(field.MaxTime);
        Assert.Equal(1, field.MinuteStep);
        Assert.False(field.ShowSeconds);
        Assert.False(field.ShowPicker);
        Assert.False(field.ReadOnly);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Null(field.Name);
        Assert.Null(field.InputClass);
        // 未传 AriaLabel 时组名回落底座 Localizer 英文缺省词条（TimeInputTests 先例）。
        Assert.DoesNotContain("aria-required=\"true\"", renderer.Markup());
        Assert.Contains("aria-label=\"Time\"", renderer.Markup());
        Assert.DoesNotContain("aria-label=\"Open time picker\"", renderer.Markup());
    }
}
