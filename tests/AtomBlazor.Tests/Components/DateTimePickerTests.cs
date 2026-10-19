using AtomBlazor.Primitives;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
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
/// DateTimePicker 测试：非受控触发按钮的占位/格式化文本（EffectiveFormat 随
/// ShowSeconds 在 "g"/"G" 双档）、弹层月历+时分（可选秒/AM-PM）滚钮列与本地化钮标、
/// 点击日历日保留时间部分且浮层不关闭（行为核心差异面，区别于 DatePicker 选中即关）、
/// 12 小时制时段内循环（11 时上点落午夜 12 AM 的取模沿 bug-for-bug）、24 小时制昼夜回绕、
/// MinuteStep 下溢取模回绕为同刻 45 分（TimePicker 同款 quirk）、秒位回绕、AM/PM 平移
/// 与正午翻 PM 无操作恒重发回调、Now 步长对齐与秒位随 ShowSeconds、Clear 复位落午夜缺省
/// （内部钟清 0 → 显示 12 而 AM 高亮，区别于 TimePicker 清 12 正午）、SetValue 三源越界
/// 拒绝、MinuteStep 守卫异常、Required/Disabled 面、EditContext 字段变更通知、
/// HandleContentReady 聚焦派发加闩与 HandleOpenChanged 重新装闩、附加属性 splat。
/// headless 限制：弹层关闭后 FloatingPortal 内容滞留 DOM（基元 JsOnClosed 沿），改断
/// 触发器 aria-expanded；OnContentReady 定位回调在 NoopJavaScript 下不自动触发，反射直调
/// 处理沿，断 elementUtils.focusElement 派发载荷 id；Calendar 默认年月下拉占 onclick
/// 序 0-3，日格自 4 起且日格数动态反查（照 DatePickerTests 口径），滚钮与时段钮/操作钮
/// 依次续排；基元 PopoverTrigger AsChild 显式 aria 值按 splat 位置规则压过透传值，断言据实。
/// 快照含 calendar GUID、popover-N 与 select-N 短 id，先经正则归一化；打开态快照含
/// zh-CN 机器区域性月名/周一起算面，随本仓基线据实入册（/ 同口径）。
/// </summary>
public class DateTimePickerTests
{
    /// <summary>归一化 calendar GUID 稳定 id、popover-N 与 select-N 自增短 id，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "calendar-[0-9a-f]{32}", "calendar-<id>"),
                "popover-[0-9]+", "popover-<n>"),
            "select-[0-9]+", "select-<n>");

    /// <summary>构造带基元服务的渲染器（与 DatePickerTests 同构，弹层需要门户/定位/焦点服务）。</summary>
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

    /// <summary>挂载 PortalHost（根 0）+ DateTimePicker（根 1），使弹层门户内容可进入渲染树遍历。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, DateTimePicker Picker)> MountAsync(
        Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var picker = await renderer.MountAsync<DateTimePicker>(extra);
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

    /// <summary>反射直调私有异步/同步处理方法（OnContentReady 定位沿与 HandleOpenChanged
    /// 装闩沿 headless 无稳定派发序，照 DatePickerTests 注记③口径反射直调）。</summary>
    private static Task InvokePrivateAsync(ComponentTestRenderer renderer, object instance, string name, params object?[] args) =>
        renderer.Dispatcher.InvokeAsync(async () =>
        {
            var method = instance.GetType()
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
            if (method.Invoke(instance, args) is Task task)
            {
                await task;
            }
        });

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

    /// <summary>抓取列显示位（tabular-nums 数值 span）的两位数字串，按时/分（/秒）渲染序返回。</summary>
    private static List<string> DisplaySpans(string markup) =>
        Regex.Matches(markup, """tabular-nums">\s*(\d{2})\s*<""").Select(m => m.Groups[1].Value).ToList();

    /// <summary>打开态滚钮基址：门户根先遍历——Calendar 年月下拉与导航钮占序 0-3
    /// 日格自 4 起共 dayCount 枚（随月份 5/6 周动态反查），滚钮自 4+dayCount 续排。</summary>
    private static int ScrollBase(string markup) =>
        4 + Regex.Count(markup, """-day-20\d{6}""");

    private sealed class DateTimePickerModel
    {
        public DateTime? Start { get; set; }
    }

    [Fact]
    public async Task 缺省闭合态应显本地化占位与日历时钟钮()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.AriaLabel)] = "Appointment",
        });
        var markup = renderer.Markup();

        // 占位沿：Placeholder 未设取 Localizer["DateTimePicker.Placeholder"]。
        Assert.Contains("Pick a date and time", markup);
        Assert.Contains("atom:w-[280px]", markup);
        Assert.DoesNotContain("role=\"grid\"", markup);
        Assert.DoesNotContain("Increment hour", markup); // 弹层未渲染
        var button = ButtonMarkup(markup, "Pick a date and time");
        Assert.Contains("aria-label=\"Appointment\"", button);
        Assert.Contains("aria-expanded=\"false\"", button);
    }

    [Fact]
    public async Task 有值触发按钮应显格式化文本且摘除占位灰字()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 14, 30, 45),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm:ss",
        });
        var markup = renderer.Markup();

        Assert.Contains("2026-10-12 14:30:45", markup);
        // 有值时 ButtonCssClass 不再叠加 atom:text-muted-foreground。
        Assert.DoesNotContain("atom:text-muted-foreground", markup);
    }

    [Fact]
    public async Task EffectiveFormat应随ShowSeconds在g与G双档切换()
    {
        var value = new DateTime(2026, 10, 12, 14, 30, 45);
        // 缺省 "g"（不含秒）档：断言按当前文化区实算，避免文化漂移。
        var (shortFmt, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = value,
        });
        Assert.Contains(value.ToString("g", CultureInfo.CurrentCulture), shortFmt.Markup());

        var (longFmt, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = value,
            [nameof(DateTimePicker.ShowSeconds)] = true,
        });
        Assert.Contains(value.ToString("G", CultureInfo.CurrentCulture), longFmt.Markup());
    }

    [Fact]
    public async Task 打开弹层应显日历与时分列本地化钮标且PM高亮()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 14, 30, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        Assert.Contains("role=\"grid\"", markup);
        Assert.Contains(">Hour<", markup);
        Assert.Contains(">Min<", markup);
        Assert.DoesNotContain(">Sec<", markup); // ShowSeconds=false
 // 本地化钮标全量在册：滚钮 aria-label 逐枚落位。
        Assert.Contains("aria-label=\"Increment hour\"", markup);
        Assert.Contains("aria-label=\"Decrement hour\"", markup);
        Assert.Contains("aria-label=\"Increment minute\"", markup);
        Assert.Contains("aria-label=\"Decrement minute\"", markup);
        Assert.DoesNotContain("Increment second", markup);

        // 14:30 → Hour12 显示 "02"/"30"；IsAm=14<12 false → PM 高亮、AM 不高亮。
        // （AM/PM 钮内文本两侧有 Razor 空白节点，按 TimePickerTests 口径以裸串抓取。）
        Assert.Equal(["02", "30"], DisplaySpans(markup));
        Assert.DoesNotContain("atom:bg-primary", ButtonMarkup(markup, "AM"));
        Assert.Contains("atom:bg-primary", ButtonMarkup(markup, "PM"));
    }

    [Fact]
    public async Task 点击日历日应保留时间部分且浮层不关闭()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 14, 30, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.AriaLabel)] = "Appointment",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        // 日格序随机器区域性动态反查（照 DatePickerTests 口径），选中 20 日格号。
        var open = renderer.Markup();
        var dayIndex = Regex.Matches(open, "-day-(20\\d{6})").ToList()
            .FindIndex(m => m.Groups[1].Value == "20261020");
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 4 + dayIndex);
        await RerenderPortalAsync(renderer);

        // HandleDateSelected 沿：日期换 20 而时间部分自内部钟保留 14:30:00。
        Assert.Equal(new DateTime(2026, 10, 20, 14, 30, 0), reports.Last());
        Assert.Equal(new DateTime(2026, 10, 20, 14, 30, 0), picker.Value);
        Assert.Contains("2026-10-20 14:30", renderer.Markup());
        // 与 DatePicker 差异面：选中日期不关层——触发器 aria-expanded 恒 true
        // （打开态 Now/Clear 操作钮亦携同 aria-label，锚点取唯一带 aria-expanded 的钮）。
        Assert.Contains("aria-expanded=\"true\"",
            Regex.Match(renderer.Markup(), """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);
    }

    [Fact]
    public async Task 十二小时制加时应时段内循环且十一AM上点落午夜()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 11, 0, 0), // 11:00 AM
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        // quirk bug-for-bug：hour12=(11%12+1)%12=0，AM 段保留 → 内部 0 时
        // 即 11 时上点不是 12 时午后而是落回午夜 12:00 AM。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: ScrollBase(renderer.Markup()));
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0), reports.Last());
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0), picker.Value);
        Assert.Equal(["12", "00"], DisplaySpans(renderer.Markup()));
        Assert.Contains("atom:bg-primary", ButtonMarkup(renderer.Markup(), "AM"));

        // PM 段常规循环：13 时（01 PM）上点 → 14 时（02 PM），时段保留。
        var (pm, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 13, 0, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(pm);
        await FireAsync(pm, "onclick", new MouseEventArgs(), occurrence: ScrollBase(pm.Markup()));
        await SettleAsync(pm);
        Assert.Equal(new DateTime(2026, 10, 12, 14, 0, 0), reports.Last());
        Assert.Contains("2026-10-12 14:00", pm.Markup()); // 触发文本按自定义格式档
    }

    [Fact]
    public async Task 二十四小时制时钮应昼夜回绕且无AMPM段()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.TimeFormat)] = TimeFormat.Hour24,
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 23, 0, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        // Hour24 无 AM/PM 段（atom:bg-primary 系日历选中/今天高亮类，不作本断言据）。
        Assert.DoesNotContain("PM", markup);

        // 23 时上点回绕 0 时；0 时再下点回绕 23 时。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: ScrollBase(markup));
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0), reports.Last());

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: ScrollBase(renderer.Markup()) + 1);
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 12, 23, 0, 0), reports.Last());
        Assert.Equal(["23", "00"], DisplaySpans(renderer.Markup()));
    }

    [Fact]
    public async Task MinuteStep分步下溢应取模回绕为同刻四十五分()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.TimeFormat)] = TimeFormat.Hour24,
            [nameof(DateTimePicker.MinuteStep)] = 15,
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 2, 0, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var baseIndex = ScrollBase(renderer.Markup());

        // quirk bug-for-bug（TimePicker 同款）：(0-15+60)%60=45，零分下"减分"
        // 不进位回退反而前进到同刻 45 分。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: baseIndex + 3); // 分-
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 12, 2, 45, 0), reports.Last());

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: baseIndex + 2); // 分+
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 12, 2, 0, 0), reports.Last()); // 45+15=60%60=0
    }

    [Fact]
    public async Task 秒列应随ShowSeconds显形且两端回绕()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.TimeFormat)] = TimeFormat.Hour24,
            [nameof(DateTimePicker.ShowSeconds)] = true,
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 0, 0, 59),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm:ss",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        Assert.Contains(">Sec<", markup);
        Assert.Contains("aria-label=\"Increment second\"", markup);
        Assert.Equal(["00", "00", "59"], DisplaySpans(markup));

        var baseIndex = ScrollBase(markup);
        // 59 秒上点回绕 0；0 秒下点回绕 59。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: baseIndex + 4); // 秒+
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0), reports.Last());

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: baseIndex + 5); // 秒-
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 59), reports.Last());
        Assert.Contains("2026-10-12 00:00:59", renderer.Markup());
    }

    [Fact]
    public async Task 正午翻PM应无操作恒重发回调且翻AM落午夜边界()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 12, 0, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var baseIndex = ScrollBase(renderer.Markup());

        // 12 时：IsAm=false → PM 高亮；翻 PM 不命中双条件（12 不 <12）→ 时值不动
        // 但恒重发回调（沿，据实断言）。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: baseIndex + 5); // PM
        await SettleAsync(renderer);
        Assert.Single(reports);
        Assert.Equal(new DateTime(2026, 10, 12, 12, 0, 0), reports.Last());

        // 翻 AM：12>=12 → 0 时，回调落午夜，显示位仍 "12" 而 AM 高亮。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: baseIndex + 4); // AM
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0), reports.Last());
        Assert.Equal(["12", "00"], DisplaySpans(renderer.Markup()));
        Assert.Contains("atom:bg-primary", ButtonMarkup(renderer.Markup(), "AM"));
    }

    [Fact]
    public async Task Now钮应即时钟分按步长对齐秒位随ShowSeconds()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.TimeFormat)] = TimeFormat.Hour24,
            [nameof(DateTimePicker.MinuteStep)] = 15,
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        // Hour24 无秒列：Now = 基址 + 4。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: ScrollBase(renderer.Markup()) + 4);
        await SettleAsync(renderer);

        var now = DateTime.Now;
        var value = picker.Value!.Value;
        Assert.Equal(value, reports.Last());
        Assert.Equal(now.Date, value.Date); // 日期落今天
        Assert.Equal(now.Hour, value.Hour); // 时沿即时
        Assert.Equal(0, value.Minute % 15); // 分按步长向下对齐
        Assert.Equal(0, value.Second); // ShowSeconds=false → 秒清零
    }

    [Fact]
    public async Task 无值时Now可用Clear禁用且Clear经激活后复位落午夜缺省()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        // 操作钮禁用面不对称（沿）：Clear 受 !Value.HasValue 门、Now 不受。
        Assert.DoesNotMatch(" disabled[ >]", ButtonMarkup(markup, "Now"));
        Assert.Matches(" disabled[ >]", ButtonMarkup(markup, "Clear"));

        // Hour12 无秒列：Now=基址+6、Clear=基址+7。
        var baseIndex = ScrollBase(markup);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: baseIndex + 6); // Now → 有值
        await SettleAsync(renderer);
        Assert.NotNull(picker.Value);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: ScrollBase(renderer.Markup()) + 7); // Clear
        await SettleAsync(renderer);

        Assert.Null(reports.Last());
        Assert.Null(picker.Value);
        Assert.Contains("Pick a date and time", renderer.Markup());
        // 沿：Clear 清内部钟为 0 → Hour12 显示位 "12" 且 AM 高亮
        // （区别于 TimePicker 复位落 12 时正午而 PM 高亮）。
        Assert.Equal(["12", "00"], DisplaySpans(renderer.Markup()));
        Assert.Contains("atom:bg-primary", ButtonMarkup(renderer.Markup(), "AM"));
    }

    [Fact]
    public async Task SetValue应被MinDateMaxDate与DisabledDates三源拒绝零回调()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.MinDate)] = new DateTime(2026, 10, 12),
            [nameof(DateTimePicker.MaxDate)] = new DateTime(2026, 10, 18),
            [nameof(DateTimePicker.DisabledDates)] = (Func<DateTime, bool>)(d => d.Day == 15),
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 8, 0, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        // 反射直调 SetValue 三拒一受：越 MinDate、越 MaxDate、谓词命中皆零回调不改值。
        await InvokePrivateAsync(renderer, picker, "SetValue", new DateTime(2026, 10, 1));
        await InvokePrivateAsync(renderer, picker, "SetValue", new DateTime(2026, 10, 20));
        await InvokePrivateAsync(renderer, picker, "SetValue", new DateTime(2026, 10, 15));
        await SettleAsync(renderer);
        Assert.Empty(reports);
        Assert.Equal(new DateTime(2026, 10, 12, 8, 0, 0), picker.Value);

        await InvokePrivateAsync(renderer, picker, "SetValue", new DateTime(2026, 10, 14, 9, 30, 0));
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 14, 9, 30, 0), reports.Single());
        Assert.Equal(new DateTime(2026, 10, 14, 9, 30, 0), picker.Value);
    }

    [Fact]
    public async Task 越界MinuteStep应挂载即抛守卫异常()
    {
        var (renderer, _) = CreateRenderer();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => renderer.MountAsync<DateTimePicker>(
            new Dictionary<string, object?> { [nameof(DateTimePicker.MinuteStep)] = 0 }));

        var (renderer2, _) = CreateRenderer();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => renderer2.MountAsync<DateTimePicker>(
            new Dictionary<string, object?> { [nameof(DateTimePicker.MinuteStep)] = 60 }));
    }

    [Fact]
    public async Task 外部Value变更应回灌内部钟并刷新显示位()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.AriaLabel)] = "Appointment",
            [nameof(DateTimePicker.DateTimeFormat)] = "HH:mm",
        };
        var (renderer, _, _) = await MountAsync(parameters);

        // 打开前先外部改值：OnParametersSet 有值沿回灌内部钟，触发文本按 "HH:mm" 刷新。
        parameters[nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 20, 15, 0);
        await renderer.RerenderRootAsync(1, parameters);
        await SettleAsync(renderer);
        Assert.Contains(">20:15<", renderer.Markup());

        await OpenAsync(renderer);
        // 显示位与高亮自回灌后的内部钟：20 时 → Hour12 显 "08"、PM 高亮。
        Assert.Equal(["08", "15"], DisplaySpans(renderer.Markup()));
        Assert.Contains("atom:bg-primary", ButtonMarkup(renderer.Markup(), "PM"));
    }

    [Fact]
    public async Task 必填与禁用面应落触发按钮与弹层滚钮()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Required)] = true,
            [nameof(DateTimePicker.AriaLabel)] = "Appointment",
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 8, 0, 0),
        };
        var (renderer, _, _) = await MountAsync(parameters);

        // 必填沿恒在：aria-required 落触发按钮。
        Assert.Contains("aria-required=\"true\"",
            Regex.Match(renderer.Markup(), """<button[^>]*aria-label="Appointment"[^>]*>""").Value);

        // 先以可用态打开弹层，再切 Disabled=true：滚钮继承 disabled 沿（照 TimePickerTests 参数翻转沿）。
        await OpenAsync(renderer);
        parameters[nameof(DateTimePicker.Disabled)] = true;
        await renderer.RerenderRootAsync(1, parameters);
        await SettleAsync(renderer);
        var markup = renderer.Markup();

        // 触发钮锚点取唯一带 aria-expanded 的钮（Now/Clear 也携同 aria-label）。
        var trigger = Regex.Match(markup, """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value;
        Assert.Matches(" disabled[ >]", trigger);
        Assert.Contains("atom:opacity-50 atom:pointer-events-none", markup);
        Assert.Matches(" disabled[ >]", ButtonMarkup(markup, "Increment hour"));
        Assert.Matches(" disabled[ >]", ButtonMarkup(markup, "AM"));
    }

    [Fact]
    public async Task EditContext下步进应通知字段变更()
    {
        var model = new DateTimePickerModel();
        var context = new EditContext(model);
        var changedFields = new List<string>();
        context.OnFieldChanged += (_, e) => changedFields.Add(e.FieldIdentifier.FieldName);
        Expression<Func<DateTime?>> expression = () => model.Start;

        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>()); // 根 0：门户宿主
        RenderFragment fieldFragment = builder =>
        {
            builder.OpenComponent<DateTimePicker>(0);
            builder.AddAttribute(1, nameof(DateTimePicker.TimeFormat), TimeFormat.Hour24);
            builder.AddAttribute(2, nameof(DateTimePicker.Value), new DateTime(2026, 10, 12, 8, 0, 0));
            builder.AddAttribute(3, nameof(DateTimePicker.ValueExpression), expression);
            builder.CloseComponent();
        };
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = fieldFragment,
        });
        await SettleAsync(renderer);

        await FireAsync(renderer, "onclick", new MouseEventArgs()); // 打开
        await RerenderPortalAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: ScrollBase(renderer.Markup())); // 时+
        await SettleAsync(renderer);

        Assert.Contains("Start", changedFields);
    }

    [Fact]
    public async Task HandleContentReady未挂载日历应吞且打开后派发聚焦加闩()
    {
        var (renderer, js, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 8, 0, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
        });

        // 关闭态 _calendar 为 null：直调吞报零派发。
        await InvokePrivateAsync(renderer, picker, "HandleContentReady");
        await SettleAsync(renderer);
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "elementUtils.focusElement");

        await OpenAsync(renderer);
        Assert.Contains("role=\"grid\"", renderer.Markup());

        // OnContentReady 依赖定位服务回调，headless 下不自动触发：反射直调处理沿
        // 断 elementUtils.focusElement 派发载荷 id（真实聚焦不可断言）。
        await InvokePrivateAsync(renderer, picker, "HandleContentReady");
        await SettleAsync(renderer);
        var focus = js.Invocations.Last(i => i.Identifier == "elementUtils.focusElement");
        Assert.Matches(@"calendar-[0-9a-f]{32}-day-20261012", (string?)focus.Args![0]!);

        // _focusDone 闩：重复触发（重新定位沿）不再拽回焦点。
        await InvokePrivateAsync(renderer, picker, "HandleContentReady");
        await SettleAsync(renderer);
        Assert.Single(js.Invocations, i => i.Identifier == "elementUtils.focusElement");
    }

    [Fact]
    public async Task HandleOpenChanged开合沿应重新装载聚焦闩()
    {
        var (renderer, js, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 8, 0, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
        });
        await OpenAsync(renderer);

        await InvokePrivateAsync(renderer, picker, "HandleContentReady");
        await SettleAsync(renderer);
        Assert.Single(js.Invocations, i => i.Identifier == "elementUtils.focusElement");

        // 关→开沿重装载闩（HandleOpenChanged(isOpen:true) → _focusDone=false）
        // 再调 HandleContentReady 应二次派发聚焦。
        await InvokePrivateAsync(renderer, picker, "HandleOpenChanged", false);
        await InvokePrivateAsync(renderer, picker, "HandleOpenChanged", true);
        await InvokePrivateAsync(renderer, picker, "HandleContentReady");
        await SettleAsync(renderer);
        Assert.Equal(2, js.Invocations.Count(i => i.Identifier == "elementUtils.focusElement"));
    }

    [Fact]
    public async Task 附加属性应透传到触发按钮()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.AriaLabel)] = "Appointment",
            ["data-test"] = "dtp-1",
        });
        Assert.Contains("data-test=\"dtp-1\"",
            Regex.Match(renderer.Markup(), """<button[^>]*aria-label="Appointment"[^>]*>""").Value);
    }

    [Fact]
    public async Task 缺省十二小时制闭合态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.Value)] = new DateTime(2026, 10, 12, 14, 30, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm",
            [nameof(DateTimePicker.AriaLabel)] = "Appointment",
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 二十四小时制含秒与步长打开态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateTimePicker.TimeFormat)] = TimeFormat.Hour24,
            [nameof(DateTimePicker.ShowSeconds)] = true,
            [nameof(DateTimePicker.MinuteStep)] = 15,
            // 钉已过去的月份（2025-10）：弹层 Calendar 的今日高亮随 DateTime.Today 漂移。
            [nameof(DateTimePicker.Value)] = new DateTime(2025, 10, 12, 9, 5, 0),
            [nameof(DateTimePicker.DateTimeFormat)] = "yyyy-MM-dd HH:mm:ss",
            [nameof(DateTimePicker.AriaLabel)] = "Appointment",
        });
        await OpenAsync(renderer);
        await Verify(Scrub(renderer.Markup()));
    }
}
