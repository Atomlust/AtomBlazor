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
/// FormFieldSelect 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldSelect&lt;string&gt;（根 1），验证 Options 模式自动生成条目与展示文本沿、
/// ChildContent 组合模式与 DisplayTextSelector 透传、触发器携带 ControlId 与 aria-describedby
/// 接线、标签不带 for（ quirk）、ErrorText 手动覆写优先链、EditContext 校验消息随选择消长、
/// Required/Disabled/AriaLabel/InputClass/splat 透传面与加载态/到底消息切换。
/// 弹层开合断言沿用 headless 口径（aria-expanded 闭合态）与 SelectTests 先例（先点触发、
/// 再按 occurrence 派发条目点击，打开后强制重渲染门户宿主）。
/// </summary>
public class FormFieldSelectTests
{
    /// <summary>
    /// 归一化 ControlId 的随机 GUID、上下文自增短 id 与注册项 GUID 片段，使快照与断言可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                "select-[0-9]+", "select-<n>"),
            "-item-[0-9a-f]{8}", "-item-<id>");

    /// <summary>测试复用选项数组（CA1861 先例：常量数组参数首选 static readonly 字段）。</summary>
    private static readonly SelectOption<string>[] fruitOptions = [new("a", "苹果"), new("b", "香蕉")];

    /// <summary>单选项数组（禁用/必填测试复用）。</summary>
    private static readonly SelectOption<string>[] singleOption = [new("a", "苹果")];

    /// <summary>
    /// 构造带全套基元服务的渲染器：门户/定位/焦点/下拉管理器/本地化器 + NoopJavaScript
    /// （沿 SelectTests 先例，弹层打开与浮层注册需要这些服务）。
    /// </summary>
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
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>
    /// 打开后强制重渲染门户宿主（根 0），让列表框内容确定性地进入渲染树（SelectTests 先例）。
    /// </summary>
    private static async Task OpenAndSettleAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>校验沿测试模型：Fruit 非 "a" 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public string? Fruit { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Fruit 不为 "a" 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Fruit));

        void Validate()
        {
            messages.Clear();
            if (doc.Fruit != "a")
            {
                messages.Add(field, "必须选择苹果");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        string? value = null,
        EventCallback<string?> valueChanged = default,
        Expression<Func<string?>>? valueExpression = null,
        bool? disabled = null,
        bool? required = null,
        string? placeholder = null,
        Func<string, string>? displayTextSelector = null,
        string? inputClass = null,
        IEnumerable<SelectOption<string>>? options = null,
        RenderFragment? childContent = null,
        bool? isLoading = null,
        string? endOfListMessage = null,
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
            parameters[nameof(FormFieldSelect<string>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldSelect<string>.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldSelect<string>.ValueExpression)] = valueExpression;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldSelect<string>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldSelect<string>.Required)] = required.Value;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldSelect<string>.Placeholder)] = placeholder;
        }
        if (displayTextSelector is not null)
        {
            parameters[nameof(FormFieldSelect<string>.DisplayTextSelector)] = displayTextSelector;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldSelect<string>.InputClass)] = inputClass;
        }
        if (options is not null)
        {
            parameters[nameof(FormFieldSelect<string>.Options)] = options;
        }
        if (childContent is not null)
        {
            parameters[nameof(FormFieldSelect<string>.ChildContent)] = childContent;
        }
        if (isLoading is not null)
        {
            parameters[nameof(FormFieldSelect<string>.IsLoading)] = isLoading.Value;
        }
        if (endOfListMessage is not null)
        {
            parameters[nameof(FormFieldSelect<string>.EndOfListMessage)] = endOfListMessage;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldSelect<string>.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldSelect<string>.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldSelect<string>.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldSelect<string>.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldSelect<string>.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>ChildContent 组合模式家族片段：触发器+SelectValue+内容两选项（SelectTests 先例形态）。</summary>
    private static RenderFragment CompositeFamily(string placeholder = "请选择") => builder =>
    {
        builder.OpenComponent<SelectTrigger<string>>(0);
        builder.AddAttribute(1, "ChildContent", (RenderFragment)(inner =>
        {
            inner.OpenComponent<SelectValue>(0);
            inner.AddAttribute(1, nameof(SelectValue.Placeholder), placeholder);
            inner.CloseComponent();
        }));
        builder.CloseComponent();

        builder.OpenComponent<SelectContent<string>>(2);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(inner =>
        {
            var seq = 0;
            foreach (var (value, text) in new (string, string)[] { ("a", "苹果"), ("b", "香蕉") })
            {
                inner.OpenComponent<SelectItem<string>>(seq);
                inner.AddAttribute(seq + 1, nameof(SelectItem<string>.Value), value);
                inner.AddAttribute(seq + 2, nameof(SelectItem<string>.Text), text);
                inner.CloseComponent();
                seq += 3;
            }
        }));
        builder.CloseComponent();
    };

    /// <summary>单独一枚诱饵条目（芋头），用于断言 Options 优先时 ChildContent 被忽略。</summary>
    private static RenderFragment DecoyItem => builder =>
    {
        builder.OpenComponent<SelectItem<string>>(0);
        builder.AddAttribute(1, nameof(SelectItem<string>.Value), "z");
        builder.AddAttribute(2, nameof(SelectItem<string>.Text), "芋头");
        builder.CloseComponent();
    };

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            label: "水果", helperText: "挑一个水果", placeholder: "请选择",
            options: fruitOptions));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应不带for而触发器应携带ControlId与describedby()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            label: "水果", helperText: "挑一个水果", options: singleOption));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var match = Regex.Match(markup, @"id=""(formfield-[0-9a-f]{32})""");
        Assert.True(match.Success, "触发器应携带 ControlId 作为 id（基元将其同步进上下文 TriggerId）");

        var controlId = match.Groups[1].Value;
        // quirk：Select 家族 FieldLabel 不带 For（与 Textarea/NativeSelect 家族不对称）。
        Assert.DoesNotContain($"for=\"{controlId}\"", markup);
        Assert.Contains($"aria-describedby=\"{controlId}-description\"", markup);
        Assert.Contains("data-slot=\"field-label\"", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
    }

    // ------------------------------------------------------------------
    // Options 与组合双模式
    // ------------------------------------------------------------------

    [Fact]
    public async Task Options模式应展示文本并优先于ChildContent()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            value: "b", options: fruitOptions, childContent: DecoyItem,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), _ => { })));
        await SettleAsync(renderer);

        // 关闭态：GetEffectiveDisplayTextSelector 由 Options ，预选值 b 直接显示"香蕉"。
        var markup = renderer.Markup();
        Assert.Contains("香蕉", markup);

        // 打开后：仅 Options 生成的两枚条目呈现，ChildContent 诱饵被忽略。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);

        markup = renderer.Markup();
        Assert.Contains("苹果", markup);
        Assert.Contains("香蕉", markup);
        Assert.DoesNotContain("芋头", markup);
    }

    [Fact]
    public async Task 点击选项应回填值并关闭弹层()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            placeholder: "选一个",
            options: fruitOptions,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 关闭态：占位符呈现，触发按钮是关闭态唯一 onclick 携带者。
        Assert.Contains("选一个", renderer.Markup());
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);

        // 打开后：根 0 门户里两个选项（occurrence 0/1），点第二项"香蕉"。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs { Detail = 1 }, occurrence: 1));
        await SettleAsync(renderer);

        Assert.Equal(["b"], reports);
        var markup = renderer.Markup();
        Assert.Contains("香蕉", markup);
        Assert.DoesNotContain("选一个", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
    }

    [Fact]
    public async Task 组合模式应原样透传DisplayTextSelector()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            value: "b",
            displayTextSelector: new Func<string, string>(v => v == "b" ? "香蕉" : "苹果"),
            childContent: CompositeFamily(),
            valueChanged: EventCallback.Factory.Create<string?>(new object(), _ => { })));
        await SettleAsync(renderer);

        Assert.Contains("香蕉", renderer.Markup());
        Assert.DoesNotContain("请选择", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随选择消长并经FieldError呈现()
    {
        var doc = new Doc { Fruit = "x" };
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        Expression<Func<string?>> valueExpression = () => doc.Fruit;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldSelect<string>>(0);
                builder.AddAttribute(1, nameof(FormFieldSelect<string>.Value), doc.Fruit);
                builder.AddAttribute(2, nameof(FormFieldSelect<string>.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldSelect<string>.ValueChanged),
                    EventCallback.Factory.Create<string?>(doc, value =>
                    {
                        doc.Fruit = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(4, nameof(FormFieldSelect<string>.Label), (string?)"水果");
                builder.AddAttribute(5, nameof(FormFieldSelect<string>.Options), fruitOptions);
                builder.AddAttribute(6, nameof(FormFieldSelect<string>.Placeholder), "选一个");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始 "x" 非 "a"：广播整体校验后错误经 FieldError 呈现，describedby 指向错误元素。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("必须选择苹果", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", markup);

        // 打开并点"苹果"（根 0 门户 occurrence 0）→ 外层 HandleValueChanged 三步沿 → 校验消解。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs { Detail = 1 }, occurrence: 0));
        await SettleAsync(renderer);

        Assert.Equal(["a"], reports);
        Assert.Equal("a", doc.Fruit);
        markup = renderer.Markup();
        Assert.DoesNotContain("必须选择苹果", markup);
        Assert.Contains("苹果", markup);                       // 触发器回填展示文本
        Assert.Contains("aria-expanded=\"false\"", markup);
    }

    // ------------------------------------------------------------------
    // 错误覆写与透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应显形并切换describedby()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            label: "水果", helperText: "挑一个水果", errorText: "服务端驳回：必须选择苹果",
            options: singleOption));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：必须选择苹果", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);

        // 错误态下帮助文本隐藏，describedby 切换到错误元素。
        Assert.DoesNotContain("挑一个水果", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
    }

    [Fact]
    public async Task 必填应播报ariaRequired且禁用应吞掉触发点击()
    {
        var requiredRenderer = CreateRenderer();
        await requiredRenderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await requiredRenderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            required: true, options: singleOption,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), _ => { })));
        await SettleAsync(requiredRenderer);

        Assert.Contains("aria-required=\"true\"", requiredRenderer.Markup());

        var disabledRenderer = CreateRenderer();
        await disabledRenderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await disabledRenderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            disabled: true, options: singleOption,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), _ => { })));
        await SettleAsync(disabledRenderer);

        await disabledRenderer.Dispatcher.InvokeAsync(async () =>
            await disabledRenderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(disabledRenderer);

        // 禁用原子对：aria-disabled="true" 且点击被吞（弹层保持闭合）。
        Assert.Contains("aria-disabled=\"true\"", disabledRenderer.Markup());
        Assert.Contains("aria-expanded=\"false\"", disabledRenderer.Markup());
    }

    [Fact]
    public async Task 无障碍名称输入类与透传参数应传给内部Select()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            ariaLabel: "选择城市", inputClass: "atom:custom-select", options: singleOption,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), _ => { }),
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-select" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // AriaLabel 经包装层 Select 级联落触发按钮（SelectTrigger 读 ParentSelect.AriaLabel）。
        Assert.Contains("aria-label=\"选择城市\"", markup);
        // InputClass 汇入内部 Select 根容器类面；splat 直落 Select 元素而非 Field 根。
        Assert.Contains("atom:custom-select", markup);
        Assert.Contains("data-testid=\"field-select\"", markup);
    }

    [Fact]
    public async Task 加载态与到底消息应随参数切换()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldSelect<string>>(FieldParams(
            options: singleOption, endOfListMessage: "已加载全部",
            valueChanged: EventCallback.Factory.Create<string?>(new object(), _ => { })));
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);
        Assert.Contains("已加载全部", renderer.Markup());

        // 转入加载态：消息让位于加载指示（根 1 是 FormFieldSelect）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            options: singleOption, endOfListMessage: "已加载全部", isLoading: true,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), _ => { }))));
        await OpenAndSettleAsync(renderer);

        Assert.DoesNotContain("已加载全部", renderer.Markup());
        Assert.Contains("animate-spin", renderer.Markup()); // Spinner 的旋转类
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldSelect<string>>(FieldParams());
        await SettleAsync(renderer);

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、Options/Placeholder/DisplayTextSelector 皆空、
        // IsLoading/Disabled/Required 皆 false。
        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Options);
        Assert.Null(field.Placeholder);
        Assert.Null(field.DisplayTextSelector);
        Assert.Null(field.EndOfListMessage);
        Assert.False(field.IsLoading);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
        Assert.DoesNotContain("aria-required=\"true\"", renderer.Markup());
    }
}
