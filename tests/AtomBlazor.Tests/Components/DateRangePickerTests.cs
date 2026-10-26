using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// DateRangePicker 测试：触发按钮占位/格式化范围文本（FormatRange 随 DateFormat 与文化）、
/// 弹层内联双月历（自绘表格而非 Calendar 组件）与月份导航双月同步、草稿选区三段沿
/// （首点定起点、次点完成并按 MinDays/MaxDays 校验、逆序自动对调）、Apply 写回并关层、
/// AutoApply/ShowButtons=false 自动应用关层、Clear 清草稿发 null 但不关层、快捷预设
/// （内置 Today 高亮回显、原生 select 经 ApplyPreset 沿、自定义 Custom(label, factory) 条目）、
/// MinDate/MaxDate/DisabledDates 三源禁选与 CountSelectedDays 慢路径拒绝预设、DayTemplate
/// 携 IsInRange/IsSelected 上下文、ShowTwoMonths/ShowPresets/MonthNames 开关面、外部 Value
/// 同步守卫（record 等值不覆盖草稿的 bug-for-bug 沿）、附加属性 splat、Disabled 叠类。
/// headless 限制：弹层关闭后 FloatingPortal 内容滞留 DOM，改断触发器 aria-expanded
/// 打开态门户根先遍历——onclick 序为预设钮 0..P-1、月导航（双月 3 枚/单月 2 枚）续排、
/// 日格自 P+navs 起（月内日 d 即第 d-1 枚，空白格无按钮）、清/ apply 收尾、触发器最后
/// 原生 select 走 @onchange 派发。快照含 popover-N 与 select-短id，先经正则归一化
/// 打开态快照含 zh-CN 机器区域性月名/周一起算面，随本仓基线据实入册（// 同口径）。
/// </summary>
public class DateRangePickerTests
{
    /// <summary>归一化 popover-N 与 popover-N-trigger/content、NativeSelect 自增短 id，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        ScrubToday(
            Regex.Replace(
                Regex.Replace(markup, "popover-[0-9]+", "popover-<n>"),
                "select-[0-9a-f]{8}", "select-<id>"));

    /// <summary>
    /// “今天”格（data-today）的高亮随系统日期漂移，使结构快照无法复现。
    /// 将其退化为普通越界日（dayDefault）样式并从快照中移除 data-today 标记，使快照与运行日无关。
    /// </summary>
    private static string ScrubToday(string markup) =>
        Regex.Replace(
            markup,
            @"<button\b[^>]*\bdata-today\b[^>]*>",
            m => NormalizeTodayButton(m.Value));

    private static string NormalizeTodayButton(string tag)
    {
        var cls = Regex.Match(tag, @"class=""([^""]*)""").Groups[1].Value;
        var normalized = cls.Replace(
            "atom:bg-accent atom:text-accent-foreground",
            "atom:hover:bg-accent atom:hover:text-accent-foreground");
        return tag
            .Replace($"class=\"{cls}\"", $"class=\"{normalized}\"")
            .Replace("data-today=\"\"", string.Empty)
            .Replace("data-today", string.Empty);
    }

    /// <summary>构造带基元服务的渲染器（与 DateTimePickerTests 同构，弹层需要门户/定位/焦点服务）。</summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
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
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>排空渲染队列。</summary>
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>挂载 PortalHost（根 0）+ DateRangePicker（根 1），使弹层门户内容可进入渲染树遍历。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, DateRangePicker Picker)> MountAsync(
        Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var picker = await renderer.MountAsync<DateRangePicker>(extra);
        await SettleAsync(renderer);
        return (renderer, js, picker);
    }

    /// <summary>强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树（照 ColorPickerTests 先例）。</summary>
    private static async Task RerenderPortalAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer, rounds: 1);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer, rounds: 1);
    }

    /// <summary>触发一次 DOM 事件（沿 CascaderTests 先例须在 Dispatcher 上派发）。</summary>
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

    /// <summary>全部日格按钮（按渲染序：日历一整月后接日历二整月；空白格无按钮）。</summary>
    private static List<Match> DayButtons(string markup) =>
        Regex.Matches(markup, "<button[^>]*data-drp-day-btn[^>]*>.*?</button>", RegexOptions.Singleline)
            .Cast<Match>().ToList();

    /// <summary>第一月（_displayMonth1）内日 d 的 onclick 序位：预设钮 + 月一头部 2 枚导航钮 + d-1
    /// （双月与单月面月一头部皆恰 2 枚 button；月二的下月钮夹在两月日格之间）。</summary>
    private static int Cal1DayOcc(string markup, int day) =>
        Regex.Count(markup, "data-drp-preset-btn") + 2 + (day - 1);

    /// <summary>底部操作钮 onclick 序位：全部日格 +（双月时月二头部 1 枚导航钮）之后，Clear 先、Apply 后。</summary>
    private static int FooterOcc(string markup, bool clear, bool twoMonths = true) =>
        Regex.Count(markup, "data-drp-preset-btn") + 2
        + Regex.Count(markup, "data-drp-day-btn") + (twoMonths ? 1 : 0) + (clear ? 0 : 1);

    private static DateRange Range(int startDay, int endDay) =>
        new(new DateTime(2026, 10, startDay), new DateTime(2026, 10, endDay));

    /// <summary>文化无关月名（CA1861：static readonly 复用，不断言机器区域性面）。</summary>
    private static readonly string[] fullMonthNames =
    [
        "Januar", "Februar", "Mars", "April", "Maj", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    private static readonly string[] shortMonthNames =
    [
        "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec",
    ];

    /// <summary>周日起排的 7 个单字星期名（CustomDayNames 面）。</summary>
    private static readonly string[] sundayFirstDayNames = ["S", "M", "T", "W", "T", "F", "S"];

    [Fact]
    public async Task 缺省闭合态应显本地化占位与日历钮且弹层未入树()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.AriaLabel)] = "Vacation range",
        });
        var markup = renderer.Markup();

        // 占位沿：Placeholder 未设取 Localizer["DateRangePicker.Placeholder"]。
        Assert.Contains("Select date range", markup);
        Assert.Contains("atom:w-[300px]", markup);
        Assert.DoesNotContain("data-drp", markup); // 弹层未渲染
        var button = ButtonMarkup(markup, "Select date range");
        Assert.Contains("aria-label=\"Vacation range\"", button);
        Assert.Contains("aria-expanded=\"false\"", button);
    }

    [Fact]
    public async Task 有值触发按钮应显格式化范围文本且摘除占位灰字()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.Value)] = Range(1, 12),
            [nameof(DateRangePicker.DateFormat)] = "yyyy-MM-dd",
        });
        var markup = renderer.Markup();

        Assert.Contains("2026-10-01 - 2026-10-12", markup);
        // 有值时 ButtonCssClass 不再叠加 atom:text-muted-foreground（闭合态唯一携带者即弹层灰字，此处无弹层）。
        Assert.DoesNotContain("atom:text-muted-foreground", markup);
    }

    [Fact]
    public async Task 首点日格应定草稿起点显选止提示且Apply禁用()
    {
        var reports = new List<DateRange?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 5));
        await RerenderPortalAsync(renderer);
        markup = renderer.Markup();

        Assert.Contains("Select end date", markup);
        Assert.Empty(reports);
        Assert.Null(picker.Value);
        Assert.Matches(" disabled[ >]", ButtonMarkup(markup, "Apply"));
        Assert.DoesNotMatch(" disabled[ >]", ButtonMarkup(markup, "Clear"));
        // 草稿起点即高亮端点（IsRangeStart 单点沿）且浮层不关。
        Assert.Contains("atom:bg-primary", DayButtons(markup)[4].Value);
        Assert.Contains("aria-expanded=\"true\"",
            Regex.Match(markup, """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);
    }

    [Fact]
    public async Task 次点逆序应自动对调并显天数与起止圆角()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 20));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 8));
        await RerenderPortalAsync(renderer);
        markup = renderer.Markup();

        // 对调后范围 10-08..10-20 共 13 天（含首尾）。
        Assert.Contains("13 day(s) selected", markup);
        var days = DayButtons(markup);
        Assert.Contains("atom:bg-primary", days[7].Value);  // 10-08 起点
        Assert.Contains("atom:bg-primary", days[19].Value); // 10-20 终点
        // 端点格圆角方向：起点 rounded-s-md、终点 rounded-e-md（范围中间格 atom:bg-accent）。
        Assert.Contains("atom:rounded-s-md", markup);
        Assert.Contains("atom:rounded-e-md", markup);
        Assert.Contains("atom:bg-accent", days[10].Value); // 10-11 范围内
    }

    [Fact]
    public async Task 点Apply应写回范围回调并关闭浮层()
    {
        var reports = new List<DateRange?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DateRangePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 8));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 20));
        await RerenderPortalAsync(renderer);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: FooterOcc(renderer.Markup(), clear: false));
        await RerenderPortalAsync(renderer);

        Assert.Single(reports);
        Assert.Equal(new DateRange(new DateTime(2026, 10, 8), new DateTime(2026, 10, 20)), picker.Value);
        Assert.Contains("2026-10-08 - 2026-10-20", renderer.Markup()); // FormatRange 随钉死的 DateFormat 档
        // Apply 关层沿：触发器 aria-expanded=false。
        Assert.Contains("aria-expanded=\"false\"",
            Regex.Match(renderer.Markup(), """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);
    }

    [Fact]
    public async Task AutoApply应在范围完成即回调并关层()
    {
        var reports = new List<DateRange?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.AutoApply)] = true,
            [nameof(DateRangePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 1));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 10));
        await RerenderPortalAsync(renderer);

        Assert.Single(reports);
        Assert.Equal(new DateRange(new DateTime(2026, 10, 1), new DateTime(2026, 10, 10)), picker.Value);
        Assert.Contains("aria-expanded=\"false\"",
            Regex.Match(renderer.Markup(), """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);
    }

    [Fact]
    public async Task 隐藏按钮面应无清汇钮且完成即自动应用()
    {
        var reports = new List<DateRange?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.ShowButtons)] = false,
            [nameof(DateRangePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();
        Assert.DoesNotContain("Clear", markup);
        Assert.DoesNotContain("Apply", markup);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 1));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 10));
        await RerenderPortalAsync(renderer);

        // ShowButtons=false 视同 AutoApply（注释沿）。
        Assert.Single(reports);
        Assert.Equal(new DateRange(new DateTime(2026, 10, 1), new DateTime(2026, 10, 10)), picker.Value);
        Assert.Contains("aria-expanded=\"false\"",
            Regex.Match(renderer.Markup(), """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);
    }

    [Fact]
    public async Task MinDays不足应重置起点于次点且补齐后方完成()
    {
        var reports = new List<DateRange?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.MinDays)] = 5,
            [nameof(DateRangePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 1));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 3));
        await RerenderPortalAsync(renderer);

        // 3 天 < MinDays=5 → 重置：起点落到次点 10-03，等待终点，零回调。
        Assert.Contains("Select end date", renderer.Markup());
        Assert.Empty(reports);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 8));
        await RerenderPortalAsync(renderer);
        Assert.Contains("6 day(s) selected", renderer.Markup()); // 10-03..10-08
        Assert.Empty(reports); // ShowButtons=true 且非 AutoApply → 不自动应用
    }

    [Fact]
    public async Task MaxDays超限应拒绝并重置起点于次点()
    {
        var reports = new List<DateRange?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.MaxDays)] = 3,
            [nameof(DateRangePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 1));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 10));
        await RerenderPortalAsync(renderer);

        // 10 天 > MaxDays=3 → 重置：起点落 10-10、终点清空、零回调。
        Assert.Contains("Select end date", renderer.Markup());
        Assert.DoesNotContain("day(s) selected", renderer.Markup());
        Assert.Empty(reports);
    }

    [Fact]
    public async Task 内置Today预设应填草稿并回显选中态()
    {
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer);

        // 预设钮占 onclick 序 0..5（默认 6 枚），Today 为第 0 枚。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await RerenderPortalAsync(renderer);
        var markup = renderer.Markup();

        Assert.Contains("1 day(s) selected", markup);
        Assert.Null(picker.Value); // 草稿沿：未 Apply 不外发
        // 选中回显：Today 预设钮叠高亮类，原生 select 对齐到索引 0。
        Assert.Contains("atom:bg-primary", ButtonMarkup(markup, "Today"));
        Assert.Matches("""select[^>]*value="0"[^>]*>""", markup);
    }

    [Fact]
    public async Task 原生select变更应经ApplyPreset沿落草稿()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer);

        // 唯一 @onchange 携带者即预设原生 select；"2" → Last7Days。
        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "2" });
        await RerenderPortalAsync(renderer);
        var markup = renderer.Markup();

        Assert.Contains("7 day(s) selected", markup);
        Assert.Contains("atom:bg-primary", ButtonMarkup(markup, "Last 7 days"));
        Assert.Matches("""select[^>]*value="2"[^>]*>""", markup);
    }

    [Fact]
    public async Task 自定义预设应取Custom标签与工厂范围()
    {
        var presets = new List<DateRangeQuickPick>
        {
            DateRangeQuickPick.Custom("Q4 FY26", () => new DateRange(new DateTime(2026, 10, 1), new DateTime(2026, 12, 31))),
        };
        var reports = new List<DateRange?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.Presets)] = presets,
            [nameof(DateRangePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await RerenderPortalAsync(renderer);
        var markup = renderer.Markup();

        Assert.Contains("Q4 FY26", markup); // option 与预设钮两处同名
        Assert.Contains("92 day(s) selected", markup);
        Assert.Empty(reports);
    }

    [Fact]
    public async Task 无预设面应显自绘月历且月名可CustomDayNames定制()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.ShowPresets)] = false,
            [nameof(DateRangePicker.MonthNames)] = shortMonthNames,
            [nameof(DateRangePicker.CustomDayNames)] = sundayFirstDayNames,
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        Assert.DoesNotContain("data-drp-presets", markup);
        Assert.Contains("Oct 2026", markup);
        Assert.Contains("Nov 2026", markup);
        // 自绘表格而非 Calendar 组件：无 role=grid 与日历日格 GUID id 形态。
        Assert.DoesNotContain("role=\"grid\"", markup);
        Assert.DoesNotContain("-day-20", markup);
    }

    [Fact]
    public async Task 月导航应双月同步移页且缓存随迁()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.MonthNames)] = fullMonthNames,
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        // 预设 6 枚后续排：上月=6、日历一移动端下月=7、日历二下月=8。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 6); // PreviousMonth
        await RerenderPortalAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains("September 2026", markup);
        Assert.Contains("October 2026", markup);
        // 双月格数随迁：Sep 30 + Oct 31。
        Assert.Equal(61, DayButtons(markup).Count);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 7); // NextMonth（日历一头部的移动端钮）
        await RerenderPortalAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains("October 2026", markup);
        Assert.Contains("November 2026", markup);
    }

    [Fact]
    public async Task DisabledDates应灰化日格并在处理器内拒绝点击()
    {
        var reports = new List<DateRange?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.DisabledDates)] = (Func<DateTime, bool>)(d => d.Day % 2 == 0),
            [nameof(DateRangePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        // headless 直派 handler：10-02 被 SelectDate 的 IsDateDisabled 守卫吞掉，草稿不动。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 2));
        await RerenderPortalAsync(renderer);
        Assert.DoesNotContain("Select end date", renderer.Markup());
        Assert.Empty(reports);

        var second = DayButtons(renderer.Markup())[1].Value;
        Assert.Matches(" disabled[ >]", second);
        Assert.Contains("atom:text-muted-foreground atom:opacity-50", second);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 3));
        await RerenderPortalAsync(renderer);
        Assert.Contains("Select end date", renderer.Markup()); // 奇数日可选
    }

    [Fact]
    public async Task MinDate界外应禁选且慢路径使整段禁用预设被拒()
    {
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.MinDate)] = DateTime.Today.AddDays(5),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        // 今天格越 MinDate：按钮 disabled 且点击被吞（headless 直派亦被 IsDateDisabled 拒）。
        var todayIdx = DateTime.Today.Day - 1;
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, DateTime.Today.Day));
        await RerenderPortalAsync(renderer);
        Assert.DoesNotContain("Select end date", renderer.Markup());
        Assert.Matches(" disabled[ >]", DayButtons(renderer.Markup())[todayIdx].Value);

        // CountSelectedDays 慢路径：Today 预设整段禁用 → 天数 0 → ApplyPreset 拒绝，草稿与 select 均不回填。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await RerenderPortalAsync(renderer);
        markup = renderer.Markup();
        Assert.DoesNotContain("day(s) selected", markup);
        Assert.DoesNotMatch("""select[^>]*value="\d"[^>]*>""", markup);
        Assert.Null(picker.Value);
    }

    [Fact]
    public async Task DayTemplate应携范围上下文渲染()
    {
        RenderFragment<CalendarDayContext> template = ctx => builder =>
            builder.AddContent(0, ctx.IsSelected ? "S" : ctx.IsInRange ? "R" : "N");
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.DayTemplate)] = template,
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 1));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 3));
        await RerenderPortalAsync(renderer);

        var days = DayButtons(renderer.Markup());
        Assert.Equal("S", DayText(days[0].Value));  // 10-01 起点
        Assert.Equal("R", DayText(days[1].Value));  // 10-02 范围内
        Assert.Equal("S", DayText(days[2].Value));  // 10-03 终点
        Assert.Equal("N", DayText(days[3].Value));  // 10-04 范围外
    }

    /// <summary>抓取日按钮 inner 文本（trim 后），DayTemplate 断言用。</summary>
    private static string DayText(string buttonMarkup)
    {
        var gt = buttonMarkup.IndexOf('>') + 1;
        var lt = buttonMarkup.LastIndexOf('<');
        return buttonMarkup[gt..lt].Trim();
    }

    [Fact]
    public async Task 单月面应显240px触发钮且导航仅两枚()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.ShowTwoMonths)] = false,
            [nameof(DateRangePicker.ShowPresets)] = false,
            [nameof(DateRangePicker.MonthNames)] = shortMonthNames,
        });
        Assert.Contains("atom:w-[240px]", ButtonMarkup(renderer.Markup(), "Select date range"));
        Assert.DoesNotContain("data-drp-calendar-2", renderer.Markup());

        await OpenAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("Oct 2026", markup);
        Assert.DoesNotContain("Nov 2026", markup);

        // 无预设单月：上月=0、下月=1、日格自 2 起。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await RerenderPortalAsync(renderer);
        Assert.Contains("Nov 2026", renderer.Markup());
        Assert.Equal(30, DayButtons(renderer.Markup()).Count); // 单月仅 Nov 30 日
    }

    [Fact]
    public async Task 外部Value变更应同步草稿而等值重推不覆盖草稿编辑()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.Value)] = new DateRange(new DateTime(2026, 11, 3), new DateTime(2026, 11, 10)),
        };
        var (renderer, _, _) = await MountAsync(parameters);
        await OpenAsync(renderer);
        Assert.Contains("8 day(s) selected", renderer.Markup()); // 初值同步入草稿

        // 草稿编辑：重推同一 Value（record 结构等值 → != _previousValue 不成立）不得覆盖草稿（沿）。
        var markup = renderer.Markup();
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 5)); // 新月 11-05 起新选
        await RerenderPortalAsync(renderer);
        await renderer.RerenderRootAsync(1, parameters); // 等值重推
        await RerenderPortalAsync(renderer);
        Assert.Contains("Select end date", renderer.Markup());

        // 异值外部变更：正常覆盖草稿并同步起止。
        parameters[nameof(DateRangePicker.Value)] =
            new DateRange(new DateTime(2026, 11, 20), new DateTime(2026, 11, 21));
        await renderer.RerenderRootAsync(1, parameters);
        await RerenderPortalAsync(renderer);
        Assert.Contains("2 day(s) selected", renderer.Markup());
    }

    [Fact]
    public async Task Clear应清草稿发null且不关闭浮层()
    {
        var reports = new List<DateRange?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateRange?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(markup, 1));
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: Cal1DayOcc(renderer.Markup(), 3));
        await RerenderPortalAsync(renderer);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: FooterOcc(renderer.Markup(), clear: true));
        await RerenderPortalAsync(renderer);
        markup = renderer.Markup();

        Assert.Null(reports.Last());
        Assert.Null(picker.Value);
        Assert.DoesNotContain("Select end date", markup);
        Assert.DoesNotContain("day(s) selected", markup);
        // 沿：Clear 不关层（对照 Apply 关层），仅末尾 StateHasChanged 按 BL0012 先例移除。
        Assert.Contains("aria-expanded=\"true\"",
            Regex.Match(markup, """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);
    }

    [Fact]
    public async Task 附加属性应透传到触发按钮()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.AriaLabel)] = "Vacation range",
            ["data-test"] = "drp-1",
        });
        Assert.Contains("data-test=\"drp-1\"",
            Regex.Match(renderer.Markup(), """<button[^>]*aria-label="Vacation range"[^>]*>""").Value);
    }

    [Fact]
    public async Task 禁用面应叠opacity与pointer事件遮罩()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.Disabled)] = true,
            [nameof(DateRangePicker.AriaLabel)] = "Vacation range",
        });
        var trigger = ButtonMarkup(renderer.Markup(), "Select date range");
        Assert.Matches(" disabled[ >]", trigger);
        Assert.Contains("atom:opacity-50 atom:pointer-events-none", trigger);
    }

    [Fact]
    public async Task 缺省闭合态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.AriaLabel)] = "Vacation range",
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 含预设侧栏与双月格打开态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateRangePicker.Value)] = Range(1, 25),
            [nameof(DateRangePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DateRangePicker.AriaLabel)] = "Vacation range",
        });
        await OpenAsync(renderer);
        await Verify(Scrub(renderer.Markup()));
    }
}
