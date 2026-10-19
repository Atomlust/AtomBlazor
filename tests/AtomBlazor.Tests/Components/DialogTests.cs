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

    private static (ComponentTestRenderer Renderer, DialogService Service) CreateProviderRenderer()
    {
        var js = new NoopJavaScript();
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
    // 测试用最小组件
    // ------------------------------------------------------------------

    private sealed class SampleDialogBody : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            builder.AddContent(0, "自定义组件正文");
    }
}
