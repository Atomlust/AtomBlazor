using AtomBlazor.Primitives;
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
/// DatePicker 测试：非可编辑模式触发按钮的占位/格式化文本与弹层月历
/// 日历选中回填 ValueChanged 并关闭弹层；可编辑模式（DatePickerInput）文本输入
/// 提交解析（InputFormats/DateFormat 与 ISO 恒接受）、不可解析回退当前值、空提交
/// 置 null、Min/Max/DisabledDates 排除回退、切换按钮经 TriggerContext 开合浮层、
/// Escape 关闭、Required/Disabled/IsInvalid 无障碍面、EditContext 字段变更通知、
/// HandleContentReady 把焦点派发进日历（elementUtils.focusElement 载荷）并加闩防重。
/// headless 限制：弹层关闭后 FloatingPortal 内容滞留 DOM（基元 JsOnClosed 沿）
/// 改断触发器 aria-expanded；OnContentReady 定位回调在 NoopJavaScript 下不自动触发
/// 反射直调处理沿；Calendar 默认年月下拉占 onclick 序 1/2，日格自 occurrence 4 起
/// （照 DateInputTests 口径）；基元 PopoverTrigger AsChild 显式 aria 值按 splat
/// 位置规则压过透传值，断言据实。快照含 calendar GUID、popover-N 与 select-N 短 id
/// 先经正则归一化。
/// </summary>
public class DatePickerTests
{
    /// <summary>归一化 calendar GUID 稳定 id、popover-N 与 select-N 自增短 id，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "calendar-[0-9a-f]{32}", "calendar-<id>"),
                "popover-[0-9]+", "popover-<n>"),
            "select-[0-9]+", "select-<n>");

    /// <summary>构造带基元服务的渲染器（与 DateInputTests 同构，弹层需要门户/定位/焦点服务）。</summary>
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

    /// <summary>挂载 PortalHost（根 0）+ DatePicker（根 1），使弹层门户内容可进入渲染树遍历。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, DatePicker Picker)> MountAsync(
        Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var picker = await renderer.MountAsync<DatePicker>(extra);
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

    /// <summary>反射直调私有异步/同步处理方法（OnContentReady 定位沿 headless 不自动触发
    /// 裸 @onkeydown 虽可派发，但门户内 Calendar 表亦携带 @onkeydown 修饰符沿，序位不可靠
    /// 照注记③口径反射直调）。</summary>
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

    /// <summary>按可见文本抓取 button 全标签（含内部标记），用于禁用面断言。</summary>
    private static string ButtonMarkup(string markup, string innerText) =>
        Regex.Matches(markup, "<button[^>]*>.*?</button>", RegexOptions.Singleline)
            .Cast<Match>().First(m => m.Value.Contains(innerText)).Value;

    /// <summary>CA1861：InputFormats 常量数组提为静态字段。</summary>
    private static readonly string[] dmyFormats = ["dd/MM/yyyy", "d/M/yyyy"];

    private sealed class DatePickerModel
    {
        public DateTime? Start { get; set; }
    }

    [Fact]
    public async Task 缺省非可编辑触发器应显占位文本且弹层关闭无月历()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.AriaLabel)] = "Due date",
        });
        var markup = renderer.Markup();

        // 占位沿：Placeholder 未设时取 Localizer["DatePicker.Placeholder"]。
        Assert.Contains("Pick a date", markup);
        Assert.Contains("atom:w-[280px]", markup);
        Assert.DoesNotContain("role=\"grid\"", markup);

        // 触发按钮（AsChild 沿）：无障碍名与关闭态 aria-expanded。
        var button = ButtonMarkup(markup, "Pick a date");
        Assert.Contains("aria-label=\"Due date\"", button);
        Assert.Contains("aria-expanded=\"false\"", button);
    }

    [Fact]
    public async Task 选中值后触发按钮应显格式化文本且摘除占位灰字()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
        });
        var markup = renderer.Markup();

        Assert.Contains("2026-10-12", markup);
        // 有值时 ButtonCssClass 不再叠加 atom:text-muted-foreground。
        Assert.DoesNotContain("atom:text-muted-foreground", markup);
    }

    [Fact]
    public async Task 打开弹层点击日历日应回调选中值并关闭浮层()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.AriaLabel)] = "Due date",
            [nameof(DatePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        // 关闭态唯一 onclick 携带者为触发按钮。
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await RerenderPortalAsync(renderer);
        var open = renderer.Markup();
        Assert.Contains("day-20261020", open);

        // 门户宿主根先遍历：Calendar 默认年月下拉占序 0-3，日格自 4 起
        // 日格序随机器区域性，按渲染 id 反查 20 日的格号（照 DateInputTests 口径）。
        var dayIndex = Regex.Matches(open, "-day-(20\\d{6})").ToList()
            .FindIndex(m => m.Groups[1].Value == "20261020");
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 4 + dayIndex);
        await RerenderPortalAsync(renderer);

        Assert.Equal(new DateTime(2026, 10, 20), reports.Last());
        Assert.Equal(new DateTime(2026, 10, 20), picker.Value);
        // 选中经 HandleDateSelected 置 _isOpen=false：headless 无 JS 退场回调
 // 门户内容滞留 DOM（据实登记），改断触发器 aria-expanded 关闭沿。
        Assert.Contains("aria-expanded=\"false\"",
            Regex.Match(renderer.Markup(), """<button[^>]*aria-label="Due date"[^>]*>""").Value);
    }

    [Fact]
    public async Task 可编辑模式应渲染文本输入与日历切换按钮组合()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.AriaLabel)] = "Invoice date",
        });
        var markup = renderer.Markup();

        var input = Regex.Match(markup, """<input type="text"[^>]*>""").Value;
        Assert.Contains("value=\"2026-10-12\"", input);
        Assert.Contains("aria-label=\"Invoice date\"", input);
        Assert.Contains("placeholder=\"Pick a date\"", markup);
        Assert.Contains("atom:w-[280px]", markup); // 包装元素宽度沿

        var toggle = ButtonMarkup(markup, "Open calendar");
        Assert.Contains("aria-label=\"Open calendar\"", toggle);
        Assert.Contains("aria-expanded=\"false\"", toggle);
        Assert.Contains("aria-haspopup", toggle);
        Assert.DoesNotContain("role=\"grid\"", markup);
    }

    [Fact]
    public async Task 可编辑提交ISO文本应解析并回调选中值()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "2026-10-20" });
        await SettleAsync(renderer);

        Assert.Equal(new DateTime(2026, 10, 20), reports.Single());
        Assert.Equal(new DateTime(2026, 10, 20), picker.Value);
        Assert.Contains("value=\"2026-10-20\"", renderer.Markup());
    }

    [Fact]
    public async Task InputFormats应优先于DateFormat解析键入文本()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.InputFormats)] = dmyFormats,
            [nameof(DatePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "20/10/2026" });
        await SettleAsync(renderer);

        Assert.Equal(new DateTime(2026, 10, 20), reports.Single());
        // ISO 恒被接受：附加格式之外照收 yyyy-MM-dd。
        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "2026-11-01" });
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 11, 1), reports.Last());
    }

    [Fact]
    public async Task 不可解析文本提交应回退当前值且零回调()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "  not-a-date " });
        await SettleAsync(renderer);

        Assert.Empty(reports);
        Assert.Equal(new DateTime(2026, 10, 12), picker.Value);
        Assert.Contains("value=\"2026-10-12\"", renderer.Markup());
    }

    [Fact]
    public async Task 空文本提交应置空值并回调null()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "  " });
        await SettleAsync(renderer);

        Assert.Equal([null], reports);
        Assert.Null(picker.Value);
    }

    [Fact]
    public async Task 越界与被禁日期提交应回退不回调()
    {
        var reports = new List<DateTime?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.MaxDate)] = new DateTime(2026, 10, 18),
            [nameof(DatePicker.DisabledDates)] = (Func<DateTime, bool>)(d => d.Day == 15),
            [nameof(DatePicker.ValueChanged)] =
                EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)),
        });

        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "2026-10-20" }); // 超 MaxDate
        await SettleAsync(renderer);
        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "2026-10-15" }); // 谓词命中
        await SettleAsync(renderer);

        Assert.Empty(reports);
        Assert.Equal(new DateTime(2026, 10, 12), picker.Value);
        Assert.Contains("value=\"2026-10-12\"", renderer.Markup());
    }

    [Fact]
    public async Task 外部Value变更应同步可编辑输入文本()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
        };
        var (renderer, _, _) = await MountAsync(parameters);

        parameters[nameof(DatePicker.Value)] = new DateTime(2027, 1, 5);
        await renderer.RerenderRootAsync(1, parameters);
        await SettleAsync(renderer);

        Assert.Contains("value=\"2027-01-05\"", renderer.Markup());
    }

    [Fact]
    public async Task 切换按钮应经TriggerContext打开浮层且Escape关闭()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
        });

        // 关闭态唯一 onclick 携带者为切换按钮（输入框只有 onchange/onkeydown）。
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await RerenderPortalAsync(renderer);
        var open = renderer.Markup();
        Assert.Contains("role=\"grid\"", open);
        Assert.Contains("aria-expanded=\"true\"",
            ButtonMarkup(open, "Open calendar"));

        // Escape 只关闭日历不动文本：反射直调 DatePickerInput 的 HandleKeyDown
        // （门户打开态 Calendar 表亦在 onkeydown 序上，派发序位不可靠，注记③口径）。
        var editableInput = renderer.FindComponent<DatePickerInput>();
        await InvokePrivateAsync(renderer, editableInput, "HandleKeyDown", new KeyboardEventArgs { Key = "Escape" });
        await RerenderPortalAsync(renderer);
        Assert.Contains("aria-expanded=\"false\"",
            ButtonMarkup(renderer.Markup(), "Open calendar"));
    }

    [Fact]
    public async Task 禁用面应落输入框与切换按钮并遮罩包装元素()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.Disabled)] = true,
        });
        var markup = renderer.Markup();

        Assert.Matches(" disabled[ >]", Regex.Match(markup, """<input type="text"[^>]*>""").Value);
        Assert.Matches(" disabled[ >]", ButtonMarkup(markup, "Open calendar"));
        // 本组件禁用对的词序为 cursor-not-allowed 在前（与 DateInput 容器相反），据实断言。
        Assert.Contains("atom:cursor-not-allowed atom:opacity-50", markup);
        Assert.Contains("tabindex=\"-1\"", ButtonMarkup(markup, "Open calendar"));
    }

    [Fact]
    public async Task 必填面应落AriaRequired到输入与按钮两种触发器()
    {
        var (editable, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.Required)] = true,
        });
        Assert.Contains("aria-required=\"true\"",
            Regex.Match(editable.Markup(), """<input type="text"[^>]*>""").Value);

        var (plain, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Required)] = true,
            [nameof(DatePicker.AriaLabel)] = "Due date",
        });
        Assert.Contains("aria-required=\"true\"",
            Regex.Match(plain.Markup(), """<button[^>]*aria-label="Due date"[^>]*>""").Value);
    }

    [Fact]
    public async Task IsInvalid应落AriaInvalid到可编辑输入()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            [nameof(DatePicker.IsInvalid)] = true,
        });
        var markup = renderer.Markup();
        Assert.Contains("aria-invalid=\"true\"", markup);
        Assert.Contains("atom:aria-[invalid=true]:border-destructive", markup);
    }

    [Fact]
    public async Task EditContext下提交有效日期应通知字段变更()
    {
        var model = new DatePickerModel();
        var context = new EditContext(model);
        var changedFields = new List<string>();
        context.OnFieldChanged += (_, e) => changedFields.Add(e.FieldIdentifier.FieldName);
        Expression<Func<DateTime?>> expression = () => model.Start;

        var (renderer, _) = CreateRenderer();
        RenderFragment fieldFragment = builder =>
        {
            builder.OpenComponent<DatePicker>(0);
            builder.AddAttribute(1, nameof(DatePicker.Editable), true);
            builder.AddAttribute(2, nameof(DatePicker.DateFormat), "yyyy-MM-dd");
            builder.AddAttribute(3, nameof(DatePicker.ValueExpression), expression);
            builder.CloseComponent();
        };
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = fieldFragment,
        });
        await SettleAsync(renderer);

        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "2026-10-20" });
        await SettleAsync(renderer);

        Assert.Contains("Start", changedFields);
    }

    [Fact]
    public async Task HandleContentReady应派发活动日焦点并加闩防重()
    {
        var (renderer, js, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.AriaLabel)] = "Due date",
        });

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await RerenderPortalAsync(renderer);
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
    public async Task 附加属性应透传到触发按钮()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.AriaLabel)] = "Due date",
            ["data-test"] = "dp-1",
        });
        Assert.Contains("data-test=\"dp-1\"",
            Regex.Match(renderer.Markup(), """<button[^>]*aria-label="Due date"[^>]*>""").Value);
    }

    [Fact]
    public async Task 缺省非可编辑闭合态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Value)] = new DateTime(2026, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.AriaLabel)] = "Due date",
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 可编辑与日历打开态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(DatePicker.Editable)] = true,
            // 钉已过去的月份（2025-10）：弹层 Calendar 的今日高亮随 DateTime.Today 漂移。
            [nameof(DatePicker.Value)] = new DateTime(2025, 10, 12),
            [nameof(DatePicker.DateFormat)] = "yyyy-MM-dd",
            [nameof(DatePicker.AriaLabel)] = "Invoice date",
        });
        await FireAsync(renderer, "onclick", new MouseEventArgs()); // 切换按钮打开日历
        await RerenderPortalAsync(renderer);
        await Verify(Scrub(renderer.Markup()));
    }
}
