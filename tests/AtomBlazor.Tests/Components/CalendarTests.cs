using AtomBlazor.Primitives;
using System.Reflection;
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
// 组件名 Calendar 与 System.Globalization.Calendar 同名（brief 预警的命名冲突），
// 不引 System.Globalization 命名空间，CultureInfo 一律全限定引用。
using Calendar = AtomBlazor.Calendar;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Calendar 测试：月历网格生成（5/6 周补齐、闰年/平年二月、外部日期显隐、
/// 周起点显式参数与当前区域性默认沿）、月份导航（prev/next 越年界；上游对导航按钮
/// 不设 MinDate/MaxDate 守卫，越界日期按上游实际面表现为网格内 disabled 而非按钮禁用）、
/// 选中回调（SelectedChanged 与 OnSelect 双沿、外部日期点击换月、禁用日期不回调）、
/// 今天高亮与焦点 tabindex、CalendarDayContext 语义（IsSelected/IsDisabled/IsOutsideMonth/
/// IsToday/IsInRange 恒 false）、键盘导航（方向键/Home/End/PageUp(±12)/PageDown、禁用跳过、
/// Min/Max 边界钳制、Enter/Space 选中、跨月自动换页）、Select 年月联动（反射直调
/// OnMonthChanged/OnYearChanged 与 GetYears 区间）、外部参数变更同步显示月份、
/// FocusActiveDayAsync/AutoFocus 的 elementUtils.focusElement interop 载荷、DisposeAsync。
/// headless 限制：真实 DOM 聚焦不可断言，仅断言派发内容；快照含 calendar GUID 与
/// select-N 短 id，先经正则归一化。上游 Calendar 仅月视图，无 View 切换参数面，
/// 亦无 SelectionMode 多/范围选择（范围语义属 DateRangePicker，随后续里程碑交付）。
/// </summary>
public class CalendarTests
{
    private static readonly string[] monthNamesEn =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    private static readonly string[] dayNamesEnFromSunday =
    ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    /// <summary>
    /// 归一化 calendar GUID 稳定 id 与 select-N 自增短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "calendar-[0-9a-f]{32}", "calendar-<id>"),
            "select-[0-9]+", "select-<n>");

    /// <summary>构造带基元服务的渲染器（与 CascaderTests 同构，Select 头部需要门户/定位/焦点服务）。</summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
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
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>排空渲染队列；FocusCurrentButton 的 Task.Yield 续沿需要多轮冲刷。</summary>
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>挂载 Calendar；extra 覆盖基础参数。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Calendar Calendar)> MountAsync(
        Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        var calendar = await renderer.MountAsync<Calendar>(extra);
        await SettleAsync(renderer);
        return (renderer, js, calendar);
    }

    /// <summary>
    /// 文化钉死参数：显式月份名/星期名/周日起点，使网格与头部文本与机器区域设置无关。
    /// </summary>
    private static Dictionary<string, object?> PinnedParams(
        DateTime? selected = null,
        bool dropdowns = false,
        bool showOutsideDays = true,
        DateTime? minDate = null,
        DateTime? maxDate = null,
        Func<DateTime, bool>? disabledDates = null)
    {
        var dict = new Dictionary<string, object?>
        {
            [nameof(Calendar.MonthNames)] = monthNamesEn,
            [nameof(Calendar.CustomDayNames)] = dayNamesEnFromSunday,
            [nameof(Calendar.FirstDayOfWeek)] = DayOfWeek.Sunday,
            [nameof(Calendar.ShowMonthYearDropdowns)] = dropdowns,
            [nameof(Calendar.ShowOutsideDays)] = showOutsideDays,
        };
        if (selected.HasValue)
        {
            dict[nameof(Calendar.Selected)] = selected.Value;
        }
        if (minDate.HasValue)
        {
            dict[nameof(Calendar.MinDate)] = minDate.Value;
        }
        if (maxDate.HasValue)
        {
            dict[nameof(Calendar.MaxDate)] = maxDate.Value;
        }
        if (disabledDates != null)
        {
            dict[nameof(Calendar.DisabledDates)] = disabledDates;
        }
        return dict;
    }

    /// <summary>触发一次 DOM 事件（沿 CascaderTests 先例须在 Dispatcher 上派发）。</summary>
    private static Task FireAsync(ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () => await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>
    /// 直调 HandleKeyDown：table 的 @onkeydown 携带 preventDefault 修饰符，在独立渲染器下
    /// 属性沿编译为事件字典而非裸 EventCallback，DispatchAsync 派发不到组件处理沿；
    /// 故按 cascader.connect 反射直调先例，用真实 KeyboardEventArgs 走组件内逻辑。
    /// </summary>
    private static Task PressKeyAsync(ComponentTestRenderer renderer, Calendar calendar, string key, bool shift = false) =>
        renderer.Dispatcher.InvokeAsync(async () =>
        {
            var handler = typeof(Calendar)
                .GetMethod("HandleKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<KeyboardEventArgs, Task>>(calendar);
            await handler(new KeyboardEventArgs { Key = key, ShiftKey = shift });
            // 直调沿缺少 EventCallback 包装的自动重渲染，手动补一轮（ShouldRender 自行去重）。
            await renderer.RerenderRootAsync(0, new Dictionary<string, object?>());
        });

    /// <summary>反射读取实例的私有日历 id（构造日按钮断言 id 用）。</summary>
    private static string CalendarId(Calendar calendar) =>
        (string)typeof(Calendar)
            .GetField("_calendarId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(calendar)!;

    /// <summary>反射直调私有方法（OnMonthChanged/OnYearChanged/GetYears）。</summary>
    private static object? InvokePrivate(Calendar calendar, string name, params object?[] args) =>
        typeof(Calendar)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(calendar, args);

    /// <summary>断言指定日期的按钮携带 tabindex="0"（即当前焦点格）。注意导航按钮自身也带
    /// tabindex="0"（Button 封装沿），故只按日按钮 id 前缀限定断言，不数全局个数。</summary>
    private static void AssertFocusedDay(string markup, string calendarId, DateTime date)
    {
        var id = $"{calendarId}-day-{date:yyyyMMdd}";
        Assert.True(
            Regex.IsMatch(markup, $@"id=""{Regex.Escape(id)}""[^>]*tabindex=""0"""),
            $"期望 {date:yyyy-MM-dd} 的按钮 tabindex=0，实际标记不含该模式。");
    }

    /// <summary>断言指定日期按钮带 disabled 属性（属性沿为独立 token，不与类名中的 atom:disabled: 混淆）。</summary>
    private static bool HasDisabledAttr(string markup, string dayIdFragment) =>
        Regex.IsMatch(markup, $@"{Regex.Escape(dayIdFragment)}""[^>]* disabled[ >]");

    [Fact]
    public async Task 月历网格生成2026年10月周日起点应为5周35格()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 12)));
        var markup = renderer.Markup();

        // 2026-10-01 是周四，周日起点回退到 09-27，恰满 5 周 35 格。
        Assert.Equal(35, Regex.Count(markup, "role=\"gridcell\""));
        Assert.Equal(7, Regex.Count(markup, "scope=\"col\""));
        Assert.Contains("day-20260927", markup); // 首格为外部日期 9 月 27 日
        Assert.DoesNotContain("day-20260906", markup); // 9 月 6 日不出现（仅 27-30）
    }

    [Fact]
    public async Task 月历网格生成2026年8月应补齐6周42格()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(selected: new DateTime(2026, 8, 15)));
        var markup = renderer.Markup();

        // 2026-08-01 是周六，31 天跨 6 周；第 6 周含 9 月 1-5 日外部日期。
        Assert.Equal(42, Regex.Count(markup, "role=\"gridcell\""));
        Assert.Contains("day-20260905", markup);
        Assert.DoesNotContain("day-20260906", markup);
    }

    [Fact]
    public async Task 月历网格生成闰年2028年2月应含29日()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(selected: new DateTime(2028, 2, 14)));
        var markup = renderer.Markup();

        // 2028-02-01 是周二，周日起点回退到 01-30；5 周 35 格。
        Assert.Equal(35, Regex.Count(markup, "role=\"gridcell\""));
        Assert.Contains("day-20280229", markup);
        Assert.Contains("day-20280130", markup); // 外部日期（上月）
    }

    [Fact]
    public async Task 月历网格生成平年2027年2月应止于28日()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(selected: new DateTime(2027, 2, 14)));
        var markup = renderer.Markup();

        Assert.Equal(35, Regex.Count(markup, "role=\"gridcell\""));
        Assert.Contains("day-20270228", markup);
        Assert.DoesNotContain("day-20270229", markup); // 平年无 2 月 29 日
    }

    [Fact]
    public async Task 外部日期隐藏时应只剩月内日期按钮()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(
            selected: new DateTime(2026, 10, 12), showOutsideDays: false));
        var markup = renderer.Markup();

        // 网格仍 35 格（null 单元格保留 td），但按钮仅 31 个（10 月每天一个）。
        Assert.Equal(35, Regex.Count(markup, "role=\"gridcell\""));
        Assert.Equal(31, Regex.Count(markup, """-day-20\d{6}"""));
        Assert.DoesNotContain("day-20260927", markup);
        Assert.Contains("atom:opacity-50", markup); // 空格走 CellEmptyClasses
    }

    [Fact]
    public async Task 周起点参数应将星期表头旋转至指定起点()
    {
        var dict = PinnedParams(selected: new DateTime(2026, 10, 12));
        dict[nameof(Calendar.FirstDayOfWeek)] = DayOfWeek.Monday;
        var (renderer, _, _) = await MountAsync(dict);
        var markup = renderer.Markup();

        var headers = Regex.Matches(markup, @"<th[^>]*>\s*([A-Za-z]+)\s*</th>")
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], headers);
    }

    [Fact]
    public async Task 周起点默认应沿当前区域性的每周首日()
    {
        var dict = PinnedParams(selected: new DateTime(2026, 10, 12));
        dict.Remove(nameof(Calendar.FirstDayOfWeek));
        var (renderer, _, _) = await MountAsync(dict);

        var format = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat;
        // CustomDayNames 已钉死（自周日起），故预期为按区域性每周首日旋转后的钉死名。
        var expected = dayNamesEnFromSunday[(int)format.FirstDayOfWeek];
        var first = Regex.Match(renderer.Markup(), @"<th[^>]*>\s*([^<]+?)\s*</th>");
        Assert.Equal(expected, first.Groups[1].Value);
    }

    [Fact]
    public async Task 上个月导航应换月且越年界回退到上年12月()
    {
        // 头部文本模式（无下拉）下 onclick 沿：0=上月按钮，1=下月按钮，2 起为日格按钮。
        var (renderer, _, _) = await MountAsync(PinnedParams(selected: new DateTime(2026, 1, 15)));
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);

        Assert.Contains(">December 2025<", renderer.Markup());
    }

    [Fact]
    public async Task 下个月导航应换月且越年界前进到次年1月()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(selected: new DateTime(2026, 12, 15)));
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);

        Assert.Contains(">January 2027<", renderer.Markup());
    }

    [Fact]
    public async Task 下月导航越过最小日期边界仍换页而越界日期全禁用()
    {
        // 上游实际面：PreviousMonth/NextMonth 无 MinDate/MaxDate 守卫，
        // 越出下界的月份照常渲染，但月内日期全部 disabled。
        var (renderer, _, _) = await MountAsync(PinnedParams(
            selected: new DateTime(2026, 10, 12), minDate: new DateTime(2026, 10, 05)));
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        var markup = renderer.Markup();

        Assert.Contains(">September 2026<", markup);
        // 越出下界的月份照常渲染：9 月视图整月 35 个日期按钮（含 8 月末与 10 月初溢出格）均禁用。
        Assert.Equal(35, Regex.Count(markup, "-day-20\\d{6}\""));
        Assert.Equal(35, Regex.Count(markup, " disabled[ >]"));
    }

    [Fact]
    public async Task 最小日期之前的日格应带disabled属性而之后可选()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(
            selected: new DateTime(2026, 10, 12), minDate: new DateTime(2026, 10, 05)));
        var markup = renderer.Markup();

        // 10 月 1-4 日禁用（含外部 9 月格也越界禁用），10 月 5 日起可选。
        Assert.True(HasDisabledAttr(markup, "day-20261001"));
        Assert.False(HasDisabledAttr(markup, "day-20261005"));
    }

    [Fact]
    public async Task 点击日格应同时回调SelectedChanged与OnSelect()
    {
        DateTime? reported = null;
        DateTime selected = default;
        var dict = PinnedParams(selected: new DateTime(2026, 10, 12));
        dict[nameof(Calendar.SelectedChanged)] =
            EventCallback.Factory.Create<DateTime?>(new object(), d => reported = d);
        dict[nameof(Calendar.OnSelect)] =
            EventCallback.Factory.Create<DateTime>(new object(), d => selected = d);

        var (renderer, _, _) = await MountAsync(dict);

        // 10 月 20 日：9 月 27 日起第 23 格（10-20 = 09-27 + 23 天），onclick 沿偏移 2。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 2 + 23);

        Assert.Equal(new DateTime(2026, 10, 20), reported);
        Assert.Equal(new DateTime(2026, 10, 20), selected);
        Assert.Equal(1, Regex.Count(renderer.Markup(), "aria-selected=\"true\""));
    }

    [Fact]
    public async Task 点击外部日期应回调并把显示月份导航到该日()
    {
        DateTime? reported = null;
        var dict = PinnedParams(selected: new DateTime(2026, 10, 12));
        dict[nameof(Calendar.SelectedChanged)] =
            EventCallback.Factory.Create<DateTime?>(new object(), d => reported = d);

        var (renderer, _, _) = await MountAsync(dict);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 2); // 首格 = 9 月 27 日

        Assert.Equal(new DateTime(2026, 9, 27), reported);
        Assert.Contains(">September 2026<", renderer.Markup());
    }

    [Fact]
    public async Task 点击禁用日期不应触发任何回调()
    {
        var fired = false;
        var dict = PinnedParams(selected: new DateTime(2026, 10, 12), minDate: new DateTime(2026, 10, 05));
        dict[nameof(Calendar.SelectedChanged)] =
            EventCallback.Factory.Create<DateTime?>(new object(), _ => fired = true);

        var (renderer, _, _) = await MountAsync(dict);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 2 + 4); // 10 月 1 日（禁用）

        Assert.False(fired);
    }

    [Fact]
    public async Task DisabledDates谓词命中的日格应禁用而不回调()
    {
        var fired = false;
        var dict = PinnedParams(
            selected: new DateTime(2026, 10, 12),
            disabledDates: d => d.Day == 13);
        dict[nameof(Calendar.SelectedChanged)] =
            EventCallback.Factory.Create<DateTime?>(new object(), _ => fired = true);

        var (renderer, _, _) = await MountAsync(dict);
        var markup = renderer.Markup();

        Assert.True(HasDisabledAttr(markup, "day-20261013"));
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 2 + 16); // 10 月 13 日
        Assert.False(fired);
    }

    [Fact]
    public async Task 今天高亮应命中DayToday类且默认聚焦今天()
    {
        // 不传 Selected：DisplayDate=DateTime.Today，焦点优先级沿命中今天。
        var (renderer, _, calendar) = await MountAsync(PinnedParams());
        var markup = renderer.Markup();

        var todayClass = "atom:bg-accent atom:text-accent-foreground";
        Assert.Equal(1, Regex.Count(markup, Regex.Escape(todayClass)));
        AssertFocusedDay(markup, CalendarId(calendar), DateTime.Today);
    }

    [Fact]
    public async Task 选中日应命中DaySelected与CellSelected类()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 12)));
        var markup = renderer.Markup();

        Assert.Equal(1, Regex.Count(markup, Regex.Escape("atom:bg-primary atom:text-primary-foreground")));
        // CellSelectedClasses 的 :has 组合子按原样序列化（测试渲染器不转义属性值）。
        Assert.Equal(1, Regex.Count(markup, Regex.Escape("[&:has([aria-selected])]:bg-accent")));
        Assert.Equal(1, Regex.Count(markup, "aria-selected=\"true\""));
    }

    [Fact]
    public async Task DayTemplate上下文应如实描述各日状态()
    {
        var captured = new List<CalendarDayContext>();
        RenderFragment<CalendarDayContext> template = ctx => builder =>
        {
            captured.Add(ctx);
            builder.AddContent(0, ctx.Date.Day);
        };

        var dict = PinnedParams(selected: new DateTime(2026, 10, 12), minDate: new DateTime(2026, 10, 05));
        dict[nameof(Calendar.DayTemplate)] = template;

        var (renderer, _, _) = await MountAsync(dict);

        Assert.NotEmpty(captured);
        Assert.All(captured, ctx => Assert.False(ctx.IsInRange)); // 单日期日历恒 false（上游语义）

        var sep27 = captured.Single(c => c.Date == new DateTime(2026, 9, 27));
        Assert.True(sep27.IsOutsideMonth);
        Assert.False(sep27.IsSelected);

        var oct1 = captured.Single(c => c.Date == new DateTime(2026, 10, 1));
        Assert.True(oct1.IsDisabled); // 越出 MinDate

        var oct12 = captured.Single(c => c.Date == new DateTime(2026, 10, 12));
        Assert.True(oct12.IsSelected);
        Assert.False(oct12.IsOutsideMonth);

        Assert.Single(captured, c => c.IsToday); // 今天至多一个
    }

    [Fact]
    public async Task DayTemplate缺省时按钮文本应为日号()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 12)));
        Assert.Contains(">12</button>", renderer.Markup());
    }

    [Fact]
    public async Task 方向键应移动焦点并跳过禁用日期()
    {
        var (renderer, js, calendar) = await MountAsync(PinnedParams(
            selected: new DateTime(2026, 10, 12), disabledDates: d => d.Day == 13));
        var id = CalendarId(calendar);

        await PressKeyAsync(renderer, calendar, "ArrowRight");
        await SettleAsync(renderer);
        // 10 月 13 日被跳过，焦点落在 10 月 14 日。
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 10, 14));

        await PressKeyAsync(renderer, calendar, "ArrowUp");
        await SettleAsync(renderer);
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 10, 7));

        await PressKeyAsync(renderer, calendar, "ArrowLeft");
        await SettleAsync(renderer);
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 10, 6));

        await PressKeyAsync(renderer, calendar, "ArrowDown");
        await SettleAsync(renderer);
        // 6 日 +7 = 13 日被禁用谓词命中，跳过沿前进到 14 日。
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 10, 14));
    }

    [Fact]
    public async Task 键盘导航跨月边界应自动换页()
    {
        var (renderer, _, calendar) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 31)));
        var id = CalendarId(calendar);

        await PressKeyAsync(renderer, calendar, "ArrowRight");
        await SettleAsync(renderer);

        Assert.Contains(">November 2026<", renderer.Markup());
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 11, 1));
    }

    [Fact]
    public async Task Home与End应落在本月首个与末个可选日()
    {
        var (renderer, _, calendar) = await MountAsync(PinnedParams(
            selected: new DateTime(2026, 10, 12), minDate: new DateTime(2026, 10, 05)));
        var id = CalendarId(calendar);

        await PressKeyAsync(renderer, calendar, "Home");
        await SettleAsync(renderer);
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 10, 5));

        await PressKeyAsync(renderer, calendar, "End");
        await SettleAsync(renderer);
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 10, 31));
    }

    [Fact]
    public async Task PageUp与PageDown应换月并保持日号且Shift加PageUp换年()
    {
        var (renderer, _, calendar) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 12)));
        var id = CalendarId(calendar);

        await PressKeyAsync(renderer, calendar, "PageDown");
        await SettleAsync(renderer);
        Assert.Contains(">November 2026<", renderer.Markup());
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 11, 12));

        await PressKeyAsync(renderer, calendar, "PageUp", shift: true);
        await SettleAsync(renderer);
        Assert.Contains(">November 2025<", renderer.Markup());
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2025, 11, 12));
    }

    [Fact]
    public async Task PageDown月末溢出应钳制到目标月末天数()
    {
        // 10 月 31 日 PageDown 到 11 月：11 月无 31 日，钳到 11 月 30 日。
        var (renderer, _, calendar) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 31)));
        var id = CalendarId(calendar);

        await PressKeyAsync(renderer, calendar, "PageDown");
        await SettleAsync(renderer);
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 11, 30));
    }

    [Fact]
    public async Task Enter与空格应选中焦点日期而其他按键应被忽略()
    {
        DateTime? reported = null;
        var dict = PinnedParams(selected: new DateTime(2026, 10, 12));
        dict[nameof(Calendar.SelectedChanged)] =
            EventCallback.Factory.Create<DateTime?>(new object(), d => reported = d);

        var (renderer, _, calendar) = await MountAsync(dict);
        var id = CalendarId(calendar);

        await PressKeyAsync(renderer, calendar, "a");
        await SettleAsync(renderer);
        Assert.Null(reported); // 非导航键直接返回

        // ArrowRight 移到 13 日（可选），Enter 选中它。
        await PressKeyAsync(renderer, calendar, "ArrowRight");
        await SettleAsync(renderer);
        await PressKeyAsync(renderer, calendar, "Enter");
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 13), reported);

        reported = null;
        await PressKeyAsync(renderer, calendar, " ");
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 13), reported);
        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 10, 13));
    }

    [Fact]
    public async Task 方向键越出最小日期边界应不移动焦点()
    {
        var (renderer, _, calendar) = await MountAsync(PinnedParams(
            selected: new DateTime(2026, 10, 12), minDate: new DateTime(2026, 10, 12)));
        var id = CalendarId(calendar);

        await PressKeyAsync(renderer, calendar, "ArrowLeft");
        await SettleAsync(renderer);

        AssertFocusedDay(renderer.Markup(), id, new DateTime(2026, 10, 12));
    }

    [Fact]
    public async Task 键盘导航焦点格应追加ring类而鼠标点击不追加()
    {
        var (renderer, _, calendar) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 12)));
        var id = CalendarId(calendar);
        var focusRing = "atom:ring-2 atom:ring-ring atom:ring-offset-2";

        await PressKeyAsync(renderer, calendar, "ArrowRight");
        await SettleAsync(renderer);
        Assert.Contains(focusRing, renderer.Markup()); // _isKeyboardNavigating 为 true

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 2 + 17); // 点击 10 月 14 日
        await SettleAsync(renderer);
        Assert.DoesNotContain(focusRing, renderer.Markup()); // SelectDate 重置导航模式
    }

    [Fact]
    public async Task 年月下拉联动应切换显示月份与年份()
    {
        var (renderer, _, calendar) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 12), dropdowns: true));

        // 下拉头部沿触发器 span 呈现月名/年份；反射直调私有回调模拟 Select 的 ValueChanged 沿。
        InvokePrivate(calendar, "OnMonthChanged", 3);
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>());
        Assert.Contains(">March<", renderer.Markup());

        InvokePrivate(calendar, "OnYearChanged", 2027);
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>());
        Assert.Contains(">March<", renderer.Markup());
        Assert.Contains(">2027<", renderer.Markup());
    }

    [Fact]
    public async Task 年份区间应随最小最大日期裁剪且缺省为今天前后各50年()
    {
        var (_, _, clamped) = await MountAsync(PinnedParams(
            selected: new DateTime(2026, 10, 12),
            minDate: new DateTime(2026, 1, 1), maxDate: new DateTime(2027, 12, 31)));
        var years = ((IEnumerable<int>)InvokePrivate(clamped, "GetYears")!).ToList();
        Assert.Equal([2026, 2027], years);

        var (_, _, fallback) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 12)));
        var wide = ((IEnumerable<int>)InvokePrivate(fallback, "GetYears")!).ToList();
        var current = DateTime.Today.Year;
        Assert.Equal(101, wide.Count);
        Assert.Equal(current - 50, wide[0]);
        Assert.Equal(current + 50, wide[^1]);
    }

    [Fact]
    public async Task 外部变更Selected应同步头部月份而网格沿用缓存()
    {
        var dict = PinnedParams(selected: new DateTime(2026, 10, 12));
        var (renderer, _, calendar) = await MountAsync(dict);

        // 重渲染根组件并改 Selected——上游 OnParametersSet 直接写 _displayDate 字段而非
        // DisplayDate 属性，不经属性 setter 的清缓存沿：头部月份随 DisplayDate 跳到 2027 年
        // 1 月，但周网格沿用 _cachedWeeks 缓存仍为上月的 10 月格（上游行为，bug-for-bug 保留）。
        dict[nameof(Calendar.Selected)] = new DateTime(2027, 1, 5);
        await renderer.RerenderRootAsync(0, dict);
        await SettleAsync(renderer);
        var markup = renderer.Markup();

        Assert.Contains(">January 2027<", markup);
        Assert.Contains("day-20261012", markup); // 缓存沿：网格仍是 10 月格
        Assert.DoesNotContain("day-20270105", markup);
    }

    [Fact]
    public async Task FocusActiveDayAsync应派发焦点日按钮id到elementUtils()
    {
        var (renderer, js, calendar) = await MountAsync(PinnedParams(
            selected: null, minDate: new DateTime(2026, 10, 5), maxDate: new DateTime(2026, 10, 20)));
        // Selected 为空时 InitializeFocusedDate 命中"今天在显示月"分支，
        // FocusActiveDayAsync 的 ??= 不再兜底，派发沿即今天按钮的 id。
        await renderer.Dispatcher.InvokeAsync(() => calendar.FocusActiveDayAsync());
        await SettleAsync(renderer);

        var id = CalendarId(calendar);
        var focusCall = js.Invocations.Last(i => i.Identifier == "elementUtils.focusElement");
        Assert.Equal($"{id}-day-{DateTime.Today:yyyyMMdd}", (string?)focusCall.Args![0]);
    }

    [Fact]
    public async Task AutoFocus应在首渲染后派发focusElement()
    {
        var dict = PinnedParams(selected: new DateTime(2026, 10, 12));
        dict[nameof(Calendar.AutoFocus)] = true;
        var (renderer, js, calendar) = await MountAsync(dict);
        await SettleAsync(renderer, rounds: 3);

        var id = CalendarId(calendar);
        Assert.Contains(js.Invocations, i =>
            i.Identifier == "elementUtils.focusElement" && (string?)i.Args![0] == $"{id}-day-20261012");
    }

    [Fact]
    public async Task DisposeAsync应幂等完成且不派发dispose互操作()
    {
        var (renderer, js, calendar) = await MountAsync(PinnedParams(selected: new DateTime(2026, 10, 12)));
        await renderer.Dispatcher.InvokeAsync(async () => await calendar.DisposeAsync());

        Assert.DoesNotContain(js.Invocations, i => i.Identifier.Contains("dispose", StringComparison.OrdinalIgnoreCase));
    }

    // 快照夹具一概钉在已过去的月份（2025-10）：Calendar 的今日高亮与聚焦优先级都以
    // DateTime.Today 判定，显示月一旦含"今天"，快照就会逐日漂移（跨午夜即红）。
    // 钉到过去月后 today 永不落入显示月，快照恒稳；今日高亮本身由
    // 今天高亮应命中DayToday类且默认聚焦今天 等行为用例专项覆盖。
    private static readonly DateTime snapshotPinnedDate = new(2025, 10, 12);

    [Fact]
    public async Task 默认月历与文本头部结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(selected: snapshotPinnedDate));
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 年月下拉头部结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(
            selected: snapshotPinnedDate, dropdowns: true));
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 隐藏外部日期与下界禁用结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(PinnedParams(
            selected: snapshotPinnedDate, showOutsideDays: false,
            minDate: new DateTime(2025, 10, 5)));
        await Verify(Scrub(renderer.Markup()));
    }
}
