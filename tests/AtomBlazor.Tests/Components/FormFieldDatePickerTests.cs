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
using System.Globalization;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FormFieldDatePicker 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldDatePicker（根 1），验证标签不带 for 且 ControlId 从不落控件（底座 DatePicker 无 Id
/// 参数， quirk）、describedby 全树零引用（包装层不把 DescribedById 传底座，底座无该参数面）、
/// 开层点日历日格回填三步沿（Calendar 年月下拉占 onclick 序 1/2、日格自 4 起）、可编辑模式
/// 文本提交与 InputFormats、IsInvalid 显式透传仅落可编辑输入档（非编辑档 Button 无无效面
/// 与 DateInput 家族不对称）、ErrorText 覆写优先链、EditContext 校验消长（底座与外层双通知沿）、
/// Required→aria-required 与 Disabled→pointer-events 叠类（非原生 disabled，headless 据实）、
/// AriaLabel/InputClass/splat 落触发按钮与缺省参数面。headless 口径：弹层闭合断 aria-expanded、
/// 弹层内容经门户重渲染入树；格式化断言钉 DateFormat="yyyy-MM-dd" 免机器区域性漂移
/// 快照含 formfield GUID、calendar GUID、popover-N 与 select-N 短 id，先经正则归一化。
/// </summary>
public class FormFieldDatePickerTests
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

    /// <summary>构造带全套基元服务的渲染器（照 DatePickerTests 先例，弹层需要门户/定位/焦点服务）。</summary>
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

    /// <summary>强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树（照 DatePickerTests 先例）。</summary>
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

    /// <summary>反射取底座 DatePicker 实例（包装层内部控件，根 1 直挂时经 FindComponent 取）。</summary>
    private static DatePicker InnerPicker(ComponentTestRenderer renderer) =>
        renderer.FindComponent<DatePicker>();

    /// <summary>按可见文本抓取 button 全标签（DatePickerTests 先例，禁用面断言用）。</summary>
    private static string ButtonMarkup(string markup, string innerText) =>
        Regex.Matches(markup, "<button[^>]*>.*?</button>", RegexOptions.Singleline)
            .Cast<Match>().First(m => m.Value.Contains(innerText)).Value;

    /// <summary>校验沿测试模型：Date 非 2026-10-12 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public DateTime? Date { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Date 不为 2026-10-12 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Date));

        void Validate()
        {
            messages.Clear();
            if (doc.Date != new DateTime(2026, 10, 12))
            {
                messages.Add(field, "必须选择 2026-10-12");
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
        string? dateFormat = null,
        bool? editable = null,
        string[]? inputFormats = null,
        string? placeholder = null,
        DateTime? minDate = null,
        DateTime? maxDate = null,
        Func<DateTime, bool>? disabledDates = null,
        DayOfWeek? firstDayOfWeek = null,
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
            parameters[nameof(FormFieldDatePicker.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldDatePicker.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldDatePicker.ValueExpression)] = valueExpression;
        }
        if (dateFormat is not null)
        {
            parameters[nameof(FormFieldDatePicker.DateFormat)] = dateFormat;
        }
        if (editable is not null)
        {
            parameters[nameof(FormFieldDatePicker.Editable)] = editable.Value;
        }
        if (inputFormats is not null)
        {
            parameters[nameof(FormFieldDatePicker.InputFormats)] = inputFormats;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldDatePicker.Placeholder)] = placeholder;
        }
        if (minDate is not null)
        {
            parameters[nameof(FormFieldDatePicker.MinDate)] = minDate;
        }
        if (maxDate is not null)
        {
            parameters[nameof(FormFieldDatePicker.MaxDate)] = maxDate;
        }
        if (disabledDates is not null)
        {
            parameters[nameof(FormFieldDatePicker.DisabledDates)] = disabledDates;
        }
        if (firstDayOfWeek is not null)
        {
            parameters[nameof(FormFieldDatePicker.FirstDayOfWeek)] = firstDayOfWeek;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldDatePicker.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldDatePicker.Required)] = required.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldDatePicker.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldDatePicker.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldDatePicker.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldDatePicker.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldDatePicker.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldDatePicker.Orientation)] = orientation.Value;
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

        await renderer.MountAsync<FormFieldDatePicker>(FieldParams(
            label: "生效日期", helperText: "选择生效日", placeholder: "请选择日期",
            value: new DateTime(2026, 10, 12), dateFormat: "yyyy-MM-dd"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应不带for且ControlId不落控件describedby全树零引用()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDatePicker>(FieldParams(
            label: "生效日期", helperText: "选择生效日"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // quirk：FieldLabel 不带 For，且 DatePicker 无 Id 参数——ControlId 从不落控件。
        Assert.DoesNotContain("for=", markup);
        Assert.DoesNotContain("id=\"formfield-", Regex.Replace(markup, @"id=""formfield-[0-9a-f]{32}-(?:description|error)""", ""));
        // 触发器锚点为基元自造 popover-<n>-trigger。
        Assert.Matches(@"id=""popover-[0-9]+-trigger""", markup);
        // 包装层不把 DescribedById 传底座（底座无 AriaDescribedBy 参数面）→ 全树零 aria-describedby。
        Assert.Equal(0, Regex.Count(markup, "aria-describedby"));
    }

    // ------------------------------------------------------------------
    // 值回填沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 开层点日历日格应经三步沿回填并刷新触发文本()
    {
        var reports = new List<DateTime?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            placeholder: "请选择日期", dateFormat: "yyyy-MM-dd",
            value: new DateTime(2026, 10, 12),
            valueChanged: EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v)));
        await renderer.MountAsync<FormFieldDatePicker>(dict);
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var open = renderer.Markup();

        // 打开态门户根先遍历：Calendar 头部上月 0、月下拉 1、年下拉 2、下月 3
        // 日格自 4 起；周序随机器区域性，按渲染序动态定位 10 月 20 日格号（DatePickerTests 先例）。
        var dayIndex = Regex.Matches(open, "-day-(20\\d{6})").ToList()
            .FindIndex(m => m.Groups[1].Value == "20261020");
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 4 + dayIndex);
        await RerenderPortalAsync(renderer);

        Assert.Equal(new DateTime(2026, 10, 20), Assert.Single(reports));
        Assert.Equal(new DateTime(2026, 10, 20), InnerPicker(renderer).Value); // HandleValueChanged 赋值步沿

        // 受控回填：宿主更新 Value 重渲染触发按钮文本。
        dict[nameof(FormFieldDatePicker.Value)] = new DateTime(2026, 10, 20);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        Assert.Contains("2026-10-20", renderer.Markup());
        // 选中后关层沿（headless 口径断触发器 aria-expanded）。
        Assert.Contains("aria-expanded=\"false\"",
            Regex.Match(renderer.Markup(), """<button[^>]*id="popover-[0-9]+-trigger"[^>]*>""").Value);
    }

    [Fact]
    public async Task 可编辑模式应落文本输入且ISO提交回填InputFormats面接受自定义格式()
    {
        var reports = new List<DateTime?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDatePicker>(FieldParams(
            editable: true, inputFormats: ["dd/MM/yyyy", "d/M/yyyy"],
            valueChanged: EventCallback.Factory.Create<DateTime?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 可编辑档：DatePickerInput 落 input 元素（关闭态唯一 onchange 携带者）。
        Assert.Contains("type=\"text\"", renderer.Markup());

        // ISO 恒接受沿。
        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "2026-10-20" });
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 10, 20), reports.Single());

        // InputFormats 透传生效：dd/MM/yyyy 键入被接受（DatePickerTests 先例）。
        await FireAsync(renderer, "onchange", new ChangeEventArgs { Value = "01/11/2026" });
        await SettleAsync(renderer);
        Assert.Equal(new DateTime(2026, 11, 1), reports.Last());
    }

    // ------------------------------------------------------------------
    // IsInvalid 透传与错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误覆写在非编辑档应不落控件而在可编辑档落输入ariaInvalid()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(label: "生效日期", helperText: "选择生效日",
            errorText: "服务端驳回：已过期", dateFormat: "yyyy-MM-dd");
        await renderer.MountAsync<FormFieldDatePicker>(dict);
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：已过期", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.DoesNotContain("选择生效日", markup);
        // 错误元素按 DescriptionId/ErrorId 建档但包装层不把 DescribedById 传底座（quirk）：
        // 错误态仅 -error id 在场、全树零 aria-describedby 引用者。
        Assert.Contains("id=\"formfield-<id>-error\"", markup);
        Assert.Equal(0, Regex.Count(markup, "aria-describedby"));
        // quirk：DatePicker 家族把 IsInvalid 显式传底座，但非编辑档 Button 无无效面——
        // EffectiveInvalid 只落 DatePickerInput，全树零 aria-invalid。
        Assert.DoesNotContain("aria-invalid", markup);

        // 转可编辑档：IsInvalid 透传落输入 aria-invalid（与 DateInput 家族不传 IsInvalid 不对称）。
        dict[nameof(FormFieldDatePicker.Editable)] = true;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        Assert.Contains("aria-invalid=\"true\"", renderer.Markup());
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

        Expression<Func<DateTime?>> valueExpression = () => doc.Date;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldDatePicker>(0);
                builder.AddAttribute(1, nameof(FormFieldDatePicker.ValueExpression), valueExpression);
                builder.AddAttribute(2, nameof(FormFieldDatePicker.ValueChanged),
                    EventCallback.Factory.Create<DateTime?>(doc, value => doc.Date = value));
                builder.AddAttribute(3, nameof(FormFieldDatePicker.DateFormat), "yyyy-MM-dd");
                builder.AddAttribute(4, nameof(FormFieldDatePicker.Label), (string?)"生效日期");
                builder.CloseComponent();
            }),
        });
        context.OnFieldChanged += (_, _) => notifyCount++;
        await SettleAsync(renderer);

        // 广播校验显形。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("必须选择 2026-10-12", markup);
        Assert.Contains("data-invalid=\"true\"", markup);

        // 开层点 10-12 日格：外层 NotifyFieldChanged 与底座自身校验通知双发沿。
        await OpenAsync(renderer);
        var open = renderer.Markup();
        var dayIndex = Regex.Matches(open, "-day-(20\\d{6})").ToList()
            .FindIndex(m => m.Groups[1].Value == "20261012");
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 4 + dayIndex);
        await RerenderPortalAsync(renderer);
        await SettleAsync(renderer);

        Assert.Equal(new DateTime(2026, 10, 12), doc.Date);
        Assert.True(notifyCount >= 2); // 双发（语义无害，照留）
        Assert.DoesNotContain("必须选择 2026-10-12", renderer.Markup());
        Assert.Contains("2026-10-12", renderer.Markup()); // 触发按钮回填格式化文本
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 必填应播ariaRequired且禁用面应叠pointerEvents类而非原生属性()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDatePicker>(FieldParams(
            required: true, disabled: true, placeholder: "请选择日期"));
        await SettleAsync(renderer);

        var button = ButtonMarkup(renderer.Markup(), "请选择日期");
        Assert.Contains("aria-required=\"true\"", button);
        // 沿：非编辑档触发按钮禁用面为 pointer-events 叠类（DateInput 家族原生 disabled 档不同）
        // headless 无浏览器语义，点击吞没属浏览器侧行为（底座 DatePickerTests 已覆盖语义面）。
        Assert.Contains("atom:opacity-50 atom:pointer-events-none", renderer.Markup());
    }

    [Fact]
    public async Task 最小最大与禁选谓词应在开层日格显形禁用()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            value: new DateTime(2026, 10, 12), dateFormat: "yyyy-MM-dd",
            minDate: new DateTime(2026, 10, 5), maxDate: new DateTime(2026, 10, 18),
            disabledDates: d => d.Day == 12, firstDayOfWeek: DayOfWeek.Monday);
        await renderer.MountAsync<FormFieldDatePicker>(dict);
        await SettleAsync(renderer);

        await OpenAsync(renderer);
        var markup = renderer.Markup();
        string DayTag(string iso) => Regex.Match(markup, $"<button[^>]*-day-{iso}[^>]*>").Value;
        Assert.Matches(" disabled[ >]", DayTag("20261001")); // < MinDate
        Assert.Matches(" disabled[ >]", DayTag("20261020")); // > MaxDate
        Assert.Matches(" disabled[ >]", DayTag("20261012")); // 谓词命中
        Assert.DoesNotMatch(" disabled[ >]", DayTag("20261010")); // 区间内放行
        // FirstDayOfWeek 经同名参数位透传（其星期头面属 CalendarTests 已覆盖语义，此处据实不重复断言）。
    }

    [Fact]
    public async Task 占位与格式化文本应落触发按钮且缺省格式随文化()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDatePicker>(FieldParams(placeholder: "请选择日期"));
        await SettleAsync(renderer);
        Assert.Contains("请选择日期", renderer.Markup()); // 无值占位灰字档

        var dict = FieldParams(placeholder: "请选择日期", value: new DateTime(2026, 10, 12));
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        // 缺省 DateFormat="d" 随当前文化格式化（断言按当前文化区实算，照 DateTimePickerTests 口径免漂移）。
        Assert.Contains(new DateTime(2026, 10, 12).ToString("d", CultureInfo.CurrentCulture), renderer.Markup());
    }

    [Fact]
    public async Task 无障碍名称输入类与透传应落触发按钮()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldDatePicker>(FieldParams(
            ariaLabel: "选择生效日", placeholder: "请选择日期", inputClass: "atom:custom-picker",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-datepicker" }));
        await SettleAsync(renderer);

        var button = ButtonMarkup(renderer.Markup(), "请选择日期");
        Assert.Contains("aria-label=\"选择生效日\"", button);
        Assert.Contains("atom:custom-picker", button); // InputClass 经 ButtonCssClass 落按钮
        Assert.Contains("data-testid=\"field-datepicker\"", button); // splat 直落底座 Button
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldDatePicker>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        // 缺省：DateFormat="d"、可编辑关、引用面全 null、开关全 false、Orientation=Vertical。
        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Value);
        Assert.Equal("d", field.DateFormat);
        Assert.False(field.Editable);
        Assert.Null(field.InputFormats);
        Assert.Null(field.Placeholder);
        Assert.Null(field.MinDate);
        Assert.Null(field.MaxDate);
        Assert.Null(field.DisabledDates);
        Assert.Null(field.FirstDayOfWeek);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Null(field.InputClass);
        Assert.DoesNotContain("aria-required=\"true\"", renderer.Markup());
    }
}
