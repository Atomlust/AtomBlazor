using AtomBlazor.Primitives;
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
/// FormFieldDateRangePicker 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldDateRangePicker（根 1），验证标签不带 for 且 ControlId 不落控件（底座无 Id/AriaDescribedBy
/// 参数面，describedby 全树零引用， quirk）、草稿选区三段沿与 Apply 写回关层、AutoApply 与
/// ShowButtons=false 视同自动应用（注释沿）、MinDays 不足重置起点拒完成、自定义 Presets 与
/// ShowPresets 开关面、EditContext 经包装层 ValueExpression 消长（底座无 EditContext 集成——
/// 本家族仅外层单通知，与其余四家族双发不对称， quirk）、Disabled 落原生 disabled（非
/// pointer-events 叠类档，与 DatePicker/DateTimePicker 家族不对称）、AriaLabel/InputClass/splat
/// 落触发按钮、缺省三开关面（ShowTwoMonths/ShowPresets/ShowButtons true、AutoApply false）。
/// headless 口径：弹层闭合断 aria-expanded、触发锚点 popover-<n>-trigger、门户重渲染入树
/// 打开态 onclick 序照 DateRangePickerTests：预设钮 0..P-1、月一头部 2 枚导航续排、
/// 日格自 P+2 起、清/汇钮收尾；格式化断言钉 yyyy-MM-dd 免机器区域性漂移
/// 快照含 formfield GUID、popover-N 与 select-N 短 id，先经正则归一化。
/// </summary>
public class FormFieldDateRangePickerTests
{
    /// <summary>归一化 ControlId 随机 GUID、popover-N 与 NativeSelect 自增短 id（DateRangePickerTests 口径）。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                "popover-[0-9]+", "popover-<n>"),
            "select-[0-9a-f]{8}", "select-<id>");

    /// <summary>构造带全套基元服务的渲染器（照 DateRangePickerTests 先例）。</summary>
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

    /// <summary>第一月内日 d 的 onclick 序位（DateRangePickerTests 先例公式）。</summary>
    private static int Cal1DayOcc(string markup, int day) =>
        Regex.Count(markup, "data-drp-preset-btn") + 2 + (day - 1);

    /// <summary>底部操作钮 onclick 序位：Clear 先、Apply 后。</summary>
    private static int FooterOcc(string markup, bool clear, bool twoMonths = true) =>
        Regex.Count(markup, "data-drp-preset-btn") + 2
        + Regex.Count(markup, "data-drp-day-btn") + (twoMonths ? 1 : 0) + (clear ? 0 : 1);

    /// <summary>日号 d 的日格按钮出现次数（自绘月历无 -day-yyyyMMdd id，取 data-drp-day-btn 文本哨兵
    /// 双月档 10/11 月各显一次同号，单月档仅 10 月——DateRangePickerTests 先例 424 行证 -day- 形不存在）。</summary>
    private static int DayButtonCount(string markup, int day) =>
        Regex.Count(markup, $@"<button[^>]*data-drp-day-btn[^>]*>\s*{day}\s*</button>");

    private static DateRange Range(int startDay, int endDay) =>
        new(new DateTime(2026, 10, startDay), new DateTime(2026, 10, endDay));

    /// <summary>校验沿测试模型：Range 未覆盖 10 月 15 日即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public DateRange? Range { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Range 未覆盖 2026-10-15 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Range));

        void Validate()
        {
            messages.Clear();
            if (doc.Range is not { } range || !range.Contains(new DateTime(2026, 10, 15)))
            {
                messages.Add(field, "必须覆盖 10 月 15 日");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        DateRange? value = null,
        EventCallback<DateRange?> valueChanged = default,
        Expression<Func<DateRange?>>? valueExpression = null,
        string? dateFormat = null,
        string? placeholder = null,
        DateTime? minDate = null,
        DateTime? maxDate = null,
        Func<DateTime, bool>? disabledDates = null,
        int? minDays = null,
        int? maxDays = null,
        bool? showTwoMonths = null,
        bool? showPresets = null,
        IReadOnlyList<DateRangeQuickPick>? presets = null,
        bool? showButtons = null,
        bool? autoApply = null,
        DayOfWeek? firstDayOfWeek = null,
        bool? disabled = null,
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
            parameters[nameof(FormFieldDateRangePicker.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldDateRangePicker.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.ValueExpression)] = valueExpression;
        }
        if (dateFormat is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.DateFormat)] = dateFormat;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.Placeholder)] = placeholder;
        }
        if (minDate is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.MinDate)] = minDate;
        }
        if (maxDate is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.MaxDate)] = maxDate;
        }
        if (disabledDates is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.DisabledDates)] = disabledDates;
        }
        if (minDays is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.MinDays)] = minDays;
        }
        if (maxDays is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.MaxDays)] = maxDays;
        }
        if (showTwoMonths is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.ShowTwoMonths)] = showTwoMonths.Value;
        }
        if (showPresets is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.ShowPresets)] = showPresets.Value;
        }
        if (presets is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.Presets)] = presets;
        }
        if (showButtons is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.ShowButtons)] = showButtons.Value;
        }
        if (autoApply is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.AutoApply)] = autoApply.Value;
        }
        if (firstDayOfWeek is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.FirstDayOfWeek)] = firstDayOfWeek;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.Disabled)] = disabled.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldDateRangePicker.AriaLabel)] = ariaLabel;
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

        await renderer.MountAsync<FormFieldDateRangePicker>(FieldParams(
            label: "休假区间", helperText: "选择起止日期", dateFormat: "yyyy-MM-dd",
            value: Range(1, 12)));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应不带for且ControlId不落控件describedby全树零引用()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateRangePicker>(FieldParams(
            label: "休假区间", helperText: "选择起止日期", placeholder: "选一段假期"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.DoesNotContain("for=", markup);
        // 裸 ControlId 仅现于 -description 后缀档（id 建档），控件面零痕迹。
        Assert.DoesNotContain("id=\"formfield-", Regex.Replace(markup, @"id=""formfield-[0-9a-f]{32}-(?:description|error)""", ""));
        Assert.Matches(@"id=""popover-[0-9]+-trigger""", markup);
        // 底座无 AriaDescribedBy 参数面且包装层不传 → 全树零 aria-describedby（quirk）。
        Assert.Equal(0, Regex.Count(markup, "aria-describedby"));
        // 缺省触发宽度类随底座 ButtonCssClass 在场（改名面 atom: 前缀）。
        Assert.Contains("atom:w-[300px]", markup);
        Assert.Contains("选一段假期", markup);
    }

    // ------------------------------------------------------------------
    // 草稿选区与应用沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 草稿选区应经Apply写回范围三步沿并关层刷新触发文本()
    {
        var reports = new List<DateRange?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            dateFormat: "yyyy-MM-dd", placeholder: "选一段假期",
            valueChanged: EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)));
        var field = await renderer.MountAsync<FormFieldDateRangePicker>(dict);
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var markup = renderer.Markup();

        // 首点定起点：显选止提示、Apply 禁用。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 8));
        await RerenderPortalAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains("Select end date", markup);
        Assert.Matches(" disabled[ >]", ButtonMarkup(markup, "Apply"));

        // 次点完成选区（10-08..10-20 共 13 天）。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 20));
        await RerenderPortalAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains("13 day(s) selected", markup);

        // Apply 写回：HandleValueChanged 三步沿 + 关层。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: FooterOcc(markup, clear: false));
        await RerenderPortalAsync(renderer);
        Assert.Equal(Range(8, 20), Assert.Single(reports));
        Assert.Equal(Range(8, 20), field.Value);
        Assert.Contains("aria-expanded=\"false\"",
            Regex.Match(renderer.Markup(), """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);

        // 受控回填：宿主更新 Value 重渲染触发按钮格式化范围文本。
        dict[nameof(FormFieldDateRangePicker.Value)] = reports[0];
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        Assert.Contains("2026-10-08 - 2026-10-20", renderer.Markup());
    }

    [Fact]
    public async Task AutoApply应在范围完成即回调并关层()
    {
        var reports = new List<DateRange?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateRangePicker>(FieldParams(
            autoApply: true, dateFormat: "yyyy-MM-dd",
            valueChanged: EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var markup = renderer.Markup();
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 1));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 10));
        await RerenderPortalAsync(renderer);

        Assert.Equal(Range(1, 10), Assert.Single(reports));
        Assert.Contains("aria-expanded=\"false\"",
            Regex.Match(renderer.Markup(), """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);
    }

    [Fact]
    public async Task ShowButtonsFalse应撤清汇钮并视同自动应用()
    {
        var reports = new List<DateRange?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateRangePicker>(FieldParams(
            showButtons: false,
            valueChanged: EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var markup = renderer.Markup();
        Assert.DoesNotContain("Clear", markup);
        Assert.DoesNotContain("Apply", markup);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 1));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 10));
        await RerenderPortalAsync(renderer);

        // 注释沿：ShowButtons=false 视同 AutoApply。
        Assert.Equal(Range(1, 10), Assert.Single(reports));
    }

    [Fact]
    public async Task MinDays不足应重置起点于次点且透传生效()
    {
        var reports = new List<DateRange?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateRangePicker>(FieldParams(
            minDays: 5,
            valueChanged: EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var markup = renderer.Markup();
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 1));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 3));
        await RerenderPortalAsync(renderer);

        // 3 天 < MinDays=5 → 重置：起点落到次点 10-03，等待终点，零回调（透传生效）。
        Assert.Contains("Select end date", renderer.Markup());
        Assert.Empty(reports);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 8));
        await RerenderPortalAsync(renderer);
        Assert.Contains("6 day(s) selected", renderer.Markup()); // 10-03..10-08
        Assert.Empty(reports); // ShowButtons=true 且非 AutoApply → 不自动应用
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随Apply写回消长且本家族仅外层单通知()
    {
        var doc = new Doc();
        var notifyCount = 0;
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        Expression<Func<DateRange?>> valueExpression = () => doc.Range;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldDateRangePicker>(0);
                builder.AddAttribute(1, nameof(FormFieldDateRangePicker.ValueExpression), valueExpression);
                builder.AddAttribute(2, nameof(FormFieldDateRangePicker.ValueChanged),
                    EventCallback.Factory.Create<DateRange?>(doc, value => doc.Range = value));
                builder.AddAttribute(3, nameof(FormFieldDateRangePicker.DateFormat), "yyyy-MM-dd");
                builder.AddAttribute(4, nameof(FormFieldDateRangePicker.Label), (string?)"休假区间");
                builder.CloseComponent();
            }),
        });
        context.OnFieldChanged += (_, _) => notifyCount++;
        await SettleAsync(renderer);

        // 广播校验显形 + describedby 切错误档（错误元素 id 在场但控件零引用，quirk）。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("必须覆盖 10 月 15 日", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Contains("id=\"formfield-", markup);
        Assert.Equal(0, Regex.Count(markup, "aria-describedby"));

        // 选 10-08..10-20 并 Apply → 外层 NotifyFieldChanged 单发沿（底座零 EditContext 集成）。
        await OpenAsync(renderer);
        markup = renderer.Markup();
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 8));
        await RerenderPortalAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 20));
        await RerenderPortalAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: FooterOcc(renderer.Markup(), clear: false));
        await SettleAsync(renderer);

        Assert.Equal(Range(8, 20), doc.Range);
        Assert.Equal(1, notifyCount); // 单通知（与其余四家族双发沿不对称，如此）
        Assert.DoesNotContain("必须覆盖 10 月 15 日", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 预设与开关面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 自定义预设应取Custom标签入钮且点选只填草稿不外发()
    {
        var reports = new List<DateRange?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateRangePicker>(FieldParams(
            presets: [DateRangeQuickPick.Custom("Q4 FY26", () => Range(1, 30))],
            valueChanged: EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("Q4 FY26", markup); // option 与预设钮两处同名
        Assert.Equal(1, Regex.Count(markup, "data-drp-preset-btn")); // 自定义集取代内置六枚

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0); // 预设钮 0..P-1 之第 0 枚
        await RerenderPortalAsync(renderer);

        Assert.Contains("30 day(s) selected", renderer.Markup()); // 草稿沿：未 Apply 不外发
        Assert.Empty(reports);
    }

    [Fact]
    public async Task 预设与双月开关应决定侧栏与月格在场面()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(placeholder: "选一段假期");
        await renderer.MountAsync<FormFieldDateRangePicker>(dict);
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        // 缺省双开：预设侧栏与双月日格在场（10/11 月各显日号 8 一次=2 枚）。
        Assert.Contains("data-drp-presets", renderer.Markup());
        Assert.Equal(2, DayButtonCount(renderer.Markup(), 8));

        // 双双关档：侧栏撤下、单月格。
        dict[nameof(FormFieldDateRangePicker.ShowPresets)] = false;
        dict[nameof(FormFieldDateRangePicker.ShowTwoMonths)] = false;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await RerenderPortalAsync(renderer);
        var markup = renderer.Markup();
        Assert.DoesNotContain("data-drp-presets", markup);
        Assert.Equal(0, Regex.Count(markup, "data-drp-preset-btn"));
        Assert.Equal(1, DayButtonCount(markup, 8)); // 单月档
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用应落原生disabled属性且无障碍名称输入类与透传落触发按钮()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDateRangePicker>(FieldParams(
            disabled: true, ariaLabel: "选择休假区间", placeholder: "选一段假期",
            inputClass: "atom:custom-range",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-daterangepicker" }));
        await SettleAsync(renderer);

        var button = ButtonMarkup(renderer.Markup(), "选一段假期");
        // quirk：本家族禁用面为原生 disabled 属性（与 DatePicker/DateTimePicker
        // 家族 pointer-events 叠类档不对称，照底座 Button Disabled="@Disabled" 实断）。
        Assert.Matches(" disabled[ >]", button);
        Assert.Contains("aria-label=\"选择休假区间\"", button);
        Assert.Contains("atom:custom-range", button);
        Assert.Contains("data-testid=\"field-daterangepicker\"", button);
        // 本家族无 Required 参数面（底座亦无 Required 参数）——全树零 aria-required。
        Assert.DoesNotContain("aria-required", renderer.Markup());
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldDateRangePicker>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        // 缺省：三开关 true、AutoApply false、DateFormat="d"、引用面全 null、Disabled false。
        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Value);
        Assert.Null(field.ValueExpression);
        Assert.Equal("d", field.DateFormat);
        Assert.Null(field.Placeholder);
        Assert.Null(field.MinDate);
        Assert.Null(field.MaxDate);
        Assert.Null(field.DisabledDates);
        Assert.Null(field.MinDays);
        Assert.Null(field.MaxDays);
        Assert.True(field.ShowTwoMonths);
        Assert.True(field.ShowPresets);
        Assert.Null(field.Presets);
        Assert.True(field.ShowButtons);
        Assert.False(field.AutoApply);
        Assert.Null(field.FirstDayOfWeek);
        Assert.False(field.Disabled);
        Assert.Null(field.InputClass);
        // 缺省占位随底座 Localizer 英文词条在场。
        Assert.Contains("Select date range", renderer.Markup());
    }
}
