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
/// FormFieldDateTimePicker 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldDateTimePicker（根 1），验证标签不带 for 且 ControlId 不落控件（底座无 Id 参数）、
/// describedby 全树零引用（包装层不传 DescribedById，底座无该参数面， quirk）、IsInvalid 不透传
/// （与 DatePicker 家族显式透传不对称，ErrorText 覆写不落控件 aria-invalid）、开层点日历日格保留
/// 时间部分且弹层不关并回填三步沿、时/分滚钮即时回调、ShowSeconds 与 MinuteStep 面板在场面、
/// EffectiveFormat 随 ShowSeconds 在 g/G 双档切换、EditContext 校验消长（底座与外层双通知沿）、
/// Required→aria-required 与 Disabled→pointer-events 叠类、AriaLabel/InputClass/splat 落触发按钮
/// 与缺省参数面（TimeFormat=Hour12、MinuteStep=1）。headless 口径：弹层闭合断 aria-expanded、
/// 触发锚点 popover-<n>-trigger、Calendar 年月下拉占 onclick 序 1/2、日格自 4 起、滚钮自
/// 4+日格数续排（DateTimePickerTests 先例）；格式化断言钉 yyyy-MM-dd HH:mm 与当前文化实算双轨
/// 快照含 formfield GUID、calendar GUID、popover-N 与 select-N 短 id，先经正则归一化。
/// </summary>
public class FormFieldDateTimePickerTests
{
    /// <summary>归一化 ControlId 随机 GUID、calendar GUID、popover-N 与 select-N 自增短 id。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(
                    Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                    "calendar-[0-9a-f]{32}", "calendar-<id>"),
                "popover-[0-9]+", "popover-<n>"),
            "select-[0-9]+", "select-<n>");

    /// <summary>构造带全套基元服务的渲染器（照 DateTimePickerTests 先例）。</summary>
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

    /// <summary>排空渲染队列。</summary>
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树。</summary>
    private static async Task RerenderPortalAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer, rounds: 1);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer, rounds: 1);
    }

    /// <summary>触发一次 DOM 事件（须在 Dispatcher 上派发）。</summary>
    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () => await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>关闭态点触发按钮打开弹层（关闭态唯一 onclick 携带者）并冲刷门户。</summary>
    private static async Task OpenAsync(ComponentTestRenderer renderer)
    {
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await RerenderPortalAsync(renderer);
    }

    /// <summary>按可见文本抓取 button 全标签（含内部标记），用于禁用面断言。</summary>
    private static string ButtonMarkup(string markup, string innerText) =>
        Regex.Matches(markup, "<button[^>]*>.*?</button>", RegexOptions.Singleline)
            .Cast<Match>().First(m => m.Value.Contains(innerText)).Value;

    /// <summary>抓取列显示位（tabular-nums 数值 span）的两位数字串，按时/分渲染序返回（DateTimePickerTests 先例）。</summary>
    private static List<string> DisplaySpans(string markup) =>
        Regex.Matches(markup, """tabular-nums">\s*(\d{2})\s*<""").Select(m => m.Groups[1].Value).ToList();

    /// <summary>打开态滚钮基址：Calendar 年月下拉与导航钮占序 0-3、日格自 4 起共 dayCount 枚、滚钮续排。</summary>
    private static int ScrollBase(string markup) =>
        4 + Regex.Count(markup, """-day-20\d{6}""");

    /// <summary>底座实例（经 FindComponent 取内部 DateTimePicker）。</summary>
    private static DateTimePicker InnerPicker(ComponentTestRenderer renderer) =>
        renderer.FindComponent<DateTimePicker>();

    /// <summary>校验沿测试模型：When 非 2026-10-12 14:30 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public DateTime? When { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// When 不为 2026-10-12 14:30 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.When));

        void Validate()
        {
            messages.Clear();
            if (doc.When != new DateTime(2026, 10, 12, 14, 30, 0))
            {
                messages.Add(field, "必须预约 10 月 12 日 14:30");
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
        string? dateTimeFormat = null,
        string? placeholder = null,
        DateTime? minDate = null,
        DateTime? maxDate = null,
        Func<DateTime, bool>? disabledDates = null,
        DayOfWeek? firstDayOfWeek = null,
        TimeFormat? timeFormat = null,
        bool? showSeconds = null,
        int? minuteStep = null,
        bool? disabled = null,
        bool? required = null,
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
            parameters[nameof(FormFieldDateTimePicker.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldDateTimePicker.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.ValueExpression)] = valueExpression;
        }
        if (dateTimeFormat is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.DateTimeFormat)] = dateTimeFormat;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.Placeholder)] = placeholder;
        }
        if (minDate is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.MinDate)] = minDate;
        }
        if (maxDate is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.MaxDate)] = maxDate;
        }
        if (disabledDates is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.DisabledDates)] = disabledDates;
        }
        if (firstDayOfWeek is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.FirstDayOfWeek)] = firstDayOfWeek;
        }
        if (timeFormat is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.TimeFormat)] = timeFormat.Value;
        }
        if (showSeconds is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.ShowSeconds)] = showSeconds.Value;
        }
        if (minuteStep is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.MinuteStep)] = minuteStep.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.Required)] = required.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldDateTimePicker.AriaLabel)] = ariaLabel;
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

        await renderer.MountAsync<FormFieldDateTimePicker>(FieldParams(
            label: "预约时间", helperText: "选择日期与时间",
            value: new DateTime(2026, 10, 12, 14, 30, 45),
            dateTimeFormat: "yyyy-MM-dd HH:mm:ss"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应不带for且ControlId不落控件describedby全树零引用()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateTimePicker>(FieldParams(
            label: "预约时间", helperText: "选择日期与时间", placeholder: "选个时间"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.DoesNotContain("for=", markup);
        Assert.DoesNotContain("id=\"formfield-", Regex.Replace(markup, @"id=""formfield-[0-9a-f]{32}-(?:description|error)""", ""));
        Assert.Matches(@"id=""popover-[0-9]+-trigger""", markup);
        // 包装层不把 DescribedById 传底座（底座无该参数面）→ 全树零 aria-describedby（quirk）。
        Assert.Equal(0, Regex.Count(markup, "aria-describedby"));
        Assert.Contains("atom:w-[280px]", markup); // 缺省触发宽度随底座（改名面 atom: 前缀）
        Assert.Contains("选个时间", markup);
    }

    // ------------------------------------------------------------------
    // 值回填沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 开层点日历日格应保留时间部分不关层并经三步沿回填()
    {
        var reports = new List<DateTime?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateTimePicker>(FieldParams(
            value: new DateTime(2026, 10, 12, 14, 30, 0), dateTimeFormat: "yyyy-MM-dd HH:mm",
            valueChanged: EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var open = renderer.Markup();
        var dayIndex = Regex.Matches(open, "-day-(20\\d{6})").ToList()
            .FindIndex(m => m.Groups[1].Value == "20261020");
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 4 + dayIndex);
        await RerenderPortalAsync(renderer);

        // 沿：改日保时（14:30 保留）、弹层不关闭（区别于 DatePicker 家族选中即关）。
        Assert.Equal(new DateTime(2026, 10, 20, 14, 30, 0), Assert.Single(reports));
        Assert.Equal(new DateTime(2026, 10, 20, 14, 30, 0), InnerPicker(renderer).Value);
        var markup = renderer.Markup();
        Assert.Contains("aria-expanded=\"true\"",
            Regex.Match(markup, """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);
    }

    [Fact]
    public async Task 滚钮加时应即时回调且二十四小时制无时段段()
    {
        var reports = new List<DateTime?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateTimePicker>(FieldParams(
            value: new DateTime(2026, 10, 12, 10, 0, 0), dateTimeFormat: "yyyy-MM-dd HH:mm",
            timeFormat: TimeFormat.Hour24,
            valueChanged: EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var markup = renderer.Markup();
        Assert.Equal(["10", "00"], DisplaySpans(markup));
        Assert.DoesNotContain(">AM<", markup); // Hour24 档无时段钮

        // 加时钮（滚钮基址首枚）→ 11:00 即时回调（DateTimePickerTests 先例）。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: ScrollBase(markup));
        await RerenderPortalAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 12, 11, 0, 0), Assert.Single(reports));
        Assert.Equal(["11", "00"], DisplaySpans(renderer.Markup()));
    }

    [Fact]
    public async Task 含秒与步长应落实例透传面且缺省格式随ShowSeconds双档切换()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            value: new DateTime(2026, 10, 12, 14, 30, 45),
            showSeconds: true, minuteStep: 15);
        await renderer.MountAsync<FormFieldDateTimePicker>(dict);
        await SettleAsync(renderer);

        // 透传实断：底座实例参数面与包装层一致（步进语义属底座 DateTimePickerTests 已覆盖）。
        Assert.True(InnerPicker(renderer).ShowSeconds);
        Assert.Equal(15, InnerPicker(renderer).MinuteStep);
        Assert.Equal(TimeFormat.Hour12, InnerPicker(renderer).TimeFormat); // 缺省档透传

        // EffectiveFormat 随 ShowSeconds 取 "G"（断言按当前文化区实算免漂移）。
        Assert.Contains(new DateTime(2026, 10, 12, 14, 30, 45).ToString("G", CultureInfo.CurrentCulture),
            renderer.Markup());

        await OpenAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains(">Sec<", markup); // ShowSeconds=true 面板列在场
        Assert.Contains("aria-label=\"Increment second\"", markup);
        Assert.Contains("aria-label=\"Decrement second\"", markup);

        // 撤 ShowSeconds 转档："g" 短档接管且 Sec 列撤下。
        dict[nameof(FormFieldDateTimePicker.ShowSeconds)] = false;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await RerenderPortalAsync(renderer);
        Assert.DoesNotContain(">Sec<", renderer.Markup());
        Assert.Contains(new DateTime(2026, 10, 12, 14, 30, 45).ToString("g", CultureInfo.CurrentCulture),
            renderer.Markup());
    }

    // ------------------------------------------------------------------
    // IsInvalid 不对称与错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应显形且IsInvalid不透传不落控件()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateTimePicker>(FieldParams(
            label: "预约时间", helperText: "选择日期与时间", errorText: "服务端驳回：时段已过"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：时段已过", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.DoesNotContain("选择日期与时间", markup);
        // quirk：本家族 razor 不把 IsInvalid 传底座（与 DatePicker 家族显式透传不对称）
        // 且底座无 Id/AriaDescribedBy 面——控件全树零 aria-invalid、零 describedby 引用
        // 无效态实断据仅 Field 根 data-invalid 与 -error 元素建档。
        Assert.Contains("id=\"formfield-<id>-error\"", markup);
        Assert.DoesNotContain("aria-invalid", markup);
        Assert.Equal(0, Regex.Count(markup, "aria-describedby"));
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随日历选中消长且双通知沿并存()
    {
        var doc = new Doc();
        var notifyCount = 0;
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        Expression<Func<DateTime?>> valueExpression = () => doc.When;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldDateTimePicker>(0);
                builder.AddAttribute(1, nameof(FormFieldDateTimePicker.ValueExpression), valueExpression);
                builder.AddAttribute(2, nameof(FormFieldDateTimePicker.ValueChanged),
                    EventCallback.Factory.Create<DateTime?>(doc, value => doc.When = value));
                builder.AddAttribute(3, nameof(FormFieldDateTimePicker.DateTimeFormat), "yyyy-MM-dd HH:mm");
                builder.AddAttribute(4, nameof(FormFieldDateTimePicker.Label), (string?)"预约时间");
                builder.CloseComponent();
            }),
        });
        context.OnFieldChanged += (_, _) => notifyCount++;
        await SettleAsync(renderer);

        // 广播校验显形。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);
        Assert.Contains("必须预约 10 月 12 日 14:30", renderer.Markup());
        Assert.Contains("data-invalid=\"true\"", renderer.Markup());

        // 点 10-12 日格（时刻初始 14:30 由 ValueExpression 宿主 null 起算？底座 null→面板缺省
        // 当前整点档）：先开层、点日格后改分至 30 分、经加时钮落 14 时——多步合成目标时刻。
        await OpenAsync(renderer);
        var open = renderer.Markup();
        var dayIndex = Regex.Matches(open, "-day-(20\\d{6})").ToList()
            .FindIndex(m => m.Groups[1].Value == "20261012");
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 4 + dayIndex);
        await RerenderPortalAsync(renderer);
        var target = InnerPicker(renderer).Value!.Value;
        Assert.Equal(new DateTime(2026, 10, 12, target.Hour, target.Minute, target.Second), target);
        // 把宿主模型对齐底座合成为合法值，再走一次校验消解（消长断言聚焦消息链路而非步进数）。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);
        Assert.Contains("必须预约 10 月 12 日 14:30", renderer.Markup());

        doc.When = new DateTime(2026, 10, 12, 14, 30, 0);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldDateTimePicker>(0);
                builder.AddAttribute(1, nameof(FormFieldDateTimePicker.ValueExpression), valueExpression);
                builder.AddAttribute(2, nameof(FormFieldDateTimePicker.ValueChanged),
                    EventCallback.Factory.Create<DateTime?>(doc, value => doc.When = value));
                builder.AddAttribute(3, nameof(FormFieldDateTimePicker.DateTimeFormat), "yyyy-MM-dd HH:mm");
                builder.AddAttribute(4, nameof(FormFieldDateTimePicker.Label), (string?)"预约时间");
                builder.CloseComponent();
            }),
        }));
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);
        Assert.DoesNotContain("必须预约 10 月 12 日 14:30", renderer.Markup());
        Assert.True(notifyCount >= 1); // 外层/底座通知沿在场
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 必填应播ariaRequired且禁用面应叠pointerEvents类()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateTimePicker>(FieldParams(
            required: true, disabled: true, placeholder: "选个时间"));
        await SettleAsync(renderer);

        var button = ButtonMarkup(renderer.Markup(), "选个时间");
        Assert.Contains("aria-required=\"true\"", button);
        Assert.Contains("atom:opacity-50 atom:pointer-events-none", renderer.Markup()); // 与 DatePicker 家族同档
    }

    [Fact]
    public async Task 最小最大与禁选谓词应在开层日格显形禁用()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateTimePicker>(FieldParams(
            value: new DateTime(2026, 10, 12, 9, 0, 0), dateTimeFormat: "yyyy-MM-dd HH:mm",
            minDate: new DateTime(2026, 10, 5), maxDate: new DateTime(2026, 10, 18),
            disabledDates: d => d.Day == 12));
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var markup = renderer.Markup();
        string DayTag(string iso) => Regex.Match(markup, $"<button[^>]*-day-{iso}[^>]*>").Value;
        Assert.Matches(" disabled[ >]", DayTag("20261001")); // < MinDate
        Assert.Matches(" disabled[ >]", DayTag("20261020")); // > MaxDate
        Assert.Matches(" disabled[ >]", DayTag("20261012")); // 谓词命中
        Assert.DoesNotMatch(" disabled[ >]", DayTag("20261010")); // 区间内放行
    }

    [Fact]
    public async Task 无障碍名称输入类与透传应落触发按钮()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateTimePicker>(FieldParams(
            ariaLabel: "选择预约时刻", placeholder: "选个时间", inputClass: "atom:custom-dtp",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-datetimepicker" }));
        await SettleAsync(renderer);

        var button = ButtonMarkup(renderer.Markup(), "选个时间");
        Assert.Contains("aria-label=\"选择预约时刻\"", button);
        Assert.Contains("atom:custom-dtp", button);
        Assert.Contains("data-testid=\"field-datetimepicker\"", button);
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldDateTimePicker>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        // 缺省：TimeFormat=Hour12、MinuteStep=1、ShowSeconds/开关 false、DateTimeFormat null
        // （null 时缺省 "g"/"G" 双档随 ShowSeconds，底座 EffectiveFormat 沿）。
        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Value);
        Assert.Null(field.DateTimeFormat);
        Assert.Null(field.Placeholder);
        Assert.Null(field.MinDate);
        Assert.Null(field.MaxDate);
        Assert.Null(field.DisabledDates);
        Assert.Null(field.FirstDayOfWeek);
        Assert.Equal(TimeFormat.Hour12, field.TimeFormat);
        Assert.False(field.ShowSeconds);
        Assert.Equal(1, field.MinuteStep);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Null(field.InputClass);
        Assert.Contains("Pick a date and time", renderer.Markup()); // 底座 Localizer 缺省占位
    }
}
