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
/// FormFieldTreeSelect 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldTreeSelect&lt;Node&gt;（根 1），验证单选/多选双态各自的 For 路径（GetFieldExpression
/// 随 Multiple 切换 ValueExpression/ValuesExpression）、标签带 For=ControlId 且控件 id 经正式
/// Id 参数实通（与 Combobox/MultiSelect 家族不对称的接线沿）、多选级联勾选回填透传
/// （分支节点汇总含子键）、隐藏 input 的 Name 透传、Clearable 清空双态、ErrorText 覆写链、
/// Disabled 强制收起弹层并拦选择、Required/AriaLabel/InputClass/splat/ShowSearch 透传面与缺省参数面。
/// 树内点击沿 TreeSelectTests 先例经基元 TreeView 的 JsOnNodeActivate/JsOnNodeCheck 入口模拟。
/// </summary>
public class FormFieldTreeSelectTests
{
    /// <summary>
    /// 归一化 ControlId 的随机 GUID、popover-N / tree-N 自增短 id 与
    /// atom-tree-select(-search)-{GUID:N} 稳定标识，使快照与断言可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(
                    Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                    "atom-tree-select(?:-search)?-[0-9a-f]{32}", "atom-tree-select-<id>"),
                "popover-[0-9]+", "popover-<n>"),
            "tree-[0-9]+", "tree-<n>");

    /// <summary>树节点模型（值/文本/可选子级）。</summary>
    private sealed record Node(string Value, string Text, Node[]? Children);

    /// <summary>测试复用树：分支 p（子 c1/c2）+ 独立叶 r。</summary>
    private static readonly Node[] treeItems =
    [
        new("p", "父节点", [new Node("c1", "子节点一", null), new Node("c2", "子节点二", null)]),
        new Node("r", "独立叶", null),
    ];

    private static readonly Func<Node, string> valueOf = n => n.Value;
    private static readonly Func<Node, string> textOf = n => n.Text;
    private static readonly Func<Node, IEnumerable<Node>?> childrenOf = n => n.Children;

    /// <summary>
    /// 构造带全套基元服务的渲染器（门户/定位/焦点/本地化器 + NoopJavaScript）
    /// 沿 TreeSelectTests 先例。
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

    /// <summary>打开后强制重渲染门户宿主（根 0），让树内容确定性地进入渲染树。</summary>
    private static async Task OpenAndSettleAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>点击触发器打开弹层（关闭态触发器为 occurrence 0，清空按钮为 1）。</summary>
    private static async Task OpenPopupAsync(ComponentTestRenderer renderer)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);
    }

    /// <summary>反射取内部基元 Popover 的上下文（先例：TreeSelectTests 的 ContextOf）。</summary>
    private static AtomBlazor.Primitives.PopoverContext ContextOf(ComponentTestRenderer renderer) =>
        (AtomBlazor.Primitives.PopoverContext)typeof(AtomBlazor.Primitives.PopoverPrimitive)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(renderer.FindComponent<AtomBlazor.Primitives.PopoverPrimitive>())!;

    /// <summary>校验沿测试模型：Key 非 "r" 即无效；Keys 为空即无效。</summary>
    internal sealed class Doc
    {
        public string? Key { get; set; }
        public HashSet<string> Keys { get; set; } = new(StringComparer.Ordinal);
    }

    private static Dictionary<string, object?> FieldParams(
        IEnumerable<Node>? items = null,
        string? value = null,
        EventCallback<string?> valueChanged = default,
        Expression<Func<string?>>? valueExpression = null,
        HashSet<string>? values = null,
        EventCallback<HashSet<string>> valuesChanged = default,
        Expression<Func<HashSet<string>>>? valuesExpression = null,
        bool? multiple = null,
        bool? leafOnly = null,
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
        FieldOrientation? orientation = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FormFieldTreeSelect<Node>.ValueField)] = valueOf,
            [nameof(FormFieldTreeSelect<Node>.TextField)] = textOf,
            [nameof(FormFieldTreeSelect<Node>.ChildrenProperty)] = childrenOf,
        };
        if (items is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Items)] = items;
        }
        if (value is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Value)] = value;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.ValueChanged)] = valueChanged;
        }
        if (valueExpression is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.ValueExpression)] = valueExpression;
        }
        if (values is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Values)] = values;
        }
        if (valuesChanged.HasDelegate)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.ValuesChanged)] = valuesChanged;
        }
        if (valuesExpression is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.ValuesExpression)] = valuesExpression;
        }
        if (multiple is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Multiple)] = multiple.Value;
        }
        if (leafOnly is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.LeafOnly)] = leafOnly.Value;
        }
        if (showSearch is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.ShowSearch)] = showSearch.Value;
        }
        if (clearable is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Clearable)] = clearable.Value;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Placeholder)] = placeholder;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Required)] = required.Value;
        }
        if (name is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Name)] = name;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldTreeSelect<Node>.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    // ------------------------------------------------------------------
    // 快照与 For 接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTreeSelect<Node>>(FieldParams(
            items: treeItems, label: "节点", helperText: "选一枚节点",
            value: "r", name: "tree-key"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应经For与控件id实通且describedby落触发按钮()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTreeSelect<Node>>(FieldParams(
            items: treeItems, label: "节点", helperText: "选一枚节点"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        var match = Regex.Match(markup, @"id=""(formfield-[0-9a-f]{32})""");
        Assert.True(match.Success, "控件 id 经正式 Id 参数落触发按钮（按位置规则压过基元 popover 触发 id）");

        var controlId = match.Groups[1].Value;
        // 与 Combobox/MultiSelect 家族不对称的接线沿：TreeSelect 家族 FieldLabel 带 For 实通。
        Assert.Contains($"for=\"{controlId}\"", markup);
        Assert.Contains($"aria-describedby=\"{controlId}-description\"", markup);
        // 基元触发器的显式 aria-haspopup="true" 按位置规则压过包装层透传的 "tree"（TreeSelectTests 先例）。
        Assert.Contains("aria-haspopup=\"true\"", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
    }

    // ------------------------------------------------------------------
    // 单选沿与 For 路径
    // ------------------------------------------------------------------

    [Fact]
    public async Task 单选点击节点应经三步沿回填并收起弹层()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            items: treeItems, placeholder: "选一选",
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)));
        await renderer.MountAsync<FormFieldTreeSelect<Node>>(dict);
        await SettleAsync(renderer);

        Assert.Contains("选一选", renderer.Markup());

        await OpenPopupAsync(renderer);
        Assert.Contains("role=\"tree\"", renderer.Markup());

        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();
        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeActivate("r"));
        await SettleAsync(renderer);

        Assert.Equal(["r"], reports);
        Assert.False(ContextOf(renderer).IsOpen); // EventCallback 接收方重渲染 → Open=false 同步回上下文
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            items: treeItems, value: "r",
            valueChanged: EventCallback.Factory.Create<string?>(new object(), _ => { }))));
        await SettleAsync(renderer);
        Assert.Contains("独立叶", renderer.Markup()); // 触发器标题回填节点文本
        Assert.DoesNotContain("选一选", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 多选沿与级联回填透传
    // ------------------------------------------------------------------

    [Fact]
    public async Task 多选级联勾选应回填含分支汇总的键集合并逐项落隐藏input()
    {
        var reports = new List<HashSet<string>>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTreeSelect<Node>>(FieldParams(
            items: treeItems, multiple: true, showSearch: false,
            values: new HashSet<string>(StringComparer.Ordinal),
            name: "tree-multi",
            valuesChanged: EventCallback.Factory.Create<HashSet<string>>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        await OpenPopupAsync(renderer);
        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();

        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeCheck("p"));
        await SettleAsync(renderer);

        // 级联汇总沿经包装层原样透传：分支键连同两子键一起回填。
        Assert.Single(reports);
        Assert.True(reports[0].SetEquals(["p", "c1", "c2"]));

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            items: treeItems, multiple: true, showSearch: false,
            values: reports[0], name: "tree-multi",
            valuesChanged: EventCallback.Factory.Create<HashSet<string>>(new object(), _ => { }))));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Equal(3, Regex.Count(markup, "type=\"hidden\""));
        Assert.Equal(3, Regex.Count(markup, "name=\"tree-multi\"")); // Name 透传落每枚隐藏 input
        foreach (var value in new[] { "p", "c1", "c2" })
        {
            Assert.Contains($"value=\"{value}\"", markup);
        }
    }

    [Fact]
    public async Task Clearable清空应分别复位单选与多选双态()
    {
        var singleReports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTreeSelect<Node>>(FieldParams(
            items: treeItems, showSearch: false, value: "r",
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => singleReports.Add(v))));
        await SettleAsync(renderer);

        // 关闭态：触发按钮 occurrence 0、清空按钮 occurrence 1（TreeSelectTests 先例）。
        Assert.Contains("Clear selection", renderer.Markup());
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(renderer);

        Assert.Equal([null], singleReports); // 三步沿：赋值守卫→回调→NotifyFieldChanged

        var multiReports = new List<HashSet<string>>();
        var multiRenderer = CreateRenderer();
        await multiRenderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await multiRenderer.MountAsync<FormFieldTreeSelect<Node>>(FieldParams(
            items: treeItems, showSearch: false, multiple: true,
            values: new HashSet<string>(["r"], StringComparer.Ordinal),
            valuesChanged: EventCallback.Factory.Create<HashSet<string>>(new object(), v => multiReports.Add(v))));
        await SettleAsync(multiRenderer);

        await multiRenderer.Dispatcher.InvokeAsync(async () =>
            await multiRenderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(multiRenderer);

        Assert.Single(multiReports);
        Assert.Empty(multiReports[0]); // 多选态清空回填空集
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿（For 路径双态）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 单选态校验消息应经ValueExpression路径随选择消解()
    {
        var doc = new Doc { Key = "x" };
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Key));

        void Validate()
        {
            messages.Clear();
            if (doc.Key != "r")
            {
                messages.Add(field, "必须选择独立叶");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();

        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        Expression<Func<string?>> valueExpression = () => doc.Key;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldTreeSelect<Node>>(0);
                builder.AddAttribute(1, nameof(FormFieldTreeSelect<Node>.Items), treeItems);
                builder.AddAttribute(2, nameof(FormFieldTreeSelect<Node>.ValueField), valueOf);
                builder.AddAttribute(3, nameof(FormFieldTreeSelect<Node>.TextField), textOf);
                builder.AddAttribute(4, nameof(FormFieldTreeSelect<Node>.ChildrenProperty), childrenOf);
                builder.AddAttribute(5, nameof(FormFieldTreeSelect<Node>.ShowSearch), false);
                builder.AddAttribute(6, nameof(FormFieldTreeSelect<Node>.ValueExpression), valueExpression);
                builder.AddAttribute(7, nameof(FormFieldTreeSelect<Node>.ValueChanged),
                    EventCallback.Factory.Create<string?>(doc, value =>
                    {
                        doc.Key = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(8, nameof(FormFieldTreeSelect<Node>.Label), (string?)"节点");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("必须选择独立叶", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", markup);

        await OpenPopupAsync(renderer);
        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();
        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeActivate("r"));
        await SettleAsync(renderer);

        Assert.Equal(["r"], reports);
        markup = renderer.Markup();
        Assert.DoesNotContain("必须选择独立叶", markup); // 消解（GetFieldExpression=ValueExpression 单选档）
        Assert.Contains("独立叶", markup);
    }

    [Fact]
    public async Task 多选态字段表达式应切换为ValuesExpression路径()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var keysField = new FieldIdentifier(doc, nameof(Doc.Keys));

        void Validate()
        {
            messages.Clear();
            if (doc.Keys.Count == 0)
            {
                messages.Add(keysField, "至少选择一枚节点");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();

        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        Expression<Func<HashSet<string>>> valuesExpression = () => doc.Keys;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldTreeSelect<Node>>(0);
                builder.AddAttribute(1, nameof(FormFieldTreeSelect<Node>.Items), treeItems);
                builder.AddAttribute(2, nameof(FormFieldTreeSelect<Node>.ValueField), valueOf);
                builder.AddAttribute(3, nameof(FormFieldTreeSelect<Node>.TextField), textOf);
                builder.AddAttribute(4, nameof(FormFieldTreeSelect<Node>.ChildrenProperty), childrenOf);
                builder.AddAttribute(5, nameof(FormFieldTreeSelect<Node>.ShowSearch), false);
                builder.AddAttribute(6, nameof(FormFieldTreeSelect<Node>.Multiple), true);
                // 两枚表达式同时在场：Multiple=true 时归属必须走 ValuesExpression（三目沿）。
                builder.AddAttribute(7, nameof(FormFieldTreeSelect<Node>.ValuesExpression), valuesExpression);
                builder.AddAttribute(8, nameof(FormFieldTreeSelect<Node>.ValueExpression),
                    (Expression<Func<string?>>)(() => doc.Key));
                builder.AddAttribute(9, nameof(FormFieldTreeSelect<Node>.ValuesChanged),
                    EventCallback.Factory.Create<HashSet<string>>(doc, value => doc.Keys = value));
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);
        Assert.Contains("至少选择一枚节点", renderer.Markup());

        await OpenPopupAsync(renderer);
        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();
        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeCheck("r"));
        await SettleAsync(renderer);

        Assert.DoesNotContain("至少选择一枚节点", renderer.Markup()); // 多选档归属 Keys 字段
    }

    // ------------------------------------------------------------------
    // 错误覆写与参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应显形且触发按钮不带ariaInvalid()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTreeSelect<Node>>(FieldParams(
            items: treeItems, label: "节点", helperText: "选一枚节点",
            errorText: "服务端驳回：必须选择独立叶"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：必须选择独立叶", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.DoesNotContain("选一枚节点", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        // TreeSelect 本体的 aria-invalid 面只认 EditContext 校验态——包装层 ErrorText
 // 不落控件（与 两族口径一致），实断据仍为 Field 根 data-invalid。
        Assert.DoesNotContain("aria-invalid", markup);
    }

    [Fact]
    public async Task 禁用应强制收起弹层并拦下选择()
    {
        var reports = new List<string?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            items: treeItems, showSearch: false,
            valueChanged: EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)));
        await renderer.MountAsync<FormFieldTreeSelect<Node>>(dict);
        await SettleAsync(renderer);

        // 先以可用态打开弹层（照本体先例：Disabled 挂载态下受控守卫即刻压回）。
        await OpenPopupAsync(renderer);
        Assert.Contains("role=\"tree\"", renderer.Markup());

        dict[nameof(FormFieldTreeSelect<Node>.Disabled)] = true;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);

        Assert.False(ContextOf(renderer).IsOpen);
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
        Assert.Contains("disabled", renderer.Markup());

        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();
        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeActivate("r"));
        await SettleAsync(renderer);
        Assert.Empty(reports); // Disabled 拦在 SelectAsync 门口
    }

    [Fact]
    public async Task 必填名称与AriaLabel及透传应落透传参数面()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldTreeSelect<Node>>(FieldParams(
            items: treeItems, required: true, name: "tree-city",
            ariaLabel: "选择城市", inputClass: "atom:custom-tree",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-treeselect" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("aria-label=\"选择城市\"", markup);
        Assert.Contains("atom:custom-tree", markup);
        Assert.Contains("data-testid=\"field-treeselect\"", markup);
        // 无 EditContext 时隐藏 input 的 name 取正式 Name 参数透传。
        Assert.Contains("name=\"tree-city\"", markup);
    }

    [Fact]
    public async Task ShowSearch开关应决定弹层搜索框渲染()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        // 缺省 ShowSearch=true：开层显搜索框（Localizer 英文缺省面）。
        var dict = FieldParams(items: treeItems);
        await renderer.MountAsync<FormFieldTreeSelect<Node>>(dict);
        await SettleAsync(renderer);
        await OpenPopupAsync(renderer);
        Assert.Contains("Search", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            items: treeItems, showSearch: false)));
        await OpenAndSettleAsync(renderer);
        Assert.DoesNotContain("atom-tree-select-search-", renderer.Markup());
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldTreeSelect<Node>>(FieldParams(items: treeItems));
        await SettleAsync(renderer);

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、ShowSearch/Clearable 缺省开、
        // Multiple/LeafOnly 关、Values 空集、Value/Placeholder/Name/InputClass 皆空。
        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.False(field.Multiple);
        Assert.False(field.LeafOnly);
        Assert.True(field.ShowSearch);
        Assert.True(field.Clearable);
        Assert.Null(field.Value);
        Assert.Empty(field.Values);
        Assert.Null(field.Placeholder);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Null(field.Name);
        Assert.Null(field.InputClass);
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
        Assert.DoesNotContain("aria-required=\"true\"", renderer.Markup());
    }
}
