using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
// 双层同名家族（照 AccordionTests 先例）：机械平移自
// ui/tests/Rendering/TabsEditingTests.cs（337 行 13 枚），
// 别名钉死到 Components 侧（Tabs*）。
using Tabs = AtomBlazor.Tabs;
using TabsContent = AtomBlazor.TabsContent;
using TabsList = AtomBlazor.TabsList;
using TabsTrigger = AtomBlazor.TabsTrigger;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// <see cref="TabsList"/> 上的关闭、新增、重命名与移动（移植自 TabsEditingTests）。
/// </summary>
/// <remarks>
/// <para>
/// 四者默认全关，且每一项都要开关<em>与</em>回调同时到位才会画出东西。这层配对正是这些测试
/// 花最多时间守护的东西：只有开关没有回调，会画出一个用起来毫无作用的控件，
/// 比什么都不画还要糟。
/// </para>
/// <para>
/// 拖拽重排不在这里覆盖。它的位置要经 JavaScript 模块从 DOM 读取，而测试运行时没有 DOM
/// 可读——那条通路改在浏览器里验证（本仓另以 node 测试守护 tabs-reorder.js 的模块行为）。
/// </para>
/// </remarks>
public class TabsEditingTests
{
    [Fact]
    public async Task 普通标签列表不应画出关闭图标与新增按钮()
    {
        var markup = await Render(_ => { });

        Assert.DoesNotContain("role=\"presentation\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-tabs-id", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 只有关闭开关没有处理器时应什么都不画()
    {
        // 光有开关不够。无处可报的图标是一个死控件。
        var markup = await Render(p => p[nameof(TabsList.Closable)] = true);

        Assert.DoesNotContain("role=\"presentation\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 接了处理器的Closable应在每个标签上画关闭图标()
    {
        var markup = await Render(p =>
        {
            p[nameof(TabsList.Closable)] = true;
            p[nameof(TabsList.OnClose)] = EventCallback.Factory.Create<string>(new object(), _ => { });
        });

        Assert.Equal(2, Occurrences(markup, "role=\"presentation\""));
    }

    [Fact]
    public async Task 单个标签可在列表相反意愿下拒绝被关闭()
    {
        // 最常见的理由是固定标签：什么都能关，除了那个不许关的。
        var markup = await Render(
            p =>
            {
                p[nameof(TabsList.Closable)] = true;
                p[nameof(TabsList.OnClose)] = EventCallback.Factory.Create<string>(new object(), _ => { });
            },
            firstTabClosable: false);

        Assert.Equal(1, Occurrences(markup, "role=\"presentation\""));
    }

    [Fact]
    public async Task 只有新增开关没有处理器时应什么都不画()
    {
        var markup = await Render(p => p[nameof(TabsList.Addable)] = true);

        Assert.DoesNotContain("data-tabs-id", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 新增按钮应坐在tablist之外()
    {
        // tablist 里只许放标签。放在其中的按钮要么对屏幕阅读器谎称自己是标签，
        // 要么挤进谁都不预期的方向键顺序里。
        var markup = await Render(p =>
        {
            p[nameof(TabsList.Addable)] = true;
            p[nameof(TabsList.AddLabel)] = "New sheet";
            p[nameof(TabsList.OnAdd)] = EventCallback.Factory.Create(new object(), () => { });
        });

        var tablistEnd = markup.IndexOf("</div>", markup.IndexOf("role=\"tablist\"", StringComparison.Ordinal), StringComparison.Ordinal);
        var addButton = markup.IndexOf("New sheet", StringComparison.Ordinal);

        Assert.True(addButton > tablistEnd, "新增按钮必须摆在 tablist 收尾之后。");
    }

    [Fact]
    public async Task Delete在聚焦标签上应请求关闭它()
    {
        var closed = new List<string>();

        await RunAsync(async (renderer, _) =>
        {
            await Press(renderer, "Delete");
            Assert.Equal(["one"], closed);
        },
        p =>
        {
            p[nameof(TabsList.Closable)] = true;
            p[nameof(TabsList.OnClose)] = EventCallback.Factory.Create<string>(new object(), closed.Add);
        });
    }

    [Fact]
    public async Task 标签不可关闭时Delete应无事发生()
    {
        var closed = new List<string>();

        await RunAsync(async (renderer, _) =>
        {
            await Press(renderer, "Delete");
            Assert.Empty(closed);
        },
        p => p[nameof(TabsList.OnClose)] = EventCallback.Factory.Create<string>(new object(), closed.Add));
    }

    [Fact]
    public async Task F2应打开编辑器并预填当前标签文本()
    {
        await RunAsync(async (renderer, _) =>
        {
            await Press(renderer, "F2");

            var markup = await renderer.Dispatcher.InvokeAsync(renderer.Markup);
            Assert.Contains("value=\"One\"", markup, StringComparison.Ordinal);
        },
        Renamable);
    }

    [Fact]
    public async Task 提交未改动的编辑不应上报()
    {
        var renames = new List<TabRenameContext>();

        await RunAsync(async (renderer, trigger) =>
        {
            await Press(renderer, "F2");
            await Commit(renderer, trigger, "One");

            Assert.Empty(renames);
        },
        p => Renamable(p, renames));
    }

    [Fact]
    public async Task 提交空编辑应视为取消()
    {
        var renames = new List<TabRenameContext>();

        await RunAsync(async (renderer, trigger) =>
        {
            await Press(renderer, "F2");
            await Commit(renderer, trigger, "   ");

            Assert.Empty(renames);
            Assert.False(ComponentProbe.Field<bool>(trigger, "isRenaming"));
        },
        p => Renamable(p, renames));
    }

    [Fact]
    public async Task 被拒绝的重命名应带已输入内容重开编辑器()
    {
        // 因为一个字符冲突就要重打整个长名字，正是那种让人从此不再重命名的小折磨。
        var renames = new List<TabRenameContext>();

        await RunAsync(async (renderer, trigger) =>
        {
            await Press(renderer, "F2");
            await Commit(renderer, trigger, "Taken");

            Assert.Single(renames);
            Assert.True(ComponentProbe.Field<bool>(trigger, "isRenaming"));
            Assert.Equal("Taken", ComponentProbe.Field<string>(trigger, "renameText"));
        },
        p => Renamable(p, renames, refuse: true));
    }

    [Fact]
    public async Task 被接受的重命名应关闭编辑器并同时上报新旧标签()
    {
        var renames = new List<TabRenameContext>();

        await RunAsync(async (renderer, trigger) =>
        {
            await Press(renderer, "F2");
            await Commit(renderer, trigger, "  Renamed  ");

            var reported = Assert.Single(renames);
            Assert.Equal("one", reported.Value);
            Assert.Equal("One", reported.OldLabel);

            // 已去空格，免得一个游散空格变成名字的一部分。
            Assert.Equal("Renamed", reported.NewLabel);
            Assert.False(ComponentProbe.Field<bool>(trigger, "isRenaming"));
        },
        p => Renamable(p, renames));
    }

    // -------------------------------------------------------------------------------------

    private static void Renamable(Dictionary<string, object?> parameters) =>
        Renamable(parameters, []);

    private static void Renamable(
        Dictionary<string, object?> parameters,
        List<TabRenameContext> sink,
        bool refuse = false)
    {
        parameters[nameof(TabsList.Renamable)] = true;
        parameters[nameof(TabsList.OnRename)] =
            EventCallback.Factory.Create<TabRenameContext>(sink, context =>
            {
                sink.Add(context);
                context.Cancel = refuse;
            });
    }

    /// <summary>在第一个携带 keydown 处理器的元素上按键。</summary>
    private static Task Press(ComponentTestRenderer renderer, string key) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = key }));

    /// <summary>向打开的编辑器里输入文本并按 Enter。</summary>
    private static async Task Commit(ComponentTestRenderer renderer, TabsTrigger trigger, string text)
    {
        await renderer.Dispatcher.InvokeAsync(() => ComponentProbe.SetField(trigger, "renameText", text));
        await Press(renderer, "Enter");
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static async Task<string> Render(
        Action<Dictionary<string, object?>> configure,
        bool? firstTabClosable = null)
    {
        var markup = string.Empty;

        await RunAsync(
            async (renderer, _) => markup = await renderer.Dispatcher.InvokeAsync(renderer.Markup),
            configure,
            firstTabClosable);

        return markup;
    }

    private static async Task RunAsync(
        Func<ComponentTestRenderer, TabsTrigger, Task> body,
        Action<Dictionary<string, object?>> configure,
        bool? firstTabClosable = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddSingleton<ILocalizer, DefaultLocalizer>();

        await using var provider = services.BuildServiceProvider();
        var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var listParameters = new Dictionary<string, object?>();
        configure(listParameters);

        var parameters = new Dictionary<string, object?>
        {
            [nameof(Tabs.DefaultValue)] = "one",
            [nameof(Tabs.ChildContent)] = (RenderFragment)(builder => BuildList(builder, listParameters, firstTabClosable)),
        };

        await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<Tabs>(parameters));

        // .NET 11 渲染器强制 Dispatcher 线程亲和：取组件句柄须走派发线程（照 PrimitiveTabsTests）。
        var trigger = await renderer.Dispatcher.InvokeAsync(renderer.FindComponent<TabsTrigger>);
        await body(renderer, trigger);
    }

    private static void BuildList(
        RenderTreeBuilder builder,
        Dictionary<string, object?> listParameters,
        bool? firstTabClosable)
    {
        builder.OpenComponent<TabsList>(0);

        var sequence = 1;
        foreach (var (name, value) in listParameters)
        {
            builder.AddAttribute(sequence++, name, value);
        }

        builder.AddAttribute(50, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<TabsTrigger>(0);
            inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
            inner.AddAttribute(2, nameof(TabsTrigger.Label), "One");

            if (firstTabClosable is { } closable)
            {
                inner.AddAttribute(3, nameof(TabsTrigger.Closable), (bool?)closable);
            }

            inner.CloseComponent();

            inner.OpenComponent<TabsTrigger>(10);
            inner.AddAttribute(11, nameof(TabsTrigger.Value), "two");
            inner.AddAttribute(12, nameof(TabsTrigger.Label), "Two");
            inner.CloseComponent();
        }));

        builder.CloseComponent();
    }
}
