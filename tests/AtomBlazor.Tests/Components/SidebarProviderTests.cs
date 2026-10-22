using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services; // PortalService / FocusManager DI 依赖（照 ResponsiveNavTests 先例）
using System.Text.Json;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// SidebarProvider 波测试：SidebarContext 状态机直驱（默认态、ToggleSidebar 桌面/移动
/// 双路、Set* 同值静默/异值单发、IsOpen 依视口切换来源、Initialize 三字段收敛、
/// SetCollapsedMode、State 容器同步）与 Provider 经 components-core.js 共享桶的
/// sidebar.initialize 点分装载（cookie 键载荷、受控模式不发键、savedOpen 优先级）、
/// 快捷键开关异值重发 setToggleShortcutEnabled、状态变更 saveSidebarState 持久化、
/// 释放 sidebar.cleanup 带实例 id、预渲染兜底与 JSInvokable 回调落态。
/// 本文件照 ResponsiveNavTests/NavigationMenuTests 密度自撰。
/// </summary>
public class SidebarProviderTests
{
    private static ComponentTestRenderer CreateRenderer(out SidebarJavaScript js)
    {
        var stub = new SidebarJavaScript();
        js = stub;
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton<NavigationManager>(new Nav("https://localhost/", "https://localhost/"));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IFocusManager>(new FocusManager(stub));
        services.AddSingleton<IPositioningService>(new PositioningService(stub)); // Tooltip/Sheet 定位面依赖（照 CalendarTests 先例）
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static ComponentTestRenderer CreateRendererWith(IJSRuntime runtime)
    {
        var services = new ServiceCollection();
        services.AddSingleton(runtime);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton<NavigationManager>(new Nav("https://localhost/", "https://localhost/"));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IFocusManager>(new FocusManager(new NoopJavaScript()));
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private sealed class Nav : NavigationManager
    {
        public Nav(string baseUri, string uri) { Initialize(baseUri, uri); }
        public void Go(string absoluteUri) { Uri = absoluteUri; NotifyLocationChanged(false); }
    }

    private sealed class ContextProbe : ComponentBase
    {
        [CascadingParameter] public SidebarContext? Context { get; set; }
    }

    /// <summary>可编程桩：sidebar.initialize 返回 {instanceId, savedOpen} 的 JsonElement。</summary>
    private sealed class SidebarJavaScript : IJSRuntime, IJSObjectReference
    {
        internal List<(string Identifier, object?[]? Args)> Invocations { get; } = [];
        internal int NextInstanceId = 7;
        internal bool? SavedOpen;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add((identifier, args));
            if (identifier == "sidebar.initialize")
            {
                var saved = SavedOpen.HasValue ? (SavedOpen.Value ? "true" : "false") : "null";
                using var doc = JsonDocument.Parse($"{{\"instanceId\":{NextInstanceId},\"savedOpen\":{saved}}}");
                return ValueTask.FromResult((TValue)(object)doc.RootElement.Clone());
            }

            if (typeof(TValue) == typeof(IJSObjectReference))
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            return ValueTask.FromResult<TValue>(default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new InvalidOperationException("预渲染期间 JS interop 不可用");
    }

    /// <summary>
    /// 挂载 SidebarProvider 并回给四件套：捕获到的上下文、可编程 JS 桩、渲染器、Provider 实例本身
    /// （Provider 是根组件，FindComponent 只下钻子树，故须从 MountAsync 的返回值取）。
    /// savedOpen 用于预置 cookie 恢复沿。
    /// </summary>
    private static async Task<(SidebarContext Context, SidebarJavaScript Js, ComponentTestRenderer Renderer, SidebarProvider Provider)> MountProviderAsync(
        Dictionary<string, object?> parameters, bool? savedOpen = null)
    {
        var renderer = CreateRenderer(out var js);
        js.SavedOpen = savedOpen;
        SidebarContext? captured = null;
        parameters["ChildContent"] = (RenderFragment)(builder =>
        {
            builder.OpenComponent<ContextProbe>(0);
            builder.CloseComponent();
        });
        var provider = await renderer.MountAsync<SidebarProvider>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => captured = renderer.FindComponent<ContextProbe>().Context);
        Assert.NotNull(captured);
        return (captured!, js, renderer, provider);
    }

    // ------------------------------------------------------------------ SidebarContext 状态机

    [Fact]
    public void 上下文默认桌面展开移动关闭左侧默认变体()
    {
        var context = new SidebarContext();
        Assert.True(context.Open);
        Assert.False(context.OpenMobile);
        Assert.False(context.IsMobile);
        Assert.True(context.IsOpen);
        Assert.Equal(SidebarVariant.Sidebar, context.Variant);
        Assert.Equal(SidebarSide.Left, context.Side);
        Assert.Equal(SidebarCollapsedMode.Icon, context.CollapsedMode);
        Assert.Same(context.State, context.State);
    }

    [Fact]
    public void 桌面切换开合发一次事件并可切回()
    {
        var context = new SidebarContext();
        var fires = 0;
        context.StateChanged += (_, _) => fires++;

        context.ToggleSidebar();
        Assert.False(context.Open);
        Assert.Equal(1, fires);

        context.ToggleSidebar();
        Assert.True(context.Open);
        Assert.Equal(2, fires);
    }

    [Fact]
    public void 移动端切换的是OpenMobile桌面态不受影响()
    {
        var context = new SidebarContext();
        context.SetIsMobile(true);
        var openBefore = context.Open;
        context.ToggleSidebar();
        Assert.True(context.OpenMobile);
        Assert.Equal(openBefore, context.Open); // 桌面态原封不动
        Assert.True(context.IsOpen); // IsOpen 已切到移动来源
    }

    [Fact]
    public void 同值Set一律静默异值恰好发一次()
    {
        var context = new SidebarContext();
        var fires = 0;
        context.StateChanged += (_, _) => fires++;

        context.SetOpen(true); // 与默认同值
        context.SetOpenMobile(false);
        context.SetIsMobile(false);
        context.SetVariant(SidebarVariant.Sidebar);
        context.SetSide(SidebarSide.Left);
        Assert.Equal(0, fires);

        context.SetOpen(false);
        context.SetOpenMobile(true);
        context.SetIsMobile(true);
        context.SetVariant(SidebarVariant.Floating);
        context.SetSide(SidebarSide.Right);
 // sender 身份沿：组件侧按 sender 摘钩，事件源必须是上下文本身（并入本测， 预算收口）
        object? sender = null;
        context.StateChanged += (src, _) => sender = src;
        context.ToggleSidebar();
        Assert.Same(context, sender);

        Assert.Equal(6, fires); // 五枚异值 Set 各发一次 + ToggleSidebar（移动沿翻回 OpenMobile）第六发（最小修复：前波误记 5）
        Assert.False(context.State.Open);
        Assert.False(context.State.OpenMobile); // 翻回 false：true→false 正是第六发的来源
        Assert.True(context.State.IsMobile);
        Assert.Equal(SidebarVariant.Floating, context.State.Variant);
        Assert.Equal(SidebarSide.Right, context.State.Side);
    }

    [Fact]
    public void IsOpen依视口在桌面与移动来源间切换()
    {
        var context = new SidebarContext();
        context.SetOpen(true);
        context.SetOpenMobile(false);
        Assert.True(context.IsOpen); // 桌面视口取 Open
        context.SetIsMobile(true);
        Assert.False(context.IsOpen); // 移动视口取 OpenMobile
    }

    [Fact]
    public void 折叠呈现模式同值静默异值发事件()
    {
        var context = new SidebarContext();
        var fires = 0;
        context.StateChanged += (_, _) => fires++;
        context.SetCollapsedMode(SidebarCollapsedMode.Icon); // 默认即 Icon
        Assert.Equal(0, fires);
        context.SetCollapsedMode(SidebarCollapsedMode.Pill);
        Assert.Equal(1, fires);
        Assert.Equal(SidebarCollapsedMode.Pill, context.CollapsedMode);
    }

    [Fact]
    public void Initialize变更收敛为一次事件且null参数不动()
    {
        var context = new SidebarContext();
        var fires = 0;
        context.StateChanged += (_, _) => fires++;
        context.Initialize(open: false, variant: SidebarVariant.Inset, side: SidebarSide.Right);
        Assert.Equal(1, fires); // 三字段变更也只发一次
        Assert.False(context.Open);
        Assert.Equal(SidebarVariant.Inset, context.Variant);
        Assert.Equal(SidebarSide.Right, context.Side);

        context.Initialize(); // 全 null：什么都不动
        Assert.Equal(1, fires);
        context.Initialize(open: false); // 同值：不动
        Assert.Equal(1, fires);
    }

    // ------------------------------------------------------------------ Provider 经共享桶的装载面

    [Fact]
    public async Task 首渲染点分调用sidebarinitialize并携带默认cookie键()
    {
        var (context, js, _, _) = await MountProviderAsync(new());
        var initialize = js.Invocations.Single(i => i.Identifier == "sidebar.initialize");
        Assert.Equal(3, initialize.Args!.Length);
        Assert.NotNull(initialize.Args[0]); // DotNetObjectReference
        Assert.Equal(true, initialize.Args[1]); // EnableToggleShortcut 默认 true
        Assert.Equal("sidebar:state", initialize.Args[2]); // 默认 CookieKey
        Assert.True(context.Open); // savedOpen null → DefaultOpen true
    }

    [Fact]
    public async Task 受控模式不发cookie键且绑定值压过savedOpen()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Open"] = false,
            ["OpenChanged"] = EventCallback.Factory.Create<bool>(this, _ => { }),
        };
        var renderer = CreateRenderer(out var js);
        SidebarContext? captured = null;
        parameters["ChildContent"] = (RenderFragment)(builder =>
        {
            builder.OpenComponent<ContextProbe>(0);
            builder.CloseComponent();
        });
        js.SavedOpen = true;
        await renderer.MountAsync<SidebarProvider>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => captured = renderer.FindComponent<ContextProbe>().Context);

        var initialize = js.Invocations.Single(i => i.Identifier == "sidebar.initialize");
        Assert.Null(initialize.Args![2]); // 受控：cookie 读取关闭
        Assert.False(captured!.Open); // 绑定值 false 压过 savedOpen true
    }

    [Fact]
    public async Task cookie恢复值优先于DefaultOpen()
    {
        var (context, _, _, _) = await MountProviderAsync(new() { ["DefaultOpen"] = false }, savedOpen: true);
        Assert.True(context.Open); // savedOpen=true 覆盖 DefaultOpen=false
    }

    [Fact]
    public async Task cookie恢复false也优先于DefaultOpen()
    {
        var renderer = CreateRenderer(out var js);
        js.SavedOpen = false;
        SidebarContext? captured = null;
        await renderer.MountAsync<SidebarProvider>(new()
        {
            ["DefaultOpen"] = true,
            ["ChildContent"] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<ContextProbe>(0);
                builder.CloseComponent();
            }),
        });
        await renderer.Dispatcher.InvokeAsync(() => captured = renderer.FindComponent<ContextProbe>().Context);
        Assert.False(captured!.Open);
    }

    [Fact]
    public async Task 快捷键参数首渲染后变更重发点分开关()
    {
        var (_, js, renderer, _) = await MountProviderAsync(new());
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?> { ["EnableToggleShortcut"] = false });
        var toggle = js.Invocations.Single(i => i.Identifier == "sidebar.setToggleShortcutEnabled");
        Assert.Equal(7, toggle.Args![0]); // initialize 回给的 instanceId
        Assert.Equal(false, toggle.Args[1]);

        // 同值再渲染不重发（lastToggleShortcutEnabled 门）
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?> { ["EnableToggleShortcut"] = false });
        Assert.Single(js.Invocations, i => i.Identifier == "sidebar.setToggleShortcutEnabled");
    }

    [Fact]
    public async Task 状态变更经saveSidebarState持久化到cookie()
    {
        var (context, js, renderer, _) = await MountProviderAsync(new());
        await renderer.Dispatcher.InvokeAsync(() => context.SetOpen(false));
        for (var i = 0; i < 3; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }

        var save = js.Invocations.Single(i => i.Identifier == "sidebar.saveSidebarState");
        Assert.Equal("sidebar:state", save.Args![0]);
        Assert.Equal(false, save.Args[1]);
    }

    [Fact]
    public async Task 绑定回调只发真移动的那一半()
    {
        var openChanges = 0;
        var mobileChanges = 0;
        var parameters = new Dictionary<string, object?>
        {
            ["OpenChanged"] = EventCallback.Factory.Create<bool>(this, _ => openChanges++),
            ["OpenMobileChanged"] = EventCallback.Factory.Create<bool>(this, _ => mobileChanges++),
        };
        var (context, _, renderer, _) = await MountProviderAsync(parameters);
        await renderer.Dispatcher.InvokeAsync(() => context.SetOpen(false));
        for (var i = 0; i < 3; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }

        Assert.Equal(1, openChanges);
        Assert.Equal(0, mobileChanges); // 桌面切换不得连带触发 OpenMobileChanged
    }

    [Fact]
    public async Task 释放带实例id点分清理且未装载时静默()
    {
        var (_, js, _, provider) = await MountProviderAsync(new());
        await provider.DisposeAsync();
        Assert.Contains(js.Invocations, i => i.Identifier == "sidebar.cleanup" && Equals(7, i.Args![0]));
    }

    [Fact]
    public async Task 预渲染兜底走DefaultOpen且子树照常渲染()
    {
        var renderer = CreateRendererWith(new ThrowingJavaScript());
        SidebarContext? captured = null;
        await renderer.MountAsync<SidebarProvider>(new()
        {
            ["DefaultOpen"] = false,
            ["ChildContent"] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<ContextProbe>(0);
                builder.CloseComponent();
            }),
        });
        await renderer.Dispatcher.InvokeAsync(() => captured = renderer.FindComponent<ContextProbe>().Context);
        Assert.False(captured!.Open); // InvalidOperationException 兜底 → DefaultOpen=false
        Assert.Contains("atom:min-h-screen", renderer.Markup()); // 容器照常渲染（HeightClass 默认串）
    }

    [Fact]
    public async Task JSInvokable回调直接落上下文()
    {
        var (context, _, renderer, provider) = await MountProviderAsync(new());

        // 桌面沿：快捷键切 Open，抽屉值不动
        var openBefore = context.Open;
        await renderer.Dispatcher.InvokeAsync(() => provider.OnToggleShortcut());
        Assert.NotEqual(openBefore, context.Open); // JS 侧快捷键沿直落 ToggleSidebar
        Assert.False(context.OpenMobile);

        // 移动沿：视口置真后，快捷键改切 OpenMobile 而桌面 Open 原样保留
        await renderer.Dispatcher.InvokeAsync(() => provider.OnMobileChange(true));
        Assert.True(context.IsMobile);
        var desktopOpen = context.Open;
        await renderer.Dispatcher.InvokeAsync(() => provider.OnToggleShortcut());
        Assert.True(context.OpenMobile);
        Assert.Equal(desktopOpen, context.Open);
    }
}
