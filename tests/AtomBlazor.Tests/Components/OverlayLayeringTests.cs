using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using PSelect = AtomBlazor.Primitives.SelectPrimitive<string>;
using PSelectContent = AtomBlazor.Primitives.SelectContentPrimitive<string>;
using PSelectItem = AtomBlazor.Primitives.SelectItemPrimitive<string>;
using PSelectTrigger = AtomBlazor.Primitives.SelectTriggerPrimitive<string>;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 浮层堆叠次序的跨组件不变量：Toast 必须严格压过 Popover 与 Select，
/// 否则一个打开的下拉会把瞬时通知盖住（等于没送达）。
/// 同时锁定 Select 的 ZIndex 是公开可调参数，且包装层与基元层都把它透传到门户，
/// 而不是像过去那样在基元里硬编码 9999。刻度见 THEMING.md §6.1。
/// </summary>
public class OverlayLayeringTests
{
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateSelectRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<DropdownManagerService>();
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>组合式家族：触发器 + 内容（内容上可注入 ZIndex）。</summary>
    private static RenderFragment Family(int? zIndex) => builder =>
    {
        builder.OpenComponent<PSelectTrigger>(0);
        builder.CloseComponent();
        builder.OpenComponent<PSelectContent>(1);
        if (zIndex.HasValue)
        {
            builder.AddAttribute(2, nameof(PSelectContent.ZIndex), zIndex.Value);
        }
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(inner =>
        {
            inner.OpenComponent<PSelectItem>(0);
            inner.AddAttribute(1, "Value", "a");
            inner.AddAttribute(2, "Text", "选项 a");
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    /// <summary>打开 Select 并取回门户 overlay.open 实际收到的 zIndex。</summary>
    private static async Task<int> OpenedPortalZIndexAsync(int? zIndex)
    {
        var (renderer, js) = CreateSelectRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var select = await renderer.MountAsync<PSelect>(new Dictionary<string, object?>
        {
            [nameof(PSelect.ChildContent)] = Family(zIndex),
        });
        await SettleAsync(renderer);

        var context = (SelectContext<string>)typeof(PSelect)
            .GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(select)!;

        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);

        var invocation = js.Invocations.Single(i => i.Identifier == "overlay.open");
        // overlay.open 的参数次序是 (PortalId, AnchorElement, options, SelfRef)。
        // 经反射直读 zIndex——整对象序列化会撞上 keyboard.callbackRef 这类 DotNetObjectReference。
        var options = invocation.Args![2]!;
        return (int)options.GetType().GetProperty("zIndex")!.GetValue(options)!;
    }

    /// <summary>挂载 ToastProvider 并取回容器实际的 z-[N] 档位。</summary>
    private static async Task<int> ToastContainerZIndexAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton(new ToastService());
        var renderer = new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);

        await renderer.MountAsync<ToastProvider>(new Dictionary<string, object?>());
        await SettleAsync(renderer);
        var markup = await renderer.Dispatcher.InvokeAsync(renderer.Markup);

        var match = Regex.Match(markup, @"atom:z-\[(\d+)\]");
        Assert.True(match.Success, "ToastProvider 容器应带一个显式 z-[N] 档位");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task Select基元默认ZIndex应为9999() =>
        Assert.Equal(9999, await OpenedPortalZIndexAsync(null));

    [Fact]
    public async Task Select基元ZIndex应可覆盖并透传到门户() =>
        Assert.Equal(1234, await OpenedPortalZIndexAsync(1234));

    [Fact]
    public void Select包装层与基元层的ZIndex默认值应一致()
    {
        // 包装层默认值若与基元层漂移，同一个 Select 在两种模式下会落在不同档位。
        Assert.Equal(
            new PSelectContent().ZIndex,
            new SelectContent<string>().ZIndex);
    }

    // 两者都要盖在 Dialog/Sheet/Drawer 遮罩（z-50）之上，故同档。
    [Fact]
    public void Popover默认ZIndex应与Select同档() =>
        Assert.Equal(new PSelectContent().ZIndex, new PopoverContentPrimitive().ZIndex);

    [Fact]
    public async Task Toast层级应严格压过Select与Popover()
    {
        var toast = await ToastContainerZIndexAsync();
        var select = await OpenedPortalZIndexAsync(null);
        var popover = new PopoverContentPrimitive().ZIndex;

        Assert.True(toast > select, $"Toast({toast}) 必须高于 Select({select})，否则打开的下拉会盖住通知");
        Assert.True(toast > popover, $"Toast({toast}) 必须高于 Popover({popover})");
    }
}