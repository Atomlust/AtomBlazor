using AtomBlazor.Primitives;
using System.Text.Json;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Floating;

/// <summary>
/// FloatingPortal 的渲染级测试：挂载即向宿主注册门户、参数默认值、
/// 打开/关闭沿在 overlay.open / overlay.close 单次 interop 调用里构造的选项对象、
/// JsOnClosed 的延迟注销，以及 Dismiss/Placement 回调的转发行为。
/// interop 由扩展后的 NoopJavaScript 记录调用清单供断言，不做真实 JS 执行。
/// </summary>
public class FloatingPortalTests
{
    /// <summary>构造一段写出固定文本的 RenderFragment，作为门户内容。</summary>
    private static RenderFragment Content(string text) =>
        builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddContent(1, text);
            builder.CloseElement();
        };

    private static ElementReference Anchor(string id = "anchor-1") => new(id);

    private static (ComponentTestRenderer Renderer, PortalService Portal, NoopJavaScript Js)
        CreateHarness()
    {
        var js = new NoopJavaScript();
        var portal = new PortalService(NullLogger<PortalService>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(portal);
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var renderer = new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
        return (renderer, portal, js);
    }

    /// <summary>把某次记录调用的第 index 个参数（匿名选项对象）按其运行时类型转为 JsonElement。</summary>
    private static JsonElement ArgsAsJson(NoopJavaScript js, string identifier, int index)
    {
        var value = js.Invocations.Single(i => i.Identifier == identifier).Args![index]!;
        return JsonSerializer.SerializeToElement(value, value.GetType());
    }

    // ------------------------------------------------------------------
    // 挂载与注册
    // ------------------------------------------------------------------

    [Fact]
    public async Task 挂载即注册门户并渲染内容包裹元素()
    {
        var (renderer, portal, _) = CreateHarness();
        await renderer.MountAsync<PortalHost>([]);
        await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.ChildContent)] = Content("浮层内容"),
        });

        // ForceMount 默认 true：即使从未打开，内容也已注册到 Overlay 类别。
        Assert.Contains("p1", portal.GetPortals(PortalCategory.Overlay).Select(p => p.Key));

        var markup = renderer.Markup();
        Assert.Contains("data-atom-portal=\"p1\"", markup);
        Assert.Contains("data-portal-content=\"true\"", markup);
        Assert.Contains("data-state=\"closed\"", markup);
        Assert.Contains("浮层内容", markup);
    }

    [Fact]
    public async Task 空PortalId应拒绝挂载()
    {
        var (renderer, _, _) = CreateHarness();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            renderer.MountAsync<FloatingPortal>(new()
            {
                [nameof(FloatingPortal.ChildContent)] = Content("x"),
            }));
    }

    [Fact]
    public async Task 未提供锚元素时不应派发打开()
    {
        var (renderer, _, js) = CreateHarness();
        await renderer.MountAsync<PortalHost>([]);
        await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = true,
        });

        // 缺锚点只记日志，不派发 overlay.open；组件照常渲染（data-state 仍为 open）。
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "overlay.open");
        Assert.Contains("data-state=\"open\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 参数默认值
    // ------------------------------------------------------------------

    [Fact]
    public async Task 参数默认值应与源项目一致()
    {
        var (renderer, _, _) = CreateHarness();
        var component = await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
        });

        Assert.True(component.ForceMount);
        Assert.Equal(PopoverSide.Bottom, component.Side);
        Assert.Equal(PopoverAlign.Center, component.Align);
        Assert.Equal(4, component.Offset);
        Assert.Equal(50, component.ZIndex);
        Assert.Equal(PositioningStrategy.Absolute, component.Strategy);
        Assert.False(component.MatchAnchorWidth);
        Assert.False(component.IsOpen);
        Assert.False(component.RestoreFocusOnClose);
        Assert.Null(component.Dismiss);
        Assert.Null(component.Keyboard);
        Assert.Null(component.AnchorElement);
        Assert.Null(component.SideElementId);
        Assert.Null(component.ScrollToCurrentIn);
        Assert.Null(component.AutoFocusId);
        Assert.Null(component.RestoreFocusToId);
        Assert.Equal("[role=\"option\"][aria-selected=\"true\"]", component.ScrollToCurrentSelector);
    }

    // ------------------------------------------------------------------
    // 打开沿：overlay.open 选项构造
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开沿应在单次调用内构造完整选项并触发OnReady()
    {
        var (renderer, _, js) = CreateHarness();
        var readyCount = 0;
        var placements = new List<string>();

        await renderer.MountAsync<PortalHost>([]);
        await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = true,
            [nameof(FloatingPortal.AnchorElement)] = Anchor(),
            [nameof(FloatingPortal.ChildContent)] = Content("选项构造"),
            [nameof(FloatingPortal.Side)] = PopoverSide.Right,
            [nameof(FloatingPortal.Align)] = PopoverAlign.Start,
            [nameof(FloatingPortal.Offset)] = 10,
            [nameof(FloatingPortal.ZIndex)] = 99,
            [nameof(FloatingPortal.MatchAnchorWidth)] = true,
            [nameof(FloatingPortal.Strategy)] = PositioningStrategy.Fixed,
            [nameof(FloatingPortal.SideElementId)] = "side-el",
            [nameof(FloatingPortal.ScrollToCurrentIn)] = "list-1",
            [nameof(FloatingPortal.AutoFocusId)] = "search-1",
            [nameof(FloatingPortal.OnReady)] = new EventCallback(null, (Action)(() => readyCount++)),
            [nameof(FloatingPortal.OnPlacementChanged)] = new EventCallback<string>(null, placements.Add),
            [nameof(FloatingPortal.Dismiss)] = new FloatingDismissOptions
            {
                ContentId = "content-1",
                TriggerId = "trigger-1",
                OnOutsideInteraction = true,
                OnEscapeKey = false,
            },
            [nameof(FloatingPortal.Keyboard)] = new FloatingKeyboardOptions
            {
                Kind = FloatingKeyboardKind.Menu,
                ContentId = "menu-1",
                SelectedValue = "v",
                Loop = false,
                Mode = "both",
                InitialFocus = "first",
            },
        });

        var options = ArgsAsJson(js, "overlay.open", 2);

        // placement 由 Side/Align 组合：非居中对齐拼接 "-align" 后缀。
        Assert.Equal("right-start", options.GetProperty("placement").GetString());
        Assert.Equal(10, options.GetProperty("offset").GetInt32());
        Assert.Equal(99, options.GetProperty("zIndex").GetInt32());
        Assert.True(options.GetProperty("matchReferenceWidth").GetBoolean());
        Assert.Equal("fixed", options.GetProperty("strategy").GetString());
        Assert.True(options.GetProperty("flip").GetBoolean());
        Assert.True(options.GetProperty("shift").GetBoolean());
        Assert.Equal("side-el", options.GetProperty("sideElementId").GetString());

        // 挂了 OnPlacementChanged 回调才要求 JS 回报方位（回报即一次往返，按需付费）。
        Assert.True(options.GetProperty("reportPlacement").GetBoolean());

        Assert.Equal("content-1", options.GetProperty("dismiss").GetProperty("contentId").GetString());
        Assert.Equal("trigger-1", options.GetProperty("dismiss").GetProperty("triggerId").GetString());
        Assert.True(options.GetProperty("dismiss").GetProperty("onOutsideInteraction").GetBoolean());
        Assert.False(options.GetProperty("dismiss").GetProperty("onEscapeKey").GetBoolean());

        Assert.Equal("list-1", options.GetProperty("scrollToCurrent").GetProperty("containerId").GetString());
        Assert.Equal("[role=\"option\"][aria-selected=\"true\"]",
            options.GetProperty("scrollToCurrent").GetProperty("selector").GetString());

        Assert.Equal("search-1", options.GetProperty("autoFocusId").GetString());

        Assert.Equal("Menu", options.GetProperty("keyboard").GetProperty("kind").GetString());
        Assert.Equal("menu-1", options.GetProperty("keyboard").GetProperty("contentId").GetString());
        Assert.Equal("v", options.GetProperty("keyboard").GetProperty("selectedValue").GetString());
        Assert.False(options.GetProperty("keyboard").GetProperty("loop").GetBoolean());
        Assert.Equal("both", options.GetProperty("keyboard").GetProperty("mode").GetString());
        Assert.Equal("first", options.GetProperty("keyboard").GetProperty("initialFocus").GetString());

        // 打开派发之后 OnReady 在首个渲染回调里触发，且只触发一次。
        Assert.Equal(1, readyCount);
        Assert.Contains("data-state=\"open\"", renderer.Markup());
    }

    [Fact]
    public async Task 居中对齐不拼接后缀且未声明的选项应为null()
    {
        var (renderer, _, js) = CreateHarness();
        await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = true,
            [nameof(FloatingPortal.AnchorElement)] = Anchor(),
        });

        var options = ArgsAsJson(js, "overlay.open", 2);
        Assert.Equal("bottom", options.GetProperty("placement").GetString());
        Assert.False(options.GetProperty("reportPlacement").GetBoolean());
        Assert.Equal(JsonValueKind.Null, options.GetProperty("dismiss").ValueKind);
        Assert.Equal(JsonValueKind.Null, options.GetProperty("scrollToCurrent").ValueKind);
        Assert.Equal(JsonValueKind.Null, options.GetProperty("keyboard").ValueKind);
        Assert.Equal(JsonValueKind.Null, options.GetProperty("sideElementId").ValueKind);
    }

    // ------------------------------------------------------------------
    // 关闭沿：overlay.close 选项构造与 JsOnClosed
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭沿应派发notifyClosed并配合JsOnClosed完成延迟注销()
    {
        var (renderer, portal, js) = CreateHarness();
        await renderer.MountAsync<PortalHost>([]);
        var component = await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = true,
            [nameof(FloatingPortal.ForceMount)] = false,
            [nameof(FloatingPortal.AnchorElement)] = Anchor(),
            [nameof(FloatingPortal.RestoreFocusOnClose)] = true,
            [nameof(FloatingPortal.RestoreFocusToId)] = "trigger-1",
            [nameof(FloatingPortal.ChildContent)] = Content("退出动画"),
        });

        await renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = false,
        });

        var options = ArgsAsJson(js, "overlay.close", 1);
        Assert.True(options.GetProperty("notifyClosed").GetBoolean());
        Assert.Equal("trigger-1", options.GetProperty("restoreFocusToId").GetString());
        Assert.Equal(JsonValueKind.Null, options.GetProperty("keyboard").ValueKind);

        // ForceMount=false：注销要等退场动画结束的 JsOnClosed 回调，此刻仍在注册。
        Assert.Contains("p1", portal.GetPortals(PortalCategory.Overlay).Select(p => p.Key));

        await component.JsOnClosed();

        Assert.DoesNotContain("p1", portal.GetPortals(PortalCategory.Overlay).Select(p => p.Key));
    }

    [Fact]
    public async Task ForceMount时关闭只隐藏不注销且不请求closed回调()
    {
        var (renderer, portal, js) = CreateHarness();
        await renderer.MountAsync<PortalHost>([]);
        await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = true,
            [nameof(FloatingPortal.AnchorElement)] = Anchor(),
            [nameof(FloatingPortal.ChildContent)] = Content("常驻"),
        });

        await renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = false,
        });

        var options = ArgsAsJson(js, "overlay.close", 1);
        Assert.False(options.GetProperty("notifyClosed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, options.GetProperty("restoreFocusToId").ValueKind);

        // 内容留在 DOM：仍注册、已渲染为 closed 态。
        Assert.Contains("p1", portal.GetPortals(PortalCategory.Overlay).Select(p => p.Key));
        Assert.Contains("data-state=\"closed\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // Dismiss / Placement 回调
    // ------------------------------------------------------------------

    [Fact]
    public async Task JsOnDismiss应转发手势类型且关闭后不再打扰()
    {
        var (renderer, _, js) = CreateHarness();
        var dismissed = new List<FloatingDismissReason>();
        var component = await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = true,
            [nameof(FloatingPortal.AnchorElement)] = Anchor(),
            [nameof(FloatingPortal.OnDismiss)] = new EventCallback<FloatingDismissReason>(null, dismissed.Add),
        });

        await component.JsOnDismissOutside();
        await component.JsOnDismissEscape();
        Assert.Equal([FloatingDismissReason.OutsideInteraction, FloatingDismissReason.EscapeKey], dismissed);

        // 监听器比浮层活得久：手势晚到时浮层已在关闭，不应再上报。
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = false,
        });

        await component.JsOnDismissOutside();
        Assert.Equal(2, dismissed.Count);

        // 开关两沿都在同一次 interop 调用里接线，不额外花往返。
        Assert.Contains(js.Invocations, i => i.Identifier == "overlay.open");
        Assert.Contains(js.Invocations, i => i.Identifier == "overlay.close");
    }

    [Fact]
    public async Task JsOnPlacementChanged应转发方位而无回调时静默()
    {
        var (renderer, _, _) = CreateHarness();
        var placements = new List<string>();
        var component = await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = true,
            [nameof(FloatingPortal.AnchorElement)] = Anchor(),
            [nameof(FloatingPortal.OnPlacementChanged)] = new EventCallback<string>(null, placements.Add),
        });

        await component.JsOnPlacementChanged("top-end");
        Assert.Equal(["top-end"], placements);

        // 无回调的实例：JS 迟到的回报直接吞掉，不得抛错。
        var bare = await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p2",
        });
        await bare.JsOnPlacementChanged("left");
        Assert.Single(placements);
    }

    // ------------------------------------------------------------------
    // 释放
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开状态下释放应补发关闭并注销门户()
    {
        var (renderer, portal, js) = CreateHarness();
        await renderer.MountAsync<PortalHost>([]);
        var component = await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.IsOpen)] = true,
            [nameof(FloatingPortal.AnchorElement)] = Anchor(),
            [nameof(FloatingPortal.ChildContent)] = Content("导航离开"),
        });

        await component.DisposeAsync();

        // 释放路径的关闭：notifyClosed=false（没有东西等着接收回调），dotNetRef 传 null。
        var close = js.Invocations.Last(i => i.Identifier == "overlay.close");
        var options = JsonSerializer.SerializeToElement(close.Args![1]!, close.Args![1]!.GetType());
        Assert.False(options.GetProperty("notifyClosed").GetBoolean());
        Assert.Null(close.Args![2]);

        Assert.DoesNotContain("p1", portal.GetPortals(PortalCategory.Overlay).Select(p => p.Key));
    }

    [Fact]
    public async Task 释放幂等且停放样式恒定()
    {
        var (renderer, _, _) = CreateHarness();
        await renderer.MountAsync<PortalHost>([]);
        var component = await renderer.MountAsync<FloatingPortal>(new()
        {
            [nameof(FloatingPortal.PortalId)] = "p1",
            [nameof(FloatingPortal.ZIndex)] = 60,
            [nameof(FloatingPortal.AdditionalAttributes)] = new Dictionary<string, object>
            {
                ["style"] = "color: red",
                ["data-tag"] = "kept",
            },
        });

        var markup = renderer.Markup();
        // 用户样式在前、停放样式在后（后写的赢）；style 键不会经展开重复写出。
        Assert.Contains("style=\"color: red; position: fixed; z-index: 60; top: -9999px;", markup);
        Assert.Contains("data-tag=\"kept\"", markup);

        await component.DisposeAsync();
        await component.DisposeAsync();   // 二次释放应直接返回
    }
}
