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
/// TimePicker 测试：非受控触发按钮的占位/格式化文本（12 与 24 小时制双分支、
/// ShowSeconds 附加秒位）、弹层三列滚钮步进（Increment/Decrement Hour/Minute/Second）、
/// MinuteStep 步进沿与分钟下溢回绕（取模语义 bug-for-bug）、AM/PM 切换高亮与
/// 12 时边界（正午/午夜落 12）、MinTime/MaxTime 钳制、Now 按步长对齐且秒位清零、
/// Clear 复位占位并回调 null、Required/Disabled 触发器面、附加属性 splat、
/// EditContext 字段变更通知、ShouldRender 三态跟踪下的开合重渲染。
/// headless 限制：弹层关闭后 FloatingPortal 内容滞留 DOM（基元 JsOnClosed 沿）
/// 改断触发器 aria-expanded；打开态 onclick 序沿门户宿主根先遍历——滚钮与时段钮
/// 与操作钮占序 0-7（ShowSeconds 追加秒列两位，后序整体 +2），触发按钮居后
/// 基元 PopoverTrigger AsChild 显式 aria 值按 splat 位置规则压过透传值，断言据实。
/// Hour/Min/Sec/Now/Clear/AM/PM 与缺省占位 "Select time" 为硬编码英文字面量
/// （未注入 Localizer，TimePicker.* 键面零引用），快照不含随机器文化漂移内容
/// 仅 popover-N 短 id 需正则归一化。
/// </summary>
public class TimePickerTests
{
    /// <summary>归一化 popover-N 自增短 id，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "popover-[0-9]+", "popover-<n>");

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

    /// <summary>挂载 PortalHost（根 0）+ TimePicker（根 1），使弹层门户内容可进入渲染树遍历。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, TimePicker Picker)> MountAsync(
        Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var picker = await renderer.MountAsync<TimePicker>(extra);
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

    /// <summary>抓取三列显示位（tabular-nums 数值 span）的两位数字串，按时/分/秒渲染序返回。</summary>
    private static List<string> DisplaySpans(string markup) =>
        Regex.Matches(markup, """tabular-nums">\s*(\d{2})\s*<""").Select(m => m.Groups[1].Value).ToList();

    private sealed class TimePickerModel
    {
        public TimeSpan? Start { get; set; }
    }

    [Fact]
    public async Task 缺省十二小时制闭合态应显占位文本且弹层未入树()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.AriaLabel)] = "Meeting time",
        });
        var markup = renderer.Markup();

        // 占位沿：Placeholder 缺省硬编码 "Select time"（零 Localizer 注入）。
        Assert.Contains("Select time", markup);
        Assert.Contains("atom:w-[180px]", markup);
        Assert.DoesNotContain("Hour", markup); // 弹层未渲染
        var button = Regex.Match(markup, """<button[^>]*aria-label="Meeting time"[^>]*>""").Value;
        Assert.Contains("aria-expanded=\"false\"", button);
    }

    [Fact]
    public async Task 选中值后触发按钮应显十二小时制格式化文本()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Value)] = new TimeSpan(14, 30, 0),
        });
        var markup = renderer.Markup();

        // 14:30 → 02:30 PM（FormatTime Hour12 分支）。
        Assert.Contains("02:30 PM", markup);
        Assert.DoesNotContain("atom:text-muted-foreground", markup);
    }

    [Fact]
    public async Task 二十四小时制应显hh冒号mm且零AMPM字样()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Value)] = new TimeSpan(14, 30, 0),
            [nameof(TimePicker.Format)] = TimeFormat.Hour24,
        });
        var markup = renderer.Markup();

        Assert.Contains("14:30", markup);
        Assert.DoesNotContain("PM", markup);
    }

    [Fact]
    public async Task 打开弹层应显双列滚钮与默认正午且Clear禁用()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer);
        var markup = renderer.Markup();

        Assert.Contains("Hour", markup);
        Assert.Contains("Min", markup);
        Assert.DoesNotContain("Sec", markup); // ShowSeconds=false
        // 无值缺省：Hour12 下内部落 12 时；显示位为 "12" 与 "00"。
        Assert.Equal(["12", "00"], DisplaySpans(markup));

        // Clear 无值禁用；缺省内部落 12 时而 IsAm 计算沿为 CurrentHour<12=false → PM 高亮
        // （初值 _isAm=true 与 IsAm 属性在正午缺省态不同步的沿，据实断言）。
        Assert.Matches(" disabled[ >]", ButtonMarkup(markup, "Clear"));
        Assert.DoesNotContain("atom:bg-primary", ButtonMarkup(markup, "AM"));
        Assert.Contains("atom:bg-primary", ButtonMarkup(markup, "PM"));
    }

    [Fact]
    public async Task 默认正午时上点小时应落一时并回调()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        // 打开态 onclick 序：门户根先遍历——0 时+、1 时-、2 分+、3 分-、4 AM、5 PM、
        // 6 Now、7 Clear（触发按钮居后）。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await SettleAsync(renderer);

        // 12 小时制沿：hour12=(12%12+1)%12=1，AM 下 _internalHour=1 → 01:00。
        Assert.Equal(new TimeSpan(1, 0, 0), reports.Single());
        Assert.Equal(new TimeSpan(1, 0, 0), picker.Value);
        Assert.Contains("01:00 AM", renderer.Markup());
    }

    [Fact]
    public async Task 二十四小时制应回绕午夜且分秒步进独立()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Format)] = TimeFormat.Hour24,
            [nameof(TimePicker.ShowSeconds)] = true,
            [nameof(TimePicker.Value)] = new TimeSpan(23, 59, 59),
            [nameof(TimePicker.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        // ShowSeconds 追加秒列两钮：0 时+、1 时-、2 分+、3 分-、4 秒+、5 秒-、6 AM? 无——
        // Hour24 无 AM/PM 段，7 Now、8 Clear（段面按渲染序）。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0); // 时+ → 23+1 回绕 0
        await SettleAsync(renderer);
        Assert.Equal(new TimeSpan(0, 59, 59), reports.Last());

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 4); // 秒+ → 59+1 回绕 0
        await SettleAsync(renderer);
        Assert.Equal(new TimeSpan(0, 59, 0), reports.Last());
        Assert.Contains("00:59:00", renderer.Markup());
    }

    [Fact]
    public async Task MinuteStep应参与分步且下溢取模回绕为语义()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Format)] = TimeFormat.Hour24,
            [nameof(TimePicker.MinuteStep)] = 15,
            [nameof(TimePicker.Value)] = new TimeSpan(2, 0, 0),
            [nameof(TimePicker.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        // quirk bug-for-bug：DecrementMinute 取模 (0-15+60)%60=45
        // 零分下点"减分"不进位回退反而前进到同刻 45 分。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 3); // 分-
        await SettleAsync(renderer);
        Assert.Equal(new TimeSpan(2, 45, 0), reports.Last());

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 2); // 分+
        await SettleAsync(renderer);
        Assert.Equal(new TimeSpan(2, 0, 0), reports.Last()); // 45+15=60%60=0
    }

    [Fact]
    public async Task 步进越界应被MinTime与MaxTime钳制()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Format)] = TimeFormat.Hour24,
            [nameof(TimePicker.Value)] = new TimeSpan(22, 0, 0),
            [nameof(TimePicker.MaxTime)] = new TimeSpan(22, 0, 0),
            [nameof(TimePicker.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        // 时+ → 内部 23 时，但 UpdateValue 钳回 22:00（每拍钳制沿）。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await SettleAsync(renderer);
        Assert.Equal(new TimeSpan(22, 0, 0), reports.Last());
        Assert.Equal(new TimeSpan(22, 0, 0), picker.Value);
        // 触发按钮文本按 Value 显示，钳制后无前进。
        Assert.Contains("22:00", renderer.Markup());
    }

    [Fact]
    public async Task 翻PM应平移十二小时且翻AM回落()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Value)] = new TimeSpan(2, 30, 0), // 02:30 AM
            [nameof(TimePicker.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        Assert.Matches("atom:bg-primary", ButtonMarkup(renderer.Markup(), "AM"));

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 5); // PM
        await SettleAsync(renderer);
        Assert.Equal(new TimeSpan(14, 30, 0), reports.Last());
        Assert.Contains("02:30 PM", renderer.Markup()); // 回显仍 12 小时制
        Assert.Matches("atom:bg-primary", ButtonMarkup(renderer.Markup(), "PM"));

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 4); // AM
        await SettleAsync(renderer);
        Assert.Equal(new TimeSpan(2, 30, 0), reports.Last());
    }

    [Fact]
    public async Task 正午与午夜应落十二小时制的12点边界()
    {
        var (noon, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Value)] = new TimeSpan(12, 0, 0),
        });
        await OpenAsync(noon);
        // 12 时：IsAm=false → PM 高亮；DisplayHour=12；SetAmPm 双条件皆不命中（12 不下翻）。
        Assert.Matches("atom:bg-primary", ButtonMarkup(noon.Markup(), "PM"));
        Assert.Equal("12", DisplaySpans(noon.Markup())[0]);

        var (midnight, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Value)] = new TimeSpan(0, 0, 0),
        });
        await OpenAsync(midnight);
        Assert.Matches("atom:bg-primary", ButtonMarkup(midnight.Markup(), "AM"));
        Assert.Equal("12", DisplaySpans(midnight.Markup())[0]); // 0 % 12 == 0 → 显示 12
    }

    [Fact]
    public async Task Now按钮应即时钟且分按步长对齐秒位清零()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.MinuteStep)] = 15,
            [nameof(TimePicker.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 6); // Now
        await SettleAsync(renderer);

        var now = DateTime.Now.TimeOfDay;
        var value = picker.Value!.Value;
        Assert.Equal(value, reports.Last());
        Assert.Equal(now.Hours, value.Hours); // 时沿即时
        Assert.Equal(0, value.Minutes % 15); // 分按步长向下对齐
        Assert.Equal(0, value.Seconds); // ShowSeconds=false → 秒清零
    }

    [Fact]
    public async Task Clear应先经Now激活再复位占位并回调null()
    {
        var reports = new List<TimeSpan?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 6); // Now → 有值
        await SettleAsync(renderer);
        Assert.NotNull(picker.Value);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 7); // Clear
        await SettleAsync(renderer);

        Assert.Null(reports.Last());
        Assert.Null(picker.Value);
        Assert.Contains("Select time", renderer.Markup());
        // 复位沿：Hour12 内部回到 12 时正午缺省。
        Assert.Equal(["12", "00"], DisplaySpans(renderer.Markup()));
    }

    [Fact]
    public async Task 禁用与必填面应落触发按钮与弹层滚钮()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(TimePicker.Required)] = true,
            [nameof(TimePicker.AriaLabel)] = "Meeting time",
        };
        var (renderer, _, _) = await MountAsync(parameters);

        // 必填沿恒在：aria-required 落触发按钮。
        Assert.Contains("aria-required=\"true\"",
            Regex.Match(renderer.Markup(), """<button[^>]*aria-label="Meeting time"[^>]*>""").Value);

        // 先以可用态打开弹层，再切 Disabled=true：打开态弹层保留（门户内容随参数
        // 重渲染），滚钮继承 disabled 沿；禁用遮罩类与按钮禁用态同帧可见。
        await OpenAsync(renderer);
        parameters[nameof(TimePicker.Disabled)] = true;
        await renderer.RerenderRootAsync(1, parameters);
        await SettleAsync(renderer);
        var markup = renderer.Markup();

        var trigger = Regex.Match(markup, """<button[^>]*aria-label="Meeting time"[^>]*>""").Value;
        Assert.Matches(" disabled[ >]", trigger);
        Assert.Contains("atom:opacity-50 atom:pointer-events-none", markup);
        Assert.Matches(" disabled[ >]", ButtonMarkup(markup, "AM"));
    }

    [Fact]
    public async Task EditContext下步进应通知字段变更()
    {
        var model = new TimePickerModel();
        var context = new EditContext(model);
        var changedFields = new List<string>();
        context.OnFieldChanged += (_, e) => changedFields.Add(e.FieldIdentifier.FieldName);
        Expression<Func<TimeSpan?>> expression = () => model.Start;

        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>()); // 根 0：门户宿主
        RenderFragment fieldFragment = builder =>
        {
            builder.OpenComponent<TimePicker>(0);
            builder.AddAttribute(1, nameof(TimePicker.Format), TimeFormat.Hour24);
            builder.AddAttribute(2, nameof(TimePicker.ValueExpression), expression);
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
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0); // 时+
        await SettleAsync(renderer);

        Assert.Contains("Start", changedFields);
    }

    [Fact]
    public async Task 附加属性应透传到触发按钮()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.AriaLabel)] = "Meeting time",
            ["data-test"] = "tp-1",
        });
        Assert.Contains("data-test=\"tp-1\"",
            Regex.Match(renderer.Markup(), """<button[^>]*aria-label="Meeting time"[^>]*>""").Value);
    }

    [Fact]
    public async Task 反射直调SetAmPm越界内部小时应仅动AMPM不改时值()
    {
        // 正午翻 AM：SetAmPm(true) 命中 _internalHour>=12 → 0 时；翻回 PM 命中 <12 → +12。
        var reports = new List<TimeSpan?>();
        var (renderer, _, picker) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Value)] = new TimeSpan(12, 0, 0),
            [nameof(TimePicker.ValueChanged)] =
                EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)),
        });
        await OpenAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var method = typeof(TimePicker)
                .GetMethod("SetAmPm", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)method.Invoke(picker, [false])!; // PM 态下翻 PM：12 不 <12，时值不动
        });
        await SettleAsync(renderer);
        Assert.Equal(new TimeSpan(12, 0, 0), reports.Last());
    }

    [Fact]
    public async Task 缺省十二小时制闭合态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Value)] = new TimeSpan(14, 30, 0),
            [nameof(TimePicker.AriaLabel)] = "Meeting time",
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 二十四小时制含秒与步长打开态结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TimePicker.Format)] = TimeFormat.Hour24,
            [nameof(TimePicker.ShowSeconds)] = true,
            [nameof(TimePicker.MinuteStep)] = 15,
            [nameof(TimePicker.Value)] = new TimeSpan(9, 5, 0),
            [nameof(TimePicker.AriaLabel)] = "Meeting time",
        });
        await OpenAsync(renderer);
        await Verify(Scrub(renderer.Markup()));
    }
}
