using AtomBlazor.Primitives;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
// 双层同名家族（照 AccordionTests 先例）：本文件测组件包装层，别名钉死到 Components 侧，
// 枚举沿仍来自 Primitives 侧，避免 CS0104 歧义。
using Tabs = AtomBlazor.Tabs;
using PTabsActivationMode = AtomBlazor.Primitives.TabsActivationMode;
using TabsContent = AtomBlazor.TabsContent;
using TabsList = AtomBlazor.TabsList;
using PTabsOrientation = AtomBlazor.Primitives.TabsOrientation;
using TabsTrigger = AtomBlazor.TabsTrigger;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Tabs 组件包装层测试：样式类指纹与 Class 透传、受控/非受控值沿透传、
/// Orientation/ActivationMode 沿透传、responsive 与 reorder 的 interop 载荷与挂摘沿
/// （照既有 JS stub 口径：点分标识符 + Args 记录）、OnTabDropped/键盘移动（locate 桩编排）、
/// selectAll 焦点通路、关闭图标与新增按钮的本地化名称与点击上报、默认结构快照。
/// </summary>
public class TabsTests
{
    /// <summary>归一化上下文自增短 id（tabs-{n} 与 atom-tabs-{n}），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "tabs-\\d+", "tabs-<n>");

    /// <summary>可编排返回值的 JS 桩：按点分标识符查表，import 交出自引用句柄。</summary>
    private sealed class ReorderJavaScript : IJSRuntime, IJSObjectReference
    {
        internal List<(string Identifier, object?[]? Args)> Invocations { get; } = [];
        internal Dictionary<string, object?> Results { get; } = new(StringComparer.Ordinal);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add((identifier, args));

            if (identifier == "import")
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            if (Results.TryGetValue(identifier, out var result))
            {
                return ValueTask.FromResult(result is null ? default! : (TValue)result);
            }

            return ValueTask.FromResult(typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static ComponentTestRenderer CreateRenderer(out ReorderJavaScript js)
    {
        var stub = new ReorderJavaScript();
        js = stub;
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>两个标签（带 Label）+ 两个面板的默认内容树（组件层家族）。</summary>
    private static RenderFragment TwoTabs() => builder =>
    {
        builder.OpenComponent<TabsList>(0);
        builder.AddAttribute(1, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<TabsTrigger>(0);
            inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
            inner.AddAttribute(2, nameof(TabsTrigger.Label), "One");
            inner.AddAttribute(3, nameof(TabsTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "标签一")));
            inner.CloseComponent();

            inner.OpenComponent<TabsTrigger>(4);
            inner.AddAttribute(5, nameof(TabsTrigger.Value), "two");
            inner.AddAttribute(6, nameof(TabsTrigger.Label), "Two");
            inner.AddAttribute(7, nameof(TabsTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "标签二")));
            inner.CloseComponent();
        }));
        builder.CloseComponent();

        builder.AddContent(2, PairContent("one"));
        builder.AddContent(3, PairContent("two"));
    };

    private static RenderFragment PairContent(string value) => builder =>
    {
        builder.OpenComponent<TabsContent>(0);
        builder.AddAttribute(1, nameof(TabsContent.Value), value);
        builder.AddAttribute(2, nameof(TabsContent.ChildContent), (RenderFragment)(b => b.AddContent(0, $"内容-{value}")));
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, ReorderJavaScript Js)> MountAsync(
        Dictionary<string, object?> parameters, RenderFragment? childContent = null)
    {
        var renderer = CreateRenderer(out var js);
        parameters.TryAdd(nameof(Tabs.ChildContent), childContent ?? TwoTabs());
        await renderer.MountAsync<Tabs>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return (renderer, js);
    }

    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attributeName, args, occurrence));

    private static Task ClickAsync(ComponentTestRenderer renderer, int occurrence = 0) =>
        FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence);

    private static Task PressAsync(ComponentTestRenderer renderer, string key, int occurrence = 0, bool ctrl = false) =>
        FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = key, CtrlKey = ctrl }, occurrence);

    /// <summary>构造 TabsList 的私有嵌套记录 TabsList.TabPosition（locate 的 C# 反序列化目标）。</summary>
    private static object MakeTabPosition(int index, int count)
    {
        var type = typeof(TabsList).GetNestedType("TabPosition", BindingFlags.NonPublic)!;
        return Activator.CreateInstance(type, index, count)!;
    }

    // ------------------------------------------------------------------

    [Fact]
    public async Task 组件包装应把自定义类透传到基元根与标签列表()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.Class)] = "my-tabs",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Class), "my-list");
                builder.AddAttribute(2, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        Assert.Contains("class=\"my-tabs\"", markup);
        // TabsList 样式指纹 + Class 合并进 composite class。
        Assert.Contains("atom:inline-flex atom:h-10 atom:items-center", markup);
        Assert.Contains("my-list", markup);
    }

    [Fact]
    public async Task 非受控默认值应激活面板且点击沿上报OnValueChange()
    {
        var reports = new List<string?>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.OnValueChange)] = EventCallback.Factory.Create<string?>(new object(), reports.Add),
        });

        Assert.Contains(">内容-one<", renderer.Markup());

        await ClickAsync(renderer, 1);

        Assert.Contains(">内容-two<", renderer.Markup());
        Assert.Equal(["two"], reports);
    }

    [Fact]
    public async Task 受控模式点击应上报ValueChanged并随上下文翻转沿()
    {
        var reports = new List<string?>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.Value)] = "one",
            [nameof(Tabs.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), reports.Add),
        });

        await ClickAsync(renderer, 1);

        // 上游行为（包装层逐行透传基元）：父级未回写前渲染沿保持新值。
        Assert.Equal(["two"], reports);
        Assert.Contains(">内容-two<", renderer.Markup());
    }

    [Fact]
    public async Task Orientation与ActivationMode应透传到基元并保持手动模式语义()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.Orientation)] = PTabsOrientation.Vertical,
            [nameof(Tabs.ActivationMode)] = PTabsActivationMode.Manual,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-orientation=\"vertical\"", markup);

        // 手动模式：方向键只移动焦点不激活（透传基元语义）。
        await PressAsync(renderer, "ArrowDown");
        Assert.DoesNotContain(">内容-two<", renderer.Markup());
    }

    [Fact]
    public async Task TabsContent包装应带面板样式类与激活内容()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:mt-2 atom:ring-offset-background", markup);
        Assert.Contains(">内容-one<", markup);
    }

    [Fact]
    public async Task TabsTrigger包装应带标签样式指纹与dataTabValue属性()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:inline-flex atom:items-center atom:justify-center atom:whitespace-nowrap", markup);
        Assert.Contains("data-tab-value=\"one\"", markup);
        Assert.Contains("data-tab-value=\"two\"", markup);
    }

    [Fact]
    public async Task Responsive首渲染应以三参调用responsiveTabs初始化并包出测量容器()
    {
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Responsive), true);
                builder.AddAttribute(2, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.AddAttribute(2, nameof(TabsTrigger.Label), "One");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
                builder.AddContent(3, PairContent("one"));
            }),
        });

        var init = Assert.Single(js.Invocations, i => i.Identifier == "responsiveTabs.initialize");
        Assert.NotNull(init.Args);
        Assert.Equal(3, init.Args!.Length);
        // 载荷口径：DotNetObjectReference、8 位实例 id、外层容器引用。
        Assert.IsType<DotNetObjectReference<TabsList>>(init.Args[0]);
        var componentId = Assert.IsType<string>(init.Args[1]);
        Assert.Equal(8, componentId.Length);
        Assert.IsType<ElementReference>(init.Args[2]);

        // 响应式分支的双层包装：外层带 data-tabs-id（atom-tabs-{n}），内层为测量容器。
        var markup = renderer.Markup();
        Assert.Matches("data-tabs-id=\"atom-tabs-\\d+\"", markup);
        Assert.Contains("atom:relative atom:min-w-0 atom:flex-1", markup);
    }

    [Fact]
    public async Task Responsive释放应以实例id调用responsiveTabs清理()
    {
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Responsive), true);
                builder.AddAttribute(2, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        var list = await renderer.Dispatcher.InvokeAsync(renderer.FindComponent<TabsList>);
        await renderer.Dispatcher.InvokeAsync(() => ((IAsyncDisposable)list).DisposeAsync().AsTask());

        var dispose = Assert.Single(js.Invocations, i => i.Identifier == "responsiveTabs.dispose");
        var init = Assert.Single(js.Invocations, i => i.Identifier == "responsiveTabs.initialize");
        Assert.Equal(init.Args![1], dispose.Args![0]);
    }

    [Fact]
    public async Task Reorderable接了移动回调应包装器并初始化tabsReorder()
    {
        var moves = new List<TabMoveContext>();
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Reorderable), true);
                builder.AddAttribute(2, nameof(TabsList.OnMove),
                    EventCallback.Factory.Create<TabMoveContext>(new object(), moves.Add));
                builder.AddAttribute(3, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.AddAttribute(2, nameof(TabsTrigger.Label), "One");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        var init = Assert.Single(js.Invocations, i => i.Identifier == "tabsReorder.initialize");
        Assert.Equal(3, init.Args!.Length);
        Assert.IsType<ElementReference>(init.Args[0]);
        Assert.IsType<DotNetObjectReference<TabsList>>(init.Args[1]);
        Assert.StartsWith("atom-tabs-", (string)init.Args[2]!);

        var markup = renderer.Markup();
        Assert.Matches("data-tabs-id=\"atom-tabs-\\d+\"", markup);
        // 可拖拽沿：draggable 只落在列表允许（或覆写）且有回调的标签上。
        Assert.Contains("draggable=\"true\"", markup);
        Assert.True(moves.Count == 0);
    }

    [Fact]
    public async Task OnTabDropped应上报带旧新下标的移动上下文()
    {
        var moves = new List<TabMoveContext>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Reorderable), true);
                builder.AddAttribute(2, nameof(TabsList.OnMove),
                    EventCallback.Factory.Create<TabMoveContext>(new object(), moves.Add));
                builder.AddAttribute(3, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.CloseComponent();

                    inner.OpenComponent<TabsTrigger>(2);
                    inner.AddAttribute(3, nameof(TabsTrigger.Value), "two");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        var list = await renderer.Dispatcher.InvokeAsync(renderer.FindComponent<TabsList>);
        await renderer.Dispatcher.InvokeAsync(() => list.OnTabDropped("one", 0, 1));

        var reported = Assert.Single(moves);
        Assert.Equal("one", reported.Value);
        Assert.Equal(0, reported.OldIndex);
        Assert.Equal(1, reported.NewIndex);
    }

    [Fact]
    public async Task OnTabDropped的无效与原地落点不应上报()
    {
        var moves = new List<TabMoveContext>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Reorderable), true);
                builder.AddAttribute(2, nameof(TabsList.OnMove),
                    EventCallback.Factory.Create<TabMoveContext>(new object(), moves.Add));
                builder.AddAttribute(3, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        var list = await renderer.Dispatcher.InvokeAsync(renderer.FindComponent<TabsList>);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await list.OnTabDropped("one", 0, 0);   // 原地落点
            await list.OnTabDropped("one", -1, 1);  // 非法起点
            await list.OnTabDropped("one", 1, -1);  // 非法终点
        });

        Assert.Empty(moves);
    }

    [Fact]
    public async Task Ctrl方向键应经locate取位后上报移动()
    {
        var moves = new List<TabMoveContext>();
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Reorderable), true);
                builder.AddAttribute(2, nameof(TabsList.OnMove),
                    EventCallback.Factory.Create<TabMoveContext>(new object(), moves.Add));
                builder.AddAttribute(3, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.AddAttribute(2, nameof(TabsTrigger.Label), "One");
                    inner.CloseComponent();

                    inner.OpenComponent<TabsTrigger>(3);
                    inner.AddAttribute(4, nameof(TabsTrigger.Value), "two");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        // locate 桩：one 当前在 (0/2)。Ctrl+ArrowRight → 0+1=1，合法，上报。
        js.Results["tabsReorder.locate"] = MakeTabPosition(0, 2);

        await PressAsync(renderer, "ArrowRight", ctrl: true);

        Assert.Contains("tabsReorder.locate", js.Invocations.Select(i => i.Identifier));
        var reported = Assert.Single(moves);
        Assert.Equal("one", reported.Value);
        Assert.Equal(0, reported.OldIndex);
        Assert.Equal(1, reported.NewIndex);
    }

    [Fact]
    public async Task Ctrl方向键在列表两端应不回绕不上报()
    {
        var moves = new List<TabMoveContext>();
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Reorderable), true);
                builder.AddAttribute(2, nameof(TabsList.OnMove),
                    EventCallback.Factory.Create<TabMoveContext>(new object(), moves.Add));
                builder.AddAttribute(3, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.AddAttribute(2, nameof(TabsTrigger.Label), "One");
                    inner.CloseComponent();

                    inner.OpenComponent<TabsTrigger>(3);
                    inner.AddAttribute(4, nameof(TabsTrigger.Value), "two");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        // locate 桩：one 在末位 (1/2)。Ctrl+ArrowRight → to=2 ≥ count，钳住不上报。
        js.Results["tabsReorder.locate"] = MakeTabPosition(1, 2);

        await PressAsync(renderer, "ArrowRight", ctrl: true);

        Assert.Contains("tabsReorder.locate", js.Invocations.Select(i => i.Identifier));
        Assert.Empty(moves);
    }

    [Fact]
    public async Task 重命名聚焦通路应经tabsReorder全选既有文本()
    {
        var renames = new List<TabRenameContext>();
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Renamable), true);
                builder.AddAttribute(2, nameof(TabsList.OnRename),
                    EventCallback.Factory.Create<TabRenameContext>(new object(), renames.Add));
                builder.AddAttribute(3, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.AddAttribute(2, nameof(TabsTrigger.Label), "One");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        var list = await renderer.Dispatcher.InvokeAsync(renderer.FindComponent<TabsList>);
        await renderer.Dispatcher.InvokeAsync(() =>
            (Task)ComponentProbe.Call(list, "SelectRenameTextAsync", new ElementReference())!);

        var selectAll = Assert.Single(js.Invocations, i => i.Identifier == "tabsReorder.selectAll");
        Assert.Single(selectAll.Args!);
    }

    [Fact]
    public async Task 关闭图标与新增按钮应带默认本地化名称并点击上报()
    {
        var closed = new List<string>();
        var added = 0;
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<TabsList>(0);
                builder.AddAttribute(1, nameof(TabsList.Closable), true);
                builder.AddAttribute(2, nameof(TabsList.OnClose),
                    EventCallback.Factory.Create<string>(new object(), closed.Add));
                builder.AddAttribute(3, nameof(TabsList.Addable), true);
                builder.AddAttribute(4, nameof(TabsList.OnAdd),
                    EventCallback.Factory.Create(new object(), () => added++));
                builder.AddAttribute(5, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<TabsTrigger>(0);
                    inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
                    inner.AddAttribute(2, nameof(TabsTrigger.Label), "One");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        // DefaultLocalizer 键：Tabs.Close → "Close"，Tabs.Add → "New tab"。
        Assert.Contains("title=\"Close\"", markup);
        Assert.Contains("aria-label=\"New tab\"", markup);

        // onclick 序：标签按钮(0) → 标签内关闭 span(1) → 新增按钮(2)。
        await ClickAsync(renderer, 1);
        Assert.Equal(["one"], closed);

        await ClickAsync(renderer, 2);
        Assert.Equal(1, added);
    }

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
        });

        await Verify(Scrub(renderer.Markup()));
    }
}
