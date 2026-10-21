using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
// using AtomBlazor.Dialog; // removed: Dialog is now in AtomBlazor namespace
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 命令式对话框测试：DialogService 的 Alert/Confirm/Prompt/OpenAsync 编程入口、
/// 完成源（completion）经 Resolve 解算、以及 DialogProvider 依 DialogService.Dialogs 渲染
/// 出的无障碍角色（alertdialog / dialog）与 ARIA 标记。
/// 对话框 Id 为 Guid，快照类断言仅用 Contains，不依赖确定性 id。
/// </summary>
public class DialogTests
{
    // ------------------------------------------------------------------
    // 渲染器 + 共享 DialogService
    // ------------------------------------------------------------------

    private static (ComponentTestRenderer Renderer, DialogService Service) CreateProviderRenderer(
        IJSRuntime? js = null)
    {
        js ??= new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var dialogService = new DialogService();
        services.AddSingleton(dialogService);

        var provider = services.BuildServiceProvider();
        var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        return (renderer, dialogService);
    }

    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    private static async Task MountProviderAsync(ComponentTestRenderer renderer)
    {
        await renderer.MountAsync<DialogProvider>(new Dictionary<string, object?>());
        await SettleAsync(renderer);
        // 强制重渲染以确保 OnAfterRender 的 JS 路径（焦点陷阱/滚动锁）在 Noop 下不崩。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>轮询等待渲染结果满足谓词，超时即返回 false。</summary>
    private static async Task<bool> WaitForMarkupAsync(
        ComponentTestRenderer renderer, Func<string, bool> predicate, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (predicate(await renderer.Dispatcher.InvokeAsync(renderer.Markup)))
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    /// <summary>轮询等待任意条件成立，超时即返回 false。</summary>
    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    // ------------------------------------------------------------------
    // DialogService 完成源解算（不依赖渲染）
    // ------------------------------------------------------------------

    [Fact]
    public async Task AlertAsync应入队并完成源()
    {
        var (_, service) = CreateProviderRenderer();

        var t = service.AlertAsync("警告标题", "警告描述");
        Assert.Single(service.Dialogs);

        var id = service.Dialogs[0].Id;
        service.Resolve(id, DialogResult.Ok());

        var result = await t;
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task ConfirmAsync应随Ok解算为已确认()
    {
        var (_, service) = CreateProviderRenderer();

        var t = service.ConfirmAsync("确认标题", "确认描述");
        var id = service.Dialogs[0].Id;
        service.Resolve(id, DialogResult.Ok(true));

        var result = await t;
        Assert.True(result.Confirmed);
    }

    [Fact]
    public async Task PromptAsync应随Ok解算回传输入值()
    {
        var (_, service) = CreateProviderRenderer();

        var t = service.PromptAsync("输入标题");
        var id = service.Dialogs[0].Id;
        service.Resolve(id, DialogResult.Ok("用户输入"));

        var result = await t;
        Assert.Equal("用户输入", result.Value);
    }

    [Fact]
    public async Task OpenAsync应入队自定义组件对话框()
    {
        var (_, service) = CreateProviderRenderer();

        var t = service.OpenAsync<SampleDialogBody>(new DialogOpenOptions { Title = "自定义标题" });
        Assert.Single(service.Dialogs);
        Assert.IsType<ComponentDialogData>(service.Dialogs[0]);

        var id = service.Dialogs[0].Id;
        service.Resolve(id, DialogResult.Cancel());
        var result = await t;
        Assert.True(result.Cancelled);
    }

    [Fact]
    public void Dialogs应返回稳定快照()
    {
        var (_, service) = CreateProviderRenderer();

        var empty = service.Dialogs;
        _ = service.AlertAsync("快照");
        Assert.Empty(empty); // 入队后旧快照不变
        Assert.Single(service.Dialogs);

        var withOne = service.Dialogs;
        service.Resolve(withOne[0].Id, DialogResult.Ok());
        Assert.Single(withOne); // 解算后旧快照仍保留当刻内容
        Assert.Empty(service.Dialogs);
    }

    [Fact]
    public async Task 并发入队与解算应保持快照一致且每枚恰好完成()
    {
        var (_, service) = CreateProviderRenderer();
        const int count = 100;
        var completions = new Task<DialogResult>[count];

        await Parallel.ForEachAsync(Enumerable.Range(0, count), (i, _) =>
        {
            completions[i] = service.AlertAsync($"第{i}条");
            return ValueTask.CompletedTask;
        });

        Assert.Equal(count, service.Dialogs.Count);
        var snapshot = service.Dialogs;
        var ids = snapshot.Select(d => d.Id).ToArray();
        Assert.Equal(count, ids.Distinct().Count());

        await Parallel.ForEachAsync(ids, (id, _) =>
        {
            service.Resolve(id, DialogResult.Ok());
            return ValueTask.CompletedTask;
        });

        Assert.Empty(service.Dialogs);
        Assert.Equal(count, snapshot.Count);
        Assert.All(completions, t => Assert.True(t.IsCompletedSuccessfully));
        Assert.All(completions, t => Assert.False(t.Result.Cancelled));
    }

    // ------------------------------------------------------------------
    // DialogProvider 渲染
    // ------------------------------------------------------------------

    [Fact]
    public async Task 无对话框时Provider不应渲染浮层角色()
    {
        var (renderer, _) = CreateProviderRenderer();
        await MountProviderAsync(renderer);

        Assert.DoesNotContain("role=\"dialog\"", renderer.Markup());
        Assert.DoesNotContain("role=\"alertdialog\"", renderer.Markup());
    }

    [Fact]
    public async Task AlertAsync应经Provider渲染alertdialog角色与标题()
    {
        var (renderer, service) = CreateProviderRenderer();
        await MountProviderAsync(renderer);

        // 注意：AlertAsync 返回的是完成源（仅在对话框被解算时完成），
        // 此处仅触发入队与 OnChange，不 await（否则会永久挂起）。
        _ = service.AlertAsync("警告标题", "警告描述");
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"alertdialog\"", markup);
        Assert.Contains("aria-modal=\"true\"", markup);
        Assert.Contains("警告标题", markup);
        Assert.Contains("警告描述", markup);
        Assert.Contains("aria-labelledby", markup);
        Assert.Contains("aria-describedby", markup);
    }

    [Fact]
    public async Task ConfirmAsync应经Provider渲染确认对话框()
    {
        var (renderer, service) = CreateProviderRenderer();
        await MountProviderAsync(renderer);

        _ = service.ConfirmAsync("确认标题", "确认描述");
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"alertdialog\"", markup);
        Assert.Contains("aria-modal=\"true\"", markup);
        Assert.Contains("确认标题", markup);
    }

    [Fact]
    public async Task PromptAsync应经Provider渲染输入框()
    {
        var (renderer, service) = CreateProviderRenderer();
        await MountProviderAsync(renderer);

        _ = service.PromptAsync("提示标题", "提示描述");
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"dialog\"", markup);
        Assert.Contains("提示标题", markup);
        Assert.Contains("<input", markup);
    }

    [Fact]
    public async Task 后台线程入队的对话框应经提供器渲染()
    {
        var (renderer, service) = CreateProviderRenderer();
        await MountProviderAsync(renderer);

        await Task.Run(() => { _ = service.AlertAsync("后台对话框", "来自任意线程"); });

        Assert.True(
            await WaitForMarkupAsync(renderer, m => m.Contains("后台对话框", StringComparison.Ordinal)),
            "后台线程触发的 OnChange 未在渲染器线程上兑现");

        var dialog = Assert.Single(service.Dialogs);
        service.Resolve(dialog.Id, DialogResult.Ok());
    }

    [Fact]
    public async Task OpenAsync应经Provider渲染自定义组件与标题()
    {
        var (renderer, service) = CreateProviderRenderer();
        await MountProviderAsync(renderer);

        _ = service.OpenAsync<SampleDialogBody>(new DialogOpenOptions { Title = "自定义标题" });
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"dialog\"", markup);
        Assert.Contains("自定义标题", markup);
        Assert.Contains("自定义组件正文", markup);
    }

    // ------------------------------------------------------------------
    // 初始化 / 清理竞态
    // ------------------------------------------------------------------

    [Fact]
    public async Task 初始化等待期间关闭对话框仍应完成JS清理()
    {
        var js = new GatedJavaScript();
        var (renderer, service) = CreateProviderRenderer(js);
        await renderer.MountAsync<DialogProvider>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        var pending = service.AlertAsync("初始化竞态");
        Assert.True(await WaitForMarkupAsync(renderer, m => m.Contains("初始化竞态", StringComparison.Ordinal)));
        Assert.True(await WaitUntilAsync(() => js.Calls.Contains("portal.lockBodyScroll")),
            "初始化未进入被阻塞的滚动锁调用");

        var id = service.Dialogs[0].Id;
        service.Resolve(id, DialogResult.Ok());
        await pending;

        // 关闭触发的清理必须排在初始化之后；先让它的渲染进入等待，再放行初始化。
        await SettleAsync(renderer);
        js.Release();

        Assert.True(await WaitUntilAsync(() => js.Calls.Contains("escapeKeydown.dispose")),
            "初始化完成后没有清理逃逸键监听与滚动锁");
        Assert.Contains("apply", js.Calls);
    }

    [Fact]
    public async Task 清理时JS异常不应中断提供器销毁()
    {
        var js = new FaultingJavaScript("escapeKeydown.dispose");
        var (renderer, service) = CreateProviderRenderer(js);

        // 放在宿主里挂载，便于按组件类型取回提供器实例以显式销毁。
        await renderer.MountAsync<ProviderHost>(new Dictionary<string, object?>
        {
            [nameof(ProviderHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<DialogProvider>(0);
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        _ = service.AlertAsync("清理异常");
        Assert.True(await WaitForMarkupAsync(renderer, m => m.Contains("清理异常", StringComparison.Ordinal)));
        Assert.True(await WaitUntilAsync(() => js.Calls.Contains("escapeKeydown.initialize")));

        var provider = renderer.FindComponent<DialogProvider>();
        await renderer.Dispatcher.InvokeAsync(async () => await provider.DisposeAsync());

        // 清理后续步骤（CancelAll）仍应执行：完成源不得悬空。
        Assert.Empty(service.Dialogs);
    }

    // ------------------------------------------------------------------
    // 测试用最小组件与 JS 运行时
    // ------------------------------------------------------------------

    private sealed class SampleDialogBody : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, "自定义组件正文");
    }

    /// <summary>把提供器作为子组件承载，便于测试按类型取回其实例。</summary>
    private sealed class ProviderHost : ComponentBase
    {
        [Parameter] public RenderFragment? ChildContent { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, ChildContent);
    }

    /// <summary>把 portal.lockBodyScroll 挂起到显式放行，用来制造初始化与清理的竞态。</summary>
    private sealed class GatedJavaScript : IJSRuntime, IJSObjectReference
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object sync = new();
        private readonly List<string> calls = [];

        public IReadOnlyList<string> Calls
        {
            get
            {
                lock (sync)
                {
                    return calls.ToArray();
                }
            }
        }

        public void Release() => gate.TrySetResult();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            lock (sync)
            {
                calls.Add(identifier);
            }

            return identifier == "portal.lockBodyScroll"
                ? AwaitGateAsync<TValue>()
                : ValueTask.FromResult(Result<TValue>());
        }

        private async ValueTask<TValue> AwaitGateAsync<TValue>()
        {
            await gate.Task.ConfigureAwait(false);
            return Result<TValue>();
        }

        private TValue Result<TValue>() =>
            typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>在指定方法上抛 JSException 的运行时。</summary>
    private sealed class FaultingJavaScript(string faultIdentifier) : IJSRuntime, IJSObjectReference
    {
        private readonly object sync = new();
        private readonly List<string> calls = [];

        public IReadOnlyList<string> Calls
        {
            get
            {
                lock (sync)
                {
                    return calls.ToArray();
                }
            }
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            lock (sync)
            {
                calls.Add(identifier);
            }

            if (identifier == faultIdentifier)
            {
                throw new JSException("模拟清理失败");
            }

            return ValueTask.FromResult(
                typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
