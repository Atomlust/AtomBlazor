using AtomBlazor.Primitives;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Command 核心家族测试（自提前供 Combobox 依赖）：根容器的 listbox
/// 语义与禁用沿、输入框的 combobox 语义与 compositionGuard.attach 抑制声明、防抖开/关两
/// 条输入沿、方向键/Home/End/Enter 的焦点索引状态沿（headless 只断言上下文状态，不断言
/// 纯焦点链）、悬停沿（JsOnHover 经 elementUtils.observeHover 回调）、分组随过滤显隐、
/// 空状态显隐、选中回调双沿（项 OnSelect 与命令 OnValueChange）、外部 FilterFunction
/// 委托、ItemsProvider 委托与请求/结果结构。虚拟化分支（IVirtualizedGroupHandler 参与者）
/// 随 的 CommandVirtualizedGroup 交付，此处仅覆盖上下文纯状态沿。
/// 快照含上下文 GUID、输入与分组 GUID，先经正则归一化。
/// </summary>
public class CommandTests
{
    /// <summary>
    /// 归一化上下文 GUID（command-&lt;hex&gt; 及其 -list/-item-N ）、输入 id
    /// （command-input-&lt;hex&gt;）与分组 id（command-group-&lt;hex&gt;），使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "command-[0-9a-f]{32}", "command-<id>"),
                "command-input-[0-9a-f]{32}", "command-input-<id>"),
            "command-group-[0-9a-f]{32}", "command-group-<id>");

    /// <summary>
    /// 构造带 Command 家族所需最小服务集（IJSRuntime + ILocalizer）的渲染器。
    /// </summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());

        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer)
    {
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>在 Dispatcher 上触发一次 DOM 事件。</summary>
    private static Task DispatchAsync(ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>按标识符取出唯一一次 interop 调用记录。</summary>
    private static (string Identifier, object?[] Args) Single(NoopJavaScript js, string identifier)
    {
        var invocation = Assert.Single(js.Invocations, i => i.Identifier == identifier);
        Assert.NotNull(invocation.Args);
        return (invocation.Identifier, invocation.Args!);
    }

    // ------------------------------------------------------------------
    // 片段构造
    // ------------------------------------------------------------------

    /// <summary>输入框 + 列表 + 两条命令项 + 空状态的标准家族片段。</summary>
    private static RenderFragment StandardFamily(bool disableDebounce = true) => builder =>
    {
        var seq = 0;
        builder.OpenComponent<CommandInput>(seq++);
        builder.AddAttribute(seq++, nameof(CommandInput.DisableDebounce), disableDebounce);
        builder.CloseComponent();
        builder.OpenComponent<CommandList>(seq++);
        builder.AddAttribute(seq++, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
        {
            var iseq = 0;
            inner.OpenComponent<CommandItem>(iseq++);
            inner.AddAttribute(iseq++, nameof(CommandItem.Value), "new-task");
            inner.AddAttribute(iseq++, nameof(CommandItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "New task")));
            inner.CloseComponent();
            inner.OpenComponent<CommandItem>(iseq++);
            inner.AddAttribute(iseq++, nameof(CommandItem.Value), "settings");
            inner.AddAttribute(iseq++, nameof(CommandItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Settings")));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
        builder.OpenComponent<CommandEmpty>(seq++);
        builder.AddAttribute(seq++, nameof(CommandEmpty.ChildContent), (RenderFragment)(b => b.AddContent(0, "No results.")));
        builder.CloseComponent();
    };

    /// <summary>
    /// 挂载 Command 根并返回（渲染器, interop 记录器, 上下文）。上下文经列表的级联参数
    /// 读取——所有测试片段都携带 CommandList。
    /// </summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, CommandContext Context)> MountAsync(
        RenderFragment childContent, Dictionary<string, object?>? extra = null)
    {
        var (renderer, js) = CreateRenderer();
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Command.ChildContent)] = childContent,
        };
        if (extra != null)
        {
            foreach (var pair in extra)
            {
                parameters[pair.Key] = pair.Value;
            }
        }

        await renderer.MountAsync<Command>(parameters);
        await FlushAsync(renderer);
        var list = renderer.FindComponent<CommandList>();
        Assert.NotNull(list.Context);
        return (renderer, js, list.Context!);
    }

    // ------------------------------------------------------------------
    // 根容器与语义结构
    // ------------------------------------------------------------------

    [Fact]
    public async Task 根容器呈现listbox语义且禁用沿落属性与类()
    {
        var (renderer, _, _) = await MountAsync(StandardFamily());
        var markup = renderer.Markup();
        Assert.Contains("role=\"listbox\"", markup);
        Assert.Contains("aria-label=\"Command menu\"", markup);
        Assert.DoesNotContain("data-disabled=\"true\"", markup);

        await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(Command.Disabled)] = true,
                [nameof(Command.ChildContent)] = StandardFamily(),
            }));
        await FlushAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("data-disabled=\"true\"", markup);
        Assert.Contains("atom:pointer-events-none", markup);
    }

    [Fact]
    public async Task 输入框呈现combobox语义并声明compositionguard抑制()
    {
        var (renderer, js, context) = await MountAsync(StandardFamily());
        var markup = renderer.Markup();
        Assert.Contains("role=\"combobox\"", markup);
        Assert.Contains("aria-autocomplete=\"list\"", markup);
        Assert.Contains("aria-expanded=\"true\"", markup);
        Assert.Contains($"aria-controls=\"{context.ListId}\"", markup);
        Assert.Contains("id=\"command-input-", markup);

        // 守卫挂载：参数面为（元素引用、守卫 id、suppress 声明），suppress 数组恰为
        // input/keydown 两事件（组合期间两者都由元素层吞下）
        var (_, args) = Single(js, "compositionGuard.attach");
        Assert.Equal(3, args.Length);
        Assert.IsType<ElementReference>(args[0]);
        Assert.IsType<string>(args[1]);
        var suppress = args[2]!.GetType().GetProperty("suppress")!.GetValue(args[2]);
        Assert.Equal(["input", "keydown"], Assert.IsType<string[]>(suppress));
    }

    [Fact]
    public async Task 输入框释放时摘除compositionguard()
    {
        var (renderer, js, _) = await MountAsync(StandardFamily());
        var (_, attachArgs) = Single(js, "compositionGuard.attach");
        var guardId = Assert.IsType<string>(attachArgs[1]);
        var input = renderer.FindComponent<CommandInput>();

        await renderer.Dispatcher.InvokeAsync(() => ((IDisposable)input).Dispose());
        var (_, detachArgs) = Single(js, "compositionGuard.detach");
        Assert.Equal(guardId, Assert.IsType<string>(detachArgs[0]));
    }

    // ------------------------------------------------------------------
    // 过滤沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭防抖后输入即时过滤项可见性()
    {
        var (renderer, _, context) = await MountAsync(StandardFamily(disableDebounce: true));

        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "sett" });
        await FlushAsync(renderer);

        Assert.Equal("sett", context.SearchQuery);
        var markup = renderer.Markup();
        Assert.Contains("Settings", markup);
        Assert.DoesNotContain("New task", markup);

        // 无匹配时空状态显形，全部项隐去
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "zzz" });
        await FlushAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains("No results.", markup);
        Assert.DoesNotContain("Settings", markup);
    }

    [Fact]
    public async Task 默认防抖沿只更新显示值不写回上下文()
    {
        var (renderer, _, context) = await MountAsync(StandardFamily(disableDebounce: false));

        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "sett" });
        await FlushAsync(renderer);

        // 500ms 防抖尚未到期：上下文查询不变、全部项照旧，输入框自身已回显键入值
        Assert.Equal(string.Empty, context.SearchQuery);
        var markup = renderer.Markup();
        Assert.Contains("value=\"sett\"", markup);
        Assert.Contains("New task", markup);
    }

    [Fact]
    public async Task 外部filterfunction接管过滤判定()
    {
        // 前缀匹配谓词：只认开头，不认中间——区别于默认 Contains 语义
        Func<CommandItemMetadata, string, bool> prefixMatch =
            (item, query) => item.SearchText?.StartsWith(query, StringComparison.OrdinalIgnoreCase) == true;

        var (renderer, _, _) = await MountAsync(
            StandardFamily(),
            new Dictionary<string, object?> { [nameof(Command.FilterFunction)] = prefixMatch });

        // "ett" 位于 "Settings" 中部：默认 Contains 会命中，前缀谓词必须不命中
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "ett" });
        await FlushAsync(renderer);
        Assert.Contains("No results.", renderer.Markup());

        // "set" 命中前缀
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "set" });
        await FlushAsync(renderer);
        Assert.Contains("Settings", renderer.Markup());
    }

    [Fact]
    public async Task 搜索查询经参数下传同步且回播给宿主()
    {
        var (renderer, _, context) = await MountAsync(StandardFamily());

        // 宿主参数驱动：重渲染根，SearchQuery="new" 应下传过滤并回显
        await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(Command.SearchQuery)] = "new",
                [nameof(Command.ChildContent)] = StandardFamily(),
            }));
        await FlushAsync(renderer);
        Assert.Equal("new", context.SearchQuery);
        var markup = renderer.Markup();
        Assert.Contains("New task", markup);
        Assert.DoesNotContain("Settings", markup);

        // 输入沿驱动：键入 "sett" 后 SearchQueryChanged 应带新值回播
        var reported = new List<string>();
        await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(Command.SearchQuery)] = "new",
                [nameof(Command.SearchQueryChanged)] = EventCallback.Factory.Create<string>(this, reported.Add),
                [nameof(Command.ChildContent)] = StandardFamily(),
            }));
        await FlushAsync(renderer);
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "sett" });
        await FlushAsync(renderer);
        Assert.Equal(["sett"], reported);
    }

    // ------------------------------------------------------------------
    // 键盘导航状态沿（经输入框 @onkeydown 派发，断言上下文索引，不依赖真实 DOM 焦点）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 方向键移动焦点索引并环绕且进入键盘导航态()
    {
        var (renderer, _, context) = await MountAsync(StandardFamily());

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await FlushAsync(renderer);
        Assert.Equal(0, context.FocusedIndex);
        Assert.True(context.IsKeyboardNavigating);

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await FlushAsync(renderer);
        Assert.Equal(1, context.FocusedIndex);

        // 末尾再下移环绕回首项；首项上移环绕回末项
        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await FlushAsync(renderer);
        Assert.Equal(0, context.FocusedIndex);

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await FlushAsync(renderer);
        Assert.Equal(1, context.FocusedIndex);

        // 聚焦项在标记里带 data-focused
        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await FlushAsync(renderer);
        Assert.Equal(0, context.FocusedIndex);
        Assert.Contains($"id=\"{context.GetItemId(0)}\"", renderer.Markup());
        Assert.Contains("data-focused=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task home与end直达首末项()
    {
        var (renderer, _, context) = await MountAsync(StandardFamily());

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "End" });
        await FlushAsync(renderer);
        Assert.Equal(1, context.FocusedIndex);

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Home" });
        await FlushAsync(renderer);
        Assert.Equal(0, context.FocusedIndex);
    }

    [Fact]
    public async Task 导航跳过禁用项且回车不选中禁用项()
    {
        var selected = new List<string>();
        RenderFragment fragment = builder =>
        {
            var seq = 0;
            builder.OpenComponent<CommandInput>(seq++);
            builder.AddAttribute(seq++, nameof(CommandInput.DisableDebounce), true);
            builder.CloseComponent();
            builder.OpenComponent<CommandList>(seq++);
            builder.AddAttribute(seq++, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
            {
                var iseq = 0;
                foreach (var (value, disabled) in new[] { ("a", true), ("b", false) })
                {
                    inner.OpenComponent<CommandItem>(iseq++);
                    inner.AddAttribute(iseq++, nameof(CommandItem.Value), value);
                    inner.AddAttribute(iseq++, nameof(CommandItem.Disabled), disabled);
                    inner.CloseComponent();
                }
            }));
            builder.CloseComponent();
        };

        var (renderer, _, context) = await MountAsync(
            fragment,
            new Dictionary<string, object?>
            {
                [nameof(Command.OnValueChange)] = EventCallback.Factory.Create<string>(this, selected.Add),
            });

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await FlushAsync(renderer);
        // 首项禁用：焦点必须落到第二项
        Assert.Equal(1, context.FocusedIndex);

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Enter" });
        await FlushAsync(renderer);
        Assert.Equal(["b"], selected);

        // 上移：越过禁用项后环绕回末项（仍是 b），再回车即二次选中
        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await FlushAsync(renderer);
        Assert.Equal(1, context.FocusedIndex);
        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Enter" });
        await FlushAsync(renderer);
        Assert.Equal(["b", "b"], selected);
    }

    [Fact]
    public async Task 禁用命令吞下键盘导航()
    {
        var (renderer, _, context) = await MountAsync(
            StandardFamily(),
            new Dictionary<string, object?> { [nameof(Command.Disabled)] = true });

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await FlushAsync(renderer);
        Assert.Equal(-1, context.FocusedIndex);
        Assert.False(context.IsKeyboardNavigating);
    }

    // ------------------------------------------------------------------
    // 选中回调双沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 回车选中聚焦项并依次触发项回调与命令回调()
    {
        var order = new List<string>();
        RenderFragment fragment = builder =>
        {
            var seq = 0;
            builder.OpenComponent<CommandInput>(seq++);
            builder.AddAttribute(seq++, nameof(CommandInput.DisableDebounce), true);
            builder.CloseComponent();
            builder.OpenComponent<CommandList>(seq++);
            builder.AddAttribute(seq++, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<CommandItem>(0);
                inner.AddAttribute(1, nameof(CommandItem.Value), "alpha");
                inner.AddAttribute(2, nameof(CommandItem.OnSelect),
                    EventCallback.Factory.Create(this, () => order.Add("item")));
                inner.CloseComponent();
                inner.OpenComponent<CommandItem>(3);
                inner.AddAttribute(4, nameof(CommandItem.Value), "beta");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };

        var (renderer, _, _) = await MountAsync(
            fragment,
            new Dictionary<string, object?>
            {
                [nameof(Command.OnValueChange)] = EventCallback.Factory.Create<string>(this, v => order.Add($"value:{v}")),
            });

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await FlushAsync(renderer);
        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Enter" });
        await FlushAsync(renderer);

        // 项 OnSelect 先行，命令 OnValueChange 随后携项值
        Assert.Equal(["item", "value:alpha"], order);
    }

    [Fact]
    public async Task 点击项经值选中并禁用项吞点击()
    {
        var selected = new List<string>();
        RenderFragment fragment = builder =>
        {
            var seq = 0;
            builder.OpenComponent<CommandInput>(seq++);
            builder.AddAttribute(seq++, nameof(CommandInput.DisableDebounce), true);
            builder.CloseComponent();
            builder.OpenComponent<CommandList>(seq++);
            builder.AddAttribute(seq++, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
            {
                var iseq = 0;
                foreach (var (value, disabled) in new[] { ("pick-me", false), ("no-pick", true) })
                {
                    inner.OpenComponent<CommandItem>(iseq++);
                    inner.AddAttribute(iseq++, nameof(CommandItem.Value), value);
                    inner.AddAttribute(iseq++, nameof(CommandItem.Disabled), disabled);
                    inner.AddAttribute(iseq++, nameof(CommandItem.ChildContent), (RenderFragment)(b => b.AddContent(0, value)));
                    inner.CloseComponent();
                }
            }));
            builder.CloseComponent();
        };

        var (renderer, _, _) = await MountAsync(
            fragment,
            new Dictionary<string, object?>
            {
                [nameof(Command.OnValueChange)] = EventCallback.Factory.Create<string>(this, selected.Add),
            });

        await DispatchAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 0);
        await FlushAsync(renderer);
        Assert.Equal(["pick-me"], selected);

        // 禁用项带 data-disabled="true"，点击被吞
        await DispatchAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await FlushAsync(renderer);
        Assert.Equal(["pick-me"], selected);
        Assert.Contains("data-disabled=\"true\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 列表：结构、observeHover 悬停沿与加载尾部
    // ------------------------------------------------------------------

    [Fact]
    public async Task 列表声明observehover悬停监听且悬停回调移动焦点并退出键盘态()
    {
        var (renderer, js, context) = await MountAsync(StandardFamily());

        // 悬停监听挂在列表元素上，选择器限定 role=option
        var (_, args) = Single(js, "elementUtils.observeHover");
        Assert.Equal("JsOnHover", args[2]);
        Assert.Equal("[role=\"option\"]", args[3]);

        // 先经键盘进入导航态
        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await FlushAsync(renderer);
        Assert.True(context.IsKeyboardNavigating);

        var list = renderer.FindComponent<CommandList>();
        await renderer.Dispatcher.InvokeAsync(() => list.JsOnHover(context.GetItemId(1)));
        await FlushAsync(renderer);
        Assert.Equal(1, context.FocusedIndex);
        // 真实悬停终结键盘模式
        Assert.False(context.IsKeyboardNavigating);

        // 空 id 与越界 id 都被吞
        await renderer.Dispatcher.InvokeAsync(() => list.JsOnHover(string.Empty));
        await FlushAsync(renderer);
        Assert.Equal(1, context.FocusedIndex);
        await renderer.Dispatcher.InvokeAsync(() => list.JsOnHover("command-ffffffffffffffffffffffffffffffff-item-99"));
        await FlushAsync(renderer);
        Assert.Equal(1, context.FocusedIndex);

        // 禁用项的悬停被吞：焦点保持原位
        RenderFragment disabledFragment = builder =>
        {
            builder.OpenComponent<CommandList>(0);
            builder.AddAttribute(1, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<CommandItem>(0);
                inner.AddAttribute(1, nameof(CommandItem.Value), "z");
                inner.AddAttribute(2, nameof(CommandItem.Disabled), true);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };
        var (renderer2, _, context2) = await MountAsync(disabledFragment);
        var list2 = renderer2.FindComponent<CommandList>();
        await renderer2.Dispatcher.InvokeAsync(() => list2.JsOnHover(context2.GetItemId(0)));
        await FlushAsync(renderer2);
        Assert.Equal(-1, context2.FocusedIndex);
    }

    [Fact]
    public async Task 列表jsinvokable入口以反射核验()
    {
        // headless 下 DotNetObjectReference 回调由测试直调，属性面用反射断言
        foreach (var name in new[] { "JsOnHover", "JsOnNearBottom" })
        {
            var method = typeof(CommandList).GetMethod(name);
            Assert.NotNull(method);
            Assert.NotNull(method!.GetCustomAttribute<JSInvokableAttribute>());
        }

        var loaded = new List<int>();
        RenderFragment Fragment(bool loading) => builder =>
        {
            var seq = 0;
            builder.OpenComponent<CommandInput>(seq++);
            builder.CloseComponent();
            builder.OpenComponent<CommandList>(seq++);
            builder.AddAttribute(seq++, nameof(CommandList.IsLoading), loading);
            builder.AddAttribute(seq++, nameof(CommandList.OnLoadMore),
                EventCallback.Factory.Create(this, () => loaded.Add(1)));
            builder.AddAttribute(seq++, nameof(CommandList.EndOfListMessage), "All caught up");
            builder.AddAttribute(seq++, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<CommandItem>(0);
                inner.AddAttribute(1, nameof(CommandItem.Value), "x");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };

        var (renderer, _, _) = await MountAsync(Fragment(loading: true));

        // 加载态：显示指示器、压过尾部消息，且 JsOnNearBottom 被 IsLoading 抑制
        Assert.Contains("role=\"status\"", renderer.Markup());
        Assert.DoesNotContain("All caught up", renderer.Markup());
        var list = renderer.FindComponent<CommandList>();
        await renderer.Dispatcher.InvokeAsync(() => list.JsOnNearBottom());
        await FlushAsync(renderer);
        Assert.Empty(loaded);

        // 加载完成：指示器让位给尾部消息，JsOnNearBottom 触达 OnLoadMore
        await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(Command.ChildContent)] = Fragment(loading: false),
            }));
        await FlushAsync(renderer);
        Assert.DoesNotContain("role=\"status\"", renderer.Markup());
        Assert.Contains("All caught up", renderer.Markup());
        list = renderer.FindComponent<CommandList>();
        await renderer.Dispatcher.InvokeAsync(() => list.JsOnNearBottom());
        await FlushAsync(renderer);
        Assert.Single(loaded);
    }

    // ------------------------------------------------------------------
    // 分组与空状态
    // ------------------------------------------------------------------

    [Fact]
    public async Task 分组携带标题锚定并在成员全部滤出后隐藏()
    {
        RenderFragment fragment = builder =>
        {
            var seq = 0;
            builder.OpenComponent<CommandInput>(seq++);
            builder.AddAttribute(seq++, nameof(CommandInput.DisableDebounce), true);
            builder.CloseComponent();
            builder.OpenComponent<CommandList>(seq++);
            builder.AddAttribute(seq++, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<CommandGroup>(0);
                inner.AddAttribute(1, nameof(CommandGroup.Heading), "Actions");
                inner.AddAttribute(2, nameof(CommandGroup.ChildContent), (RenderFragment)(items =>
                {
                    items.OpenComponent<CommandItem>(0);
                    items.AddAttribute(1, nameof(CommandItem.Value), "run-task");
                    items.AddAttribute(2, nameof(CommandItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Run task")));
                    items.CloseComponent();
                }));
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };

        var (renderer, _, _) = await MountAsync(fragment);
        var markup = renderer.Markup();
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("aria-labelledby=\"command-group-", markup);
        Assert.Contains(">Actions<", markup);

        // 滤出分组唯一成员：分组以 display:none 隐藏（style 经 InlineStyleMerge 归并）
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "nomatch" });
        await FlushAsync(renderer);
        Assert.DoesNotContain(">Run task<", renderer.Markup());
        Assert.Contains("display: none", renderer.Markup());
    }

    [Fact]
    public async Task 空状态初始不显形无匹配后显形()
    {
        var (renderer, _, _) = await MountAsync(StandardFamily());
        // 尚未有任何项被滤出：空状态不显形
        Assert.DoesNotContain("No results.", renderer.Markup());

        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "zzz" });
        await FlushAsync(renderer);
        Assert.Contains("No results.", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 上下文纯状态沿与 ItemsProvider 委托
    // ------------------------------------------------------------------

    [Fact]
    public async Task 上下文id族与项元数据沿()
    {
        var (renderer, _, context) = await MountAsync(StandardFamily());

        Assert.StartsWith("command-", context.Id);
        Assert.Equal($"{context.Id}-input", context.InputId);
        Assert.Equal($"{context.Id}-list", context.ListId);
        Assert.Equal($"{context.Id}-item-0", context.GetItemId(0));

        var first = context.GetItemByIndex(0);
        Assert.NotNull(first);
        Assert.Equal("new-task", first!.Value);
        // SearchText 缺省回落 Value
        Assert.Equal("new-task", first.SearchText);
        Assert.False(first.Disabled);

        // 快照语义：GetFilteredItems 返回调用方自有列表
        var snapshot = context.GetFilteredItems();
        Assert.Equal(2, snapshot.Count);

        // SelectItemByValueAsync 命中禁用项不回调（直接以公开 API 核验守卫）
        var selected = 0;
        RenderFragment fragment = builder =>
        {
            builder.OpenComponent<CommandList>(0);
            builder.AddAttribute(1, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<CommandItem>(0);
                inner.AddAttribute(1, nameof(CommandItem.Value), "ghost");
                inner.AddAttribute(2, nameof(CommandItem.Disabled), true);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };
        var (renderer2, _, context2) = await MountAsync(
            fragment,
            new Dictionary<string, object?>
            {
                [nameof(Command.OnValueChange)] = EventCallback.Factory.Create<string>(this, _ => selected++),
            });
        await renderer2.Dispatcher.InvokeAsync(() => context2.SelectItemByValueAsync("ghost"));
        await FlushAsync(renderer2);
        Assert.Equal(0, selected);

        // SearchText 独立于 Value 参与过滤
        var (renderer3, _, _) = await MountAsync(
            builder =>
            {
                builder.OpenComponent<CommandInput>(0);
                builder.AddAttribute(1, nameof(CommandInput.DisableDebounce), true);
                builder.CloseComponent();
                builder.OpenComponent<CommandList>(2);
                builder.AddAttribute(3, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<CommandItem>(0);
                    inner.AddAttribute(1, nameof(CommandItem.Value), "v-9527");
                    inner.AddAttribute(2, nameof(CommandItem.SearchText), "deployment wizard");
                    inner.AddAttribute(3, nameof(CommandItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Deploy")));
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            });
        await DispatchAsync(renderer3, "oninput", new ChangeEventArgs { Value = "wizard" });
        await FlushAsync(renderer3);
        Assert.Contains("Deploy", renderer3.Markup());
    }

    [Fact]
    public async Task itemsprovider委托与请求结果结构沿()
    {
 // 只交付委托底座：CommandVirtualizedGroup 随到位后接管调用时序
        CommandItemsProvider<string> provider = request =>
            new ValueTask<CommandItemsProviderResult<string>>(
                new CommandItemsProviderResult<string>
                {
                    Items = [$"{request.SearchText}-a", $"{request.SearchText}-b"],
                    TotalItemCount = 42,
                });

        var result = await provider(new CommandItemsProviderRequest
        {
            StartIndex = 0,
            Count = 2,
            SearchText = "q",
            CancellationToken = CancellationToken.None,
        });

        Assert.Equal(["q-a", "q-b"], result.Items);
        Assert.Equal(42, result.TotalItemCount);

        var request = new CommandItemsProviderRequest { StartIndex = 5, Count = 10 };
        Assert.Equal(5, request.StartIndex);
        Assert.Equal(10, request.Count);
        Assert.Null(request.SearchText);
        Assert.False(request.CancellationToken.CanBeCanceled);
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 全家族结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(StandardFamily());
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 过滤后分组显隐与空状态快照应与基线一致()
    {
        RenderFragment fragment = builder =>
        {
            var seq = 0;
            builder.OpenComponent<CommandInput>(seq++);
            builder.AddAttribute(seq++, nameof(CommandInput.DisableDebounce), true);
            builder.CloseComponent();
            builder.OpenComponent<CommandList>(seq++);
            builder.AddAttribute(seq++, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
            {
                var gseq = 0;
                inner.OpenComponent<CommandGroup>(gseq++);
                inner.AddAttribute(gseq++, nameof(CommandGroup.Heading), "Kept");
                inner.AddAttribute(gseq++, nameof(CommandGroup.ChildContent), (RenderFragment)(items =>
                {
                    items.OpenComponent<CommandItem>(0);
                    items.AddAttribute(1, nameof(CommandItem.Value), "keep-me");
                    items.AddAttribute(2, nameof(CommandItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Keep me")));
                    items.CloseComponent();
                }));
                inner.CloseComponent();
                inner.OpenComponent<CommandGroup>(gseq++);
                inner.AddAttribute(gseq++, nameof(CommandGroup.Heading), "Dropped");
                inner.AddAttribute(gseq++, nameof(CommandGroup.ChildContent), (RenderFragment)(items =>
                {
                    items.OpenComponent<CommandItem>(0);
                    items.AddAttribute(1, nameof(CommandItem.Value), "drop-me");
                    items.AddAttribute(2, nameof(CommandItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Drop me")));
                    items.CloseComponent();
                }));
                inner.CloseComponent();
            }));
            builder.CloseComponent();
            builder.OpenComponent<CommandEmpty>(seq++);
            builder.AddAttribute(seq++, nameof(CommandEmpty.ChildContent), (RenderFragment)(b => b.AddContent(0, "No results.")));
            builder.CloseComponent();
        };

        var (renderer, _, _) = await MountAsync(fragment);
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "keep" });
        await FlushAsync(renderer);
        await Verify(Scrub(renderer.Markup()));
    }
}
