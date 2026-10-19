using AtomBlazor.Primitives;
using AtomBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using AtomBlazor.Tests.Rendering;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 抽屉在两种模式下的 OpenChanged 上报（自 DrawerOpenChangedTests）。
/// 遗留问题：过去抽屉自行保留状态却不发出任何通知，因此监听 OpenChanged 但未绑定 Open 的页面
/// 永远听不到它打开或关闭；Dialog 与 Sheet 两种模式都会上报，抽屉现在也如此（#508 的修复）。
/// </summary>
public class DrawerOpenChangedTests
{
    [Fact]
    public async Task 自行管理的抽屉应上报打开与关闭()
    {
        var (renderer, drawer, changes) = await RenderAsync(open: null);

        await Click(renderer, trigger);
        await Click(renderer, close);

        Assert.Equal(openedThenClosed, changes);
        Assert.False(drawer.IsOpen);
    }

    [Fact]
    public async Task 自行管理的抽屉只应上报真实变化()
    {
        // 关闭一个已经关闭的抽屉不算新闻。若上报，页面就得把这条“假信号”识别为无事发生。
        var (renderer, _, changes) = await RenderAsync(open: null);

        await Click(renderer, close);

        Assert.Empty(changes);
    }

    [Fact]
    public async Task 受控抽屉应上报并把状态交给页面()
    {
        var (renderer, drawer, changes) = await RenderAsync(open: false);

        await Click(renderer, trigger);

        Assert.Equal(opened, changes);
        Assert.False(drawer.IsOpen);
    }

    private const int trigger = 0;
    private const int close = 1;

    private static readonly bool[] openedThenClosed = [true, false];
    private static readonly bool[] opened = [true];

    private static Task Click(ComponentTestRenderer renderer, int which) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs(), which));

    private static async Task<(ComponentTestRenderer Renderer, Drawer Drawer, List<bool> Changes)> RenderAsync(bool? open)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();

        var provider = services.BuildServiceProvider();
        var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        var changes = new List<bool>();

        var parameters = new Dictionary<string, object?>
        {
            [nameof(Drawer.OpenChanged)] = EventCallback.Factory.Create<bool>(new object(), changes.Add),
            [nameof(Drawer.ChildContent)] = (RenderFragment)BuildButtons,
        };

        if (open is { } value)
        {
            parameters[nameof(Drawer.Open)] = value;
        }

        var drawer = await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<Drawer>(parameters));
        return (renderer, drawer, changes);
    }

    private static void BuildButtons(RenderTreeBuilder builder)
    {
        builder.OpenComponent<DrawerTrigger>(0);
        builder.AddAttribute(1, nameof(DrawerTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "Open")));
        builder.CloseComponent();

        builder.OpenComponent<DrawerClose>(2);
        builder.AddAttribute(3, nameof(DrawerClose.ChildContent), (RenderFragment)(b => b.AddContent(0, "Close")));
        builder.CloseComponent();
    }
}
