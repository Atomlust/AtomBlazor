using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AtomBlazor.Tests.Focus;

/// <summary>
/// FocusManager 的 interop 调用契约测试：焦点捕获派发与初始焦点模式映射、
/// 清理句柄的 apply 调用、首/末聚焦转发、RestoreFocus 的静默语义，
/// 以及释放守卫与 DI 生命周期。interop 由 NoopJavaScript 记录调用清单，
/// 不做真实 JS 执行（键盘循环的数学在 focus-trap.js 内，由浏览器侧守护）。
/// </summary>
public class FocusManagerTests
{
    private static ElementReference Element(string id = "container") => new(id);

    /// <summary>每次测试用全新的运行时实例：PrimitiveModules 按 IJSRuntime 缓存模块，
    /// 隔离实例即可隔离缓存。</summary>
    private static (FocusManager Manager, NoopJavaScript Js) CreateManager()
    {
        var js = new NoopJavaScript();
        return (new FocusManager(js), js);
    }

    private static (string Identifier, object?[]? Args) SingleInvocation(NoopJavaScript js, string identifier) =>
        Assert.Single(js.Invocations, i => i.Identifier == identifier);

    // ------------------------------------------------------------------
    // TrapFocus
    // ------------------------------------------------------------------

    [Fact]
    public async Task TrapFocus应加载bundle并派发createFocusTrap默认首焦点模式()
    {
        var (manager, js) = CreateManager();

        await manager.TrapFocus(Element());

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains(PrimitiveModules.ModulePath, (string?)import.Args![0]);

        var create = SingleInvocation(js, "focusTrap.createFocusTrap");
        Assert.NotNull(create.Args![0]);                                  // container
        Assert.Equal("first", create.Args![1]);                           // 默认模式
        Assert.Null(create.Args![2]);                                     // 无显式初始元素
    }

    [Fact]
    public async Task TrapFocus应把三种初始焦点模式映射为对应的JS模式串()
    {
        var cases = new (FocusTrapInitialFocus Focus, string Mode)[]
        {
            (FocusTrapInitialFocus.FirstFocusable, "first"),
            (FocusTrapInitialFocus.Container, "container"),
            (FocusTrapInitialFocus.None, "none"),
        };

        foreach (var (focus, mode) in cases)
        {
            var (manager, js) = CreateManager();

            await manager.TrapFocus(Element(), focus, Element("explicit"));

            var create = SingleInvocation(js, "focusTrap.createFocusTrap");
            Assert.Equal(mode, create.Args![1]);
            Assert.NotNull(create.Args![2]); // 显式元素随调用一并送达
        }
    }

    [Fact]
    public async Task 捕获句柄释放应调用JS侧apply清理函数()
    {
        var (manager, js) = CreateManager();

        var handle = await manager.TrapFocus(Element());
        await handle.DisposeAsync();

        // NoopJavaScript 把自己作为 IJSObjectReference 返回，故 apply 记录在同一清单里。
        SingleInvocation(js, "apply");
    }

    // ------------------------------------------------------------------
    // RestoreFocus / FocusFirst / FocusLast
    // ------------------------------------------------------------------

    [Fact]
    public async Task RestoreFocus在null时不应产生任何interop()
    {
        var (manager, js) = CreateManager();

        await manager.RestoreFocus(null);

        Assert.Empty(js.Invocations);
    }

    [Fact]
    public async Task RestoreFocus对不可用元素应静默不抛()
    {
        var (manager, _) = CreateManager();

        // 元素可能已不存在：FocusAsync 的失败必须被吞下，与源实现一致。
        await manager.RestoreFocus(Element());
    }

    [Fact]
    public async Task FocusFirst与FocusLast应各自转发到焦点捕获模块()
    {
        var (manager, js) = CreateManager();

        await manager.FocusFirst(Element());
        await manager.FocusLast(Element());

        Assert.NotNull(SingleInvocation(js, "focusTrap.focusFirst").Args![0]);
        Assert.NotNull(SingleInvocation(js, "focusTrap.focusLast").Args![0]);
    }

    // ------------------------------------------------------------------
    // 释放守卫与 DI
    // ------------------------------------------------------------------

    [Fact]
    public async Task 释放后任何interop入口应抛ObjectDisposedException()
    {
        var (manager, _) = CreateManager();
        await manager.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.TrapFocus(Element()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.FocusFirst(Element()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.FocusLast(Element()));

        // 二次释放应直接返回。
        await manager.DisposeAsync();
    }

    [Fact]
    public void AddAtomBlazorPrimitives应把焦点管理注册为Scoped()
    {
        var services = new ServiceCollection();
        services.AddAtomBlazorPrimitives();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IFocusManager));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(FocusManager), descriptor.ImplementationType);
    }
}
