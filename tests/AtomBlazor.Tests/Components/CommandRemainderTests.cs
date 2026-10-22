using AtomBlazor.Primitives;
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
/// Command 家族余部测试：CommandDialog 的开合双通道与 CloseOnSelect 关闭沿、
/// Shortcut 注册/注销沿；CommandSeparator 的 separator 语义、随过滤显隐与 style 归并；
/// CommandShortcut 的行内语义；CommandVirtualizedGroup&lt;string&gt; 的提供者模式守卫、
/// 过滤计数与显隐沿、ScrollToIndexAsync 钳位与 JS 载荷、聚焦回播滚动（start/nearest）、
/// 悬停移焦不滚动、选中聚焦项双模式与释放摘除。
/// CommandDialog 的对话框内容经门户注册（DialogPortal JS 策略），须以 PortalHost 作根 0
/// 方进入渲染树（照 DateInputTests 双根先例）。
/// 测试面局限：headless 下 Virtualize 拿不到浏览器尺寸，静态 Items 分支只画容器与垫片，
/// 项行与占位皆不绘制；LoadItemsAsync/LoadFromProviderAsync/TryServeFromCache 的批次切片与滚动绘制沿、
/// GetItemCssClassByIndex 的行类沿均未被 JS 桩覆盖，改由 IVirtualizedGroupHandler
/// 状态沿与 JS 调用记录覆盖。快照含 GUID，先经 32 位十六进制正则归一。
/// </summary>
public class CommandRemainderTests
{
    /// <summary>归一化一切 GUID 与对话框实例号 id（vgroup/命令上下文/分组/对话框），使快照跨运行确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(Regex.Replace(markup, "[0-9a-f]{32}", "<id>"), "dialog-\\d+-", "dialog-<n>-");

    /// <summary>构造带基元服务全集（portal/focus/快捷键/本地化）与 interop 记录器的渲染器。</summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAtomBlazorComponents();

        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>在 Dispatcher 上触发一次 DOM 事件。</summary>
    private static Task DispatchAsync(ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attributeName, args, occurrence));

    private static int Count(NoopJavaScript js, string identifier) =>
        js.Invocations.Count(i => i.Identifier == identifier);

    // ------------------------------------------------------------------
    // CommandSeparator
    // ------------------------------------------------------------------

    /// <summary>输入框 + 单项 + 分隔符的最小片段；分隔符经级联上下文订阅显隐。</summary>
    private static RenderFragment SeparatorFamilyFragment(string separatorClass = "my-sep", string? style = null) => builder =>
    {
        var seq = 0;
        builder.OpenComponent<CommandInput>(seq++);
        builder.AddAttribute(seq++, nameof(CommandInput.DisableDebounce), true);
        builder.CloseComponent();
        builder.OpenComponent<CommandSeparator>(seq++);
        builder.AddAttribute(seq++, nameof(CommandSeparator.Class), separatorClass);
        if (style != null)
        {
            builder.AddAttribute(seq++, "style", style);
        }
        builder.AddAttribute(seq++, "data-sep-probe", "on-sep");
        builder.CloseComponent();
        builder.OpenComponent<CommandList>(seq++);
        builder.AddAttribute(seq++, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<CommandItem>(0);
            inner.AddAttribute(1, nameof(CommandItem.Value), "keep-me");
            inner.AddAttribute(2, nameof(CommandItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Keep me")));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js)> MountInCommandAsync(RenderFragment childContent)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<Command>(new Dictionary<string, object?>
        {
            [nameof(Command.ChildContent)] = childContent,
        });
        await FlushAsync(renderer);
        return (renderer, js);
    }

    [Fact]
    public async Task 分隔符落separator语义并同元素承类与透传()
    {
        var (renderer, _) = await MountInCommandAsync(SeparatorFamilyFragment());
        var markup = renderer.Markup();

        // 单一 <div> 同时携带基础类、自定义类、separator 语义与 splat 探针（锚点实探自渲染标记）
        var div = Assert.Single(Regex.Matches(markup, @"<div[^>]*data-sep-probe[^>]*>", RegexOptions.Singleline));
        Assert.Contains("role=\"separator\"", div.Value);
        Assert.Contains("aria-orientation=\"horizontal\"", div.Value);
        Assert.Contains("atom:-mx-1", div.Value);
        Assert.Contains("atom:h-px", div.Value);
        Assert.Contains("atom:bg-border", div.Value);
        Assert.Contains("my-sep", div.Value);
    }

    [Fact]
    public async Task 分隔符随过滤无可见项隐藏并随恢复回归()
    {
        var (renderer, _) = await MountInCommandAsync(SeparatorFamilyFragment(style: "color: red;"));

        // 有可见项：不写 display none
        Assert.DoesNotContain("display: none", renderer.Markup());

        // 全部滤出：隐藏样式与 splat 样式经 InlineStyleMerge 归并共存
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "zzz" });
        await FlushAsync(renderer);
        var hidden = Assert.Single(Regex.Matches(renderer.Markup(), @"<div[^>]*data-sep-probe[^>]*>", RegexOptions.Singleline));
        Assert.Contains("display: none", hidden.Value);
        Assert.Contains("color: red", hidden.Value);

        // 恢复匹配：display none 摘除，splat 样式仍在
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "keep" });
        await FlushAsync(renderer);
        var shown = Assert.Single(Regex.Matches(renderer.Markup(), @"<div[^>]*data-sep-probe[^>]*>", RegexOptions.Singleline));
        Assert.DoesNotContain("display: none", shown.Value);
        Assert.Contains("color: red", shown.Value);
    }

    [Fact]
    public async Task 分隔符默认结构快照应与基线一致()
    {
        var (renderer, _) = await MountInCommandAsync(SeparatorFamilyFragment(separatorClass: string.Empty));
        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // CommandShortcut
    // ------------------------------------------------------------------

    [Fact]
    public async Task 快捷键落行内语义并同元素承类与透传()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<CommandShortcut>(new Dictionary<string, object?>
        {
            [nameof(CommandShortcut.Class)] = "my-shortcut",
            [nameof(CommandShortcut.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "⌘K")),
            ["data-shortcut-probe"] = "on-span",
        });
        await FlushAsync(renderer);

        var span = Assert.Single(Regex.Matches(renderer.Markup(), @"<span[^>]*data-shortcut-probe[^>]*>", RegexOptions.Singleline));
        Assert.Contains("atom:ms-auto", span.Value);
        Assert.Contains("atom:text-xs", span.Value);
        Assert.Contains("atom:tracking-widest", span.Value);
        Assert.Contains("atom:text-muted-foreground", span.Value);
        Assert.Contains("my-shortcut", span.Value);
        Assert.Contains("⌘K", renderer.Markup());
    }

    [Fact]
    public async Task 快捷键默认结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<CommandShortcut>(new Dictionary<string, object?>
        {
            [nameof(CommandShortcut.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "Ctrl+S")),
        });
        await FlushAsync(renderer);
        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // CommandDialog
    // ------------------------------------------------------------------

    /// <summary>对话框内部的标准命令片段：输入框 + 两项。</summary>
    private static readonly string[] dialogItemValues = ["new-file", "open-doc"];

    private static RenderFragment DialogBodyFragment => builder =>
    {
        var seq = 0;
        builder.OpenComponent<CommandInput>(seq++);
        builder.AddAttribute(seq++, nameof(CommandInput.DisableDebounce), true);
        builder.CloseComponent();
        builder.OpenComponent<CommandList>(seq++);
        builder.AddAttribute(seq++, nameof(CommandList.ChildContent), (RenderFragment)(inner =>
        {
            var iseq = 0;
            foreach (var value in dialogItemValues)
            {
                inner.OpenComponent<CommandItem>(iseq++);
                inner.AddAttribute(iseq++, nameof(CommandItem.Value), value);
                inner.AddAttribute(iseq++, nameof(CommandItem.ChildContent), (RenderFragment)(b => b.AddContent(0, value)));
                inner.CloseComponent();
            }
        }));
        builder.CloseComponent();
    };

    private static Dictionary<string, object?> DialogParams(
        List<string> selected, List<bool> openReports, bool open = true, bool closeOnSelect = true,
        string? shortcut = null) => new()
    {
        [nameof(CommandDialog.Open)] = open,
        [nameof(CommandDialog.CloseOnSelect)] = closeOnSelect,
        [nameof(CommandDialog.ChildContent)] = DialogBodyFragment,
        [nameof(CommandDialog.OnValueChange)] = EventCallback.Factory.Create<string>(new object(), selected.Add),
        [nameof(CommandDialog.OpenChanged)] = EventCallback.Factory.Create<bool>(new object(), openReports.Add),
        [nameof(CommandDialog.Shortcut)] = shortcut,
    };

    /// <summary>挂载 PortalHost（根 0）+ CommandDialog（根 1），令门户注册的对话框内容进入渲染树。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js)> MountDialogAsync(
        Dictionary<string, object?> parameters)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        await renderer.MountAsync<CommandDialog>(parameters);
        await FlushAsync(renderer);
        // 强制重渲染门户宿主（照 DateInputTests 先例），让对话框内容确定性入树
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await FlushAsync(renderer);
        return (renderer, js);
    }

    [Fact]
    public async Task 打开态对话框内渲染命令输入与列表()
    {
        var selected = new List<string>();
        var reports = new List<bool>();
        var (renderer, _) = await MountDialogAsync(DialogParams(selected, reports));
        var markup = renderer.Markup();

        Assert.Contains("role=\"dialog\"", markup);
        Assert.Contains("aria-modal=\"true\"", markup);
        Assert.Contains("role=\"listbox\"", markup);
        Assert.Contains("aria-label=\"Command menu\"", markup);
        Assert.Contains("atom:shadow-lg", markup);
        Assert.Contains("new-file", markup);
    }

    [Fact]
    public async Task 选中项回报值并按CloseOnSelect关闭()
    {
        var selected = new List<string>();
        var reports = new List<bool>();
        var (renderer, _) = await MountDialogAsync(DialogParams(selected, reports));

        // 下移聚焦首项、回车选中：OnValueChange 先携值，CloseOnSelect 默认随后上报关闭
        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await FlushAsync(renderer);
        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Enter" });
        await FlushAsync(renderer);

        Assert.Equal(["new-file"], selected);
        Assert.Equal([false], reports);
    }

    [Fact]
    public async Task 关闭CloseOnSelect后选中不播报关闭()
    {
        var selected = new List<string>();
        var reports = new List<bool>();
        var (renderer, _) = await MountDialogAsync(DialogParams(selected, reports, closeOnSelect: false));

        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await FlushAsync(renderer);
        await DispatchAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Enter" });
        await FlushAsync(renderer);

        Assert.Equal(["new-file"], selected);
        Assert.Empty(reports);
    }

    [Fact]
    public async Task Shortcut参数经快捷键服务注册并在释放时注销()
    {
        var selected = new List<string>();
        var reports = new List<bool>();
        var (renderer, js) = CreateRenderer();
        var dialog = await renderer.MountAsync<CommandDialog>(DialogParams(selected, reports, shortcut: "Ctrl+K"));
        await FlushAsync(renderer);

        // 注册沿：keyboardShortcuts.registerShortcut 收到规范化组合键
        var register = Assert.Single(js.Invocations, i => i.Identifier == "keyboardShortcuts.registerShortcut");
        Assert.Equal("ctrl+k", Assert.IsType<string>(register.Args![0]).ToLowerInvariant());

        // 释放沿：DisposeAsync 摘除注册
        await renderer.Dispatcher.InvokeAsync(() => dialog.DisposeAsync().AsTask());
        await FlushAsync(renderer);
        Assert.Contains(js.Invocations, i => i.Identifier == "keyboardShortcuts.unregisterShortcut");
    }

    [Fact]
    public async Task 对话框应具备对话框语义与命令面板结构()
    {
        // 不做整段标记快照：对话框内容经门户渲染进渲染器自建的根，单测渲染器观察不到
        // 这里只钉住与门户策略无关的结构与无障碍契约。
        var selected = new List<string>();
        var reports = new List<bool>();
        var (renderer, _) = await MountDialogAsync(DialogParams(selected, reports));
        var markup = Scrub(renderer.Markup());

        Assert.Contains("role=\"dialog\"", markup);
        Assert.Contains("aria-modal=\"true\"", markup);
        Assert.Contains("aria-labelledby=\"dialog-<n>-title\"", markup);
        Assert.Contains("aria-describedby=\"dialog-<n>-description\"", markup);

        // 命令面板本体：listbox 语义 + 搜索输入
        Assert.Contains("role=\"listbox\"", markup);
        Assert.Contains("aria-label=\"Command menu\"", markup);
        Assert.Contains("<input", markup);
    }

    // ------------------------------------------------------------------
    // CommandVirtualizedGroup<string>
    // ------------------------------------------------------------------

    private static List<string> MakeItems(int count, string prefix = "item") =>
        Enumerable.Range(0, count).Select(i => $"{prefix}-{i}").ToList();

    /// <summary>虚拟化分组挂载：Command 根级联上下文，内含输入框 + 分组；返回选中值收集器。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, CommandContext Context,
        CommandVirtualizedGroup<string> Group, List<string> Selected)> MountGroupAsync(
        IReadOnlyList<string>? items = null,
        bool enableLazyLoading = false,
        int maxDisplayCount = 100,
        CommandItemsProvider<string>? itemsProvider = null,
        Func<string, string>? itemSearchText = null,
        string heading = "Actions")
    {
        var (renderer, js) = CreateRenderer();
        var selected = new List<string>();
        items ??= MakeItems(3);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Command.ChildContent)] = (RenderFragment)(builder =>
            {
                var seq = 0;
                builder.OpenComponent<CommandInput>(seq++);
                builder.AddAttribute(seq++, nameof(CommandInput.DisableDebounce), true);
                builder.CloseComponent();
                builder.OpenComponent<CommandVirtualizedGroup<string>>(seq++);
                builder.AddAttribute(seq++, "Heading", heading);
                builder.AddAttribute(seq++, "Items", items);
                builder.AddAttribute(seq++, "EnableLazyLoading", enableLazyLoading);
                builder.AddAttribute(seq++, "MaxDisplayCount", maxDisplayCount);
                if (itemsProvider != null)
                {
                    builder.AddAttribute(seq++, "ItemsProvider", itemsProvider);
                }
                if (itemSearchText != null)
                {
                    builder.AddAttribute(seq++, "ItemSearchText", itemSearchText);
                }
                builder.AddAttribute(seq++, "ItemValue", (Func<string, string>)(v => v));
                builder.AddAttribute(seq++, "ItemTemplate",
                    (RenderFragment<string>)(v => b => b.AddContent(0, v)));
                builder.AddAttribute(seq++, "data-vgroup-probe", "on-group");
                builder.CloseComponent();
            }),
            [nameof(Command.OnValueChange)] = EventCallback.Factory.Create<string>(new object(), selected.Add),
        };

        await renderer.MountAsync<Command>(parameters);
        await FlushAsync(renderer);
        var group = renderer.FindComponent<CommandVirtualizedGroup<string>>();
        var input = renderer.FindComponent<CommandInput>();
        Assert.NotNull(input.Context);
        return (renderer, js, input.Context!, group, selected);
    }

    [Fact]
    public async Task 提供者模式未开懒加载初始化即抛()
    {
        CommandItemsProvider<string> provider = _ =>
            new ValueTask<CommandItemsProviderResult<string>>(
                new CommandItemsProviderResult<string> { Items = Array.Empty<string>(), TotalItemCount = 0 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => MountGroupAsync(itemsProvider: provider));
    }

    [Fact]
    public async Task 分组标题计数与钳位显示数随行集一致()
    {
        // 5 项、MaxDisplayCount=2：标题显 "(2 of 5)"，可见数钳到 2
        var (renderer, _, _, group, _) = await MountGroupAsync(items: MakeItems(5), maxDisplayCount: 2);
        var markup = renderer.Markup();
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("(2 of 5)", markup);
        // headless：Virtualize 无浏览器尺寸，静态 Items 分支只画容器与垫片——项行与占位皆不绘制
        Assert.DoesNotContain("role=\"option\"", markup);
        Assert.Equal(2, ((IVirtualizedGroupHandler)group).VisibleItemCount);

        // 单一 div 同承基础类与 splat 探针（锚点实探自渲染标记）
        var div = Assert.Single(Regex.Matches(markup, @"<div[^>]*data-vgroup-probe[^>]*>", RegexOptions.Singleline));
        Assert.Contains("atom:p-1", div.Value);
        Assert.Contains("atom:text-foreground", div.Value);
        Assert.Contains("aria-labelledby=\"command-group-", div.Value);
    }

    [Fact]
    public async Task 过滤缩减行集并清空后容器隐去()
    {
        var (renderer, _, context, group, _) = await MountGroupAsync(items: ["alpha", "beta", "gamma"]);
        var handler = (IVirtualizedGroupHandler)group;
        Assert.Equal(3, handler.VisibleItemCount);

        // 部分匹配：三滤一存二
        await renderer.Dispatcher.InvokeAsync(() => context.SetSearchQuery("al"));
        await FlushAsync(renderer);
        Assert.Equal(1, handler.VisibleItemCount);
        Assert.DoesNotContain("display: none", renderer.Markup());

        // 全部滤出：容器 display:none，可见数 0
        await renderer.Dispatcher.InvokeAsync(() => context.SetSearchQuery("zzz"));
        await FlushAsync(renderer);
        Assert.Equal(0, handler.VisibleItemCount);
        var div = Assert.Single(Regex.Matches(renderer.Markup(), @"<div[^>]*data-vgroup-probe[^>]*>", RegexOptions.Singleline));
        Assert.Contains("display: none", div.Value);
    }

    [Fact]
    public async Task ItemSearchText别名参与过滤而值不参与()
    {
        var (renderer, _, context, group, _) = await MountGroupAsync(
            items: ["v-1", "v-2"], itemSearchText: v => v == "v-1" ? "wizard" : "ghost");

        await renderer.Dispatcher.InvokeAsync(() => context.SetSearchQuery("wizard"));
        await FlushAsync(renderer);
        Assert.Equal(1, ((IVirtualizedGroupHandler)group).VisibleItemCount);

        await renderer.Dispatcher.InvokeAsync(() => context.SetSearchQuery("v-1"));
        await FlushAsync(renderer);
        Assert.Equal(0, ((IVirtualizedGroupHandler)group).VisibleItemCount);
    }

    [Fact]
    public async Task ScrollToIndex越界钳末项并发出滚动互操作()
    {
        var (renderer, js, context, group, _) = await MountGroupAsync(items: MakeItems(3));
        var handler = (IVirtualizedGroupHandler)group;
        var groupId = Regex.Match(renderer.Markup(), "vgroup-container-([0-9a-f]{32})").Groups[1].Value;

        // 越界钳到末项 2：JS 载荷为（列表 id、容器 id、钳后索引、32、32）
        await renderer.Dispatcher.InvokeAsync(() => handler.ScrollToIndexAsync(99));
        await FlushAsync(renderer);
        Assert.Equal(2, handler.FocusedIndex);
        var scroll = Assert.Single(js.Invocations, i => i.Identifier == "virtualizationScroll.scrollToVirtualizedIndex");
        Assert.Equal(5, scroll.Args!.Length);
        Assert.Equal(context.ListId, scroll.Args[0]);
        Assert.Equal($"vgroup-container-{groupId}", scroll.Args[1]);
        Assert.Equal(2, scroll.Args[2]);
        Assert.Equal(32, scroll.Args[3]);
        Assert.Equal(32, scroll.Args[4]);

        // 负索引吞下：不新增 JS 调用、焦点不动
        await renderer.Dispatcher.InvokeAsync(() => handler.ScrollToIndexAsync(-1));
        await FlushAsync(renderer);
        Assert.Equal(2, handler.FocusedIndex);
        Assert.Equal(1, Count(js, "virtualizationScroll.scrollToVirtualizedIndex"));

        // 行集为空：钳到 -1 直接返回，同样不发 JS
        var (_, js2, _, group2, _) = await MountGroupAsync(items: Array.Empty<string>());
        await renderer.Dispatcher.InvokeAsync(() => ((IVirtualizedGroupHandler)group2).ScrollToIndexAsync(0));
        Assert.Equal(0, Count(js2, "virtualizationScroll.scrollToVirtualizedIndex"));
    }

    [Fact]
    public async Task 聚焦索引变化回播元素滚动首组首项用start()
    {
        var (renderer, js, _, group, _) = await MountGroupAsync(items: MakeItems(3));
        var handler = (IVirtualizedGroupHandler)group;

        // 键盘聚焦索引 2 → 渲染后 scrollElementIntoView('...-item-2','nearest')
        await renderer.Dispatcher.InvokeAsync(() => handler.FocusedIndex = 2);
        await renderer.Dispatcher.InvokeAsync(() => handler.NotifyStateChanged());
        await FlushAsync(renderer);
        Assert.Equal(1, Count(js, "virtualizationScroll.scrollElementIntoView"));
        var intoView = js.Invocations.Single(i => i.Identifier == "virtualizationScroll.scrollElementIntoView");
        Assert.EndsWith("item-2", Assert.IsType<string>(intoView.Args![0]));
        Assert.Equal("nearest", intoView.Args![1]);

        // 索引归 0：首个有可见项的分组 → 块对齐升级为 'start'（露出分组标题）
        js.Invocations.Clear();
        await renderer.Dispatcher.InvokeAsync(() => handler.FocusedIndex = 0);
        await renderer.Dispatcher.InvokeAsync(() => handler.NotifyStateChanged());
        await FlushAsync(renderer);
        intoView = js.Invocations.Single(i => i.Identifier == "virtualizationScroll.scrollElementIntoView");
        Assert.Equal("start", intoView.Args![1]);
    }

    [Fact]
    public async Task 悬停命中移焦但不清零滚动且越前缀吞下()
    {
        var (renderer, js, context, group, _) = await MountGroupAsync(items: MakeItems(3));
        var handler = (IVirtualizedGroupHandler)group;
        var groupId = Regex.Match(renderer.Markup(), "vgroup-container-([0-9a-f]{32})").Groups[1].Value;

        // 命中本组项 id：移焦 + 上下文认领虚拟化分组焦点；悬停不滚动
        // （TryHoverItem 会经 StateHasChanged 触达渲染树，须在 Dispatcher 上调用）
        Assert.True(await renderer.Dispatcher.InvokeAsync(() => handler.TryHoverItem($"vgroup-{groupId}-item-1")));
        await FlushAsync(renderer);
        Assert.Equal(1, handler.FocusedIndex);
        Assert.Equal(0, context.FocusedVirtualizedGroupIndex);
        Assert.Equal(0, Count(js, "virtualizationScroll.scrollElementIntoView"));

        // 前缀不符 / 尾缀非数字：拒认领
        Assert.False(await renderer.Dispatcher.InvokeAsync(() => handler.TryHoverItem("command-xxx-item-1")));
        Assert.False(await renderer.Dispatcher.InvokeAsync(() => handler.TryHoverItem($"vgroup-{groupId}-item-ab")));
        Assert.Equal(1, handler.FocusedIndex);
    }

    [Fact]
    public async Task 选中聚焦项常规与懒加载两模式皆经值回调()
    {
        // 常规模式：_indexedItems 行集内按焦点索引选中
        var (renderer, _, _, group, selected) = await MountGroupAsync(items: MakeItems(4, "row"));
        var handler = (IVirtualizedGroupHandler)group;
        await renderer.Dispatcher.InvokeAsync(() => handler.FocusedIndex = 2);
        await renderer.Dispatcher.InvokeAsync(() => handler.SelectFocusedItemAsync());
        await FlushAsync(renderer);
        Assert.Equal(["row-2"], selected);

        // 懒加载模式：经 _lazyFilteredSource（无搜索时回落 Items）选中
        var (renderer2, _, _, group2, selected2) = await MountGroupAsync(items: MakeItems(4, "lazy"), enableLazyLoading: true);
        var handler2 = (IVirtualizedGroupHandler)group2;
        Assert.Equal(4, handler2.VisibleItemCount); // 懒加载模式可见数即过滤后总数
        await renderer2.Dispatcher.InvokeAsync(() => handler2.FocusedIndex = 3);
        await renderer2.Dispatcher.InvokeAsync(() => handler2.SelectFocusedItemAsync());
        await FlushAsync(renderer2);
        Assert.Equal(["lazy-3"], selected2);
    }

    [Fact]
    public async Task 释放后从上下文摘组且搜索不再回播()
    {
        var (renderer, _, context, group, _) = await MountGroupAsync(items: MakeItems(3));
        Assert.Contains(context.GetVirtualizedGroups(), g => g == (IVirtualizedGroupHandler)group);

        await renderer.Dispatcher.InvokeAsync(() => ((IDisposable)group).Dispose());
        Assert.Empty(context.GetVirtualizedGroups());

        // 已摘组：搜索变化不再触其状态（OnSearchChanged 已解绑，行集维持释放前快照）
        var before = ((IVirtualizedGroupHandler)group).VisibleItemCount;
        await renderer.Dispatcher.InvokeAsync(() => context.SetSearchQuery("zzz"));
        await FlushAsync(renderer);
        Assert.Equal(before, ((IVirtualizedGroupHandler)group).VisibleItemCount);
    }

    [Fact]
    public async Task 虚拟化分组默认结构快照应与基线一致()
    {
        var (renderer, _, _, _, _) = await MountGroupAsync(items: MakeItems(3), heading: "Actions");
        await Verify(Scrub(renderer.Markup()));
    }
}
