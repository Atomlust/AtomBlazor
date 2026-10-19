using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Signature 组件测试：手绘/输入双路径——默认手绘沿（ToggleGroup 笔/键双项、
/// DrawHint 覆盖层、画布、撤销与清除按钮）、Kind 切换经组回调（None 与同值按压被忽略、
/// 切换清空对方路径并发 Empty）、输入沿（值回填、TypedSvg 序列化形状与 HTML 转义、
/// 空白回落 Empty）、ClearAsync 双路径沿与 Undo 按钮派发 signaturePad.undo、
/// 外部 Typed 值一次性采纳（打开即输入路径且不重复上报）、禁用与 Label/Input 组合结构
/// （atom:opacity-60、data-disabled、label for 与 input id 同源）、AllowTyped=false 收敛、
/// pad 缺席时导出沿回落。快照先以正则归一 atom-signature GUID 稳定 id。
/// headless 限制先例：墨迹引擎在浏览器侧，FocusAsync/真实指针沿不可用，interop 载荷经
/// NoopJavaScript 调用记录断言；绘制数学由 node 侧 signature-pad-module 测试覆盖。
/// </summary>
public class SignatureTests
{
    /// <summary>归一化 atom-signature GUID 稳定 id，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "atom-signature-[0-9a-f]{32}", "atom-signature-<id>");

    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer)
    {
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>挂载组件并收集 ValueChanged 上报。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Signature Component, List<SignatureValue?> Reports)> MountAsync(
        Dictionary<string, object?>? parameters = null)
    {
        var (renderer, js) = CreateRenderer();
        var reports = new List<SignatureValue?>();
        var dict = new Dictionary<string, object?>(parameters ?? [])
        {
            [nameof(Signature.ValueChanged)] = EventCallback.Factory.Create<SignatureValue?>(
                new object(), v => reports.Add(v)),
        };
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(Signature), dict));
        await FlushAsync(renderer);
        return (renderer, js, (Signature)component, reports);
    }

    private static (string Identifier, object?[]? Args) Single(NoopJavaScript js, string identifier) =>
        Assert.Single(js.Invocations, i => i.Identifier == identifier);

    /// <summary>从内部画板的最后一次 signaturePad.initialize 载荷里取 padId。</summary>
    private static string PadId(NoopJavaScript js) =>
        Assert.IsType<string>(js.Invocations
            .Last(i => i.Identifier == "signaturePad.initialize").Args![2]);

    /// <summary>触发组回调沿的 Kind 切换（与条目点击后组的上报同构）。</summary>
    private static Task SwitchKindAsync(ComponentTestRenderer renderer, SignatureKind kind) =>
        renderer.Dispatcher.InvokeAsync(async () =>
        {
            var group = renderer.FindComponent<ToggleGroup<SignatureKind>>();
            await group.ValueChanged.InvokeAsync(kind);
        });

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认手绘路径快照应与基线一致()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Signature.Label)] = "签名",
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 外部输入值采纳后的键盘路径快照应与基线一致()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Signature.Value)] = new SignatureValue
            {
                Kind = SignatureKind.Typed,
                Text = "张三",
            },
        });
        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 参数面与结构
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认手绘沿应含组画布提示与两按钮()
    {
        var (renderer, _, _, _) = await MountAsync();
        var markup = renderer.Markup();

        var group = renderer.FindComponent<ToggleGroup<SignatureKind>>();
        Assert.Equal(ToggleGroupType.Single, group.Type);
        Assert.Equal(ToggleVariant.Outline, group.Variant);
        Assert.Equal(ToggleSize.Small, group.Size);
        Assert.Equal(SignatureKind.Drawn, group.Value);

        Assert.Contains("<canvas", markup);
        Assert.Contains("touch-action: none", markup);
        Assert.Contains("Sign here", markup);       // Signature.DrawHint
        Assert.Contains("Undo last stroke", markup); // Signature.Undo
        Assert.Contains("Clear", markup);            // Signature.Clear
        Assert.Contains("How to sign", markup);      // Signature.MethodLabel
    }

    [Fact]
    public async Task 允许输入关闭时不应出现模式切换组()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Signature.AllowTyped)] = false,
        });

        var markup = renderer.Markup();
        Assert.DoesNotContain("How to sign", markup);
        Assert.Contains("<canvas", markup);
    }

    [Fact]
    public async Task 标签与输入应共用同一域id且禁用改变结构样式()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Signature.Label)] = "签名",
            [nameof(Signature.Disabled)] = true,
        });

        var markup = renderer.Markup();
        var match = System.Text.RegularExpressions.Regex.Match(markup, "atom-signature-[0-9a-f]{32}");
        Assert.True(match.Success);
        var fieldId = match.Value;
        Assert.Contains($"<label for=\"{fieldId}\"", markup);
        Assert.Contains("atom:opacity-60", markup);      // 禁用根类
        Assert.Contains("data-disabled=\"true\"", markup); // 画布禁用沿
    }

    // ------------------------------------------------------------------
    // Kind 切换
    // ------------------------------------------------------------------

    [Fact]
    public async Task 切到输入路径应换出画布为文本框并发空值()
    {
        var (renderer, _, _, reports) = await MountAsync();

        await SwitchKindAsync(renderer, SignatureKind.Typed);
        await FlushAsync(renderer);

        var markup = renderer.Markup();
        Assert.DoesNotContain("<canvas", markup);
        Assert.Contains("<input", markup);
        Assert.Contains("Type your full name", markup); // 预览位上的 TypeHint
        Assert.Equal([SignatureValue.Empty], reports);
    }

    [Fact]
    public async Task 切回手绘路径应换回画布且丢弃对方路径状态()
    {
        var (renderer, _, _, reports) = await MountAsync();

        await SwitchKindAsync(renderer, SignatureKind.Typed);
        await FlushAsync(renderer);
        var input = renderer.FindComponent<Input>();
        await renderer.Dispatcher.InvokeAsync(async () => await input.ValueChanged.InvokeAsync("李四"));
        Assert.Equal("李四", input.Value);

        reports.Clear();
        await SwitchKindAsync(renderer, SignatureKind.Drawn);
        await FlushAsync(renderer);

        Assert.Contains("<canvas", renderer.Markup());
        // 两条路径各自持有状态：切回即丢弃输入内容并上报一次 Empty。
        Assert.Equal([SignatureValue.Empty], reports);
    }

    [Theory]
    [InlineData(SignatureKind.None)]
    [InlineData(SignatureKind.Drawn)]
    public async Task 取消选中与同值按压应被忽略(SignatureKind ignored)
    {
        var (renderer, _, _, reports) = await MountAsync();

        await SwitchKindAsync(renderer, ignored);
        await FlushAsync(renderer);

        Assert.Contains("<canvas", renderer.Markup());
        Assert.Empty(reports);
    }

    // ------------------------------------------------------------------
    // 输入沿与序列化形状
    // ------------------------------------------------------------------

    [Fact]
    public async Task 输入姓名应产出Typed形态值与转义SVG()
    {
        var (renderer, _, _, reports) = await MountAsync();
        await SwitchKindAsync(renderer, SignatureKind.Typed);
        await FlushAsync(renderer);
        reports.Clear();

        var input = renderer.FindComponent<Input>();
        await renderer.Dispatcher.InvokeAsync(async () => await input.ValueChanged.InvokeAsync("Ab C<d"));

        var value = Assert.Single(reports);
        Assert.NotNull(value);
        Assert.Equal(SignatureKind.Typed, value.Kind);
        Assert.Equal("Ab C<d", value.Text);
        Assert.False(value.IsEmpty);
        // 6 个字符 -> width = max(240, 6*22) = 240；文本经 HTML 转义。
        Assert.Contains("width=\"240\" height=\"80\"", value.Svg);
        Assert.Contains("viewBox=\"0 0 240 80\"", value.Svg);
        Assert.Contains(">Ab C&lt;d</text>", value.Svg);
        Assert.Contains("fill=\"currentColor\"", value.Svg);
        Assert.Contains("font-size=\"40\"", value.Svg);
    }

    [Fact]
    public async Task 输入转空白应回落空值且预览显示提示()
    {
        var (renderer, _, _, reports) = await MountAsync();
        await SwitchKindAsync(renderer, SignatureKind.Typed);
        await FlushAsync(renderer);
        reports.Clear();

        var input = renderer.FindComponent<Input>();
        await renderer.Dispatcher.InvokeAsync(async () => await input.ValueChanged.InvokeAsync("  "));

        Assert.Equal([SignatureValue.Empty], reports);
        Assert.Contains("Type your full name", renderer.Markup());
    }

    [Fact]
    public async Task 外部输入值应一次性采纳且不重复上报()
    {
        var (renderer, _, component, reports) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Signature.Value)] = new SignatureValue
            {
                Kind = SignatureKind.Typed,
                Text = "张三",
            },
        });

        Assert.Contains("<input", renderer.Markup());
        Assert.Contains("张三", renderer.Markup());
        Assert.Empty(reports);
        Assert.NotNull(component.Value);

        // 后续参数变化不再改写内部路径状态（一次性采纳）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(Signature.Value)] = new SignatureValue
            {
                Kind = SignatureKind.Typed,
                Text = "王五",
            },
        }));
        await FlushAsync(renderer);
        Assert.DoesNotContain("王五", renderer.Markup());
        Assert.Empty(reports);
    }

    // ------------------------------------------------------------------
    // 清除/撤销沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 手绘沿清除应派发clear并经画板回调发空值()
    {
        var (renderer, js, component, reports) = await MountAsync();
        var padId = PadId(js);

        await renderer.Dispatcher.InvokeAsync(component.ClearAsync);

        var clear = Single(js, "signaturePad.clear");
        Assert.Equal(padId, clear.Args![0]);
        // 画板 OnChange(true) 经 OnPadChangedAsync 转发为 SignatureValue.Empty。
        Assert.Equal([SignatureValue.Empty], reports);
    }

    [Fact]
    public async Task 输入沿清除应清空文本并发空值()
    {
        var (renderer, _, component, reports) = await MountAsync();
        await SwitchKindAsync(renderer, SignatureKind.Typed);
        await FlushAsync(renderer);

        var input = renderer.FindComponent<Input>();
        await renderer.Dispatcher.InvokeAsync(async () => await input.ValueChanged.InvokeAsync("李四"));
        reports.Clear();

        await renderer.Dispatcher.InvokeAsync(component.ClearAsync);

        Assert.Equal([SignatureValue.Empty], reports);
        Assert.DoesNotContain("李四", renderer.Markup());
    }

    [Fact]
    public async Task 撤销按钮应派发undo且在失败沿不上报()
    {
        var (renderer, js, _, reports) = await MountAsync();
        var padId = PadId(js);

        // 渲染顺序里第一个 Button 是撤销（清除在其后）。
        var undoButton = renderer.FindComponent<Button>();
        await renderer.Dispatcher.InvokeAsync(async () => await undoButton.OnClick.InvokeAsync());

        var undo = Single(js, "signaturePad.undo");
        Assert.Equal(padId, undo.Args![0]);
        // NoopJavaScript 返回 default(false)：无笔可撤，不上报。
        Assert.Empty(reports);
    }

    [Fact]
    public async Task 空画板时撤销按钮应被禁用()
    {
        var (renderer, _, _, _) = await MountAsync();

        var undoButton = renderer.FindComponent<Button>();
        Assert.True(undoButton.Disabled);
    }

    [Fact]
    public async Task ShowUndo关闭时应只余清除按钮()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Signature.ShowUndo)] = false,
        });

        Assert.DoesNotContain("Undo last stroke", renderer.Markup());
        Assert.Contains("Clear", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 画板缺席/在席的导出沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 输入路径下画板缺席导出应回落空()
    {
        var (renderer, _, component, _) = await MountAsync();
        await SwitchKindAsync(renderer, SignatureKind.Typed);
        await FlushAsync(renderer);

        Assert.Null(await renderer.Dispatcher.InvokeAsync(() => component.GetPngAsync()));
        Assert.Null(await renderer.Dispatcher.InvokeAsync(() => component.GetPngBytesAsync()));
        Assert.Empty(await renderer.Dispatcher.InvokeAsync(() => component.GetStrokesAsync()));
    }

    [Fact]
    public async Task 手绘路径下导出应转发画板interop标识符()
    {
        var (renderer, js, component, _) = await MountAsync();
        var padId = PadId(js);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await component.GetPngAsync();
            await component.GetStrokesAsync();
        });

        Assert.Contains(js.Invocations, i =>
            i.Identifier == "signaturePad.toPngStream" && (string)i.Args![0]! == padId);
        Assert.Contains(js.Invocations, i =>
            i.Identifier == "signaturePad.toStrokesStream" && (string)i.Args![0]! == padId);
    }

    [Fact]
    public async Task 回放笔画应在输入路径发起时切回手绘并派发setStrokes()
    {
        var (renderer, js, component, reports) = await MountAsync();
        await SwitchKindAsync(renderer, SignatureKind.Typed);
        await FlushAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(() => component.SetStrokesAsync(
            [new SignatureStroke([new SignaturePoint(1, 2, 3), new SignaturePoint(4, 5, 6)])]));
        await FlushAsync(renderer);

        Assert.Contains("<canvas", renderer.Markup());
        var setStrokes = Single(js, "signaturePad.setStrokes");
        Assert.Equal(PadId(js), setStrokes.Args![0]);
        // 画板判空（NoopJavaScript 返回 default false）经 OnPadChangedAsync 上报 Drawn 形态。
        var drawn = reports.Last();
        Assert.NotNull(drawn);
        Assert.Equal(SignatureKind.Drawn, drawn.Kind);
    }

    // ------------------------------------------------------------------
    // 释放沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 释放后公开方法应变为空操作()
    {
        var (renderer, js, component, reports) = await MountAsync();

        ((IDisposable)component).Dispose();

        // 释放后回放笔画立即返回：不再派发 setStrokes。
        js.Invocations.Clear();
        await renderer.Dispatcher.InvokeAsync(() => component.SetStrokesAsync(null));
        Assert.Empty(js.Invocations);
        Assert.Empty(reports);
    }
}
