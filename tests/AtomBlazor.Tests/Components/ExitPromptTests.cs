using AtomBlazor.Primitives;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ExitPrompt 测试：空闲零渲染与零 interop、Enabled 沿驱动 exitPrompt.arm/disarm
/// 且载荷为实例 id（atom-exit-prompt-GUID）、armedInBrowser 门控下状态不变则保留监听器
/// （不重复 arm）、应用内导航被 PreventNavigation 拦下并弹出 AlertDialog（默认本地化四文案、
/// Title/Message/StayLabel/LeaveLabel 覆写沿）、pending 已存在时二次导航不重复问询且不拦、
/// Stay（经 OpenChanged(false)）摘除对话框并触发 OnStayed、Leave 先落守护再重发导航并触发
/// OnLeft 与 disarm、DisposeAsync 仅在 armed 时补发 disarm。
/// AlertDialog 内容经 DialogPortal 注册，须挂 PortalHost 作根 0（照 双根先例）。
/// 测试面局限：headless 的 NavigationManager 双件不复现宿主端的 LocationChanging 管线
/// （基类把 NavigateToCore 留给宿主实现），拦截沿以反射直调组件注册的私有处理器
/// HandleLocationChangingAsync 驱动（照 CalendarTests"属性沿直调"口径），真实宿主走同一处理器
/// beforeunload 拦截属 JS 侧行为，由 node 侧 exit-prompt-module.test.mjs 承载。
/// 快照含实例 GUID 与对话框自增 id，先经正则归一。
/// </summary>
public class ExitPromptTests
{
    /// <summary>归一化实例 GUID 与对话框自增短 id，使快照跨运行确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "atom-exit-prompt-[0-9a-f]{32}", "atom-exit-prompt-<id>"),
            "dialog-\\d+", "dialog-<n>");

    private static readonly MethodInfo handleLocationChangingMethod =
        typeof(ExitPrompt).GetMethod("HandleLocationChangingAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>构造框架内部实例化上下文的 LocationChangingContext 并写入目标地址。</summary>
    private static LocationChangingContext NewContext(string targetLocation)
    {
        var context = (LocationChangingContext)Activator.CreateInstance(
            typeof(LocationChangingContext), nonPublic: true)!;
        typeof(LocationChangingContext)
            .GetProperty(nameof(LocationChangingContext.TargetLocation))!
            .SetValue(context, targetLocation);
        return context;
    }

    /// <summary>问询计数盒：OnPrompted 回调自增，供二次导航直通面复用。</summary>
    private sealed class PromptCounter
    {
        public int Count;
    }

    /// <summary>最小可驱动的 NavigationManager（承 MotionTests 先例）：
    /// SetNavigationLockState 接住导航锁请求（.NET 11 注册位置变更处理器的硬性要求）
    /// NavigateToCore 直落 Uri 更新——Leave 重发放行面与 URL 断言用。</summary>
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string baseUri, string uri)
        {
            Initialize(baseUri, uri);
        }

        public void SimulateNavigation(string absoluteUri)
        {
            Uri = absoluteUri;
            NotifyLocationChanged(false);
        }

        protected override void SetNavigationLockState(bool value)
        {
            // headless 无历史锁概念：接住即可
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).ToString();
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    private static (ComponentTestRenderer Renderer, NoopJavaScript Js, TestNavigationManager Navigation) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var navigation = new TestNavigationManager("https://localhost/", "https://localhost/");
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<NavigationManager>(navigation);
        services.AddAtomBlazorComponents();
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js, navigation);
    }

    /// <summary>挂载 PortalHost（根 0）+ ExitPrompt（根 1）；返回根组件实例（根组件不在
 /// 任何组件帧之下，FindComponent 探不到根本身，照台账先例经挂载返回值取用）。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, TestNavigationManager Navigation, ExitPrompt Component)> MountAsync(
        Dictionary<string, object?> parameters)
    {
        var (renderer, js, navigation) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var component = await renderer.MountAsync<ExitPrompt>(parameters);
        await FlushAsync(renderer);
        return (renderer, js, navigation, component);
    }

    private static async Task FlushAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    private static Task RerenderPortalAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));

    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attributeName, args, occurrence));

    private static int Count(NoopJavaScript js, string identifier) =>
        js.Invocations.Count(i => i.Identifier == identifier);

    /// <summary>把一次应用内导航送进组件注册的拦截处理器，返回其是否被拦（IsAborted）。</summary>
    private static async Task<bool> HoldAsync(
        ComponentTestRenderer renderer, ExitPrompt component, string absoluteTarget)
    {
        var context = NewContext(absoluteTarget);
        var aborted = false;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = handleLocationChangingMethod.Invoke(component, new object[] { context })!;
            await ((ValueTask)result).AsTask();
            // .NET 11 拦截判定面：DidPreventNavigation 为内部只读位，公开面只有 PreventNavigation()
            aborted = (bool)typeof(LocationChangingContext)
                .GetProperty("DidPreventNavigation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(context)!;
        });
        await FlushAsync(renderer);
        await RerenderPortalAsync(renderer);
        return aborted;
    }

    /// <summary>挂起一次应用内导航：Enabled 挂载 → 补挂 OnPrompted 计数 → 拦截 /next。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, TestNavigationManager Navigation, ExitPrompt Component, PromptCounter Prompted)> HeldAsync(
        Dictionary<string, object?>? extra = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.Enabled)] = true,
        };
        foreach (var (key, value) in extra ?? [])
        {
            parameters[key] = value;
        }

        var (renderer, js, navigation, component) = await MountAsync(parameters);
        var counter = new PromptCounter();
        await renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.OnPrompted)] = new EventCallback(null, (Action)(() => counter.Count++)),
        });
        await FlushAsync(renderer);

        Assert.True(await HoldAsync(renderer, component, "https://localhost/next"));
        return (renderer, js, navigation, component, counter);
    }

    // ------------------------------------------------------------------
    // 空闲与武装沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 空闲态零渲染且不改浏览器提示武装()
    {
        var (renderer, js, _, _) = await MountAsync(new Dictionary<string, object?>());

        // Enabled=false 且 armedInBrowser=false：SyncBrowserPromptAsync 早退，不碰 interop
        Assert.Equal(0, Count(js, "exitPrompt.arm"));
        Assert.Equal(0, Count(js, "exitPrompt.disarm"));
        Assert.DoesNotContain("Leave without saving?", renderer.Markup());
    }

    [Fact]
    public async Task Enabled置真后帧武装浏览器提示且载荷为实例id()
    {
        var (_, js, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.Enabled)] = true,
        });

        var arm = Assert.Single(js.Invocations, i => i.Identifier == "exitPrompt.arm");
        var instanceId = Assert.IsType<string>(Assert.Single(arm.Args!));
        Assert.Matches("^atom-exit-prompt-[0-9a-f]{32}$", instanceId);
    }

    [Fact]
    public async Task 参数沿启停各自触发一次arm与disarm且状态不变保留监听器()
    {
        var (renderer, js, _, _) = await MountAsync(new Dictionary<string, object?>());

        // 启：arm 一次
        await renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.Enabled)] = true,
        });
        await FlushAsync(renderer);
        Assert.Equal(1, Count(js, "exitPrompt.arm"));

        // 重复渲染同值：armedInBrowser 门控，不重复 arm 也不 disarm
        await renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.Enabled)] = true,
        });
        await FlushAsync(renderer);
        Assert.Equal(1, Count(js, "exitPrompt.arm"));
        Assert.Equal(0, Count(js, "exitPrompt.disarm"));

        // 停：disarm 一次
        await renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.Enabled)] = false,
        });
        await FlushAsync(renderer);
        Assert.Equal(1, Count(js, "exitPrompt.disarm"));
    }

    // ------------------------------------------------------------------
    // 应用内导航拦截与对话框
    // ------------------------------------------------------------------

    [Fact]
    public async Task 导航被拦下弹出对话框问询一次且二次导航不重复问询()
    {
        var (renderer, _, navigation, component, prompted) = await HeldAsync();

        Assert.Equal(1, prompted.Count);
        var markup = renderer.Markup();
        Assert.Contains("Leave without saving?", markup);
        Assert.Contains("Your changes have not been saved", markup);
        Assert.Contains("Stay on this page", markup);
        Assert.Contains("Leave and discard", markup);

        // pending 已存在：同处挂起期间的再次导航直通——不拦、也不再问询
        Assert.False(await HoldAsync(renderer, component, "https://localhost/other"));
        Assert.Equal(1, prompted.Count);
        _ = navigation;
    }

    [Fact]
    public async Task 覆写文案沿替换本地化默认()
    {
        var (renderer, _, _, _, _) = await HeldAsync(new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.Title)] = "Draft at risk",
            [nameof(ExitPrompt.Message)] = "Your draft is not saved yet.",
            [nameof(ExitPrompt.StayLabel)] = "Keep editing",
            [nameof(ExitPrompt.LeaveLabel)] = "Discard it",
        });

        var markup = renderer.Markup();
        Assert.Contains("Draft at risk", markup);
        Assert.Contains("Your draft is not saved yet.", markup);
        Assert.Contains("Keep editing", markup);
        Assert.Contains("Discard it", markup);
        Assert.DoesNotContain("Leave without saving?", markup);
    }

    [Fact]
    public async Task Stay经OpenChanged落回空闲并触发OnStayed()
    {
        var stayed = 0;
        var (renderer, _, navigation, _, _) = await HeldAsync(new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.OnStayed)] = new EventCallback(null, (Action)(() => stayed++)),
        });

        // AlertDialog 的 OpenChanged(false) 即 StayAsync 通道
        var dialog = renderer.FindComponent<AlertDialog>();
        await renderer.Dispatcher.InvokeAsync(async () => await dialog.OpenChanged.InvokeAsync(false));
        await FlushAsync(renderer);

        Assert.Equal(1, stayed);
        Assert.DoesNotContain("Leave without saving?", renderer.Markup());
        Assert.Equal("https://localhost/", navigation.Uri); // 导航被取消，URL 未动
    }

    [Fact]
    public async Task Leave先落守护再重发导航并触发OnLeft与disarm()
    {
        var left = 0;
        var (renderer, js, navigation, component, _) = await HeldAsync(new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.OnLeft)] = new EventCallback(null, (Action)(() => left++)),
        });

        // Leave 沿经私有处理器直调（照 CalendarTests "属性沿编译为事件字典、
        // DispatchAsync 派发不到组件处理沿"先例）；双按钮的 onclick 接线面由对话框快照承载
        var leaveAsync = typeof(ExitPrompt).GetMethod("LeaveAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await renderer.Dispatcher.InvokeAsync(async () =>
            await (Task)leaveAsync.Invoke(component, Array.Empty<object>())!);
        await FlushAsync(renderer);

        Assert.Equal(1, left);
        Assert.Equal("https://localhost/next", navigation.Uri); // 重发放行
        Assert.DoesNotContain("Leave without saving?", renderer.Markup());
        Assert.Equal(1, Count(js, "exitPrompt.disarm")); // Enabled 落假解除浏览器提示
    }

    [Fact]
    public async Task Dispose在armed电路补发disarm()
    {
        // armed 电路：dispose 补发 disarm（armedInBrowser 门控）
        var (renderer, js, _, component) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.Enabled)] = true,
        });
        await renderer.Dispatcher.InvokeAsync(async () => await component.DisposeAsync());
        await FlushAsync(renderer);
        Assert.Equal(1, Count(js, "exitPrompt.disarm"));
    }

    [Fact]
    public async Task Dispose在未武装电路不触interop()
    {
        // 从未武装的电路：dispose 不触 interop
        var (idle, idleJs, _, idleComponent) = await MountAsync(new Dictionary<string, object?>());
        await idle.Dispatcher.InvokeAsync(async () => await idleComponent.DisposeAsync());
        Assert.Equal(0, Count(idleJs, "exitPrompt.disarm"));
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ExitPrompt.Enabled)] = true,
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 拦下导航的对话框应具备可访问结构与两个动作按钮()
    {
        // 不做整段标记快照：浮层有 JS / 原生 <dialog> / 内联三条渲染策略，门户内容由
        // PortalHost 渲染进渲染器自建的根，单测渲染器观察不到（参考项目同样只保留
        // API 表面快照）。类名覆盖由 AlertDialog 组件自身的源码约定保证，这里只钉住
        // 与策略无关的结构与无障碍契约。
        var (renderer, _, _, _, _) = await HeldAsync();
        var markup = Scrub(renderer.Markup());

        // 内容面板：role/aria-modal/状态
        Assert.Contains("role=\"alertdialog\"", markup);
        Assert.Contains("aria-modal=\"true\"", markup);
        Assert.Contains("data-state=\"open\"", markup);

        // 标题与描述经 aria-labelledby / aria-describedby 关联，且锚点确实存在
        Assert.Matches(@"aria-labelledby=""dialog-<n>-title""", markup);
        Assert.Matches(@"aria-describedby=""dialog-<n>-description""", markup);
        Assert.Contains("id=\"dialog-<n>-title\"", markup);
        Assert.Contains("id=\"dialog-<n>-description\"", markup);

        // 本地化文案落在结构里
        Assert.Contains("Leave without saving?", markup);
        Assert.Contains("Your changes have not been saved.", markup);

        // 两个动作按钮
        Assert.Contains("Stay on this page", markup);
        Assert.Contains("Leave and discard", markup);
    }
}
