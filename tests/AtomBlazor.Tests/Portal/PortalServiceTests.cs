using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using System.Collections.Concurrent;
using Xunit;

namespace AtomBlazor.Tests.Portal;

/// <summary>
/// PortalService 公开面的单元测试：注册/注销（清空）、类别筛选、内容更新与刷新、
/// 渲染通知，以及"宿主按计数而非布尔标志"的回归守护（#545）。
/// </summary>
public class PortalServiceTests
{
    private static PortalService CreateService() =>
        new(NullLogger<PortalService>.Instance);

    /// <summary>构造一段写出固定文本的 RenderFragment，作为门户内容。</summary>
    private static RenderFragment Fragment(string text) =>
        builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddContent(1, text);
            builder.CloseElement();
        };

    // ------------------------------------------------------------------
    // 注册与查询
    // ------------------------------------------------------------------

    [Fact]
    public void 注册门户后应按插入顺序出现在对应类别中()
    {
        var service = CreateService();
        service.RegisterHost();

        var fa = Fragment("甲");
        var fb = Fragment("乙");
        var fc = Fragment("丙");
        service.RegisterPortal("a", fa, PortalCategory.Overlay);
        service.RegisterPortal("b", fb, PortalCategory.Container);
        service.RegisterPortal("c", fc, PortalCategory.Overlay);

        var overlays = service.GetPortals(PortalCategory.Overlay);
        Assert.Equal(["a", "c"], overlays.Select(p => p.Key));
        Assert.Same(fa, overlays[0].Value);
        Assert.Same(fc, overlays[1].Value);

        var containers = service.GetPortals(PortalCategory.Container);
        Assert.Equal(["b"], containers.Select(p => p.Key));
        Assert.Same(fb, containers[0].Value);
    }

    [Fact]
    public void 类别筛选应让另一类别看不到门户()
    {
        var service = CreateService();

        service.RegisterPortal("overlay-1", Fragment("浮层"), PortalCategory.Overlay);

        Assert.Empty(service.GetPortals(PortalCategory.Container));
        Assert.Single(service.GetPortals(PortalCategory.Overlay));
    }

    [Fact]
    public void 注册空ID门户应抛ArgumentException()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.RegisterPortal("  ", Fragment("x"), PortalCategory.Overlay));
        Assert.Throws<ArgumentException>(() => service.RegisterPortal(null!, Fragment("x"), PortalCategory.Overlay));
    }

    [Fact]
    public void 注册空内容门户应抛ArgumentNullException()
    {
        var service = CreateService();

        Assert.Throws<ArgumentNullException>(() => service.RegisterPortal("a", null!, PortalCategory.Overlay));
    }

    [Fact]
    public void 重复注册同一ID应整体覆盖旧条目()
    {
        var service = CreateService();
        var 旧片段 = Fragment("旧内容");

        service.RegisterPortal("a", 旧片段, PortalCategory.Overlay);
        service.RegisterPortal("a", Fragment("新内容"), PortalCategory.Container);

        // 同 ID 再注册：字典整体替换条目（类别也随之改变），不产生重复。
        Assert.Empty(service.GetPortals(PortalCategory.Overlay));
        var containers = service.GetPortals(PortalCategory.Container);
        Assert.Single(containers);
    }

    // ------------------------------------------------------------------
    // 注销（清空）
    // ------------------------------------------------------------------

    [Fact]
    public void 注销门户应移除内容并触发所属类别事件()
    {
        var service = CreateService();
        service.RegisterPortal("a", Fragment("甲"), PortalCategory.Overlay);

        var changed = new List<PortalCategory>();
        service.OnPortalsCategoryChanged += changed.Add;

        service.UnregisterPortal("a");

        Assert.Empty(service.GetPortals(PortalCategory.Overlay));
        Assert.Equal([PortalCategory.Overlay], changed);
    }

    [Fact]
    public void 注销未注册的门户不应触发任何事件()
    {
        var service = CreateService();

        var changed = new List<PortalCategory>();
        service.OnPortalsCategoryChanged += changed.Add;

        service.UnregisterPortal("不存在");

        Assert.Empty(changed);
    }

    [Fact]
    public void 注销应只通知所属类别的宿主()
    {
        var service = CreateService();
        service.RegisterPortal("c1", Fragment("容器"), PortalCategory.Container);
        service.RegisterPortal("o1", Fragment("浮层"), PortalCategory.Overlay);

        var changed = new List<PortalCategory>();
        service.OnPortalsCategoryChanged += changed.Add;

        service.UnregisterPortal("c1");

        Assert.Equal([PortalCategory.Container], changed);
        Assert.Single(service.GetPortals(PortalCategory.Overlay));
    }

    // ------------------------------------------------------------------
    // 内容更新与刷新
    // ------------------------------------------------------------------

    [Fact]
    public void 更新未注册门户内容应抛InvalidOperationException()
    {
        var service = CreateService();

        Assert.Throws<InvalidOperationException>(() => service.UpdatePortalContent("缺失", Fragment("x")));
    }

    [Fact]
    public void 更新内容应替换片段并保留类别与插入顺序同时触发类别事件()
    {
        var service = CreateService();
        service.RegisterPortal("a", Fragment("旧"), PortalCategory.Overlay);
        service.RegisterPortal("b", Fragment("后"), PortalCategory.Overlay);

        var changed = new List<PortalCategory>();
        service.OnPortalsCategoryChanged += changed.Add;

        var 新片段 = Fragment("新");
        service.UpdatePortalContent("a", 新片段);

        var portals = service.GetPortals(PortalCategory.Overlay);
        Assert.Equal(["a", "b"], portals.Select(p => p.Key)); // 顺序不变
        Assert.Same(新片段, portals[0].Value);                 // 内容已替换
        Assert.Equal([PortalCategory.Overlay], changed);
    }

    [Fact]
    public void 刷新门户应仅触发所属类别事件而不替换片段()
    {
        var service = CreateService();
        var 片段 = Fragment("甲");
        service.RegisterPortal("a", 片段, PortalCategory.Container);

        var changed = new List<PortalCategory>();
        service.OnPortalsCategoryChanged += changed.Add;

        service.RefreshPortal("a");

        Assert.Equal([PortalCategory.Container], changed);
        // 刷新不改动内容：仍是注册时那个片段。
        Assert.Same(片段, service.GetPortals(PortalCategory.Container)[0].Value);
    }

    [Fact]
    public void 刷新未注册门户应静默忽略()
    {
        var service = CreateService();

        var changed = new List<PortalCategory>();
        service.OnPortalsCategoryChanged += changed.Add;

        service.RefreshPortal("不存在");

        Assert.Empty(changed);
    }

    // ------------------------------------------------------------------
    // 渲染通知
    // ------------------------------------------------------------------

    [Fact]
    public void 通知门户已渲染应原样转发ID()
    {
        var service = CreateService();

        var rendered = new List<string>();
        service.OnPortalRendered += rendered.Add;

        service.NotifyPortalRendered("a");

        Assert.Equal(["a"], rendered);
    }

    // ------------------------------------------------------------------
    // 宿主计数（#545 回归）
    // ------------------------------------------------------------------

    [Fact]
    public void 默认没有宿主()
    {
        var service = CreateService();

        Assert.False(service.HasHost);
    }

    [Fact]
    public void 注册一个宿主即为有宿主()
    {
        var service = CreateService();

        service.RegisterHost();

        Assert.True(service.HasHost);
    }

    [Fact]
    public void 两个宿主时先释放一个仍保留另一个()
    {
        // PortalHost 组合了 Container 与 Overlay 两个类别宿主，二者都注册；
        // 一个离去不得带走另一个的注册。
        var service = CreateService();
        service.RegisterHost();
        service.RegisterHost();

        service.UnregisterHost();

        Assert.True(service.HasHost);
    }

    [Fact]
    public void 布局切换应先注册新宿主再释放旧宿主且仍有宿主()
    {
        // #545 的布局交换时序：新页面宿主初始化，随后旧布局宿主释放。
        var service = CreateService();
        service.RegisterHost();   // 旧布局的宿主
        service.RegisterHost();   // 新页面的宿主
        service.UnregisterHost(); // 旧布局释放

        Assert.True(service.HasHost);
    }

    [Fact]
    public void 最后一个宿主离开后应清空注册()
    {
        var service = CreateService();
        service.RegisterHost();
        service.RegisterHost();

        service.UnregisterHost();
        service.UnregisterHost();

        Assert.False(service.HasHost);
    }

    [Fact]
    public void 没有对应注册的释放不应毒化后来的注册()
    {
        // 多余释放不得把计数压成负数，否则下一个真宿主注册后仍自称缺失。
        var service = CreateService();

        service.UnregisterHost();
        service.UnregisterHost();
        service.RegisterHost();

        Assert.True(service.HasHost);
    }

    [Fact]
    public void 无宿主时注册门户应只警告一次且新宿主到来后重置闩锁()
    {
        var logger = new CapturingLogger();
        var service = new PortalService(logger);

        service.RegisterPortal("a", Fragment("甲"), PortalCategory.Overlay);
        service.RegisterPortal("b", Fragment("乙"), PortalCategory.Overlay);
        Assert.Single(logger.Messages); // "只警告一次"闩锁生效

        service.RegisterHost();          // 新宿主到来清闩锁
        service.UnregisterHost();        // 宿主又全部离开
        service.RegisterPortal("c", Fragment("丙"), PortalCategory.Overlay);

        Assert.Equal(2, logger.Messages.Count);
        Assert.All(logger.Messages, m => Assert.Contains("PortalHost", m));
    }

    /// <summary>记录格式化后消息的测试日志器。</summary>
    private sealed class CapturingLogger : ILogger<PortalService>
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Enqueue(formatter(state, exception));
    }
}

/// <summary>
/// 基于 ComponentTestRenderer 的 PortalHost 渲染测试：挂载组合宿主，验证已注册门户
/// 确实被两个类别宿主渲染出 data-portal-id 包裹元素，且宿主完成注册计数。
/// </summary>
public class PortalHostRenderTests
{
    [Fact]
    public async Task 挂载PortalHost应渲染已注册门户并注册宿主()
    {
        var portalService = new PortalService(NullLogger<PortalService>.Instance);
        portalService.RegisterPortal(
            "p1",
            builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddContent(1, "门户内容");
                builder.CloseElement();
            },
            PortalCategory.Overlay);

        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<IPortalService>(portalService);

        var renderer = new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);

        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var markup = renderer.Markup();

        // 组合宿主 = Container + Overlay 两个类别宿主；Overlay 门户应出现在包装 div 中。
        Assert.Contains("data-portal-id=\"p1\"", markup);
        Assert.Contains("data-portal-category=\"Overlay\"", markup);
        Assert.Contains("class=\"atom-portal\"", markup);
        Assert.Contains("门户内容", markup);

        // 两个类别宿主都应在服务上注册计数。
        Assert.True(portalService.HasHost);

        // 注册门户已触发过渲染通知（内容组件据此确认 ElementRef 有效）。
        var rendered = new List<string>();
        portalService.OnPortalRendered += rendered.Add;
        portalService.NotifyPortalRendered("p1");
        Assert.Equal(["p1"], rendered);
    }
}
