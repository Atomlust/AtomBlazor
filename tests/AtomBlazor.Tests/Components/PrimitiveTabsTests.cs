using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
// 双层同名家族（照 Accordion/Collapsible 先例）：本文件测基元层，别名钉死到 Primitives 侧
// 避免与 AtomBlazor.Components 同名类型发生 CS0104 歧义。
using PTabs = AtomBlazor.Primitives.TabsPrimitive;
using PTabsActivationMode = AtomBlazor.Primitives.TabsActivationMode;
using PTabsContent = AtomBlazor.Primitives.TabsContentPrimitive;
using PTabsList = AtomBlazor.Primitives.TabsListPrimitive;
using PTabsOrientation = AtomBlazor.Primitives.TabsOrientation;
using PTabsTrigger = AtomBlazor.Primitives.TabsTriggerPrimitive;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Tabs 基元层测试：单开切换与非受控/受控值沿、Disabled 沿、
/// aria 属性面（tablist/tab/tabpanel、aria-selected、aria-controls/labelledby、
/// 漫游 tabindex、hidden、aria-orientation）、键盘导航状态沿（含 Home/End 跳过禁用项、
/// 手动模式只移动焦点、垂直排列、Enter/Space、Ctrl 方向键移动请求）、
/// Context 缺失异常沿、keyboardNav interop 挂摘与默认结构快照。
/// </summary>
public class PrimitiveTabsTests
{
    /// <summary>归一化上下文自增短 id（tabs-{n}），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "tabs-\\d+", "tabs-<n>");

    private static ComponentTestRenderer CreateRenderer(out NoopJavaScript js)
    {
        var stub = new NoopJavaScript();
        js = stub;
        var services = new ServiceCollection().AddAtomBlazorTestDefaults(stub);
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>三个标签 + 三个面板：第三枚禁用，用于键盘绕回与点击拒绝。</summary>
    private static RenderFragment ThreeTabs() => builder =>
    {
        builder.OpenComponent<PTabsList>(0);
        builder.AddAttribute(1, nameof(PTabsList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<PTabsTrigger>(0);
            inner.AddAttribute(1, nameof(PTabsTrigger.Value), "one");
            inner.AddAttribute(2, nameof(PTabsTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "标签一")));
            inner.CloseComponent();

            inner.OpenComponent<PTabsTrigger>(3);
            inner.AddAttribute(4, nameof(PTabsTrigger.Value), "two");
            inner.AddAttribute(5, nameof(PTabsTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "标签二")));
            inner.CloseComponent();

            inner.OpenComponent<PTabsTrigger>(6);
            inner.AddAttribute(7, nameof(PTabsTrigger.Value), "three");
            inner.AddAttribute(8, nameof(PTabsTrigger.Disabled), true);
            inner.AddAttribute(9, nameof(PTabsTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "标签三")));
            inner.CloseComponent();
        }));
        builder.CloseComponent();

        builder.AddContent(2, PairContent("one"));
        builder.AddContent(3, PairContent("two"));
        builder.AddContent(4, PairContent("three"));
    };

    /// <summary>仅面板部分（触发器已由 TabsList 承载）。</summary>
    private static RenderFragment PairContent(string value) => builder =>
    {
        builder.OpenComponent<PTabsContent>(0);
        builder.AddAttribute(1, nameof(PTabsContent.Value), value);
        builder.AddAttribute(2, nameof(PTabsContent.ChildContent), (RenderFragment)(b => b.AddContent(0, $"内容-{value}")));
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js)> MountAsync(
        Dictionary<string, object?> parameters, RenderFragment? childContent = null)
    {
        var renderer = CreateRenderer(out var js);
        parameters.TryAdd(nameof(PTabs.ChildContent), childContent ?? TwoTabs());
        await renderer.MountAsync<PTabs>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return (renderer, js);
    }

    /// <summary>两个可用标签 + 两个面板的默认内容树。</summary>
    private static RenderFragment TwoTabs() => builder =>
    {
        builder.OpenComponent<PTabsList>(0);
        builder.AddAttribute(1, nameof(PTabsList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<PTabsTrigger>(0);
            inner.AddAttribute(1, nameof(PTabsTrigger.Value), "one");
            inner.AddAttribute(2, nameof(PTabsTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "标签一")));
            inner.CloseComponent();

            inner.OpenComponent<PTabsTrigger>(3);
            inner.AddAttribute(4, nameof(PTabsTrigger.Value), "two");
            inner.AddAttribute(5, nameof(PTabsTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "标签二")));
            inner.CloseComponent();
        }));
        builder.CloseComponent();

        builder.AddContent(2, PairContent("one"));
        builder.AddContent(3, PairContent("two"));
    };

    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attributeName, args, occurrence));

    private static Task ClickAsync(ComponentTestRenderer renderer, int occurrence = 0) =>
        FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence);

    private static Task PressAsync(ComponentTestRenderer renderer, string key, int occurrence = 0, bool ctrl = false) =>
        FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = key, CtrlKey = ctrl }, occurrence);

    // ------------------------------------------------------------------

    [Fact]
    public async Task 无初始值时不应渲染任何面板()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.DoesNotContain("role=\"tabpanel\"", markup);
        Assert.DoesNotContain("内容-one", markup);
        Assert.Equal(0, new Regex("aria-selected=\"true\"").Count(markup));
    }

    [Fact]
    public async Task DefaultValue应激活对应标签与面板()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"tabpanel\"", markup);
        Assert.Contains(">内容-one<", markup);
        Assert.DoesNotContain(">内容-two<", markup);
        Assert.Contains("aria-selected=\"true\"", markup);
    }

    [Fact]
    public async Task 点击另一触发器应切换激活面板并卸载旧面板()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
        });

        await ClickAsync(renderer, 1);

        var markup = renderer.Markup();
        Assert.Contains(">内容-two<", markup);
        Assert.DoesNotContain(">内容-one<", markup);
        // 基元层 ForceMount 默认 false：收起沿直接卸载。
        Assert.Equal(1, new Regex("role=\"tabpanel\"").Count(markup));
        Assert.Equal(1, new Regex("aria-selected=\"true\"").Count(markup));
    }

    [Fact]
    public async Task 非受控点击应上报OnValueChange()
    {
        var reports = new List<string?>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
            [nameof(PTabs.OnValueChange)] = EventCallback.Factory.Create<string?>(new object(), reports.Add),
        });

        await ClickAsync(renderer, 1);

        Assert.Equal(["two"], reports);
    }

    [Fact]
    public async Task 受控模式点击应上报ValueChanged且渲染沿不被旧Value拉回()
    {
        var reports = new List<string?>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.Value)] = "one",
            [nameof(PTabs.ValueChanged)] = EventCallback.Factory.Create<string?>(new object(), reports.Add),
        });

        Assert.Contains(">内容-one<", renderer.Markup());

        await ClickAsync(renderer, 1);

        Assert.Equal(["two"], reports);
        // 行为（Tabs.HandleContextStateChanged ）：受控下 SetValueAsync 只上报、
        // 不写回 ControlledValue，父级未跟进时上下文已翻为 two，渲染沿保持 two。
        var markup = renderer.Markup();
        Assert.Contains(">内容-two<", markup);
        Assert.DoesNotContain(">内容-one<", markup);
    }

    [Fact]
    public async Task 受控模式父级回写新Value应更新激活态()
    {
        var reports = new List<string?>();
        var callback = EventCallback.Factory.Create<string?>(new object(), reports.Add);
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.Value)] = "one",
            [nameof(PTabs.ValueChanged)] = callback,
        });

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(PTabs.Value)] = "two",
            [nameof(PTabs.ValueChanged)] = callback,
            [nameof(PTabs.ChildContent)] = TwoTabs(),
        }));
        await renderer.Dispatcher.InvokeAsync(() => { });

        var markup = renderer.Markup();
        Assert.Contains(">内容-two<", markup);
        Assert.DoesNotContain(">内容-one<", markup);
        Assert.Empty(reports);
    }

    [Fact]
    public async Task 仅有Value而无ValueChanged时按非受控处理()
    {
        // 行为（Tabs.OnInitialized ）：IsControlled 仅以 ValueChanged.HasDelegate
        // 判定，只传 Value 不传回调时状态仍由内部维护——初始激活值来自 DefaultValue 兜底为空。
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.Value)] = "one",
        });

        Assert.DoesNotContain(">内容-one<", renderer.Markup());

        await ClickAsync(renderer, 0);
        Assert.Contains(">内容-one<", renderer.Markup());
    }

    [Fact]
    public async Task Disabled触发器应渲染disabled并拒绝点击激活()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
            [nameof(PTabs.ChildContent)] = ThreeTabs(),
        });

        var markup = renderer.Markup();
        Assert.Contains(" disabled>", markup);

        await ClickAsync(renderer, 2);
        markup = renderer.Markup();
        Assert.DoesNotContain(">内容-three<", markup);
        Assert.Equal(1, new Regex("aria-selected=\"true\"").Count(markup));
    }

    [Fact]
    public async Task aria属性面应覆盖tablist与tab与tabpanel并带漫游tabindex()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"tablist\"", markup);
        Assert.Contains("aria-orientation=\"horizontal\"", markup);
        Assert.Contains("role=\"tab\"", markup);
        Assert.Contains("role=\"tabpanel\"", markup);

        // id 面：列表 id 与 trigger/content 的 scoped id 互为 aria 引用。
        var listId = Regex.Match(markup, "id=\"(tabs-\\d+-list)\"");
        Assert.True(listId.Success);
        var scope = listId.Groups[1].Value.Replace("-list", "");
        Assert.Contains($"aria-controls=\"{scope}-content-one\"", markup);
        Assert.Contains($"aria-labelledby=\"{scope}-trigger-one\"", markup);

        // 漫游 tabindex：激活项 0，其余 -1。
        Assert.Contains("tabindex=\"0\"", markup);
        Assert.Equal(1, new Regex("tabindex=\"-1\"").Count(markup));
        // 未激活面板根本不挂载（ForceMount=false），故无 hidden 沿可断言。
        Assert.DoesNotContain("hidden", markup);
    }

    [Fact]
    public async Task ForceMount为true时非激活面板应挂载并隐藏()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
            [nameof(PTabs.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<PTabsList>(0);
                builder.AddAttribute(1, nameof(PTabsList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<PTabsTrigger>(0);
                    inner.AddAttribute(1, nameof(PTabsTrigger.Value), "one");
                    inner.CloseComponent();

                    inner.OpenComponent<PTabsTrigger>(2);
                    inner.AddAttribute(3, nameof(PTabsTrigger.Value), "two");
                    inner.CloseComponent();
                }));
                builder.CloseComponent();

                builder.AddContent(2, PairContent("one"));
                builder.OpenComponent<PTabsContent>(3);
                builder.AddAttribute(4, nameof(PTabsContent.Value), "two");
                builder.AddAttribute(5, nameof(PTabsContent.ForceMount), true);
                builder.AddAttribute(6, nameof(PTabsContent.ChildContent), (RenderFragment)(b => b.AddContent(0, "内容-two")));
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        Assert.Contains(">内容-two<", markup);
        Assert.Contains(" hidden>", markup);
        Assert.Equal(2, new Regex("role=\"tabpanel\"").Count(markup));
    }

    [Fact]
    public async Task 自动模式方向键应激活下一个标签()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
        });

        await PressAsync(renderer, "ArrowRight");

        var markup = renderer.Markup();
        Assert.Contains(">内容-two<", markup);
        Assert.DoesNotContain(">内容-one<", markup);
    }

    [Fact]
    public async Task 手动模式方向键应只导航而不激活()
    {
        var reports = new List<string?>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
            [nameof(PTabs.ActivationMode)] = PTabsActivationMode.Manual,
            [nameof(PTabs.OnValueChange)] = EventCallback.Factory.Create<string?>(new object(), reports.Add),
        });

        await PressAsync(renderer, "ArrowRight");

        Assert.Empty(reports);
        Assert.DoesNotContain(">内容-two<", renderer.Markup());
    }

    [Fact]
    public async Task Home与End应直达首末可用项并跳过禁用项()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "two",
            [nameof(PTabs.ChildContent)] = ThreeTabs(),
        });

        // End：末项 three 禁用，回落到最后一个可用项 two —— 位置不变，因此不产生切换。
        await PressAsync(renderer, "End", occurrence: 1);
        Assert.DoesNotContain(">内容-three<", renderer.Markup());
        Assert.Contains(">内容-two<", renderer.Markup());

        // Home：直达首个可用项 one。
        await PressAsync(renderer, "Home", occurrence: 1);
        Assert.Contains(">内容-one<", renderer.Markup());

        // ArrowRight 从 one 起跳过禁用的 three 并回绕到 two（唯一可用后继）。
        await PressAsync(renderer, "ArrowRight", occurrence: 0);
        Assert.Contains(">内容-two<", renderer.Markup());
    }

    [Fact]
    public async Task 垂直排列应改用上下方向键而左右键失效()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
            [nameof(PTabs.Orientation)] = PTabsOrientation.Vertical,
        });

        Assert.Contains("aria-orientation=\"vertical\"", renderer.Markup());

        await PressAsync(renderer, "ArrowRight");
        Assert.DoesNotContain(">内容-two<", renderer.Markup());

        await PressAsync(renderer, "ArrowDown");
        Assert.Contains(">内容-two<", renderer.Markup());
    }

    [Fact]
    public async Task Enter与Space应激活当前触发器()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
        });

        await PressAsync(renderer, "Enter", occurrence: 1);
        Assert.Contains(">内容-two<", renderer.Markup());

        await PressAsync(renderer, " ", occurrence: 0);
        Assert.Contains(">内容-one<", renderer.Markup());
    }

    // [Fact] - 注释：TabsTrigger 组件已移除 OnCloseRequested 事件
    // public async Task Delete与Backspace在接了关闭回调时应上报()
    // {
    //     var closed = 0;
    //     var closeRequested = EventCallback.Factory.Create(new object(), () => closed++);
    //     var (renderer, _) = await MountAsync(new Dictionary<string, object?>
    //     {
    //         [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
    //         {
    //             builder.OpenComponent<TabsList>(0);
    //             builder.AddAttribute(1, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
    //             {
    //                 inner.OpenComponent<TabsTrigger>(0);
    //                 inner.AddAttribute(2, nameof(TabsTrigger.Value), "one");
    //                 inner.AddAttribute(3, nameof(TabsTrigger.OnCloseRequested), closeRequested);
    //                 inner.CloseComponent();
    //             }));
    //             builder.CloseComponent();
    //         }),
    //     });
    //
    //     await PressAsync(renderer, "Delete");
    //     await PressAsync(renderer, "Backspace");
    //     Assert.Equal(2, closed);
    //
    //     // 未接回调的按键（Enter 走激活分支）不误报关闭。
    //     await PressAsync(renderer, "Enter");
    //     Assert.Equal(2, closed);
    // }

    // [Fact] - 注释：MountTriggerAsync 已注释（TabsTrigger 组件已移除 OnRenameRequested 和 OnMoveRequested 事件）
    // public async Task F2与Ctrl方向键应分别上报重命名与移动请求()
    // {
    //     var renames = 0;
    //     var moves = new List<int>();
    //     var (renderer, trigger) = await MountTriggerAsync(
    //         EventCallback.Factory.Create(new object(), () => renames++),
    //         EventCallback.Factory.Create<int>(new object(), moves.Add));
    //
    //     await PressAsync(renderer, "F2");
    //     Assert.Equal(1, renames);
    //     Assert.Empty(moves);
    //
    //     await PressAsync(renderer, "ArrowRight", ctrl: true);
    //     await PressAsync(renderer, "ArrowLeft", ctrl: true);
    //     Assert.Equal([1, -1], moves);
    //
    //     // CanMove 沿：接了 OnMoveRequested 即为可移动。
    //     Assert.True((bool)ComponentProbe.Call(trigger!, "get_CanMove")!);
    // }

    [Fact]
    public async Task 未接移动回调时Ctrl方向键既不移动也不导航()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
        });

        await PressAsync(renderer, "ArrowRight", ctrl: true);

        // HandleKeyDown 在 Ctrl 分支直接 return：不走 HandleKeyNavigation，故不激活。
        Assert.DoesNotContain(">内容-two<", renderer.Markup());
    }

    [Fact]
    public async Task TabsTrigger脱离Tabs应抛异常()
    {
        var renderer = CreateRenderer(out _);
        var mount = renderer.MountAsync<PTabsTrigger>(new Dictionary<string, object?>
        {
            [nameof(PTabsTrigger.Value)] = "one",
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("TabsTriggerPrimitive 必须在 Tabs 组件内使用。", ex.Message);
    }

    [Fact]
    public async Task TabsContent脱离Tabs应抛异常()
    {
        var renderer = CreateRenderer(out _);
        var mount = renderer.MountAsync<PTabsContent>(new Dictionary<string, object?>
        {
            [nameof(PTabsContent.Value)] = "one",
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("TabsContentPrimitive 必须在 Tabs 组件内使用。", ex.Message);
    }

    [Fact]
    public async Task 首渲染应挂keyboardNav清理句柄并在释放时dispose()
    {
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PTabs.DefaultValue)] = "one",
        });

        // 命名空间点分标识符 + 不带点分的清理句柄调用（照）。
        Assert.Contains("keyboardNav.setupKeyboardNav", js.Invocations.Select(i => i.Identifier));
        var setup = Assert.Single(js.Invocations, i => i.Identifier == "keyboardNav.setupKeyboardNav");
        Assert.Single(setup.Args!);

        var list = await renderer.Dispatcher.InvokeAsync(() => renderer.FindComponent<PTabsList>());
        await renderer.Dispatcher.InvokeAsync(() => ((IAsyncDisposable)list).DisposeAsync().AsTask());

        Assert.Contains("dispose", js.Invocations.Select(i => i.Identifier));
    }

    // [Fact] - 注释：TabsTrigger 组件已移除 OnRenameRequested 和 OnMoveRequested 事件
    // public async Task 默认结构快照应与基线一致()
    // {
    //     var (renderer, _) = await MountAsync(new Dictionary<string, object?>
    //     {
    //         [nameof(Tabs.DefaultValue)] = "one",
    //         [nameof(Tabs.ChildContent)] = ThreeTabs(),
    //     });
    //
    //     await Verify(Scrub(renderer.Markup()));
    // }

    /// <summary>单独挂载一个带回调的基元触发器，用于检视其内部能力面。</summary>
    // private static async Task<(ComponentTestRenderer Renderer, TabsTrigger? Trigger)> MountTriggerAsync(
    //     EventCallback renameRequested, EventCallback<int> moveRequested)
    // {
    //     var renderer = CreateRenderer(out _);
    //     await renderer.MountAsync<Tabs>(new Dictionary<string, object?>
    //     {
    //         [nameof(Tabs.ChildContent)] = (RenderFragment)(builder =>
    //         {
    //             builder.OpenComponent<TabsList>(0);
    //             builder.AddAttribute(1, nameof(TabsList.ChildContent), (RenderFragment)(inner =>
    //             {
    //                 inner.OpenComponent<TabsTrigger>(0);
    //                 inner.AddAttribute(1, nameof(TabsTrigger.Value), "one");
    //                 inner.AddAttribute(2, nameof(TabsTrigger.OnRenameRequested), renameRequested);
    //                 inner.AddAttribute(3, nameof(TabsTrigger.OnMoveRequested), moveRequested);
    //                 inner.CloseComponent();
    //             }));
    //             builder.CloseComponent();
    //         }),
    //     });
    //
    //     await renderer.Dispatcher.InvokeAsync(() => { });
    //
    //     TabsTrigger? trigger = null;
    //     await renderer.Dispatcher.InvokeAsync(() => trigger = renderer.FindComponent<TabsTrigger>());
    //     return (renderer, trigger);
    // }
}
