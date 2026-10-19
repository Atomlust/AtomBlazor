using AtomBlazor.Primitives;
using System.Globalization;
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

namespace AtomBlazor.Tests.Components;

/// <summary>
/// TimeInput 测试：en-US 钉死文化下的 12 小时制默认面（ShortTimePattern 含
/// 小写 h → Hour12；午夜回显 "12" 且 AM，12:30 为 PM 的 12 小时制换算沿）、Format
/// 覆写 24 小时制（时段界 0-23、无 AM/PM 选择器）、文本输入合成回调、SetPeriodAsync
/// 翻时段重算、MinuteStep 过滤弹层分钟选项（PickerValues 反射直调）、ShowSeconds
/// 变更触发重同步、AM/PM 选择器与弹层开合（headless 下 Select 弹层交互面按反射直调
/// 与结构断言据实登记，联动键盘面属 Select 家族已有覆盖）、禁用/只读面与强制关闭、
/// MinuteStep/Value/时间界守卫异常面、segmented-input.js 初始化沿——barrel import 与
/// segmentedInput.initialize/dispose 载荷（ElementReference）。隐藏输入格式串
/// @"hh\:mm\:ss" 逐字符照，期望值由同一表达式在测试内复算（InvariantCulture）。
/// 快照含 popover-N/select-N 短 id，先经正则归一化。
/// </summary>
public class TimeInputTests
{
    private static readonly CultureInfo enUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>归一化 popover-N、select-N 与 Input 随机短 id，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "popover-[0-9]+", "popover-<n>"),
                "select-[0-9]+", "select-<n>"),
            "input-[0-9a-f]{8}", "input-<id>");

    /// <summary>构造带基元服务的渲染器（与 CalendarTests 同构，弹层与 Select 需要门户/定位/焦点服务）。</summary>
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

    /// <summary>挂载 PortalHost（根 0）+ TimeInput（根 1），使弹层门户内容可进入渲染树遍历。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, TimeInput Input)> MountAsync(
        Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        extra.TryAdd(nameof(TimeInput.Culture), enUs);
        var input = await renderer.MountAsync<TimeInput>(extra);
        await SettleAsync(renderer);
        return (renderer, js, input);
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

    /// <summary>向第 n 个分段输入派发 oninput 文本。</summary>
    private static Task TypeAsync(ComponentTestRenderer renderer, int occurrence, string text) =>
        FireAsync(renderer, "oninput", new ChangeEventArgs { Value = text }, occurrence);

    /// <summary>反射直调私有方法（SetPeriodAsync/PickerValues）。</summary>
    private static object? InvokePrivate(TimeInput input, string name, params object?[] args) =>
        typeof(TimeInput)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(input, args);

    /// <summary>按 data-atom-segment 抓取某分段 input 开标签内的属性值。</summary>
    private static string? SegmentAttr(string markup, string segment, string attr)
    {
        var match = Regex.Match(markup, $"<input[^>]*data-atom-segment=\"{segment}\"[^>]*>");
        return match.Success ? Regex.Match(match.Value, $"{attr}=\"([^\"]*)\"").Groups[1] is { Length: > 0 } g ? g.Value : null : null;
    }

    [Fact]
    public async Task 美式文化默认十二小时制并按PM回显选值()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(14, 30, 45),
            [nameof(TimeInput.ShowSeconds)] = true,
            [nameof(TimeInput.Name)] = "meeting.at",
        });
        var markup = renderer.Markup();

        // en-US ShortTimePattern "h:mm tt" → Hour12；14:30 → 02:30 PM。
        var order = Regex.Matches(markup, "data-atom-segment=\"(\\w+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["Hour", "Minute", "Second"], order);
        Assert.Equal("02", SegmentAttr(markup, "Hour", "value"));
        Assert.Equal("30", SegmentAttr(markup, "Minute", "value"));
        Assert.Equal("45", SegmentAttr(markup, "Second", "value"));
        Assert.Equal("1", SegmentAttr(markup, "Hour", "data-min")); // Hour12：时下界 1
        Assert.Equal("12", SegmentAttr(markup, "Hour", "data-max"));
        Assert.Equal("59", SegmentAttr(markup, "Second", "data-max"));
        Assert.Contains("aria-label=\"Time\"", markup);
        Assert.Contains("aria-label=\"AM or PM\"", markup);
        Assert.Contains("aria-label=\"AM or PM\">PM", markup); // AM/PM 选择器显示态（尾随换行后接箭头 svg）
        Assert.Equal(2, Regex.Count(markup, ">:<")); // 两段分隔符

        // 隐藏输入沿：格式串照抄，期望值同式复算。
        Assert.Contains(
            $"""<input type="hidden" name="meeting.at" value="{new TimeSpan(14, 30, 45).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}">""",
            markup);
    }

    [Fact]
    public async Task 午夜与正午应落十二小时制的12点边界()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(0, 30, 0),
        });
        var markup = renderer.Markup();
        Assert.Equal("12", SegmentAttr(markup, "Hour", "value")); // ((0+11)%12)+1
        Assert.Contains("aria-label=\"AM or PM\">AM", markup);

        var (noon, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(12, 0, 0),
        });
        Assert.Equal("12", SegmentAttr(noon.Markup(), "Hour", "value"));
        Assert.Contains("aria-label=\"AM or PM\">PM", noon.Markup());
    }

    [Fact]
    public async Task Format覆写应切换二十四小时制并移除时段选择器()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(14, 30, 0),
            [nameof(TimeInput.Format)] = TimeFormat.Hour24,
        });
        var markup = renderer.Markup();

        Assert.Equal("14", SegmentAttr(markup, "Hour", "value"));
        Assert.Equal("0", SegmentAttr(markup, "Hour", "data-min"));
        Assert.Equal("23", SegmentAttr(markup, "Hour", "data-max"));
        Assert.DoesNotContain("aria-label=\"AM or PM\"", markup);
    }

    [Fact]
    public async Task 逐段输入应合成时刻并回调()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Format)] = TimeFormat.Hour24, // 绕开 12 小时制下界 1 的干扰
            [nameof(TimeInput.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });

        await TypeAsync(renderer, 0, "09"); // Hour
        await TypeAsync(renderer, 1, "30"); // Minute
        await SettleAsync(renderer);

        Assert.Equal(new TimeSpan(9, 30, 0), reports.Last());
        Assert.Equal(new TimeSpan(9, 30, 0), input.Value);
    }

    [Fact]
    public async Task 翻定PM应重算时刻而分段回显保持十二小时制()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(2, 30, 0), // 02:30 AM
            [nameof(TimeInput.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });

        await renderer.Dispatcher.InvokeAsync(
            async () => await (Task)InvokePrivate(input, "SetPeriodAsync", true)!);
        await SettleAsync(renderer);

        Assert.Equal(new TimeSpan(14, 30, 0), reports.Last());
        Assert.Equal("02", SegmentAttr(renderer.Markup(), "Hour", "value")); // 回显仍是 12 小时制
    }

    [Fact]
    public async Task MinuteStep应过滤弹层分钟选项并保留当前值()
    {
        var (_, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(12, 30, 0), // pm 文化下回显 30 分
            [nameof(TimeInput.MinuteStep)] = 15,
        });

        var minutes = ((IEnumerable<int>)InvokePrivate(input, "PickerValues", "Minute")!).ToList();
        Assert.Equal([0, 15, 30, 45], minutes); // 30 既是步长倍数也在当前值豁免沿

        var (_, _, offStep) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(12, 37, 0),
            [nameof(TimeInput.MinuteStep)] = 15,
        });
        var kept = ((IEnumerable<int>)InvokePrivate(offStep, "PickerValues", "Minute")!).ToList();
        Assert.Contains(37, kept); // 非倍数当前值仍入选项面
        Assert.Equal(5, kept.Count); // 0/15/30/45 + 37
    }

    [Fact]
    public async Task 时钟弹层应可打开并在只读重渲染时强制关闭()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(14, 30, 0),
        });
        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Open time picker\"", markup);
        Assert.DoesNotContain("atom:w-20", markup); // 关闭态无弹层选择器

        // 关闭态 onclick 沿：0=AM/PM 的 SelectTrigger（combobox 按钮），1=弹层触发按钮（模板序）。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await RerenderPortalAsync(renderer); // 门户宿主根先遍历，弹层内容落入渲染树
        Assert.Contains("atom:w-20", renderer.Markup()); // 打开态：时/分弹层选择器挂载
        Assert.Equal(2, Regex.Count(renderer.Markup(), "aria-label=\"AM or PM\"")); // 顶部 + 弹层各一枚

        // 只读重渲染沿 OnParametersSet 的 open=false 强制关闭。
        var parameters = new Dictionary<string, object?>
        {
            [nameof(TimeInput.Culture)] = enUs,
            [nameof(TimeInput.Value)] = new TimeSpan(14, 30, 0),
            [nameof(TimeInput.ReadOnly)] = true,
        };
        await renderer.RerenderRootAsync(1, parameters);
        await RerenderPortalAsync(renderer);
        // 只读强制关闭沿：headless 无 JS 退场回调，门户内容滞留 DOM（基元 FloatingPortal
 // 的 JsOnClosed 沿，据实登记），改断触发器 aria-expanded 关闭沿与只读面。
        Assert.Contains("aria-expanded=\"false\"",
            Regex.Match(renderer.Markup(), """<button[^>]*aria-label="Open time picker"[^>]*>""").Value);
        // 只读面：分段带 readonly；Disabled 容器遮罩类（原子对）不出现——
        // 单写 atom:opacity-50 会误中弹层 SelectTrigger 的 V 形箭头 svg 类。
        Assert.Contains(" readonly", Regex.Match(renderer.Markup(), "<input[^>]*data-atom-segment=\"Hour\"[^>]*>").Value);
        Assert.DoesNotContain("atom:opacity-50 atom:cursor-not-allowed", renderer.Markup());
    }

    [Fact]
    public async Task 禁用面应落位到分段隐藏输入与选择器()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(14, 30, 0),
            [nameof(TimeInput.Disabled)] = true,
        });
        var markup = renderer.Markup();

        foreach (var segment in new[] { "Hour", "Minute" })
        {
            var tag = Regex.Match(markup, $"<input[^>]*data-atom-segment=\"{segment}\"[^>]*>").Value;
            Assert.Contains(" disabled", tag);
        }

        Assert.Contains("atom:opacity-50 atom:cursor-not-allowed", markup);
        var hidden = Regex.Match(markup, """<input type="hidden"[^>]*>""").Value;
        Assert.Contains(" disabled", hidden);
        // 触发按钮禁用沿：Button 把 disabled 落为独立属性 token。
        var button = Regex.Match(markup, """<button[^>]*aria-label="Open time picker"[^>]*>""").Value;
        Assert.Matches(" disabled[ >]", button);
    }

    [Fact]
    public async Task ShowSeconds变更应触发重同步并显形秒段()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(14, 30, 45),
        };
        var (renderer, _, _) = await MountAsync(parameters);
        Assert.DoesNotContain("data-atom-segment=\"Second\"", renderer.Markup());

        parameters[nameof(TimeInput.ShowSeconds)] = true;
        await renderer.RerenderRootAsync(1, parameters);
        await SettleAsync(renderer);

        Assert.Equal("45", SegmentAttr(renderer.Markup(), "Second", "value"));
        Assert.Equal("02", SegmentAttr(renderer.Markup(), "Hour", "value"));
    }

    [Fact]
    public async Task ClearAsync应清空全段并回调空值()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(14, 30, 0),
            [nameof(TimeInput.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });

        await renderer.Dispatcher.InvokeAsync(() => input.ClearAsync());
        await SettleAsync(renderer);

        Assert.Equal([null], reports);
        Assert.Null(input.Value);
        Assert.Equal(0, Regex.Count(renderer.Markup(), """value="\d"""));
    }

    [Fact]
    public async Task 越界的步进时长与时间界应抛出守卫异常()
    {
        var (renderer, _) = CreateRenderer();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => renderer.MountAsync<TimeInput>(
            new Dictionary<string, object?>
            {
                [nameof(TimeInput.Culture)] = enUs,
                [nameof(TimeInput.MinuteStep)] = 0,
            }));

        var (second, _) = CreateRenderer();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => second.MountAsync<TimeInput>(
            new Dictionary<string, object?>
            {
                [nameof(TimeInput.Culture)] = enUs,
                [nameof(TimeInput.Value)] = TimeSpan.FromHours(24),
            }));

        var (third, _) = CreateRenderer();
        await Assert.ThrowsAsync<ArgumentException>(() => third.MountAsync<TimeInput>(
            new Dictionary<string, object?>
            {
                [nameof(TimeInput.Culture)] = enUs,
                [nameof(TimeInput.MinTime)] = TimeSpan.FromHours(9),
                [nameof(TimeInput.MaxTime)] = TimeSpan.FromHours(5),
            }));
    }

    [Fact]
    public async Task 首次渲染应导入共享桶并声明segmentedInput初始化载荷()
    {
        var (_, js, _) = await MountAsync(new Dictionary<string, object?>());

        var import = Assert.Single(js.Invocations,
            i => i.Identifier == "import" && ((string?)i.Args?[0])?.Contains("components-core.js") == true);
        Assert.Contains("components-core.js", Assert.IsType<string>(import.Args![0]));

        var (_, args) = Assert.Single(js.Invocations, i => i.Identifier == "segmentedInput.initialize");
        Assert.IsType<ElementReference>(args![0]);
    }

    [Fact]
    public async Task DisposeAsync应派发segmentedInput释放而不重复初始化()
    {
        var (renderer, js, input) = await MountAsync(new Dictionary<string, object?>());
        await renderer.Dispatcher.InvokeAsync(async () => await input.DisposeAsync());

        var (_, args) = Assert.Single(js.Invocations, i => i.Identifier == "segmentedInput.dispose");
        Assert.IsType<ElementReference>(args![0]);
        Assert.Single(js.Invocations, i => i.Identifier == "segmentedInput.initialize");
    }

    [Fact]
    public async Task 十二小时制闭合态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(14, 30, 0),
            [nameof(TimeInput.Id)] = "meeting-time",
            [nameof(TimeInput.Name)] = "meeting.at",
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 二十四小时制含秒与步长结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimeInput.Value)] = new TimeSpan(14, 30, 45),
            [nameof(TimeInput.Format)] = TimeFormat.Hour24,
            [nameof(TimeInput.ShowSeconds)] = true,
            [nameof(TimeInput.MinuteStep)] = 15,
        });
        await Verify(Scrub(renderer.Markup()));
    }
}
