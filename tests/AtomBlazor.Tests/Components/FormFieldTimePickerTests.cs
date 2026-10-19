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
/// FormFieldTimePicker 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldTimePicker（根 1），验证标签不带 for 且 ControlId 完全不落触发器、TimePicker 家族
/// 无 Id/aria-describedby 通道（ quirk：describedby 只落描述/错误元素自身，控件侧零引用）、
/// 打开弹层点分+经封装层三步沿回填并回写触发器文本、MinTime/MaxTime 越界步进钳回边界、
/// Format/ShowSeconds/Placeholder 格式面、Disabled 落原生 disabled 与 atom:opacity-50
/// atom:pointer-events-none 叠类（本家族无 ReadOnly 通道，禁用形态亦非 Input 家族原子对）、
/// Required 落 aria-required、ErrorText 手动覆写显形但不落控件（无通道）、EditContext 校验消息
/// 随选择消长、AriaLabel/InputClass/splat 齐落触发按钮与缺省参数面。
/// headless 口径沿用 TimePickerTests 先例：打开态 onclick 序沿门户宿主根先遍历——
/// 0 时+、1 时-、2 分+、3 分-（ShowSeconds 追加 4 秒+、5 秒-，后序整体 +2），触发按钮居后
/// 关闭后弹层内容滞留 DOM，开合改断 aria-expanded。快照含 formfield GUID 与 popover-N 短 id
/// 先经正则归一化。
/// </summary>
public class FormFieldTimePickerTests
{
    /// <summary>归一化 ControlId 随机 GUID 与 popover-N 自增短 id，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
            "popover-[0-9]+", "popover-<n>");

    /// <summary>构造带全套基元服务的渲染器（沿 TimePickerTests 先例，弹层需要门户/定位/焦点服务）。</summary>
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

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>触发一次 DOM 事件（沿 TimePickerTests 先例须在 Dispatcher 上派发）。</summary>
    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () => await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>关闭态点触发按钮打开弹层（关闭态唯一 onclick 携带者）并冲刷门户根。</summary>
    private static async Task OpenAsync(ComponentTestRenderer renderer)
    {
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>校验沿测试模型：Time 非 12:15 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public TimeSpan? Time { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Time 不为 12:15 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Time));

        void Validate()
        {
            messages.Clear();
            if (doc.Time != new TimeSpan(12, 15, 0))
            {
                messages.Add(field, "必须选择 12:15");
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
        TimeFormat? format = null,
        bool? showSeconds = null,
        int? minuteStep = null,
        TimeSpan? minTime = null,
        TimeSpan? maxTime = null,
        string? placeholder = null,
        bool? disabled = null,
        bool? required = null,
        string? inputClass = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        FieldOrientation? orientation = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (value is not null)
        {
            parameters[nameof(FormFieldTimePicker.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldTimePicker.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldTimePicker.ValueExpression)] = valueExpression;
        }
        if (format is not null)
        {
            parameters[nameof(FormFieldTimePicker.Format)] = format.Value;
        }
        if (showSeconds is not null)
        {
            parameters[nameof(FormFieldTimePicker.ShowSeconds)] = showSeconds.Value;
        }
        if (minuteStep is not null)
        {
            parameters[nameof(FormFieldTimePicker.MinuteStep)] = minuteStep.Value;
        }
        if (minTime is not null)
        {
            parameters[nameof(FormFieldTimePicker.MinTime)] = minTime;
        }
        if (maxTime is not null)
        {
            parameters[nameof(FormFieldTimePicker.MaxTime)] = maxTime;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldTimePicker.Placeholder)] = placeholder;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldTimePicker.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldTimePicker.Required)] = required.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldTimePicker.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldTimePicker.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldTimePicker.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldTimePicker.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldTimePicker.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldTimePicker.Orientation)] = orientation.Value;
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

        await renderer.MountAsync<FormFieldTimePicker>(FieldParams(
            label: "会议时间", helperText: "仅限营业时间",
            value: new TimeSpan(14, 30, 0)));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应无for关联且控件侧不落ControlId与describedby通道()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimePicker>(FieldParams(
            label: "会议时间", helperText: "仅限营业时间"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // quirk：TimePicker 家族 FieldLabel 不带 For，且 TimePicker 无 Id/
        // AriaDescribedBy 参数——ControlId 从不落控件，describedby 只落描述元素自身。
        Assert.DoesNotContain("for=", markup);
        Assert.Equal(0, Regex.Count(markup, @"""formfield-[0-9a-f]{32}""")); // 裸 ControlId 不落任何元素（-description 后缀档除外）
        Assert.Matches(@"id=""formfield-[0-9a-f]{32}-description""", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        // 触发器锚点是基元自造的 popover-<n>-trigger（headless 闭合态断 aria-expanded）。
        Assert.Matches(@"id=""popover-[0-9]+-trigger""", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
    }

    // ------------------------------------------------------------------
    // 值回填沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开弹层点分步进应经三步沿回填并回写触发器文本()
    {
        var reports = new List<TimeSpan?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldTimePicker>(FieldParams(
            minuteStep: 15,
            valueChanged: EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        // 打开态 onclick 序（TimePickerTests 先例）：0 时+、1 时-、2 分+、3 分-……
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 2); // 分+
        await SettleAsync(renderer);

        // 缺省内部时刻 12:00（Hour12 缺省），步长 15 → 12:15。
        Assert.Equal(new TimeSpan(12, 15, 0), Assert.Single(reports));
        Assert.Equal(new TimeSpan(12, 15, 0), field.Value); // HandleValueChanged 赋值步沿
        Assert.Contains("12:15 PM", renderer.Markup()); // 触发器文本回写
    }

    [Fact]
    public async Task 越界步进应被最小最大时间钳回边界()
    {
        var reports = new List<TimeSpan?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimePicker>(FieldParams(
            value: new TimeSpan(9, 0, 0), format: TimeFormat.Hour24,
            minTime: new TimeSpan(9, 0, 0), maxTime: new TimeSpan(17, 0, 0),
            valueChanged: EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1); // 时- → 8:00 越下界
        await SettleAsync(renderer);
        Assert.Equal(new TimeSpan(9, 0, 0), Assert.Single(reports)); // 钳回 MinTime，绝不落 8 点

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            value: new TimeSpan(16, 0, 0), format: TimeFormat.Hour24,
            minTime: new TimeSpan(9, 0, 0), maxTime: new TimeSpan(17, 0, 0),
            valueChanged: EventCallback.Factory.Create<TimeSpan?>(new object(), v => reports.Add(v)))));
        await SettleAsync(renderer);

        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0); // 时+ → 17:00 合法
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0); // 时+ → 18:00 越上界
        await SettleAsync(renderer);
        Assert.Equal(3, reports.Count);
        Assert.Equal(new TimeSpan(17, 0, 0), reports[1]);
        Assert.Equal(new TimeSpan(17, 0, 0), reports[2]); // 钳回 MaxTime，绝不落 18 点
        Assert.Contains("17:00", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 格式面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 二十四小时制含秒应控触发器文本且占位透传应落占位文本()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimePicker>(FieldParams(
            value: new TimeSpan(9, 5, 7), format: TimeFormat.Hour24, showSeconds: true));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("09:05:07", markup); // hh\:mm\:ss 分支
        Assert.DoesNotContain("AM", markup); // 24 小时制无时段钮面（闭合态触发器文本亦无）

        // 置空值 + 自定义占位：透传落 muted 占位 span（显式 null 复位 Value 参数）。
        var blank = FieldParams(format: TimeFormat.Hour24, placeholder: "挑个时刻");
        blank[nameof(FormFieldTimePicker.Value)] = null;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, blank));
        await SettleAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains("挑个时刻", markup);
        Assert.Contains("atom:text-muted-foreground", markup);
        Assert.DoesNotContain("09:05:07", markup);
    }

    // ------------------------------------------------------------------
    // 禁用/必填/错误面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用应落原生disabled与置位不可点叠类()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimePicker>(FieldParams(disabled: true));
        await SettleAsync(renderer);

        var button = Regex.Match(renderer.Markup(), "<button[^>]*>").Value;
        Assert.Contains("disabled", button);
        // 形态：TimePicker 触发器禁用叠类是 atom:opacity-50 atom:pointer-events-none
        // （非 Input 家族 atom:opacity-50 atom:cursor-not-allowed 原子对；本家族亦无 ReadOnly 通道）。
        Assert.Contains("atom:opacity-50 atom:pointer-events-none", button);
    }

    [Fact]
    public async Task 必填应播报ariaRequired而缺省不播报()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimePicker>(FieldParams(required: true));
        await SettleAsync(renderer);

        var button = Regex.Match(renderer.Markup(), "<button[^>]*>").Value;
        Assert.Contains("aria-required=\"true\"", button);

        // 显式 false 复位：参数视图中未出现的参数保持组件当前值。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(required: false)));
        await SettleAsync(renderer);
        Assert.DoesNotContain("aria-required", renderer.Markup());
    }

    [Fact]
    public async Task 错误文本手动覆写应显形但不落控件面无describedby与ariaInvalid通道()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimePicker>(FieldParams(
            label: "会议时间", helperText: "仅限营业时间",
            errorText: "服务端驳回：时间超出营业时间"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：时间超出营业时间", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup); // Field 根无效态实断据

        // 错误态下帮助文本隐藏；错误元素带 id 但控件侧无通道引用。
        Assert.DoesNotContain("仅限营业时间", markup);
        Assert.Contains("id=\"formfield-<id>-error\"", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        Assert.DoesNotContain("aria-invalid", markup);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随时间选择消长并经FieldError呈现()
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
                builder.OpenComponent<FormFieldTimePicker>(0);
                builder.AddAttribute(1, nameof(FormFieldTimePicker.ValueExpression), valueExpression);
                builder.AddAttribute(2, nameof(FormFieldTimePicker.ValueChanged),
                    EventCallback.Factory.Create<TimeSpan?>(doc, value =>
                    {
                        doc.Time = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(3, nameof(FormFieldTimePicker.Label), (string?)"会议时间");
                builder.AddAttribute(4, nameof(FormFieldTimePicker.MinuteStep), 15);
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始空值：广播整体校验后错误经 FieldError 呈现（错误元素 id 入树）。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("必须选择 12:15", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"id=""formfield-[0-9a-f]{32}-error""", markup);

        // 打开弹层点分+（12:00 → 12:15）→ 外层三步沿 + 底座自身校验通知双发沿 → 校验消解。
        await OpenAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 2); // 分+
        await SettleAsync(renderer);

        Assert.Equal(new TimeSpan(12, 15, 0), doc.Time);
        Assert.DoesNotContain("必须选择 12:15", renderer.Markup());
        Assert.Contains("12:15 PM", renderer.Markup()); // 触发器回填展示
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 无障碍名称输入类与透传应齐落触发按钮()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTimePicker>(FieldParams(
            ariaLabel: "开会时刻", inputClass: "atom:custom-tp",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-timepicker" }));
        await SettleAsync(renderer);

        // 沿：AriaLabel 直落 Button 正式参数；InputClass（Class）与 splat 亦同落触发按钮
        // （TimePicker 无根容器 div，Field 根仅收 Class——与 DateInput 家族落底座根形态不同）。
        var button = Regex.Match(renderer.Markup(), "<button[^>]*>").Value;
        Assert.Contains("aria-label=\"开会时刻\"", button);
        Assert.Contains("atom:custom-tp", button);
        Assert.Contains("data-testid=\"field-timepicker\"", button);
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、Format=Hour12、MinuteStep=1、
        // Placeholder="Select time" 硬码英文、开关全 false、引用面全 null。
        var field = await renderer.MountAsync<FormFieldTimePicker>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Value);
        Assert.Null(field.ValueExpression);
        Assert.Equal(TimeFormat.Hour12, field.Format);
        Assert.False(field.ShowSeconds);
        Assert.Equal(1, field.MinuteStep);
        Assert.Null(field.MinTime);
        Assert.Null(field.MaxTime);
        Assert.Equal("Select time", field.Placeholder);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Null(field.InputClass);

        var markup = renderer.Markup();
        Assert.Contains("Select time", markup); // 缺省占位直落触发器
        Assert.Contains("aria-expanded=\"false\"", markup);
        Assert.DoesNotContain("aria-required=\"true\"", markup);
        Assert.DoesNotContain("aria-describedby", markup);
    }
}
