using AtomBlazor.Primitives;
using System.Text.Json;
using AtomBlazor.Primitives.Extensions;
using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Floating;

/// <summary>
/// PositioningService 的转发面测试：验证四个定位调用以 bundle 命名空间点分标识符
/// 派发、选项按其运行时类型送达、结果 JSON 映射回强类型，以及释放后的守卫。
/// 用带记录与固定 JSON 应答的测试替身，不做真实 JS 执行。
/// </summary>
public class PositioningServiceTests
{
    /// <summary>记录每次调用；对 JsonElement 返回固定应答，对 IJSObjectReference 返回自身。</summary>
    private sealed class RecordingJavaScript : IJSRuntime, IJSObjectReference
    {
        internal List<(string Identifier, object?[]? Args)> Invocations { get; } = [];

        /// <summary>positioning.computePosition 的 JSON 应答；null 时返回 default。</summary>
        internal string? ComputeResponse { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add((identifier, args));

            if (typeof(TValue) == typeof(IJSObjectReference))
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            if (typeof(TValue) == typeof(JsonElement) && ComputeResponse is not null)
            {
                using var document = JsonDocument.Parse(ComputeResponse);
                return ValueTask.FromResult((TValue)(object)document.RootElement.Clone());
            }

            return ValueTask.FromResult<TValue>(default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static ElementReference Element(string id) => new(id);

    private static (PositioningService Service, RecordingJavaScript Js) CreateService()
    {
        var js = new RecordingJavaScript();
        return (new PositioningService(js), js);
    }

    /// <summary>把某次记录调用的第 index 个参数按其运行时类型转为 JsonElement。</summary>
    private static JsonElement ArgsAsJson(RecordingJavaScript js, string identifier, int index)
    {
        var value = js.Invocations.Single(i => i.Identifier == identifier).Args![index]!;
        return JsonSerializer.SerializeToElement(value, value.GetType());
    }

    // ------------------------------------------------------------------
    // ComputePositionAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ComputePosition应按默认选项转发并映射完整结果()
    {
        var (service, js) = CreateService();
        js.ComputeResponse = """
            { "x": 10.5, "y": -20.25, "placement": "top-end",
              "transformOrigin": "top left", "strategy": "fixed" }
            """;

        var result = await service.ComputePositionAsync(Element("r"), Element("f"));

        var call = js.Invocations.Single(i => i.Identifier == "positioning.computePosition");
        Assert.Equal(Element("r"), call.Args![0]);
        Assert.Equal(Element("f"), call.Args![1]);

        // 未传选项时使用 PositioningOptions 的默认值拼出的 JS 侧选项对象。
        var options = ArgsAsJson(js, "positioning.computePosition", 2);
        Assert.Equal("bottom", options.GetProperty("placement").GetString());
        Assert.Equal(8, options.GetProperty("offset").GetInt32());
        Assert.True(options.GetProperty("flip").GetBoolean());
        Assert.True(options.GetProperty("shift").GetBoolean());
        Assert.Equal(8, options.GetProperty("padding").GetInt32());
        Assert.Equal("absolute", options.GetProperty("strategy").GetString());
        Assert.False(options.GetProperty("matchReferenceWidth").GetBoolean());

        Assert.Equal(10.5, result.X);
        Assert.Equal(-20.25, result.Y);
        Assert.Equal("top-end", result.Placement);
        Assert.Equal("top left", result.TransformOrigin);
        Assert.Equal("fixed", result.Strategy);
    }

    [Fact]
    public async Task ComputePosition应容忍结果缺字段并回落到请求选项()
    {
        var (service, js) = CreateService();
        js.ComputeResponse = """{ "x": 1, "y": 2, "placement": "left" }""";

        var result = await service.ComputePositionAsync(
            Element("r"), Element("f"), new PositioningOptions { Placement = "top", Strategy = PositioningStrategy.Fixed });

        // 请求里的 strategy 会随调用送达 JS；应答缺 transformOrigin/strategy 时按选项回落。
        var sent = ArgsAsJson(js, "positioning.computePosition", 2);
        Assert.Equal("top", sent.GetProperty("placement").GetString());

        Assert.Equal(1, result.X);
        Assert.Equal(2, result.Y);
        Assert.Equal("left", result.Placement);
        Assert.Null(result.TransformOrigin);
        Assert.Equal("fixed", result.Strategy);
    }

    // ------------------------------------------------------------------
    // ApplyPositionAsync / HidePositionAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApplyPosition应原样转发元素结果与可见标志()
    {
        var (service, js) = CreateService();
        var position = new PositionResult { X = 1, Y = 2, Placement = "left" };

        await service.ApplyPositionAsync(Element("f"), position, makeVisible: true);

        var call = js.Invocations.Single(i => i.Identifier == "positioning.applyPosition");
        Assert.Equal(Element("f"), call.Args![0]);
        Assert.Same(position, call.Args![1]);
        Assert.Equal(true, call.Args![2]);
    }

    [Fact]
    public async Task HidePosition应转发单个元素()
    {
        var (service, js) = CreateService();

        await service.HidePositionAsync(Element("f"));

        var call = js.Invocations.Single(i => i.Identifier == "positioning.hidePosition");
        Assert.Equal(Element("f"), call.Args![0]);
    }

    // ------------------------------------------------------------------
    // AutoUpdateAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task AutoUpdate应返回句柄且释放时经apply停掉()
    {
        var (service, js) = CreateService();
        var options = new PositioningOptions { Placement = "right", Offset = 2, Strategy = PositioningStrategy.Fixed };

        var handle = await service.AutoUpdateAsync(Element("r"), Element("f"), options);

        var call = js.Invocations.Single(i => i.Identifier == "positioning.autoUpdate");
        Assert.Equal(Element("r"), call.Args![0]);
        Assert.Equal(Element("f"), call.Args![1]);
        var sent = ArgsAsJson(js, "positioning.autoUpdate", 2);
        Assert.Equal("right", sent.GetProperty("placement").GetString());
        Assert.Equal(2, sent.GetProperty("offset").GetInt32());
        Assert.Equal("fixed", sent.GetProperty("strategy").GetString());

        // 句柄释放走 JS 返回对象自己的 apply 方法——无命名空间前缀。
        await handle.DisposeAsync();
        Assert.Contains(js.Invocations, i => i.Identifier == "apply");
    }

    [Fact]
    public async Task 句柄释放失败应保持静默()
    {
        // 白盒构造内部句柄（AutoUpdateHandle）：清理引用每次调用都抛 ObjectDisposedException
        // （断线/双释放的真实形态），句柄释放必须静默吞掉。
        var handleType = typeof(PositioningService)
            .GetNestedType("AutoUpdateHandle", System.Reflection.BindingFlags.NonPublic)!;
        // 嵌套私有类的构造函数本身是 public 成员，默认 Binding 即可取到。
        var ctor = handleType.GetConstructors().Single();

        var handle = (IAsyncDisposable)ctor.Invoke([new FailingReference()]);
        await handle.DisposeAsync();
    }

    // ------------------------------------------------------------------
    // 释放守卫与 DI
    // ------------------------------------------------------------------

    [Fact]
    public async Task 释放后任何调用应抛ObjectDisposedException()
    {
        var (service, _) = CreateService();
        await service.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            service.ComputePositionAsync(Element("r"), Element("f")));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            service.ApplyPositionAsync(Element("f"), new PositionResult()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            service.HidePositionAsync(Element("f")));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            service.AutoUpdateAsync(Element("r"), Element("f")));

        // 二次释放应直接返回，不再触碰 interop。
        await service.DisposeAsync();
    }

    [Fact]
    public void AddAtomBlazorPrimitives应把定位服务注册为Scoped()
    {
        var services = new ServiceCollection();
        services.AddAtomBlazorPrimitives(o => o.DefaultStrategy = OverlayRenderingStrategy.Native);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPositioningService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(PositioningService), descriptor.ImplementationType);

        // 浮层选项单例同时生效（就地配置模式）。
        var overlay = Assert.Single(services, d => d.ServiceType == typeof(OverlayRenderingOptions));
        Assert.Equal(ServiceLifetime.Singleton, overlay.Lifetime);
        Assert.Equal(OverlayRenderingStrategy.Native,
            ((OverlayRenderingOptions)overlay.ImplementationInstance!).DefaultStrategy);
    }

    /// <summary>每次 interop 都抛 ObjectDisposedException 的引用，模拟已断开的句柄。</summary>
    private sealed class FailingReference : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new ObjectDisposedException(nameof(FailingReference));
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new ObjectDisposedException(nameof(FailingReference));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
