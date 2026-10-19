using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Overlay;

/// <summary>
/// NativeOverlayService 的策略解析与 interop 调用契约测试：能力探测只缓存肯定结果、
/// 探测失败后不再解析为原生策略、各对话框操作的标识符与参数、setup 句柄的 dispose 语义、
/// 释放后守卫与断线静默的分工，以及 DI 生命周期。
/// </summary>
public class NativeOverlayServiceTests
{
    private static ElementReference Element(string id = "dialog") => new(id);

    private static (NativeOverlayService Service, NoopJavaScript Js) CreateService(
        OverlayRenderingStrategy defaultStrategy = OverlayRenderingStrategy.JavaScript)
    {
        var js = new NoopJavaScript();
        var options = new OverlayRenderingOptions { DefaultStrategy = defaultStrategy };
        return (new NativeOverlayService(js, options), js);
    }

    private static (string Identifier, object?[]? Args) SingleInvocation(NoopJavaScript js, string identifier) =>
        Assert.Single(js.Invocations, i => i.Identifier == identifier);

    // ------------------------------------------------------------------
    // 能力探测
    // ------------------------------------------------------------------

    [Fact]
    public async Task 探测返回false时应回退且后续Native请求解析为JavaScript()
    {
        var (service, js) = CreateService();

        // NoopJavaScript 对 bool 返回 default（false）：模拟"浏览器不支持"的定论。
        Assert.False(await service.IsDialogSupportedAsync());
        SingleInvocation(js, "nativeDialog.supportsNativeDialog");

        Assert.Equal(OverlayRenderingStrategy.JavaScript, service.ResolveStrategy(OverlayRenderingStrategy.Native));
    }

    [Fact]
    public async Task 探测返回true时应缓存且不再重复派发探测()
    {
        var js = new ProbeJs(supportsDialog: true);
        var service = new NativeOverlayService(js, new OverlayRenderingOptions());

        Assert.True(await service.IsDialogSupportedAsync());
        Assert.True(await service.IsDialogSupportedAsync());

        // 肯定结果被缓存：探测只派发一次。
        Assert.Single(js.Invocations, i => i.Identifier == "nativeDialog.supportsNativeDialog");

        // 支持时不得触发回退。
        Assert.Equal(OverlayRenderingStrategy.Native, service.ResolveStrategy(OverlayRenderingStrategy.Native));
    }

    [Fact]
    public async Task interop不可用时应按不支持处理且不触发回退()
    {
        var js = new ProbeJs(supportsDialog: true, failInterop: true);
        var service = new NativeOverlayService(js, new OverlayRenderingOptions());

        // 预渲染/断线期间的 false 是瞬时失败，不得缓存为定论——策略仍可解析为 Native。
        Assert.False(await service.IsDialogSupportedAsync());
        Assert.Equal(OverlayRenderingStrategy.Native, service.ResolveStrategy(OverlayRenderingStrategy.Native));
    }

    // ------------------------------------------------------------------
    // 策略解析
    // ------------------------------------------------------------------

    [Fact]
    public void 请求策略应压过全局默认且空请求走默认()
    {
        var (service, _) = CreateService(OverlayRenderingStrategy.Native);

        Assert.Equal(OverlayRenderingStrategy.JavaScript, service.ResolveStrategy(OverlayRenderingStrategy.JavaScript));
        Assert.Equal(OverlayRenderingStrategy.Native, service.ResolveStrategy(null));
    }

    [Fact]
    public void 显式回退后所有Native请求都应解析为JavaScript()
    {
        var (service, _) = CreateService(OverlayRenderingStrategy.Native);

        service.FallBackToJavaScript();

        Assert.Equal(OverlayRenderingStrategy.JavaScript, service.ResolveStrategy(null));
        Assert.Equal(OverlayRenderingStrategy.JavaScript, service.ResolveStrategy(OverlayRenderingStrategy.Native));
        // JavaScript 请求本就不受影响。
        Assert.Equal(OverlayRenderingStrategy.JavaScript, service.ResolveStrategy(OverlayRenderingStrategy.JavaScript));
    }

    // ------------------------------------------------------------------
    // 对话框操作
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开与关闭应转发到原生对话框模块且携带返回值()
    {
        var (service, js) = CreateService();

        await service.ShowDialogAsync(Element());
        await service.CloseDialogAsync(Element(), "ok");

        var show = SingleInvocation(js, "nativeDialog.showModal");
        Assert.NotNull(show.Args![0]);

        var close = SingleInvocation(js, "nativeDialog.closeDialog");
        Assert.NotNull(close.Args![0]);
        Assert.Equal("ok", close.Args![1]);

        // 默认 returnValue 为 null 也照实转发。
        await service.CloseDialogAsync(Element());
        Assert.Equal(2, js.Invocations.Count(i => i.Identifier == "nativeDialog.closeDialog"));
        Assert.Null(js.Invocations.Last(i => i.Identifier == "nativeDialog.closeDialog").Args![1]);
    }

    [Fact]
    public async Task 聚焦对话框与触发器应分别转发到对应函数()
    {
        var (service, js) = CreateService();

        await service.FocusDialogAsync(Element());
        await service.FocusTriggerAsync(Element("trigger"));

        Assert.NotNull(SingleInvocation(js, "nativeDialog.focusDialog").Args![0]);
        Assert.NotNull(SingleInvocation(js, "nativeDialog.focusElement").Args![0]);
    }

    [Fact]
    public async Task Setup句柄释放应调用JS侧dispose并释放引用()
    {
        var (service, js) = CreateService();

        var handle = await service.SetupDialogAsync(Element(), new object());
        SingleInvocation(js, "nativeDialog.setupDialog");

        await handle.DisposeAsync();

        // NoopJavaScript 把自身作为清理句柄返回，dispose 记录在同一清单里。
        SingleInvocation(js, "dispose");
    }

    [Fact]
    public async Task 对话框操作在断线形态下应静默而非抛出()
    {
        var js = new ProbeJs(supportsDialog: true, failInterop: true);
        var service = new NativeOverlayService(js, new OverlayRenderingOptions());

        // 这些方法的 try/catch 过滤罩住 InvalidOperationException（预渲染）：不得外抛。
        await service.ShowDialogAsync(Element());
        await service.CloseDialogAsync(Element());
        await service.FocusDialogAsync(Element());
        await service.FocusTriggerAsync(Element());
    }

    [Fact]
    public async Task 释放后探测与Setup应抛ObjectDisposedException且二次释放直接返回()
    {
        var (service, _) = CreateService();
        await service.DisposeAsync();

        // 探测路径的 ObjectDisposedException 位于 GetModuleAsync（try 之外不可达——
        // 它在 try 内被吞并返回 false），而 Setup 刻意不加守卫：失败必须让调用方察觉。
        Assert.False(await service.IsDialogSupportedAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.SetupDialogAsync(Element(), new object()));

        await service.DisposeAsync();
    }

    [Fact]
    public void AddAtomBlazorPrimitives应把原生浮层服务注册为Scoped()
    {
        var services = new ServiceCollection();
        services.AddAtomBlazorPrimitives();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(INativeOverlayService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(NativeOverlayService), descriptor.ImplementationType);
    }

    /// <summary>对指定探测标识符返回受控结果的假运行时（兼作自身模块引用）；
    /// failInterop 模拟预渲染/断线。</summary>
    private sealed class ProbeJs(bool supportsDialog, bool failInterop = false) : IJSRuntime, IJSObjectReference
    {
        internal List<(string Identifier, object?[]? Args)> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (failInterop)
            {
                throw new InvalidOperationException("JS interop unavailable (prerender).");
            }

            Invocations.Add((identifier, args));

            if (typeof(TValue) == typeof(IJSObjectReference))
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            if (typeof(TValue) == typeof(bool) && identifier == "nativeDialog.supportsNativeDialog")
            {
                return ValueTask.FromResult((TValue)(object)supportsDialog);
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
