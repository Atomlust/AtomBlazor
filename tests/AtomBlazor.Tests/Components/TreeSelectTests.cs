using AtomBlazor.Primitives;
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
/// TreeSelect 测试：弹层树单选沿（点击节点 → ValueChanged 回填、弹层随收、
/// Disabled 不响应且参数沿强制关闭、LeafOnly 禁选分支放行叶）、多选沿
/// （级联勾选回填 Values、LeafOnly 过滤分支、同集重放不重复回调、Clearable 清空）、
/// 隐藏 input 的 name/value 呈现、ShowSearch 开关决定搜索框渲染。
/// 树内点击在 headless 下经基元 TreeView 的 JsOnNodeActivate/JsOnNodeCheck 入口模拟
/// （JS 键盘模块的真实 DOM 点击由 node 侧覆盖）；快照含 popover-N、tree-N 与 GUID 短 id，
/// 先经正则归一化。
/// </summary>
public class TreeSelectTests
{
    /// <summary>
    /// 归一化 popover-N / tree-N 自增短 id 与 atom-tree-select(-search)-{GUID:N} 稳定标识，
    /// 使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "atom-tree-select(?:-search)?-[0-9a-f]{32}", "atom-tree-select-<id>"),
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
    /// 构造带全套基元服务的渲染器（门户/定位/焦点/本地化器），与 SelectTests 同构。
    /// </summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
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

    /// <summary>反射取 TreeSelect 内基元 Popover 的上下文（先例：SelectTests 的 ContextOf）。</summary>
    private static AtomBlazor.Primitives.PopoverContext ContextOf(ComponentTestRenderer renderer) =>
        (AtomBlazor.Primitives.PopoverContext)typeof(AtomBlazor.Primitives.PopoverPrimitive)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(renderer.FindComponent<AtomBlazor.Primitives.PopoverPrimitive>())!;

    /// <summary>TreeSelect 的基础参数字典（三个必需委托参数 + 默认关闭搜索框）。</summary>
    private static Dictionary<string, object?> BaseParams() => new()
    {
        [nameof(TreeSelect<Node>.Items)] = treeItems,
        [nameof(TreeSelect<Node>.ValueField)] = valueOf,
        [nameof(TreeSelect<Node>.TextField)] = textOf,
        [nameof(TreeSelect<Node>.ChildrenProperty)] = childrenOf,
        [nameof(TreeSelect<Node>.ShowSearch)] = false,
    };

    /// <summary>挂载 PortalHost（根 0）+ TreeSelect（根 1）；extra 覆盖基础参数。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, TreeSelect<Node> Select)> MountAsync(
        Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = BaseParams();
        foreach (var (key, value) in extra)
        {
            dict[key] = value;
        }

        var select = await renderer.MountAsync<TreeSelect<Node>>(dict);
        await SettleAsync(renderer);
        return (renderer, js, select);
    }

    /// <summary>点击触发器打开弹层并让门户把树画进渲染树。</summary>
    private static async Task OpenPopupAsync(ComponentTestRenderer renderer)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);
    }

    // ------------------------------------------------------------------
    // 单选沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 单选点击节点应通知ValueChanged回填标签并收起弹层()
    {
        var reports = new List<string?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        Assert.Contains("role=\"tree\"", renderer.Markup());

        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();
        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeActivate("r"));
        await SettleAsync(renderer);

        Assert.Equal(["r"], reports);
        var context = ContextOf(renderer);
        Assert.False(context.IsOpen); // EventCallback 接收方重渲染 → Open=false 同步回上下文
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
        Assert.Contains("独立叶", renderer.Markup()); // 触发器标题回填节点文本
    }

    [Fact]
    public async Task 禁用态应吞掉触发与选择并强制收起弹层()
    {
        var reports = new List<string?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        // 先以可用态打开弹层，让树完成注册（Disabled 挂载态下受控不一致守卫会在
        // 下一渲染立刻把上下文压回关闭，内容来不及注册）。
        await OpenPopupAsync(renderer);
        Assert.Contains("role=\"tree\"", renderer.Markup());

        // 转入禁用：OnParametersSet 的 Disabled 强制沿把 open 压回 false，
        // 受控不一致守卫随即关闭上下文；按钮带上 disabled。
        var disabledDict = BaseParams();
        disabledDict[nameof(TreeSelect<Node>.Disabled)] = true;
        disabledDict[nameof(TreeSelect<Node>.ValueChanged)] =
            EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v));
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, disabledDict));
        await SettleAsync(renderer);

        Assert.False(ContextOf(renderer).IsOpen);
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
        Assert.Contains("disabled", renderer.Markup());

        // 退场中的树实例仍在门户里：绕过 UI 直接驱动选中，Disabled 也拦在 SelectAsync 门口。
        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();
        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeActivate("r"));
        await SettleAsync(renderer);
        Assert.Empty(reports);
    }

    [Fact]
    public async Task LeafOnly应禁选分支节点而放行叶子节点()
    {
        var reports = new List<string?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.LeafOnly)] = true,
            [nameof(TreeSelect<Node>.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();

        // 展开分支让子叶完成注册。
        await renderer.Dispatcher.InvokeAsync(() => tree.Context.ExpandNode("p"));
        await OpenAndSettleAsync(renderer);

        var context = ContextOf(renderer);
        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeActivate("p", hasChildren: true));
        await SettleAsync(renderer);

        Assert.Empty(reports);        // 分支不可选
        Assert.True(context.IsOpen);  // 弹层保持打开

        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeActivate("c1"));
        await SettleAsync(renderer);

        Assert.Equal(["c1"], reports); // 叶子可选并收起弹层
        Assert.False(context.IsOpen);
    }

    // ------------------------------------------------------------------
    // 多选沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 多选级联勾选应回填Values并逐项渲染隐藏input()
    {
        var reports = new List<HashSet<string>>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.Multiple)] = true,
            [nameof(TreeSelect<Node>.Values)] = new HashSet<string>(StringComparer.Ordinal),
            [nameof(TreeSelect<Node>.ValuesChanged)] = EventCallback.Factory.Create<HashSet<string>>(new object(), v => reports.Add(v)),
            [nameof(TreeSelect<Node>.Name)] = "tree-multi",
        });

        await OpenPopupAsync(renderer);
        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();

        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeCheck("p"));
        await SettleAsync(renderer);

        Assert.Single(reports);
        Assert.True(reports[0].SetEquals(["p", "c1", "c2"])); // 级联汇总节点一并回填

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Equal(3, Regex.Count(markup, "type=\"hidden\""));
        Assert.Equal(3, Regex.Count(markup, "name=\"tree-multi\""));
        foreach (var value in new[] { "p", "c1", "c2" })
        {
            Assert.Contains($"value=\"{value}\"", markup);
        }
    }

    [Fact]
    public async Task LeafOnly多选应过滤分支且同集合重放不重复回调()
    {
        var reports = new List<HashSet<string>>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.Multiple)] = true,
            [nameof(TreeSelect<Node>.LeafOnly)] = true,
            [nameof(TreeSelect<Node>.Values)] = new HashSet<string>(StringComparer.Ordinal),
            [nameof(TreeSelect<Node>.ValuesChanged)] = EventCallback.Factory.Create<HashSet<string>>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        var tree = renderer.FindComponent<AtomBlazor.Primitives.TreeViewPrimitive>();

        await renderer.Dispatcher.InvokeAsync(() => tree.JsOnNodeCheck("p"));
        await SettleAsync(renderer);

        Assert.Single(reports);
        Assert.True(reports[0].SetEquals(["c1", "c2"])); // 分支被 CanSelect 过滤

        // 同集合重放：SelectManyAsync 的 SetEquals 守卫吞掉重复通知。
        await renderer.Dispatcher.InvokeAsync(() => tree.Context.SetCheckedValues(["p", "c1", "c2"]));
        await SettleAsync(renderer);
        Assert.Single(reports);
    }

    [Fact]
    public async Task Clearable清空按钮应分别清空单选与多选()
    {
        var singleReports = new List<string?>();
        var (singleRenderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.Value)] = "r",
            [nameof(TreeSelect<Node>.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), v => singleReports.Add(v)),
        });

        // 关闭态：触发按钮是 occurrence 0，清空按钮是 occurrence 1。
        Assert.Contains("Clear selection", singleRenderer.Markup());
        await singleRenderer.Dispatcher.InvokeAsync(async () =>
            await singleRenderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(singleRenderer);

        Assert.Equal([null], singleReports);
        await singleRenderer.Dispatcher.InvokeAsync(() =>
            singleRenderer.RerenderRootAsync(1, new Dictionary<string, object?>
            {
                [nameof(TreeSelect<Node>.Items)] = treeItems,
                [nameof(TreeSelect<Node>.ValueField)] = valueOf,
                [nameof(TreeSelect<Node>.TextField)] = textOf,
                [nameof(TreeSelect<Node>.ChildrenProperty)] = childrenOf,
                [nameof(TreeSelect<Node>.ShowSearch)] = false,
            }));
        await SettleAsync(singleRenderer);
        Assert.DoesNotContain("Clear selection", singleRenderer.Markup()); // 值清空后按钮消失

        var multiReports = new List<HashSet<string>>();
        var (multiRenderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.Multiple)] = true,
            [nameof(TreeSelect<Node>.Values)] = new HashSet<string>(["r"], StringComparer.Ordinal),
            [nameof(TreeSelect<Node>.ValuesChanged)] = EventCallback.Factory.Create<HashSet<string>>(new object(), v => multiReports.Add(v)),
        });

        await multiRenderer.Dispatcher.InvokeAsync(async () =>
            await multiRenderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(multiRenderer);

        Assert.Single(multiReports);
        Assert.Empty(multiReports[0]); // SelectManyAsync([]) 清空 Values
    }

    // ------------------------------------------------------------------
    // 结构与参数呈现
    // ------------------------------------------------------------------

    [Fact]
    public async Task 隐藏input应呈现Name与当前值()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.Value)] = "r",
            [nameof(TreeSelect<Node>.Name)] = "city",
        });

        var markup = renderer.Markup();
        Assert.Contains("name=\"city\"", markup);
        Assert.Contains("value=\"r\"", markup);
    }

    [Fact]
    public async Task ShowSearch开关应决定弹层内搜索框的渲染()
    {
        var (on, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.ShowSearch)] = true,
        });
        await OpenPopupAsync(on);

        var onMarkup = on.Markup();
        Assert.Contains("placeholder=\"Search…\"", onMarkup);   // DefaultLocalizer 的 TreeSelect.Search
        Assert.Contains("atom-tree-select-search-", onMarkup);  // 搜索框稳定 id（快照沿另行归一化）

        var (off, _, _) = await MountAsync(new Dictionary<string, object?>());
        await OpenPopupAsync(off);
        Assert.DoesNotContain("Search…", off.Markup());
    }

    [Fact]
    public async Task 触发器应携带combobox语义且打开后树携带稳定id()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.AriaLabel)] = "部门选择",
            [nameof(TreeSelect<Node>.Required)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"combobox\"", markup);
        // 基元触发器的显式 aria-haspopup="true" 按位置规则压过包装层透传的 "tree"。
        Assert.Contains("aria-haspopup=\"true\"", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("aria-label=\"部门选择\"", markup);
        // 基元触发器的显式 aria-controls（指向浮层内容）按位置规则覆盖包装层透传的树 id。
        Assert.Contains($"aria-controls=\"{ContextOf(renderer).ContentId}\"", markup);

        await OpenPopupAsync(renderer);
        Assert.Matches(@"atom-tree-select-[0-9a-f]{32}", renderer.Markup()); // 搜索框/树的稳定 atom-tree-select id
    }

    [Fact]
    public void HierarchyIndex应对重复键空白键与超深层级抛中文异常()
    {
        // 内部工具直测（InternalsVisibleTo 先例）：重复键撞唯一性守卫，消息为中文。
        var dup = new Node[] { new("a", "甲", null), new("a", "甲重复", null) };
        var ex = Assert.Throws<ArgumentException>(() => new HierarchyIndex<Node>(dup, valueOf, textOf, childrenOf));
        Assert.Contains("层级键必须唯一", ex.Message);

        // 空白值键走 ArgumentException.ThrowIfNullOrWhiteSpace：空白串抛 ArgumentException。
        Assert.Throws<ArgumentException>(() => new HierarchyIndex<Node>(
            [new Node(" ", "空白键", null)], valueOf, textOf, childrenOf));

        // 深链 300 层：在 256 层阈值被掐断（环同理，重复键先撞也不失守卫语义）。
        var deep = new Node("v300", "深层", null);
        for (var i = 299; i >= 0; i--)
        {
            deep = new Node($"v{i}", "深层", [deep]);
        }
        var depthEx = Assert.Throws<ArgumentException>(() => new HierarchyIndex<Node>([deep], valueOf, textOf, childrenOf));
        Assert.Contains("层级超过 256 层", depthEx.Message);
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭态触发器与隐藏输入结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.Value)] = "r",
            [nameof(TreeSelect<Node>.Name)] = "snap-value",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 打开态树结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(TreeSelect<Node>.ShowSearch)] = true,
            [nameof(TreeSelect<Node>.Value)] = "c1",
        });

        await OpenPopupAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }
}
