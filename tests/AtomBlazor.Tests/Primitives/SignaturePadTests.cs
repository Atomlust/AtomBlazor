using AtomBlazor.Primitives;
using System.Text;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using Pad = AtomBlazor.Primitives.SignaturePadPrimitive;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// SignaturePad 基元测试：canvas 参数面渲染（data 特性不变文化、data-colour、
/// data-disabled、touch-action 样式合并与 splat）、经共享 primitives bundle 的
/// signaturePad.initialize/dispose 载荷沿（元素引用 + DotNetObjectReference + 32 位 GUID padId）、
/// Clear/Undo/IsEmpty/SetStrokes 的 interop 派发与 OnChange 上报、JsSignatureChanged 回调沿与
/// 释放后吞掉、toSvgStream/toPngStream/toStrokesStream 的流读取（32MB 封顶、UTF-8 解码、
/// data URL 组装、Web 约定反序列化）、预渲染 InvalidOperationException 容错，以及
/// SignaturePoint/SignatureStroke 记录类型的值语义。
/// headless 限制先例：墨迹引擎在浏览器侧，绘制数学由 node 侧 signature-pad-module 测试覆盖；
/// ElementReference 无真实 DOM，指针事件沿不可用，只反射断言 interop 载荷。
/// </summary>
public class SignaturePadTests
{
    /// <summary>从调用记录里取某个标识符的唯一一次派发。</summary>
    private static (string Identifier, object?[]? Args) Single(
        IEnumerable<(string Identifier, object?[]? Args)> invocations, string identifier) =>
        Assert.Single(invocations, i => i.Identifier == identifier);

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer)
    {
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>挂载基元，默认不带 OnChange 回调。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Pad Pad)> MountAsync(
        Dictionary<string, object?>? parameters = null)
    {
        var (renderer, js) = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(Pad), parameters ?? []));
        await FlushAsync(renderer);
        return (renderer, js, (Pad)component);
    }

    /// <summary>挂载基元并挂上收集 OnChange 的回调。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Pad Pad, List<bool> Reports)> MountWithChangeAsync()
    {
        var reports = new List<bool>();
        var (renderer, js, pad) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Pad.OnChange)] = EventCallback.Factory.Create<bool>(new object(), e => reports.Add(e)),
        });
        return (renderer, js, pad, reports);
    }

    /// <summary>从 signaturePad.initialize 载荷里取 padId（32 位 GUID "N"）。</summary>
    private static string PadId(IEnumerable<(string Identifier, object?[]? Args)> invocations)
    {
        var initialize = Single(invocations, "signaturePad.initialize");
        return Assert.IsType<string>(initialize.Args![2]);
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 基元默认画布渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync();
        await Verify(renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 参数面渲染
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认参数应写出不变文化的data特性且无禁用标记()
    {
        var (renderer, _, _) = await MountAsync();
        var markup = renderer.Markup();

        Assert.Contains("<canvas", markup);
        Assert.Contains("data-min-width=\"0.5\"", markup);
        Assert.Contains("data-max-width=\"2.5\"", markup);
        Assert.Contains("data-velocity-weight=\"0.7\"", markup);
        Assert.DoesNotContain("data-disabled", markup);
        Assert.Contains("touch-action: none", markup);
    }

    [Fact]
    public async Task 自定义参数应逐项落到data特性上()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Pad.MinWidth)] = 1.25,
            [nameof(Pad.MaxWidth)] = 10.5,
            [nameof(Pad.VelocityWeight)] = 0.25,
            [nameof(Pad.StrokeColor)] = "#ff0000",
            [nameof(Pad.Disabled)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("data-min-width=\"1.25\"", markup);
        Assert.Contains("data-max-width=\"10.5\"", markup);
        Assert.Contains("data-velocity-weight=\"0.25\"", markup);
        Assert.Contains("data-colour=\"#ff0000\"", markup);
        Assert.Contains("data-disabled=\"true\"", markup);
    }

    [Fact]
    public async Task 附加属性应展开到画布且用户样式与触控样式合并()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Pad.AdditionalAttributes)] = new Dictionary<string, object>
            {
                ["class"] = "atom:h-40 atom:w-full",
                ["role"] = "img",
                ["aria-label"] = "签名区",
                ["style"] = "outline: 2px solid red",
            },
        });

        var markup = renderer.Markup();
        Assert.Contains("class=\"atom:h-40 atom:w-full\"", markup);
        Assert.Contains("role=\"img\"", markup);
        Assert.Contains("aria-label=\"签名区\"", markup);
        // MergeStyles：功能性样式与用户样式合并为单一 style 特性，不再经 splat 重复写出。
        Assert.Contains("touch-action: none", markup);
        Assert.Contains("outline: 2px solid red", markup);
    }

    // ------------------------------------------------------------------
    // JS 初始化沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 挂载应经共享primitives包以元素引用与dotNetRef和GUID初始化()
    {
        var (_, js, pad) = await MountAsync();

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains("primitives.js", (string)import.Args![0]!);

        var initialize = Single(js.Invocations, "signaturePad.initialize");
        Assert.Equal(3, initialize.Args!.Length);
        Assert.IsType<ElementReference>(initialize.Args![0]);

        var dotNetRef = Assert.IsType<DotNetObjectReference<Pad>>(initialize.Args![1]);
        Assert.Same(pad, dotNetRef.Value);

        var padId = Assert.IsType<string>(initialize.Args![2]);
        Assert.Matches("^[0-9a-f]{32}$", padId);
    }

    [Fact]
    public async Task 释放应派发同padId的dispose()
    {
        var (renderer, js, pad) = await MountAsync();
        var padId = PadId(js.Invocations);

        await renderer.Dispatcher.InvokeAsync(pad.DisposeAsync);

        var dispose = Single(js.Invocations, "signaturePad.dispose");
        Assert.Single(dispose.Args!);
        Assert.Equal(padId, dispose.Args![0]);
    }

    [Fact]
    public async Task 清除应派发padId并沿成功回调上报为空()
    {
        var (renderer, js, pad, reports) = await MountWithChangeAsync();
        var padId = PadId(js.Invocations);

        await renderer.Dispatcher.InvokeAsync(pad.ClearAsync);

        var clear = Single(js.Invocations, "signaturePad.clear");
        Assert.Single(clear.Args!);
        Assert.Equal(padId, clear.Args![0]);
        Assert.Equal([true], reports);
    }

    [Fact]
    public async Task 撤销在无可撤沿返回false且不上报()
    {
        var (renderer, js, pad, reports) = await MountWithChangeAsync();
        var padId = PadId(js.Invocations);

        var removed = false;
        await renderer.Dispatcher.InvokeAsync(async () => removed = await pad.UndoAsync());

        // NoopJavaScript 对 InvokeAsync<bool> 返回 default：撤销失败沿。
        Assert.False(removed);
        Assert.Empty(reports);
        var undo = Single(js.Invocations, "signaturePad.undo");
        Assert.Single(undo.Args!);
        Assert.Equal(padId, undo.Args![0]);
    }

    [Fact]
    public async Task 判空应经互操作派发padId()
    {
        var (renderer, js, pad) = await MountAsync();
        var padId = PadId(js.Invocations);

        await renderer.Dispatcher.InvokeAsync(() => pad.IsEmptyAsync());

        var isEmpty = Single(js.Invocations, "signaturePad.isEmpty");
        Assert.Equal(padId, isEmpty.Args![0]);
    }

    [Fact]
    public async Task 设置笔画应在就绪沿派发空数组并以上报判空结果()
    {
        var (renderer, js, pad, reports) = await MountWithChangeAsync();
        var padId = PadId(js.Invocations);

        await renderer.Dispatcher.InvokeAsync(() => pad.SetStrokesAsync(null));

        var setStrokes = Single(js.Invocations, "signaturePad.setStrokes");
        Assert.Equal(2, setStrokes.Args!.Length);
        Assert.Equal(padId, setStrokes.Args[0]);
        var strokes = Assert.IsType<SignatureStroke[]>(setStrokes.Args[1]);
        Assert.Empty(strokes);
        // SetStrokes 成功后以 IsEmptyAsync 的结果上报；NoopJavaScript 返回 default(false)。
        Assert.Equal([false], reports);
    }

    [Fact]
    public async Task 回调入口应转发OnChange且释放后吞掉()
    {
        var (renderer, _, pad, reports) = await MountWithChangeAsync();

        await renderer.Dispatcher.InvokeAsync(() => pad.JsSignatureChanged(true));
        Assert.Equal([true], reports);

        await renderer.Dispatcher.InvokeAsync(pad.DisposeAsync);
        // 释放后的回调入口是空操作：不再转发。
        await renderer.Dispatcher.InvokeAsync(() => pad.JsSignatureChanged(false));
        Assert.Equal([true], reports);
    }

    [Fact]
    public async Task 释放后公开方法应变为空操作()
    {
        var (renderer, js, pad, _) = await MountWithChangeAsync();

        // 挂载沿上的 import+initialize 与释放沿无关，先清空再观测 dispose。
        js.Invocations.Clear();
        await renderer.Dispatcher.InvokeAsync(pad.DisposeAsync);
        var invocationsAfterDispose = js.Invocations.Count;
        js.Invocations.Clear();

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await pad.ClearAsync();
            await pad.SetStrokesAsync([new SignatureStroke([new SignaturePoint(1, 2, 3)])]);
            await pad.UndoAsync();
            await pad.IsEmptyAsync();
        });

        Assert.Empty(js.Invocations);
        // dispose 是释放沿上唯一的派发，且释放前没有任何导出/操作派发混入。
        Assert.Equal(1, invocationsAfterDispose);
    }

    [Fact]
    public async Task 预渲染期互操作不可用应容错且方法沿退化为空()
    {
        var js = new ThrowingJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        var renderer = new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);

        var reports = new List<bool>();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(Pad), new Dictionary<string, object?>
            {
                [nameof(Pad.OnChange)] = EventCallback.Factory.Create<bool>(new object(), e => reports.Add(e)),
            }));
        await FlushAsync(renderer);
        var pad = (Pad)component;

        // 初始化沿因 InvalidOperationException 失败：后续方法不得派发或上报。
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await pad.ClearAsync();
            await pad.SetStrokesAsync(null);
        });

        Assert.Contains(js.Invocations, i => i.Identifier == "import");
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "signaturePad.initialize");
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "signaturePad.clear");
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "signaturePad.setStrokes");
        Assert.Empty(reports);
    }

    // ------------------------------------------------------------------
    // 流式导出
    // ------------------------------------------------------------------

    [Fact]
    public async Task SVG导出应读流并按UTF8解码且以32MB封顶()
    {
        var (renderer, module, pad) = await MountStreamingAsync();
        module.StreamPayloads["signaturePad.toSvgStream"] = "<svg>t</svg>"u8.ToArray();
        var padId = PadId(module.Invocations);

        var svg = await renderer.Dispatcher.InvokeAsync(() => pad.GetSvgAsync());
        Assert.Equal("<svg>t</svg>", svg);

        var read = Single(module.Invocations, "signaturePad.toSvgStream");
        Assert.Single(read.Args!);
        Assert.Equal(padId, read.Args![0]);
        Assert.Equal(32 * 1024 * 1024, module.LastMaxBytes);
    }

    [Fact]
    public async Task 无流引用时SVG与笔画与PNG导出均应回落为空()
    {
        var (renderer, _, pad) = await MountStreamingAsync();

        Assert.Null(await renderer.Dispatcher.InvokeAsync(() => pad.GetSvgAsync()));
        Assert.Null(await renderer.Dispatcher.InvokeAsync(() => pad.GetPngAsync()));
        Assert.Null(await renderer.Dispatcher.InvokeAsync(() => pad.GetPngBytesAsync()));
        Assert.Empty(await renderer.Dispatcher.InvokeAsync(() => pad.GetStrokesAsync()));
    }

    [Fact]
    public async Task PNG导出应携带底色与墨色参数并组装dataURL()
    {
        var (renderer, module, pad) = await MountStreamingAsync();
        module.StreamPayloads["signaturePad.toPngStream"] = [1, 2, 3];
        var padId = PadId(module.Invocations);

        var bytes = await renderer.Dispatcher.InvokeAsync(() => pad.GetPngBytesAsync("#ffffff"));
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);

        var read = Single(module.Invocations, "signaturePad.toPngStream");
        Assert.Equal(3, read.Args!.Length);
        Assert.Equal(padId, read.Args[0]);
        Assert.Equal("#ffffff", read.Args[1]);
        Assert.Equal("#000000", read.Args[2]); // ink 默认参数

        var dataUrl = await renderer.Dispatcher.InvokeAsync(() => pad.GetPngAsync());
        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String([1, 2, 3])}", dataUrl);
    }

    [Fact]
    public async Task 笔画导出应按Web约定反序列化为记录类型()
    {
        var (renderer, module, pad) = await MountStreamingAsync();
        module.StreamPayloads["signaturePad.toStrokesStream"] =
            Encoding.UTF8.GetBytes("""[{"points":[{"x":1.5,"y":2.5,"t":100},{"x":3,"y":4,"t":120}]}]""");

        var strokes = await renderer.Dispatcher.InvokeAsync(() => pad.GetStrokesAsync());
        Assert.Single(strokes);
        Assert.Equal(2, strokes[0].Points.Count);
        Assert.Equal(new SignaturePoint(1.5, 2.5, 100), strokes[0].Points[0]);
        Assert.Equal(new SignaturePoint(3, 4, 120), strokes[0].Points[1]);
    }

    // ------------------------------------------------------------------
    // 记录类型语义
    // ------------------------------------------------------------------

    [Fact]
    public void 位置采样点应具值相等语义()
    {
        var a = new SignaturePoint(1, 2, 3);
        var b = new SignaturePoint(1, 2, 3);
        var c = new SignaturePoint(1, 2, 4);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.Equal(1, a.X);
        Assert.Equal(2, a.Y);
        Assert.Equal(3, a.T);
        var (x, y, t) = a;
        Assert.Equal((1.0, 2.0, 3.0), (x, y, t));
    }

    [Fact]
    public void 笔画记录按位置参数浅比较且with可替换采样()
    {
        var points = new List<SignaturePoint> { new(1, 2, 3) };
        var a = new SignatureStroke(points);
        var b = new SignatureStroke(points);
        var c = new SignatureStroke([new SignaturePoint(1, 2, 3)]);

        Assert.Equal(a, b);   // 同一列表引用：相等
        Assert.NotEqual(a, c); // 内容相同但列表实例不同：记录按属性默认比较器浅比较
        Assert.Same(points, a.Points);

        var cloned = a with { Points = [new SignaturePoint(9, 9, 9)] };
        Assert.NotEqual(a, cloned);
        Assert.Equal(9, cloned.Points[0].X);
        Assert.Contains("SignatureStroke", a.ToString());
    }

    // ------------------------------------------------------------------
    // 测试替身
    // ------------------------------------------------------------------

    /// <summary>预渲染替身：任何 interop 调用都以 InvalidOperationException 拒绝。</summary>
    private sealed class ThrowingJavaScript : IJSRuntime
    {
        internal List<(string Identifier, object?[]? Args)> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Invocations.Add((identifier, args));
            throw new InvalidOperationException("JS interop is not available during prerendering");
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    /// <summary>流式导出替身：点名标识符返回内存流，其余返回 default（与 NoopJavaScript 同构）。</summary>
    private sealed class StreamingJavaScript : IJSRuntime, IJSObjectReference
    {
        internal List<(string Identifier, object?[]? Args)> Invocations { get; } = [];
        internal Dictionary<string, byte[]> StreamPayloads { get; } = [];
        internal long LastMaxBytes { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add((identifier, args));
            if (typeof(TValue) == typeof(IJSObjectReference))
            {
                return new ValueTask<TValue>((TValue)(object)this);
            }
            if (typeof(TValue) == typeof(IJSStreamReference)
                && StreamPayloads.TryGetValue(identifier, out var bytes))
            {
                return new ValueTask<TValue>((TValue)(object)new FakeStream(bytes, max => LastMaxBytes = max));
            }
            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>内存流引用替身：记录调用方给出的封顶字节数。</summary>
    private sealed class FakeStream(byte[] payload, Action<long> recordMaxSize) : IJSStreamReference
    {
        public long Length => payload.LongLength;

        public ValueTask<Stream> OpenReadStreamAsync(
            long maxAllowedSize = 30720,
            CancellationToken cancellationToken = default)
        {
            recordMaxSize(maxAllowedSize);
            return ValueTask.FromResult<Stream>(new MemoryStream(payload));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>挂载基元到流式替身上并返回三者。</summary>
    private static async Task<(ComponentTestRenderer Renderer, StreamingJavaScript Js, Pad Pad)> MountStreamingAsync()
    {
        var js = new StreamingJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        var renderer = new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);

        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(Pad), []));
        await FlushAsync(renderer);
        return (renderer, js, (Pad)component);
    }
}
