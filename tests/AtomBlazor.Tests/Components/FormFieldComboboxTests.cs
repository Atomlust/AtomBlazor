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
/// FormFieldCombobox 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldCombobox&lt;string&gt;（根 1），验证 Options 模式自动生成条目并优先于 ChildContent、
/// 标签不带 for 且触发器锚点为 popover-&lt;n&gt;-trigger（ControlId 完全不落控件， quirk）、
/// aria-describedby 经正式参数落触发按钮、ErrorText 手动覆写优先链、EditContext 校验消息随选择消长、
/// SearchQueryChanged 双态门控（未绑定时内部过滤保留、绑定后受控透出并启用直通谓词）、
/// Required/Disabled/AriaLabel/InputClass/splat/ActiveClass/PopoverWidth/MatchTriggerWidth 透传面
/// 与加载态/到底消息切换。弹层开合沿用 headless 口径（aria-expanded 闭合态）
/// 选项回填经 CommandList 上下文的 SelectItemByValueAsync（ComboboxTests 先例）。
/// </summary>
public class FormFieldComboboxTests
{
    /// <summary>
    /// 归一化 ControlId 的随机 GUID（describedby 通道）、popover-N 自增短 id、
    /// combobox 家族 GUID 稳定 id 与 command 家族 GUID，使快照与断言可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(
                    Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                    "popover-[0-9]+", "popover-<n>"),
                "combobox(?:-search)?-[0-9a-f]{32}", "combobox-<id>"),
            "command-[0-9a-f]{32}", "command-<id>");

    /// <summary>测试复用选项数组（CA1861 先例：常量数组参数首选 static readonly 字段）。</summary>
    private static readonly SelectOption<string>[] fruitOptions = [new("a", "苹果"), new("b", "香蕉")];

    /// <summary>单选项数组（禁用/必填/加载测试复用）。</summary>
    private static readonly SelectOption<string>[] singleOption = [new("a", "苹果")];

    /// <summary>
    /// 构造带全套基元服务的渲染器：门户/定位/焦点/本地化器 + NoopJavaScript
    /// （沿 ComboboxTests 先例，弹层打开与浮层注册需要这些服务）。
    /// </summary>
    private static ComponentTestRenderer CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>打开后强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树（ComboboxTests 先例）。</summary>
    private static async Task OpenAndSettleAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>点击触发器打开弹层（关闭态下唯一 onclick 携带者）并让门户把内容画进渲染树。</summary>
    private static async Task OpenPopupAsync(ComponentTestRenderer renderer)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);
    }

    /// <summary>反射取内部基元 Popover 的上下文（先例：ComboboxTests 的 ContextOf）。</summary>
    private static AtomBlazor.Primitives.PopoverContext ContextOf(ComponentTestRenderer renderer) =>
        (AtomBlazor.Primitives.PopoverContext)typeof(AtomBlazor.Primitives.PopoverPrimitive)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(renderer.FindComponent<AtomBlazor.Primitives.PopoverPrimitive>())!;

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
        string? searchPlaceholder = null,
        string? emptyMessage = null,
        string? searchQuery = null,
        EventCallback<string> searchQueryChanged = default,
        bool? isLoading = null,
        string? endOfListMessage = null,
        string? inputClass = null,
        IEnumerable<SelectOption<string>>? options = null,
        RenderFragment? childContent = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        string? popoverWidth = null,
        bool? matchTriggerWidth = null,
        string? activeClass = null,
        FieldOrientation? orientation = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (value is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldCombobox<string>.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.ValueExpression)] = valueExpression;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.Required)] = required.Value;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.Placeholder)] = placeholder;
        }
        if (searchPlaceholder is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.SearchPlaceholder)] = searchPlaceholder;
        }
        if (emptyMessage is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.EmptyMessage)] = emptyMessage;
        }
        if (searchQuery is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.SearchQuery)] = searchQuery;
        }
        if (searchQueryChanged.HasDelegate)
        {
            parameters[nameof(FormFieldCombobox<string>.SearchQueryChanged)] = searchQueryChanged;
        }
        if (isLoading is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.IsLoading)] = isLoading.Value;
        }
        if (endOfListMessage is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.EndOfListMessage)] = endOfListMessage;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.InputClass)] = inputClass;
        }
        if (options is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.Options)] = options;
        }
        if (childContent is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.ChildContent)] = childContent;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.AriaLabel)] = ariaLabel;
        }
        if (popoverWidth is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.PopoverWidth)] = popoverWidth;
        }
        if (matchTriggerWidth is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.MatchTriggerWidth)] = matchTriggerWidth.Value;
        }
        if (activeClass is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.ActiveClass)] = activeClass;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldCombobox<string>.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>单独一枚诱饵条目（芋头），用于断言 Options 优先时 ChildContent 被忽略。</summary>
    private static RenderFragment DecoyItem => builder =>
    {
        builder.OpenComponent<ComboboxItem<string>>(0);
        builder.AddAttribute(1, nameof(ComboboxItem<string>.Value), "z");
        builder.AddAttribute(2, nameof(ComboboxItem<string>.Text), "芋头");
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

        await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams(
            label: "水果", helperText: "挑一个水果", placeholder: "请选择",
            options: fruitOptions));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应不带for且ControlId完全不落控件触发器锚点为popover触发id()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams(
            label: "水果", helperText: "挑一个水果", options: singleOption));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // quirk：Combobox 家族 FieldLabel 不带 For，且 Combobox 无 Id 参数——
        // ControlId 从不落控件 id 通道，触发器锚点是基元自造的 popover-<n>-trigger。
        Assert.Matches(@"id=""popover-[0-9]+-trigger""", markup);
        Assert.Equal(0, Regex.Count(markup, @"id=""formfield-[0-9a-f]{32}""")); // 裸 ControlId 不落任何元素（-description 后缀档除外）
        Assert.DoesNotContain("for=", markup);
        // describedby 经正式参数通道落触发按钮（Help 态指向描述元素）。
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-description""", markup);
        Assert.Contains("role=\"combobox\"", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
    }

    // ------------------------------------------------------------------
    // Options 与组合双模式
    // ------------------------------------------------------------------

    [Fact]
    public async Task Options模式应优先于ChildContent且点击选项回填三步沿并关层()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            placeholder: "选一个",
            options: fruitOptions,
            childContent: DecoyItem,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)));
        await renderer.MountAsync<FormFieldCombobox<string>>(dict);
        await SettleAsync(renderer);

        // 关闭态：占位符呈现，诱饵文本不入树。
        Assert.Contains("选一个", renderer.Markup());
        Assert.DoesNotContain("芋头", renderer.Markup());

        await OpenPopupAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("苹果", markup);
        Assert.Contains("香蕉", markup);
        Assert.DoesNotContain("芋头", markup);

        // 经命令列表上下文选中"香蕉"→ 外层 HandleValueChanged 三步沿。
        var context = renderer.FindComponent<CommandList>().Context!;
        await renderer.Dispatcher.InvokeAsync(() => context.SelectItemByValueAsync("b"));
        await SettleAsync(renderer);

        Assert.Equal(["b"], reports);

        // 受控沿收起弹层（ComboboxTests 先例：重渲染根 1 把 _isOpen=false 同步回上下文）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        Assert.False(ContextOf(renderer).IsOpen);
        markup = renderer.Markup();
        Assert.Contains("香蕉", markup); // 触发器标题回填
        Assert.DoesNotContain("选一个", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
    }

    [Fact]
    public async Task 组合模式诱饵之外应正常注册并可选()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        RenderFragment items = builder =>
        {
            var seq = 0;
            foreach (var (value, text) in new (string, string)[] { ("a", "苹果"), ("b", "香蕉") })
            {
                builder.OpenComponent<ComboboxItem<string>>(seq);
                builder.AddAttribute(seq + 1, nameof(ComboboxItem<string>.Value), value);
                builder.AddAttribute(seq + 2, nameof(ComboboxItem<string>.Text), text);
                builder.CloseComponent();
                seq += 3;
            }
        };

        await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams(
            childContent: items,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenPopupAsync(renderer);
        var context = renderer.FindComponent<CommandList>().Context!;
        await renderer.Dispatcher.InvokeAsync(() => context.SelectItemByValueAsync("a"));
        await SettleAsync(renderer);

        Assert.Equal(["a"], reports);
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
                builder.OpenComponent<FormFieldCombobox<string>>(0);
                builder.AddAttribute(1, nameof(FormFieldCombobox<string>.Value), doc.Fruit);
                builder.AddAttribute(2, nameof(FormFieldCombobox<string>.ValueExpression), valueExpression);
                builder.AddAttribute(3, nameof(FormFieldCombobox<string>.ValueChanged),
                    EventCallback.Factory.Create<string?>(doc, value =>
                    {
                        doc.Fruit = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(4, nameof(FormFieldCombobox<string>.Label), (string?)"水果");
                builder.AddAttribute(5, nameof(FormFieldCombobox<string>.Options), fruitOptions);
                builder.AddAttribute(6, nameof(FormFieldCombobox<string>.Placeholder), "选一个");
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

        // 打开并经命令上下文点"苹果"→ 外层三步沿 → 校验消解。
        await OpenPopupAsync(renderer);
        var commandContext = renderer.FindComponent<CommandList>().Context!;
        await renderer.Dispatcher.InvokeAsync(() => commandContext.SelectItemByValueAsync("a"));
        await SettleAsync(renderer);

        Assert.Equal(["a"], reports);
        Assert.Equal("a", doc.Fruit);
        markup = renderer.Markup();
        Assert.DoesNotContain("必须选择苹果", markup);
        Assert.Contains("苹果", markup); // 触发器回填展示文本
    }

    // ------------------------------------------------------------------
    // 错误覆写
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应显形并切换describedby且不落ariaInvalid()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams(
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
        // quirk：Combobox 触发按钮无 aria-invalid 面，ErrorText 覆写不落控件。
        Assert.DoesNotContain("aria-invalid", markup);
    }

    // ------------------------------------------------------------------
    // SearchQuery 双态门控
    // ------------------------------------------------------------------

    [Fact]
    public async Task 未绑定SearchQueryChanged时内部过滤应保留且直通谓词不启用()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams(
            options: fruitOptions, emptyMessage: "没有水果了。"));
        await SettleAsync(renderer);

        await OpenPopupAsync(renderer);
        // 直通谓词未启用——Command 走内置 Contains 过滤（ComboboxTests 先例）。
        Assert.Null(renderer.FindComponent<Command>().FilterFunction);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("oninput", new ChangeEventArgs { Value = "zz" }));
        // CommandInput 缺省 500ms 防抖（DebounceSetSearchQuery 走真实计时器），等落定再收线。
        await Task.Delay(700);
        await SettleAsync(renderer);
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.DoesNotContain(">苹果<", markup); // 全部滤出
        Assert.DoesNotContain(">香蕉<", markup);
        Assert.Contains("没有水果了。", markup);   // 空消息经包装层透传显形
    }

    [Fact]
    public async Task 绑定SearchQueryChanged应透出查询文本并启用直通谓词()
    {
        var queries = new List<string>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams(
            options: fruitOptions,
            searchQueryChanged: EventCallback.Factory.Create<string>(new object(), q => queries.Add(q))));
        await SettleAsync(renderer);

        await OpenPopupAsync(renderer);
        Assert.NotNull(renderer.FindComponent<Command>().FilterFunction); // 外部过滤接管

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("oninput", new ChangeEventArgs { Value = "香" }));
        await Task.Delay(700); // 同上：防抖沿落定
        await SettleAsync(renderer);

        Assert.Contains("香", queries); // HandleSearchQueryChanged 两步沿（无字段通知）
    }

    // ------------------------------------------------------------------
    // 透传参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 必填应播报ariaRequired且禁用应落disabled属性面()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams(
            required: true, disabled: true, options: singleOption));
        await SettleAsync(renderer);

        // 沿 ComboboxTests 先例：本家族禁用面是原生 disabled 属性（与 Select 家族
        // aria-disabled 原子对形态不同），必填落 aria-required。
        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("disabled", markup);
    }

    [Fact]
    public async Task 无障碍名称输入类与透传应分别落触发按钮与根容器()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams(
            ariaLabel: "选择水果", inputClass: "atom:custom-combo", options: singleOption,
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-combobox" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"选择水果\"", markup);
        Assert.Contains("atom:custom-combo", markup);
        Assert.Contains("data-testid=\"field-combobox\"", markup);
        // quirk 面：InputClass 非空时触发按钮撤下缺省 PopoverWidth 宽度类。
        Assert.DoesNotContain("atom:w-[200px]", markup);
    }

    [Fact]
    public async Task 缺省宽度与激活类应落触发器且MatchTriggerWidth应改走内容宽度()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams(options: singleOption));
        await SettleAsync(renderer);

        // 关闭态：缺省 PopoverWidth 落触发按钮（ButtonCssClass 沿）。
        Assert.Contains("atom:w-[200px]", renderer.Markup());

        // 打开态：ActiveClass 缺省叠加触发按钮。
        await OpenPopupAsync(renderer);
        Assert.Contains("atom:bg-accent atom:text-accent-foreground", renderer.Markup());

        // MatchTriggerWidth：内容撤下固定宽度、改走 w-full（ComboboxTests 先例）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            options: singleOption, matchTriggerWidth: true)));
        await OpenAndSettleAsync(renderer);
        Assert.Contains("atom:w-full", renderer.Markup());
    }

    [Fact]
    public async Task 加载态与到底消息应随参数切换()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(options: singleOption, endOfListMessage: "已加载全部");
        await renderer.MountAsync<FormFieldCombobox<string>>(dict);
        await SettleAsync(renderer);

        await OpenPopupAsync(renderer);
        Assert.Contains("已加载全部", renderer.Markup());

        // 转入加载态：消息让位于加载指示（根 1 是 FormFieldCombobox）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            options: singleOption, endOfListMessage: "已加载全部", isLoading: true)));
        await OpenAndSettleAsync(renderer);

        Assert.DoesNotContain("已加载全部", renderer.Markup());
        Assert.Contains("role=\"status\"", renderer.Markup()); // 加载指示器（ComboboxTests 先例断据）
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldCombobox<string>>(FieldParams());
        await SettleAsync(renderer);

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、三枚硬码英文消息、
        // PopoverWidth/ActiveClass 缺省（bb: 已按改名惯例落 atom:）、查询面空、开关全 false。
        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Options);
        Assert.Null(field.ChildContent);
        Assert.Equal("Select an option...", field.Placeholder);
        Assert.Equal("Search...", field.SearchPlaceholder);
        Assert.Equal("No results found.", field.EmptyMessage);
        Assert.Equal(string.Empty, field.SearchQuery);
        Assert.Null(field.EndOfListMessage);
        Assert.False(field.IsLoading);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Equal("atom:w-[200px]", field.PopoverWidth);
        Assert.False(field.MatchTriggerWidth);
        Assert.Equal("atom:bg-accent atom:text-accent-foreground", field.ActiveClass);
        Assert.Contains("Select an option...", renderer.Markup()); // 缺省占位直落触发器
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
        Assert.DoesNotContain("aria-required=\"true\"", renderer.Markup());
    }
}
