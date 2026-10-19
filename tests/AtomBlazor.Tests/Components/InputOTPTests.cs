using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// InputOTP 验证码分段输入测试：Length 段数呈现与默认快照（Scrub 随机 id）、
/// Value 逐位分布与 Mask 星号遮蔽、字符模式（Numbers/Letters/LettersAndNumbers）的
/// inputmode 与逐字符过滤、分隔符开关/组宽/自定义内容、Size 尺寸类、隐藏 name 回传项、
/// 键入沿（含空格回填与 OnComplete 满位触发）、JS 粘贴沿 OnPasteJs（非法字符过滤、
/// 超长截断、空格清空）、字符模式变化经 inputOtp.updateOtpMode 同步、barrel 导入与
/// inputOtp.initialize 载荷、释放派发 inputOtp.dispose 且粘贴此后变为空操作。
/// headless 限制先例：ElementReference.FocusAsync() 抛 InvalidOperationException（组件内
/// FocusInput 已吞掉），不测纯焦点移动沿。
/// </summary>
public class InputOTPTests
{
    /// <summary>
    /// 归一化组件自生成的 otp-xxxxxxxx 随机短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "otp-[0-9a-f]{8}", "otp-<id>");

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

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, InputOTP Otp)> MountAsync(
        Dictionary<string, object?> parameters)
    {
        var (renderer, js) = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(InputOTP), parameters));
        await FlushAsync(renderer);
        return (renderer, js, (InputOTP)component);
    }

    /// <summary>在渲染树里按序派发一个 DOM 事件（occurrence 即数位索引）。</summary>
    private static Task DispatchAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>JS 回调沿：直接在 Dispatcher 上调用 [JSInvokable] 入口并等待落定。</summary>
    private static Task InvokeAsync(
        ComponentTestRenderer renderer, InputOTP otp, Func<InputOTP, Task> callback) =>
        renderer.Dispatcher.InvokeAsync(() => callback(otp));

    private static int CountInputs(string markup) => Regex.Count(markup, "<input");

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Value)] = "123456",
            [nameof(InputOTP.AriaLabel)] = "验证码",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 段数应与Length一致且默认六段一组分隔符()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>());
        var markup = renderer.Markup();
        Assert.Equal(6, CountInputs(markup));
        // Length=6、GroupSize=3：仅第三段后有一个分隔符（第六段已是末位）。
        Assert.Single(Regex.Matches(markup, "text-muted-foreground"));

        var (shortRenderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 4,
        });
        Assert.Equal(4, CountInputs(shortRenderer.Markup()));
    }

    [Fact]
    public async Task 值应逐位分布且Mask用星号遮蔽()
    {
        var (plain, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 4,
            [nameof(InputOTP.Value)] = "12",
        });
        var markup = plain.Markup();
        Assert.Contains("value=\"1\"", markup);
        Assert.Contains("value=\"2\"", markup);

        var (masked, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 4,
            [nameof(InputOTP.Value)] = "12",
            [nameof(InputOTP.Mask)] = true,
        });
        Assert.Equal(2, Regex.Count(masked.Markup(), "value=\"\\*\""));
    }

    [Theory]
    [InlineData(InputOTPInputMode.Numbers, "numeric")]
    [InlineData(InputOTPInputMode.Letters, "text")]
    [InlineData(InputOTPInputMode.LettersAndNumbers, "text")]
    public async Task 字符模式应映射inputmode(InputOTPInputMode mode, string htmlMode)
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 2,
            [nameof(InputOTP.InputMode)] = mode,
        });

        Assert.Equal(2, Regex.Count(renderer.Markup(), $"inputmode=\"{htmlMode}\""));
    }

    [Fact]
    public async Task 键入沿应过滤非法字符满位触发OnComplete()
    {
        var reports = new List<string>();
        var completed = new List<string>();
        var onChanged = EventCallback.Factory.Create<string>(new object(), v => reports.Add(v));
        var onComplete = EventCallback.Factory.Create<string>(new object(), v => completed.Add(v));

        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 2,
            [nameof(InputOTP.ValueChanged)] = onChanged,
            [nameof(InputOTP.OnComplete)] = onComplete,
        });

        // Numbers 模式：字母被拒绝，不产生上报。
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "a" });
        await FlushAsync(renderer);
        Assert.Empty(reports);

        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "7" });
        await FlushAsync(renderer);
        Assert.Equal(["7"], reports);
        Assert.Empty(completed);

        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "3" }, occurrence: 1);
        await FlushAsync(renderer);
        Assert.Equal(["7", "73"], reports);
        Assert.Equal(["73"], completed);
    }

    [Fact]
    public async Task 多字符输入应只取首位()
    {
        var reports = new List<string>();
        var onChanged = EventCallback.Factory.Create<string>(new object(), v => reports.Add(v));

        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 3,
            [nameof(InputOTP.ValueChanged)] = onChanged,
        });

        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "45" });
        await FlushAsync(renderer);

        Assert.Equal(["4"], reports);
    }

    [Fact]
    public async Task 粘贴沿应过滤非法字符截断超长并清空余位()
    {
        var reports = new List<string>();
        var completed = new List<string>();
        var onChanged = EventCallback.Factory.Create<string>(new object(), v => reports.Add(v));
        var onComplete = EventCallback.Factory.Create<string>(new object(), v => completed.Add(v));

        var (renderer, _, otp) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 4,
            [nameof(InputOTP.Value)] = "9876",
            [nameof(InputOTP.ValueChanged)] = onChanged,
            [nameof(InputOTP.OnComplete)] = onComplete,
        });
        reports.Clear();
        completed.Clear();

        // 混入分隔符与字母：只留数字 "123"，余位清空。
        await InvokeAsync(renderer, otp, static i => i.OnPasteJs("1-2a3"));
        await FlushAsync(renderer);
        Assert.Equal(["123"], reports);
        Assert.Empty(completed);
        var markup = renderer.Markup();
        Assert.Contains("value=\"1\"", markup);
        Assert.Contains("value=\"2\"", markup);
        Assert.Contains("value=\"3\"", markup);

        // 超长粘贴截断到段数。
        await InvokeAsync(renderer, otp, static i => i.OnPasteJs("556677"));
        await FlushAsync(renderer);
        Assert.Equal(["123", "5566"], reports);
        Assert.Equal(["5566"], completed);

        // 全是非法字符：不动作。
        await InvokeAsync(renderer, otp, static i => i.OnPasteJs("--"));
        await FlushAsync(renderer);
        Assert.Equal(["123", "5566"], reports);
    }

    [Fact]
    public async Task 分隔符开关组宽与自定义内容应生效()
    {
        var (hidden, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 4,
            [nameof(InputOTP.ShowSeparator)] = false,
        });
        Assert.DoesNotContain("text-muted-foreground", hidden.Markup());

        var (wide, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 6,
            [nameof(InputOTP.GroupSize)] = 2,
        });
        // GroupSize=2：第 2、4 位后各一个分隔符（第 6 位是末位不放）。
        Assert.Equal(2, Regex.Count(wide.Markup(), "text-muted-foreground"));

        var (customRenderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 2,
            [nameof(InputOTP.GroupSize)] = 1,
            [nameof(InputOTP.Separator)] = (RenderFragment)(builder => builder.AddMarkupContent(0, "<i>#</i>")),
        });
        Assert.Contains("<i>#</i>", customRenderer.Markup());
    }

    [Fact]
    public async Task 尺寸变体应映射到输入格类()
    {
        var (small, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 2,
            [nameof(InputOTP.Size)] = InputOTPSize.Small,
            [nameof(InputOTP.InputClass)] = "extra-cls",
        });
        Assert.Contains("atom:h-8 atom:w-8 atom:text-sm", small.Markup());
        Assert.Contains("extra-cls", small.Markup());

        var (large, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 2,
            [nameof(InputOTP.Size)] = InputOTPSize.Large,
        });
        Assert.Contains("atom:h-12 atom:w-12 atom:text-lg", large.Markup());
    }

    [Fact]
    public async Task 显式Name应生成隐藏回传项()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 2,
            [nameof(InputOTP.Name)] = "code",
            [nameof(InputOTP.Value)] = "42",
        });

        var markup = renderer.Markup();
        Assert.Contains("type=\"hidden\"", markup);
        Assert.Contains("name=\"code\"", markup);
        Assert.Equal(3, CountInputs(markup)); // 两个数位 + 一个隐藏项
    }

    [Fact]
    public async Task 长度变化应重建段位数组并重新分布值()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 6,
            [nameof(InputOTP.Value)] = "123456",
        });
        Assert.Equal(6, CountInputs(renderer.Markup()));

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 4,
            [nameof(InputOTP.Value)] = "123456",
        }));
        await FlushAsync(renderer);

        Assert.Equal(4, CountInputs(renderer.Markup()));
        Assert.Contains("value=\"1\"", renderer.Markup());
        Assert.Contains("value=\"4\"", renderer.Markup());
        Assert.DoesNotContain("value=\"5\"", renderer.Markup());
    }

    [Fact]
    public async Task 挂载应经共享core包一次导入并初始化inputOtp()
    {
        var (_, js, otp) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 3,
            [nameof(InputOTP.Id)] = "otp-root",
        });

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains("components-core.js", (string)import.Args![0]!);

        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "inputOtp.initialize");
        Assert.Equal(4, initialize.Args!.Length);
        Assert.Same(otp, ((DotNetObjectReference<InputOTP>)initialize.Args[1]!).Value);
        Assert.Equal("otp-root", initialize.Args![2]);
        Assert.Equal("Numbers", initialize.Args![3]);
    }

    [Fact]
    public async Task 字符模式变化应经updateOtpMode同步()
    {
        var (renderer, js, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 3,
            [nameof(InputOTP.Id)] = "otp-root",
        });

        // 首渲染不发 updateOtpMode。
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "inputOtp.updateOtpMode");

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(InputOTP.Id)] = "otp-root",
            [nameof(InputOTP.Length)] = 3,
            [nameof(InputOTP.InputMode)] = InputOTPInputMode.Letters,
        }));
        await FlushAsync(renderer);

        var update = Assert.Single(js.Invocations, i => i.Identifier == "inputOtp.updateOtpMode");
        Assert.Equal("otp-root", update.Args![0]);
        Assert.Equal("Letters", update.Args![1]);

        // HTML 侧随之切换。
        Assert.Contains("inputmode=\"text\"", renderer.Markup());
    }

    [Fact]
    public async Task 释放应派发dispose且粘贴此后变为空操作()
    {
        var reports = new List<string>();
        var onChanged = EventCallback.Factory.Create<string>(new object(), v => reports.Add(v));

        var (renderer, js, otp) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(InputOTP.Length)] = 3,
            [nameof(InputOTP.Id)] = "otp-root",
            [nameof(InputOTP.ValueChanged)] = onChanged,
        });

        await renderer.Dispatcher.InvokeAsync(otp.DisposeAsync);
        await FlushAsync(renderer);

        var dispose = Assert.Single(js.Invocations, i => i.Identifier == "inputOtp.dispose");
        Assert.Equal("otp-root", dispose.Args![0]);

        // 释放后的粘贴回调是空操作，不再上报。
        await InvokeAsync(renderer, otp, static i => i.OnPasteJs("123"));
        Assert.Empty(reports);
    }
}
