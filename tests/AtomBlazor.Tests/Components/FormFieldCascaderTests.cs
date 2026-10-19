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
/// FormFieldCascader 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下
/// 挂载 FormFieldCascader（根 1，弹层家族务必挂门户），验证标签 for 实通触发按钮
/// （Id=ControlId 经 PopoverTrigger 属性直落 button）、aria-describedby 经正式参数通道落
/// 触发按钮、打开弹层点叶子经封装层三步沿回填上报并收起、点分支默认不回填仅导航、
/// ChangeOnSelect/ShowSearch/Clearable 透传沿（分支回填保持打开、撤搜索框、撤清除钮）、
/// 清除钮上报 null、EditContext 校验消息随选择消长并经底座自算的触发器 aria-invalid
/// 显形（封装层无 AriaInvalid 通道，与 CurrencyInput 家族显式传入形态不同）、ErrorText
/// 手动覆写只切换触发器 describedby（无 EditContext 不落 aria-invalid）、Disabled 落触发器
/// 与隐藏 input 并拒绝打开、Required/Name 落触发器与隐藏 input、AriaLabel 压本地化缺省、
/// InputClass 落底座容器 div 而 splat 落触发按钮（分家形态）、ReadOnly 通道不适用
///（封装层与底座皆无该参数——反射实证）与缺省参数面。headless 口径沿用 CascaderTests：
/// 关闭态唯一 onclick 携带者为触发器（清除钮在场时居后），打开态门户根先遍历——
/// 弹层项 p(0)、r(1)，触发器随后；快照含 formfield/atom-cascader GUID 与 popover-N 短 id
/// 先经正则归一化。
/// </summary>
public class FormFieldCascaderTests
{
    /// <summary>
    /// 归一化 ControlId 随机 GUID、atom-cascader(-search|-item-…) GUID 稳定 id 与
    /// popover-N 自增短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                "atom-cascader(?:-search)?-[0-9a-f]{32}", "atom-cascader-<id>"),
            "popover-[0-9]+", "popover-<n>");

    /// <summary>层级节点模型（值/文本/可选子级），沿用 CascaderTests 口径。</summary>
    private sealed record Node(string Value, string Text, Node[]? Children);

    /// <summary>测试复用层级：分支 p（子 c1/c2）+ 独立叶 r。</summary>
    private static readonly Node[] cascaderItems =
    [
        new("p", "父节点", [new Node("c1", "子节点一", null), new Node("c2", "子节点二", null)]),
        new Node("r", "独立叶", null),
    ];

    private static readonly Func<Node, string> valueOf = n => n.Value;
    private static readonly Func<Node, string> textOf = n => n.Text;
    private static readonly Func<Node, IEnumerable<Node>?> childrenOf = n => n.Children;

    /// <summary>构造带全套基元服务的渲染器（门户/定位/焦点/本地化器），与 CascaderTests 同构。</summary>
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
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>触发一次 DOM 事件（须在 Dispatcher 上派发，沿用 CascaderTests 先例）。</summary>
    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () => await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树（CascaderTests 先例）。</summary>
    private static async Task OpenAndSettleAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>点触发器打开弹层（关闭态清除钮缺席时唯一 onclick 携带者）并冲刷门户根。</summary>
    private static async Task OpenPopupAsync(ComponentTestRenderer renderer)
    {
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await OpenAndSettleAsync(renderer);
    }

    /// <summary>反射取封装内基元 Popover 的上下文（先例：CascaderTests 的 ContextOf）。</summary>
    private static AtomBlazor.Primitives.PopoverContext ContextOf(ComponentTestRenderer renderer) =>
        (AtomBlazor.Primitives.PopoverContext)typeof(AtomBlazor.Primitives.PopoverPrimitive)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(renderer.FindComponent<AtomBlazor.Primitives.PopoverPrimitive>())!;

    /// <summary>校验沿测试模型：Category 非 "r" 即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public string? Category { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：订阅 OnFieldChanged/OnValidationRequested
    /// Category 不为 "r" 时用 ValidationMessageStore 写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Category));

        void Validate()
        {
            messages.Clear();
            if (doc.Category != "r")
            {
                messages.Add(field, "必须选择独立叶");
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
        bool? changeOnSelect = null,
        bool? showSearch = null,
        bool? clearable = null,
        string? placeholder = null,
        bool? disabled = null,
        bool? required = null,
        string? name = null,
        string? inputClass = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FormFieldCascader<Node>.Items)] = cascaderItems,
            [nameof(FormFieldCascader<Node>.ValueField)] = valueOf,
            [nameof(FormFieldCascader<Node>.TextField)] = textOf,
            [nameof(FormFieldCascader<Node>.ChildrenProperty)] = childrenOf,
        };
        if (value is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldCascader<Node>.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.ValueExpression)] = valueExpression;
        }
        if (changeOnSelect is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.ChangeOnSelect)] = changeOnSelect.Value;
        }
        if (showSearch is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.ShowSearch)] = showSearch.Value;
        }
        if (clearable is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.Clearable)] = clearable.Value;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.Placeholder)] = placeholder;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.Required)] = required.Value;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.Name)] = name;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldCascader<Node>.AriaLabel)] = ariaLabel;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>取底座触发按钮开标签（data-cascader-trigger 指纹）。</summary>
    private static string TriggerTag(string markup) =>
        Regex.Match(markup, """<button[^>]*data-cascader-trigger[^>]*>""").Value;

    /// <summary>取底座触发按钮全段（含内部文本，受限非贪婪到首个 button 闭合）。</summary>
    private static string TriggerSpan(string markup) =>
        Regex.Match(markup, """<button[^>]*data-cascader-trigger[^>]*>.*?</button>""", RegexOptions.Singleline).Value;

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认关闭态结构快照应与基线一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(
            label: "商品类目", helperText: "逐级选择到叶子"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应实通触发按钮且describedby经正式参数落触发器()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(
            label: "商品类目", helperText: "逐级选择到叶子"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // FieldLabel For=ControlId 实通：底座 Id="@ControlId" 经 PopoverTrigger 落按钮 id。
        var id = Regex.Match(TriggerTag(markup), "id=\"([^\"]+)\"").Groups[1].Value;
        Assert.Matches(@"^formfield-[0-9a-f]{32}$", id);
        Assert.Contains($"for=\"{id}\"", markup);
        Assert.Contains($"aria-describedby=\"{id}-description\"", TriggerTag(markup));
        Assert.Contains($"id=\"{id}-description\"", markup);

        // 显式 null 复位 HelperText：describedby 属性整体缺席。
        var bare = FieldParams(label: "商品类目");
        bare[nameof(FormFieldCascader<Node>.HelperText)] = null;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, bare));
        await SettleAsync(renderer);
        Assert.DoesNotContain("aria-describedby", TriggerTag(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 弹层选择沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开弹层点叶子应经封装层三步沿回填上报并收起()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var field = await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenPopupAsync(renderer);
        // 打开态门户根先遍历：p(0)、r(1)；点击独立叶 r。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);

        Assert.Equal(["r"], reports);
        Assert.Equal("r", field.Value); // HandleValueChanged 赋值步沿
        Assert.False(ContextOf(renderer).IsOpen);
        // 受控沿：封装层回填后重渲染，触发器文本换为叶子标题。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(value: "r")));
        await SettleAsync(renderer);
        Assert.Contains("独立叶", TriggerSpan(renderer.Markup()));
    }

    [Fact]
    public async Task 点击分支应展开子列且默认不回填值()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenPopupAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0); // 分支 p
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("data-cascader-column=\"1\"", markup);
        Assert.Contains("data-cascader-parent=\"p\"", markup);
        Assert.Contains("data-cascader-item=\"c1\"", markup);
        Assert.Empty(reports); // 分支导航不回填（ChangeOnSelect 关）
        Assert.True(ContextOf(renderer).IsOpen);
    }

    [Fact]
    public async Task ChangeOnSelect透传应回填分支并保持弹层打开()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var field = await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(
            changeOnSelect: true,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenPopupAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0); // 分支 p
        await OpenAndSettleAsync(renderer);

        Assert.Equal(["p"], reports);
        Assert.Equal("p", field.Value);
        Assert.True(ContextOf(renderer).IsOpen);
        Assert.Contains("data-cascader-parent=\"p\"", renderer.Markup()); // 同时继续展开子列
    }

    [Fact]
    public async Task ShowSearch为假撤搜索框且Clearable为假撤清除钮()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(showSearch: false));
        await SettleAsync(renderer);

        await OpenPopupAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("data-cascader-column=\"0\"", markup);
        Assert.DoesNotContain("Search paths…", markup); // 搜索框整体撤下
        Assert.DoesNotContain("data-cascader-search", markup);

        // Clearable=false：有值也不渲染清除按钮。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            value: "r", showSearch: false, clearable: false)));
        await SettleAsync(renderer);
        Assert.DoesNotContain("Clear selection", renderer.Markup());
    }

    [Fact]
    public async Task 清除按钮应经封装层上报null()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var field = await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(
            value: "r",
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 关闭态 onclick 序位：触发器(0)、清除按钮(1)（CascaderTests 先例）。
        Assert.Contains("Clear selection", renderer.Markup());
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);

        Assert.Equal([null], reports);
        Assert.Null(field.Value);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随选择消长并经触发器的ariaInvalid显形()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        Expression<Func<string?>> valueExpression = () => doc.Category;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldCascader<Node>>(0);
                builder.AddAttribute(1, nameof(FormFieldCascader<Node>.Items), cascaderItems);
                builder.AddAttribute(2, nameof(FormFieldCascader<Node>.ValueField), valueOf);
                builder.AddAttribute(3, nameof(FormFieldCascader<Node>.TextField), textOf);
                builder.AddAttribute(4, nameof(FormFieldCascader<Node>.ChildrenProperty), childrenOf);
                builder.AddAttribute(5, nameof(FormFieldCascader<Node>.Value), doc.Category);
                builder.AddAttribute(6, nameof(FormFieldCascader<Node>.ValueExpression), valueExpression);
                builder.AddAttribute(7, nameof(FormFieldCascader<Node>.ValueChanged),
                    EventCallback.Factory.Create<string?>(doc, value => doc.Category = value));
                builder.AddAttribute(8, nameof(FormFieldCascader<Node>.Label), (string?)"商品类目");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始无效：广播校验后错误经 FieldError 呈现；封装层无 AriaInvalid 通道
        // 触发器的 aria-invalid 由底座 validation 自算（EditContext+ValueExpression 级联沿）
        // describedby 经 DescribedById 切到错误元素并落触发器。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("必须选择独立叶", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", TriggerTag(markup));
        Assert.Contains("aria-invalid=\"true\"", TriggerTag(markup));

        // 打开弹层点独立叶 r → 封装层三步沿 + 底座自身字段通知 → 校验消解。
        await OpenPopupAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);

        Assert.Equal("r", doc.Category);
        markup = renderer.Markup();
        Assert.DoesNotContain("必须选择独立叶", markup);
        Assert.DoesNotContain("data-invalid", markup);
    }

    // ------------------------------------------------------------------
    // 错误覆写与禁用
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应切换触发器describedby且控件侧不落ariaInvalid()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(
            label: "商品类目", helperText: "逐级选择到叶子",
            errorText: "服务端驳回：类目已下线"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：类目已下线", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.DoesNotContain("逐级选择到叶子", markup);
        // 归一化残注：Scrub 注入的 "<id>" 含 '>'，会截断属性级标签正则——本断言直打全量 markup。
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        // 通道实证：手动错误态不吃封装层（无 EditContext 时底座 validation 判不了无效）
        // 触发器 aria-invalid 整体缺席——与 CurrencyInput 家族的显式通道形态不同。
        Assert.DoesNotContain("aria-invalid", renderer.Markup());
    }

    [Fact]
    public async Task 禁用态应落触发器与隐藏输入并拒绝打开弹层()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(
            value: "c1", disabled: true));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("disabled", TriggerTag(markup));
        Assert.Matches("""<input type="hidden"[^>]*disabled""", markup);

        // headless 直发 onclick；底座 SetOpen 的 !Disabled 门把弹层压回关闭态。
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        Assert.DoesNotContain("data-cascader-item", renderer.Markup());
        Assert.Contains("aria-expanded=\"false\"", TriggerTag(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 透传参数面与落点
    // ------------------------------------------------------------------

    [Fact]
    public async Task 必填与AriaLabel应落触发器且Name落隐藏输入InputClass与透传落点分家()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams(
            required: true, name: "cat", ariaLabel: "类目选择", inputClass: "atom:custom-cs",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-cascader" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var trigger = TriggerTag(markup);
        Assert.Contains("aria-required=\"true\"", trigger);
        // 沿：AriaLabel 压过底座触发器 aria-label 的 Localizer["Cascader.Label"] 英文缺省回落。
        Assert.Contains("aria-label=\"类目选择\"", trigger);
        Assert.DoesNotContain("aria-label=\"Select a category\"", markup);
        Assert.Matches(@"<input type=""hidden""[^>]*name=""cat""", markup);

        // 落点分家实证：InputClass 经底座 Class 落容器 div，splat 经 @attributes 落触发按钮。
        var container = Regex.Match(markup, """<div[^>]*atom:custom-cs[^>]*>""").Value;
        Assert.NotEmpty(container);
        Assert.Contains("data-testid=\"field-cascader\"", trigger);
        Assert.DoesNotContain("data-testid", container);
        var fieldRoot = Regex.Match(markup, """<div[^>]*data-slot="field"[^>]*>""").Value;
        Assert.DoesNotContain("data-testid", fieldRoot);
        Assert.DoesNotContain("atom:custom-cs", fieldRoot);
    }

    // ------------------------------------------------------------------
    // 缺省参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 缺省参数面应与一致且ReadOnly通道不适用()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        // 缺省：ShowSearch/Clearable=true，其余开关全 false，引用面全 null。
        var field = await renderer.MountAsync<FormFieldCascader<Node>>(FieldParams());
        await SettleAsync(renderer);

        Assert.Null(field.Value);
        Assert.Null(field.ValueExpression);
        Assert.False(field.ChangeOnSelect);
        Assert.True(field.ShowSearch);
        Assert.True(field.Clearable);
        Assert.Null(field.Placeholder);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.False(field.ValueChanged.HasDelegate);
        Assert.Null(field.Name);
        Assert.Null(field.InputClass);

        var markup = renderer.Markup();
        Assert.Contains("Select a category…", markup); // 占位本地化缺省
        Assert.Contains("aria-label=\"Select a category\"", TriggerTag(markup)); // 触发器 aria-label 回落
        Assert.DoesNotContain("aria-required", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        Assert.DoesNotContain("name=", markup);

 // 通道逐家族实证（/ 纪律）：本家族封装层与底座 Cascader 均无 ReadOnly 参数。
        Assert.Null(typeof(FormFieldCascader<Node>).GetProperty("ReadOnly", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(Cascader<Node>).GetProperty("ReadOnly", BindingFlags.Public | BindingFlags.Instance));
    }
}
