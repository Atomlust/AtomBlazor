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
/// DateInput 测试：en-US 钉死文化下的分段序（M/D/Y）与分隔符回显、文本输入
/// 合成分段回调 ValueChanged、不完整分段置 null 并显形 invalid 错误、MaxDay 随年月
/// 分段联动（data-max 动态面）、Calendar 弹层开合与选中回填（portal 内容 onclick 先于
/// 触发器的顺序沿照 ColorPickerTests）、受控 Value 同步、ClearAsync、禁用/必填面、
/// EditContext 校验沿（name 、字段变化通知、错误消息）、MinDate&gt;MaxDate 守卫、
/// segmented-input.js 初始化沿——barrel import 与 segmentedInput.initialize/dispose
/// 载荷（ElementReference）。headless 限制：段间移焦与自旋键盘属 JS 侧行为，由
/// node 侧 segmented-input-module.test.mjs 覆盖；基元 PopoverTrigger AsChild 显式值
/// 按 splat 位置覆盖据实断言（照 TreeSelect/Combobox 口径）。快照含 calendar GUID、
/// popover-N 与 select-N 短 id，先经正则归一化。
/// </summary>
public class DateInputTests
{
    private static readonly CultureInfo enUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>归一化 calendar GUID 稳定 id、popover-N 与 select-N 自增短 id，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "calendar-[0-9a-f]{32}", "calendar-<id>"),
                "popover-[0-9]+", "popover-<n>"),
            "select-[0-9]+", "select-<n>");

    /// <summary>构造带基元服务的渲染器（与 CalendarTests 同构，弹层需要门户/定位/焦点服务）。</summary>
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

    /// <summary>排空渲染队列。</summary>
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>挂载 PortalHost（根 0）+ DateInput（根 1），使弹层门户内容可进入渲染树遍历。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, DateInput Input)> MountAsync(
        Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        extra.TryAdd(nameof(DateInput.Culture), enUs);
        var input = await renderer.MountAsync<DateInput>(extra);
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

    /// <summary>反射直调私有 SelectAsync（headless 下模拟 Calendar 的 SelectedChanged 沿）。</summary>
    private static Task SelectAsync(DateInput input, DateTime? value) =>
        input.GetType()
            .GetMethod("SelectAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(input, [value]) is Task task ? task : Task.CompletedTask;

    /// <summary>按 data-atom-segment 抓取某分段 input 开标签内的属性值。</summary>
    private static string? SegmentAttr(string markup, string segment, string attr)
    {
        var match = Regex.Match(markup, $"<input[^>]*data-atom-segment=\"{segment}\"[^>]*>");
        return match.Success ? Regex.Match(match.Value, $"{attr}=\"([^\"]*)\"").Groups[1] is { Length: > 0 } g ? g.Value : null : null;
    }

    private sealed class DateModel
    {
        public DateTime? Start { get; set; }
    }

    [Fact]
    public async Task 默认渲染应按美式段序回显日期并挂ISO隐藏输入()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateInput.Value)] = new DateTime(2026, 10, 12),
            [nameof(DateInput.Name)] = "trip.start",
        });
        var markup = renderer.Markup();

        // en-US ShortDatePattern M/d/yyyy：Month、Day、Year 依次渲染，两段之间一个 "/"。
        var order = Regex.Matches(markup, "data-atom-segment=\"(\\w+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["Month", "Day", "Year"], order);
        Assert.Equal(2, Regex.Count(markup, ">/<"));

        Assert.Equal("10", SegmentAttr(markup, "Month", "value"));
        Assert.Equal("12", SegmentAttr(markup, "Day", "value"));
        Assert.Equal("2026", SegmentAttr(markup, "Year", "value"));
        Assert.Equal("mm", SegmentAttr(markup, "Month", "placeholder"));
        Assert.Equal("yyyy", SegmentAttr(markup, "Year", "placeholder"));
        Assert.Equal("4", SegmentAttr(markup, "Year", "maxlength"));
        Assert.Equal("9999", SegmentAttr(markup, "Year", "data-max"));

        // 组与段的无障碍面：Localizer 英文词条。
        Assert.Contains("aria-label=\"Date\"", markup);
        Assert.Contains("aria-label=\"Year\"", markup);
        Assert.Contains("aria-valuemax=\"31\"", markup);

        // 隐藏表单输入：不变文化 ISO 日期。
        Assert.Contains(
            """<input type="hidden" name="trip.start" value="2026-10-12">""", markup);

        // 日历触发按钮沿（AsChild 显式值按 splat 位置覆盖，据实断言）。
        Assert.Contains("aria-label=\"Open calendar\"", markup);
    }

    [Fact]
    public async Task 逐段输入完整日期应恰好一次回调合成值()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateInput.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });
        Assert.Empty(reports);

        await TypeAsync(renderer, 0, "10"); // Month
        await TypeAsync(renderer, 1, "12"); // Day
        await SettleAsync(renderer);
        Assert.Empty(reports); // 尚缺年：合成为 null，null→null 无变化不回调

        await TypeAsync(renderer, 2, "2026"); // Year
        await SettleAsync(renderer);

        Assert.Equal(new DateTime(2026, 10, 12), reports.Last());
        Assert.Equal(new DateTime(2026, 10, 12), input.Value);
        Assert.Single(reports); // 完整日期是唯一一次变化发布
    }

    [Fact]
    public async Task 不完整分段应把值置空并显形无效错误()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateInput.Value)] = new DateTime(2026, 10, 12),
            [nameof(DateInput.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        await TypeAsync(renderer, 1, ""); // 清空 Day
        await SettleAsync(renderer);

        Assert.Equal([null], reports);
        var markup = renderer.Markup();
        Assert.Contains("aria-invalid=\"true\"", markup); // 本地错误置位（无 EditContext 也显形）
        Assert.DoesNotContain("value=\"2026-10-12\"", markup);
    }

    [Fact]
    public async Task 非法完整日期应显形无效错误且值为空()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>());
        await TypeAsync(renderer, 0, "02"); // Month=2
        await TypeAsync(renderer, 1, "30"); // Day=30（2 月 30 日非法）
        await TypeAsync(renderer, 2, "2026");
        await SettleAsync(renderer);

        Assert.Null(input.Value);
        Assert.Contains("aria-invalid=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task MaxDay应随年月分段联动收窄日段上界()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>());

        await TypeAsync(renderer, 0, "02"); // Month=2（Year 仍空 → data-max 维持 31）
        Assert.Equal("31", SegmentAttr(renderer.Markup(), "Day", "data-max"));

        await TypeAsync(renderer, 2, "2026"); // 平年 2 月 28 天
        Assert.Equal("28", SegmentAttr(renderer.Markup(), "Day", "data-max"));

        await TypeAsync(renderer, 2, "2028"); // 闰年 2 月 29 天
        Assert.Equal("29", SegmentAttr(renderer.Markup(), "Day", "data-max"));

        await TypeAsync(renderer, 0, "01");
        Assert.Equal("31", SegmentAttr(renderer.Markup(), "Day", "data-max"));
    }

    [Fact]
    public async Task 日历弹层开合应经选中日回填分段与回调()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateInput.Value)] = new DateTime(2026, 10, 12),
            [nameof(DateInput.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        // 关闭态：仅触发按钮，无月历格。
        Assert.DoesNotContain("day-20261020", renderer.Markup());

        // 点击日历触发按钮打开弹层（关闭态下唯一 onclick 携带者），并冲刷门户。
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await RerenderPortalAsync(renderer);
        var open = renderer.Markup();
        Assert.Contains("day-20261020", open);

        // 打开态 onclick 沿：门户宿主根先遍历——Calendar 默认年月下拉头部
        // （ShowMonthYearDropdowns 缺省 true，接线沿）：上月 0、月 1、年 2、下月 3
        // 日格自 4 起；日格周序随机器区域性（DateInput 不向 Calendar 传钉死参数，如此）
        // 故按渲染序动态定位 10 月 20 日的格号（照 CalendarTests 的 id 口径）。
        var dayIndex = Regex.Matches(open, "-day-(20\\d{6})").ToList()
            .FindIndex(m => m.Groups[1].Value == "20261020");
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 4 + dayIndex);
        await RerenderPortalAsync(renderer);

        Assert.Equal(new DateTime(2026, 10, 20), reports.Last());
        Assert.Equal(new DateTime(2026, 10, 20), input.Value);
        var markup = renderer.Markup();
        Assert.Equal("20", SegmentAttr(markup, "Day", "value"));

        // 选中后 open 置 false：headless 无 JS 退场回调，门户内容滞留 DOM（基元
 // FloatingPortal 的 JsOnClosed 沿，据实登记），改断触发器 aria-expanded 关闭沿。
        Assert.Contains("aria-expanded=\"false\"",
            Regex.Match(markup, """<button[^>]*aria-label="Open calendar"[^>]*>""").Value);
    }

    [Fact]
    public async Task 反射直调SelectAsync越界日期应拒绝而不回调()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateInput.Value)] = new DateTime(2026, 10, 12),
            [nameof(DateInput.MaxDate)] = new DateTime(2026, 10, 18),
            [nameof(DateInput.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        await renderer.Dispatcher.InvokeAsync(() => SelectAsync(input, new DateTime(2026, 10, 20)));
        await SettleAsync(renderer);

        Assert.Empty(reports);
        Assert.Equal(new DateTime(2026, 10, 12), input.Value);
    }

    [Fact]
    public async Task 受控Value变更应同步分段与隐藏输入()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(DateInput.Value)] = new DateTime(2026, 10, 12),
            [nameof(DateInput.Name)] = "birth",
        };
        var (renderer, _, _) = await MountAsync(parameters);

        parameters[nameof(DateInput.Value)] = new DateTime(2027, 1, 5);
        await renderer.RerenderRootAsync(1, parameters);
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Equal("01", SegmentAttr(markup, "Month", "value"));
        Assert.Equal("05", SegmentAttr(markup, "Day", "value"));
        Assert.Equal("2027", SegmentAttr(markup, "Year", "value"));
        Assert.Contains("value=\"2027-01-05\"", markup);
    }

    [Fact]
    public async Task ClearAsync应清空全段并回调空值()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateInput.Value)] = new DateTime(2026, 10, 12),
            [nameof(DateInput.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        await renderer.Dispatcher.InvokeAsync(() => input.ClearAsync());
        await SettleAsync(renderer);

        Assert.Equal([null], reports);
        Assert.Null(input.Value);
        var markup = renderer.Markup();
        Assert.Equal(3, Regex.Count(markup, "data-atom-segment"));
        Assert.Equal(0, Regex.Count(markup, """value="\d""")); // 全段与隐藏输入均空
    }

    [Fact]
    public async Task 禁用与必填面应落位到每个分段与隐藏输入()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateInput.Value)] = new DateTime(2026, 10, 12),
            [nameof(DateInput.Disabled)] = true,
            [nameof(DateInput.Required)] = true,
        });
        var markup = renderer.Markup();

        foreach (var segment in new[] { "Month", "Day", "Year" })
        {
            Assert.Contains($"data-atom-segment=\"{segment}\"", markup);
            var tag = Regex.Match(markup, $"<input[^>]*data-atom-segment=\"{segment}\"[^>]*>").Value;
            Assert.Contains(" disabled", tag);
            Assert.Contains(" required", tag);
        }

        Assert.Contains("atom:opacity-50 atom:cursor-not-allowed", markup);
        var hidden = Regex.Match(markup, """<input type="hidden"[^>]*>""").Value;
        Assert.Contains(" disabled", hidden);
        // 触发按钮禁用沿：Button 把 disabled 落为独立属性 token（类串中的 atom:disabled: 前缀不匹配）。
        var button = Regex.Match(markup, """<button[^>]*aria-label="Open calendar"[^>]*>""").Value;
        Assert.Matches(" disabled[ >]", button);
    }

    [Fact]
    public async Task 最小日期大于最大日期应抛出参数异常()
    {
        var (renderer, _) = CreateRenderer();
        await Assert.ThrowsAsync<ArgumentException>(() => renderer.MountAsync<DateInput>(
            new Dictionary<string, object?>
            {
                [nameof(DateInput.Culture)] = enUs,
                [nameof(DateInput.MinDate)] = new DateTime(2026, 10, 20),
                [nameof(DateInput.MaxDate)] = new DateTime(2026, 10, 10),
            }));
    }

    [Fact]
    public async Task EditContext校验沿应name并显形本地错误与字段变更()
    {
        var model = new DateModel();
        var context = new EditContext(model);
        var changedFields = new List<string>();
        context.OnFieldChanged += (_, e) => changedFields.Add(e.FieldIdentifier.FieldName);
        Expression<Func<DateTime?>> expression = () => model.Start;

        var (renderer, _) = CreateRenderer();
        RenderFragment fieldFragment = builder =>
        {
            builder.OpenComponent<DateInput>(0);
            builder.AddAttribute(1, nameof(DateInput.Culture), enUs);
            builder.AddAttribute(2, nameof(DateInput.ValueExpression), expression);
            builder.CloseComponent();
        };
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = fieldFragment,
        });
        await SettleAsync(renderer);

        // 非法完整输入 → 消息入库 + 字段变化通知 + name 自表达式路径。
        await TypeAsync(renderer, 0, "02");
        await TypeAsync(renderer, 1, "30");
        await TypeAsync(renderer, 2, "2026");
        await SettleAsync(renderer);

        var field = FieldIdentifier.Create(expression);
        Assert.Contains("Enter a complete, valid date within the allowed range.",
            context.GetValidationMessages(field).ToList());
        Assert.Contains("Start", changedFields);
        // name：ExpressionPathFormatter 自内向外收集成员链，捕获局部变量形态下
        // 会带上闭包字段段（"model.Start"），照 CheckboxTests 口径按尾段断言。
        Assert.Matches("name=\"[^\"]*Start\"", renderer.Markup());
        Assert.Contains("aria-invalid=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 首次渲染应导入共享桶文件并声明segmentedInput初始化载荷()
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
    public async Task 默认闭合态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateInput.Value)] = new DateTime(2026, 10, 12),
            [nameof(DateInput.Id)] = "invoice-due",
            [nameof(DateInput.Name)] = "invoice.due",
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 必填与日历打开态结构快照应与基线一致()
    {
        // 夹具钉在已过去的月份（2025-10）：弹层内 Calendar 含 DateTime.Today 今日高亮
        // 显示月若含"今天"则快照逐日漂移，钉过去月后恒稳。
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DateInput.Value)] = new DateTime(2025, 10, 12),
            [nameof(DateInput.Required)] = true,
        });
        await FireAsync(renderer, "onclick", new MouseEventArgs()); // 打开日历弹层
        await RerenderPortalAsync(renderer);
        await Verify(Scrub(renderer.Markup()));
    }
}
