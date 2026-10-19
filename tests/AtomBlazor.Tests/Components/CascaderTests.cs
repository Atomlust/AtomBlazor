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
/// Cascader 测试：层级列渲染（分支带 aria-expanded、子列带 data-cascader-parent、
/// 列组 aria-label 走本地化 Level 键）、选择路径回填与隐藏 input（name/value）、
/// 分支导航不回填而叶子/ChangeOnSelect 回填并随受控沿收起、搜索沿（叶子全路径匹配、
/// 无匹配 role=status 空消息、清空回列视图）、清除按钮回填 null 后随值消失、
/// 禁用参数面强制关闭、cascader.js 初始化调用沿——barrel import 与 cascader.initialize
/// 载荷（ElementReference + atom-cascader GUID id），cascader.connect 以反射置 open 直调，
/// cascader.dispose 经 IAsyncDisposable 直调（headless 下 FocusAsync/真实 DOM 沿不可用，
/// 键盘行为由 node 侧 cascader-module.test.mjs 覆盖）。
/// 快照含 atom-cascader GUID 与 popover-N 短 id，先经正则归一化。
/// </summary>
public class CascaderTests
{
    /// <summary>
    /// 归一化 atom-cascader(-search|-item-…) GUID 稳定 id 与 popover-N 自增短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "atom-cascader(?:-search)?-[0-9a-f]{32}", "atom-cascader-<id>"),
            "popover-[0-9]+", "popover-<n>");

    /// <summary>层级节点模型（值/文本/可选子级）。</summary>
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

    /// <summary>
    /// 构造带全套基元服务的渲染器（门户/定位/焦点/本地化器），与 TreeSelectTests 同构。
    /// </summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
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

    /// <summary>强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树。</summary>
    private static async Task OpenAndSettleAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>反射取 Cascader 内基元 Popover 的上下文（先例：TreeSelectTests 的 ContextOf）。</summary>
    private static AtomBlazor.Primitives.PopoverContext ContextOf(ComponentTestRenderer renderer) =>
        (AtomBlazor.Primitives.PopoverContext)typeof(AtomBlazor.Primitives.PopoverPrimitive)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(renderer.FindComponent<AtomBlazor.Primitives.PopoverPrimitive>())!;

    /// <summary>Cascader 的基础参数字典（四个必需委托参数）。</summary>
    private static Dictionary<string, object?> BaseParams() => new()
    {
        [nameof(Cascader<Node>.Items)] = cascaderItems,
        [nameof(Cascader<Node>.ValueField)] = valueOf,
        [nameof(Cascader<Node>.TextField)] = textOf,
        [nameof(Cascader<Node>.ChildrenProperty)] = childrenOf,
    };

    /// <summary>挂载 PortalHost（根 0）+ Cascader（根 1）；extra 覆盖基础参数。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Cascader<Node> Cascader)> MountAsync(
        Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = BaseParams();
        foreach (var (key, value) in extra)
        {
            dict[key] = value;
        }

        var cascader = await renderer.MountAsync<Cascader<Node>>(dict);
        await SettleAsync(renderer);
        return (renderer, js, cascader);
    }

    /// <summary>点击触发器打开弹层（关闭态下唯一 onclick 携带者）并让门户把内容画进渲染树。</summary>
    private static async Task OpenPopupAsync(ComponentTestRenderer renderer)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await OpenAndSettleAsync(renderer);
    }

    // ------------------------------------------------------------------
    // 层级列渲染与导航
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开后应渲染层级列与分支展开语义()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>());

        await OpenPopupAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("data-cascader-column=\"0\"", markup);
        Assert.Contains("data-cascader-scroll", markup);
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("aria-label=\"Level 1\"", markup); // Localizer["Cascader.Level", 1]
        Assert.Contains("aria-expanded=\"false\"", markup); // 分支 p 未展开
        Assert.Contains("data-cascader-item=\"p\"", markup);
        Assert.Contains("data-cascader-item=\"r\"", markup);
        Assert.Contains("Search paths…", markup); // ShowSearch 默认开
    }

    [Fact]
    public async Task 点击分支应展开子列且默认不回填值()
    {
        var reports = new List<string?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        // 打开态 onclick 次序：门户内容 p(0)、r(1)，随后根 1 的触发器(2)
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("data-cascader-column=\"1\"", markup);
        Assert.Contains("data-cascader-parent=\"p\"", markup);
        Assert.Contains("aria-label=\"Level 2\"", markup);
        Assert.Contains("data-cascader-item=\"c1\"", markup);
        Assert.Empty(reports); // 分支导航不回填（ChangeOnSelect 关）
        Assert.True(ContextOf(renderer).IsOpen);
    }

    [Fact]
    public async Task 点击叶子应回填路径并随受控沿收起弹层()
    {
        var reports = new List<string?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1)); // 独立叶 r
        await SettleAsync(renderer);

        Assert.Equal(["r"], reports);
        Assert.False(ContextOf(renderer).IsOpen);
    }

    [Fact]
    public async Task ChangeOnSelect应回填分支且保持弹层打开继续导航()
    {
        var reports = new List<string?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.ChangeOnSelect)] = true,
            [nameof(Cascader<Node>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        await OpenPopupAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0)); // 分支 p
        await OpenAndSettleAsync(renderer);

        Assert.Equal(["p"], reports);
        Assert.True(ContextOf(renderer).IsOpen);
        Assert.Contains("data-cascader-parent=\"p\"", renderer.Markup()); // 同时继续展开子列
    }

    // ------------------------------------------------------------------
    // 触发器标题与隐藏 input
    // ------------------------------------------------------------------

    [Fact]
    public async Task 预绑定值应在触发器显示完整路径并经隐藏input提交()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.Value)] = "c1",
            [nameof(Cascader<Node>.Name)] = "cat",
        });

        var markup = renderer.Markup();
        Assert.Contains("父节点 / 子节点一", markup);       // PathLabel 全路径回填触发器
        Assert.Contains("name=\"cat\"", markup);
        Assert.Contains("value=\"c1\"", markup);
        Assert.Contains("type=\"hidden\"", markup);
        Assert.DoesNotContain("Select a category…", markup);
    }

    [Fact]
    public async Task 空值应显示占位符与本地化默认标题()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.Placeholder)] = "选个类目",
        });

        Assert.Contains("选个类目", renderer.Markup());

        var (plain, _, _) = await MountAsync(new Dictionary<string, object?>());
        Assert.Contains("Select a category…", plain.Markup()); // DefaultLocalizer Cascader.Placeholder
        Assert.Contains("aria-label=\"Select a category\"", plain.Markup()); // 触发器 aria-label 回落
    }

    // ------------------------------------------------------------------
    // 搜索与清除
    // ------------------------------------------------------------------

    [Fact]
    public async Task 搜索应列出命中全路径且无匹配显形状态消息()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>());

        await OpenPopupAsync(renderer);
        // Immediate 模式的 Input 不挂 Blazor oninput——键入沿由 JS 桥回调 JsOnInput，
        // 无头测试直调该入口模拟（先把输入空白的值 coercing 为 null，绑定处再回空串）。
        var searchInput = renderer.FindComponent<Input>();
        await renderer.Dispatcher.InvokeAsync(async () =>
            await searchInput.JsOnInput("子"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("父节点 / 子节点一", markup); // 命中：全路径含"子"
        Assert.DoesNotContain("data-cascader-parent", markup); // 平铺结果列，不再双列导航

        await renderer.Dispatcher.InvokeAsync(async () =>
            await searchInput.JsOnInput("zzz"));
        await SettleAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains("role=\"status\"", markup);
        Assert.Contains("No matching paths", markup); // Localizer["Cascader.Empty"]

        // 清空搜索回到层级列视图
        await renderer.Dispatcher.InvokeAsync(async () =>
            await searchInput.JsOnInput(string.Empty));
        await SettleAsync(renderer);
        Assert.Contains("data-cascader-column=\"0\"", renderer.Markup());
    }

    [Fact]
    public async Task 清除按钮应回填null并在重渲染后消失()
    {
        var reports = new List<string?>();
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.Value)] = "r",
            [nameof(Cascader<Node>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v)),
        });

        // 关闭态：触发器是 occurrence 0，清除按钮是 occurrence 1
        Assert.Contains("Clear selection", renderer.Markup());
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(renderer);

        Assert.Equal([null], reports);

        var dict = BaseParams();
        dict[nameof(Cascader<Node>.ValueChanged)] =
            EventCallback.Factory.Create<string?>(new object(), v => reports.Add(v));
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await SettleAsync(renderer);
        Assert.DoesNotContain("Clear selection", renderer.Markup()); // 值清空后按钮消失
    }

    [Fact]
    public async Task Clearable为假时不应渲染清除按钮()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.Value)] = "r",
            [nameof(Cascader<Node>.Clearable)] = false,
        });

        Assert.DoesNotContain("Clear selection", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 禁用沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用态应落参数面并拒绝打开弹层()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.Value)] = "c1",
            [nameof(Cascader<Node>.Disabled)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("disabled", markup); // 触发按钮与隐藏 input 双双带 disabled

        // headless 下仍可直发 onclick；SetOpen 的 !Disabled 门把 nextOpen 压回 false
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(renderer);
        Assert.DoesNotContain("data-cascader-item", renderer.Markup());
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // cascader.js interop 沿（barrel 门面 + 反射直调非 UI 事件链）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 首次渲染应导入共享barrel并声明cascader初始化载荷()
    {
        var (renderer, js, _) = await MountAsync(new Dictionary<string, object?>());

 // 门面沿：加载的是 components-core.js barrel，而非 cascader.js 原路径。
        // 基元服务（门户/定位）也各自 import 自己的模块路径，故谓词限定到 barrel 一枚。
        var import = Assert.Single(js.Invocations,
            i => i.Identifier == "import" && ((string?)i.Args?[0])?.Contains("components-core.js") == true);
        Assert.Contains("components-core.js", Assert.IsType<string>(import.Args![0]));

        var (_, args) = Assert.Single(js.Invocations, i => i.Identifier == "cascader.initialize");
        Assert.IsType<ElementReference>(args![0]);
        Assert.Matches(@"^atom-cascader-[0-9a-f]{32}$", Assert.IsType<string>(args[1]));
    }

    [Fact]
    public async Task connect与dispose应携带弹层稳定id()
    {
        var (renderer, js, cascader) = await MountAsync(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        // 反射直调 OnContentReady 链（headless 无浮层显现事件）：置 open 后调 ConnectKeyboardAsync
        typeof(Cascader<Node>).GetField("open", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cascader, true);
        var connect = typeof(Cascader<Node>)
            .GetMethod("ConnectKeyboardAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await renderer.Dispatcher.InvokeAsync(() => (Task)connect.Invoke(cascader, null)!);
        await SettleAsync(renderer);

        var popupId = Assert.IsType<string>(typeof(Cascader<Node>)
            .GetField("popupId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cascader));
        var (_, connectArgs) = Assert.Single(js.Invocations, i => i.Identifier == "cascader.connect");
        Assert.Equal(popupId, Assert.IsType<string>(connectArgs![0]));

        // barrel 单例沿：initialize/connect 共用同一次 components-core.js 导入
        Assert.Single(js.Invocations, i =>
            i.Identifier == "import" && (string?)i.Args?[0] is { } path && path.Contains("components-core.js"));

        await ((IAsyncDisposable)cascader).DisposeAsync();
        var (_, disposeArgs) = Assert.Single(js.Invocations, i => i.Identifier == "cascader.dispose");
        Assert.Equal(popupId, Assert.IsType<string>(disposeArgs![0]));

        // 释放后再连：disposed 门吞下，不再派发 connect
        await renderer.Dispatcher.InvokeAsync(() => (Task)connect.Invoke(cascader, null)!);
        Assert.Single(js.Invocations, i => i.Identifier == "cascader.connect");
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭态触发器与隐藏输入结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.Value)] = "c1",
            [nameof(Cascader<Node>.Name)] = "snap-cat",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 打开态层级与搜索框结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Cascader<Node>.Value)] = "c1",
        });

        await OpenPopupAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }
}
