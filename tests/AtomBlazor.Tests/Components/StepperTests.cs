using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Stepper 组件层基础面测试：指示器结构沿（li/aria-current/播报）、标记图标沿
/// （完成叉跳过与自定义图标）、Clickable 按钮与静态通路、点击与受控/非受控 ActiveStep 沿、
/// 布局与类透传、Step 内容只为活动步渲染、隐藏 data-step-id 锚点、SynchronizeOrder 回调沿
/// （经 stub 记录 interop：barrel 点分 stepper.initialize/stepper.dispose）、Context 缺失
/// 异常沿、枚举面与默认结构快照（guid 归一）。
/// </summary>
public class StepperTests
{
    /// <summary>归一化 data-step-id 的 32 位十六进制 guid，使快照可确定（照 T 族 Scrub 先例）。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "data-step-id=\"[0-9a-fA-F]{32}\"", "data-step-id=\"<n>\"");

    private static int Count(string haystack, string needle) => haystack.Split(needle).Length - 1;

    /// <summary>按渲染顺序取出全部隐藏锚点上的步骤 id。</summary>
    private static string[] StepIds(string markup) =>
        Regex.Matches(markup, "data-step-id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();

    private static ComponentTestRenderer CreateRenderer(out NoopJavaScript js)
    {
        var stub = new NoopJavaScript();
        js = stub;
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>步骤配方：标题 + 可选的描述、图标、显式状态、禁用、可选提示与内容。</summary>
    private sealed record StepSpec(
        string Title,
        string? Description = null,
        string? Icon = null,
        StepState? State = null,
        bool Disabled = false,
        bool Optional = false,
        RenderFragment? Content = null);

    private static RenderFragment Build(params StepSpec[] specs) => builder =>
    {
        var sequence = 0;
        foreach (var spec in specs)
        {
            builder.OpenComponent<Step>(sequence++);
            builder.AddComponentParameter(sequence++, nameof(Step.Title), spec.Title);

            if (spec.Description is not null)
            {
                builder.AddComponentParameter(sequence++, nameof(Step.Description), spec.Description);
            }

            if (spec.Icon is not null)
            {
                builder.AddComponentParameter(sequence++, nameof(Step.Icon), spec.Icon);
            }

            if (spec.State is not null)
            {
                builder.AddComponentParameter(sequence++, nameof(Step.State), spec.State);
            }

            if (spec.Disabled)
            {
                builder.AddComponentParameter(sequence++, nameof(Step.Disabled), true);
            }

            if (spec.Optional)
            {
                builder.AddComponentParameter(sequence++, nameof(Step.Optional), true);
            }

            if (spec.Content is not null)
            {
                builder.AddComponentParameter(sequence++, nameof(Step.ChildContent), spec.Content);
            }

            builder.CloseComponent();
        }
    };

    private static RenderFragment Text(string value) => builder => builder.AddContent(0, value);

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Stepper Stepper)> MountAsync(
        RenderFragment childContent,
        int activeStep = 0,
        bool clickable = false,
        StepperOrientation orientation = StepperOrientation.Horizontal,
        Action<int>? onActiveStepChanged = null,
        string? classOverride = null,
        string? indicatorClass = null,
        string? contentClass = null)
    {
        var renderer = CreateRenderer(out var js);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Stepper.ActiveStep)] = activeStep,
            [nameof(Stepper.Clickable)] = clickable,
            [nameof(Stepper.Orientation)] = orientation,
            [nameof(Stepper.ChildContent)] = childContent,
        };

        if (onActiveStepChanged is not null)
        {
            parameters[nameof(Stepper.ActiveStepChanged)] =
                EventCallback.Factory.Create<int>(new object(), onActiveStepChanged);
        }

        if (classOverride is not null)
        {
            parameters[nameof(Stepper.Class)] = classOverride;
        }

        if (indicatorClass is not null)
        {
            parameters[nameof(Stepper.IndicatorClass)] = indicatorClass;
        }

        if (contentClass is not null)
        {
            parameters[nameof(Stepper.ContentClass)] = contentClass;
        }

        var stepper = await renderer.MountAsync<Stepper>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return (renderer, js, stepper);
    }

    private static Task ClickAsync(ComponentTestRenderer renderer, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));

    private static StepState State(Stepper stepper, int index) =>
        (StepState)ComponentProbe.Call(stepper, "GetStepState", index)!;

    private static List<StepInfo> Registered(Stepper stepper) =>
        ComponentProbe.Field<List<StepInfo>>(stepper, "steps");

    // ------------------------------------------------------------------

    [Fact]
    public async Task 根元素应带槽位属性与本地化进度标签()
    {
        var (renderer, _, _) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二")));
        var markup = renderer.Markup();

        Assert.Contains("data-slot=\"stepper\"", markup);
        Assert.Contains("data-slot=\"stepper-content\"", markup);
        Assert.Contains("aria-label=\"Progress\"", markup);
    }

    [Fact]
    public async Task 每步应画一个li且仅活动步置当前步标记()
    {
        var (renderer, _, _) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三")), activeStep: 1);
        var markup = renderer.Markup();

        Assert.Equal(3, Count(markup, "<li"));
        Assert.Equal(1, Count(markup, "aria-current=\"step\""));
    }

    [Fact]
    public async Task 屏幕阅读器播报应含位置与完成状态词()
    {
        var (renderer, _, _) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三")), activeStep: 1);
        var markup = renderer.Markup();

        Assert.Contains("Step 1 of 3, Completed", markup);
        Assert.Contains("Step 2 of 3", markup);
        Assert.Contains("Step 3 of 3", markup);
        // Active 与 Pending 无状态词：播报里不应出现 "，已完成"之外的 Error/Skipped 字样。
        Assert.DoesNotContain(", Error", markup);
        Assert.DoesNotContain(", Skipped", markup);
    }

    [Fact]
    public async Task Clickable应渲染按钮而静态模式不渲染()
    {
        var (clickable, _, _) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三")), activeStep: 0, clickable: true);
        // 步骤 0 是活动步不可导航，步骤 1、2 各画一个按钮。
        Assert.Equal(2, Count(clickable.Markup(), "<button"));

        var (statically, _, _) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三")));
        Assert.DoesNotContain("<button", statically.Markup());
    }

    [Fact]
    public async Task 点击按钮应激活对应步骤并上报索引()
    {
        var reported = new List<int>();
        var (renderer, _, stepper) = await MountAsync(
            Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三")), activeStep: 0, clickable: true,
            onActiveStepChanged: reported.Add);

        await ClickAsync(renderer, 0);

        Assert.Equal([1], reported);
        Assert.Equal(StepState.Active, State(stepper, 1));
    }

    [Fact]
    public async Task 受控绑定下外部ActiveStep变化应移动活动步()
    {
        var (renderer, _, stepper) = await MountAsync(
            Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三")), activeStep: 0,
            onActiveStepChanged: _ => { });

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(Stepper.ActiveStep)] = 2,
        });

        Assert.Equal(StepState.Completed, State(stepper, 1));
        Assert.Equal(StepState.Active, State(stepper, 2));
    }

    [Fact]
    public async Task 非受控时外部参数变化不应接管内部值()
    {
        var (renderer, _, stepper) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三")), activeStep: 0);

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(Stepper.ActiveStep)] = 2,
        });

        // 无 ActiveStepChanged 委托时 OnParametersSet 不再覆写：组件自持内部值。
        Assert.Equal(StepState.Active, State(stepper, 0));
    }

    [Fact]
    public async Task 已完成标记应画图标而待处理标记应画序号()
    {
        var (renderer, _, _) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三")), activeStep: 1);
        var markup = renderer.Markup();

        // 步骤 0 已完成画 check 图标（不再画序号 1）；步骤 1 活动也画图标（check? 活动不画）——
        // 活动步画序号，待处理画序号 3。
        Assert.Equal(1, Count(markup, "<svg"));
        Assert.DoesNotMatch(@">\s*1\s*<", markup);
        Assert.Matches(@">\s*2\s*<", markup);
        Assert.Matches(@">\s*3\s*<", markup);
    }

    [Fact]
    public async Task 错误与跳过标记应各画对应图标()
    {
        var (renderer, _, _) = await MountAsync(Build(
            new StepSpec("一", State: StepState.Error), new StepSpec("二"), new StepSpec("三", State: StepState.Skipped)),
            activeStep: 1);
        var markup = renderer.Markup();

        // Error 画 x、Skipped 画 minus；活动步 1 画序号。
        Assert.Equal(2, Count(markup, "<svg"));
        Assert.Contains(", Error", markup);
        Assert.Contains(", Skipped", markup);
    }

    [Fact]
    public async Task 自定义图标应取代步骤序号()
    {
        var (renderer, _, _) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三", Icon: "star")));

        // 待处理的步骤 2 带自定义图标后不再画序号 3。
        Assert.Matches(@">\s*2\s*<", renderer.Markup());
        Assert.DoesNotMatch(@">\s*3\s*<", renderer.Markup());
    }

    [Fact]
    public async Task 描述与可选提示应渲染在标签区()
    {
        var (renderer, _, _) = await MountAsync(Build(
            new StepSpec("一", Description: "备注一"), new StepSpec("二"), new StepSpec("三", Optional: true)));
        var markup = renderer.Markup();

        Assert.Contains("备注一", markup);
        Assert.Contains(">Optional<", markup);
    }

    [Fact]
    public async Task 垂直布局应使用纵向类组合()
    {
        var (renderer, _, _) = await MountAsync(
            Build(new StepSpec("一"), new StepSpec("二")), orientation: StepperOrientation.Vertical);
        var markup = renderer.Markup();

        Assert.Contains("<div class=\"atom:flex atom:gap-8\"", markup);
        Assert.Contains("atom:ms-[15px]", markup);

        var (horizontal, _, _) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二")));
        Assert.Contains("<div class=\"atom:flex atom:flex-col\"", horizontal.Markup());
    }

    [Fact]
    public async Task Class与IndicatorClass与ContentClass应透传到对应元素()
    {
        var (renderer, _, _) = await MountAsync(
            Build(new StepSpec("一")),
            classOverride: "my-root",
            indicatorClass: "my-ind",
            contentClass: "my-content");
        var markup = renderer.Markup();

        Assert.Contains("<div class=\"atom:flex atom:flex-col my-root\" data-slot=\"stepper\"", markup);
        Assert.Contains("my-ind\" aria-label=\"Progress\"", markup);
        Assert.Contains("my-content\" data-slot=\"stepper-content\"", markup);
    }

    [Fact]
    public async Task Step内容只为活动步渲染并带步骤槽位标记()
    {
        var (renderer, _, _) = await MountAsync(Build(
            new StepSpec("一", Content: Text("内容一")),
            new StepSpec("二", Content: Text("内容二")),
            new StepSpec("三", Content: Text("内容三"))),
            activeStep: 1);
        var markup = renderer.Markup();

        Assert.Contains("内容二", markup);
        Assert.DoesNotContain("内容一", markup);
        Assert.DoesNotContain("内容三", markup);
        Assert.Equal(1, Count(markup, "data-slot=\"step\""));
        // 有内容时内容区回落出 atom:mt-6。
        Assert.Contains("atom:mt-6", markup);
    }

    [Fact]
    public async Task 每个Step应挂出隐藏的步骤标识锚点()
    {
        var (renderer, _, _) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二"), new StepSpec("三")));

        Assert.Equal(3, Count(renderer.Markup(), "data-step-id=\""));
        Assert.Equal(3, Count(renderer.Markup(), "<span hidden data-step-id=\""));
    }

    [Fact]
    public async Task SynchronizeOrder应按DOM顺序重排步骤()
    {
        var (renderer, _, stepper) = await MountAsync(Build(new StepSpec("标题一"), new StepSpec("标题二"), new StepSpec("标题三")));
        var ids = StepIds(renderer.Markup());
        Assert.Equal(3, ids.Length);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await stepper.SynchronizeOrder([ids[2], ids[0], ids[1]]));
        await renderer.Dispatcher.InvokeAsync(() => { });

        Assert.Equal(["标题三", "标题一", "标题二"], Registered(stepper).Select(info => info.Title).ToArray());
        var markup = renderer.Markup();
        Assert.True(markup.IndexOf("标题三", StringComparison.Ordinal) < markup.IndexOf("标题一", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SynchronizeOrder对无效相同或释放后的顺序应保持不变()
    {
        var (renderer, _, stepper) = await MountAsync(Build(new StepSpec("标题一"), new StepSpec("标题二"), new StepSpec("标题三")));
        var ids = StepIds(renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            // 长度不符、含重复、含未知 id、与现状相同——四类输入都不应改动注册顺序。
            await stepper.SynchronizeOrder([ids[0], ids[1]]);
            await stepper.SynchronizeOrder([ids[0], ids[0], ids[1]]);
            await stepper.SynchronizeOrder(["未知", ids[1], ids[2]]);
            await stepper.SynchronizeOrder([ids[0], ids[1], ids[2]]);
        });

        Assert.Equal(["标题一", "标题二", "标题三"], Registered(stepper).Select(info => info.Title).ToArray());

        // 释放后 disposed 守卫短路：乱序上报也不再改动。
        await renderer.Dispatcher.InvokeAsync(async () => await stepper.DisposeAsync());
        await renderer.Dispatcher.InvokeAsync(async () =>
            await stepper.SynchronizeOrder([ids[2], ids[1], ids[0]]));

        Assert.Equal(["标题一", "标题二", "标题三"], Registered(stepper).Select(info => info.Title).ToArray());
    }

    [Fact]
    public async Task 互操作挂载与释放应经core桶文件走点分标识符()
    {
        var (renderer, js, stepper) = await MountAsync(Build(new StepSpec("一"), new StepSpec("二")));

        var import = js.Invocations.Single(entry => entry.Identifier == "import");
        Assert.StartsWith(
            "./_content/AtomBlazor/js/components-core.js?assets=1",
            (string)import.Args![0]!);

        var initialize = js.Invocations.Single(entry => entry.Identifier == "stepper.initialize");
        Assert.Equal(2, initialize.Args!.Length);

        await renderer.Dispatcher.InvokeAsync(async () => await stepper.DisposeAsync());

        var dispose = js.Invocations.Single(entry => entry.Identifier == "stepper.dispose");
        Assert.Single(dispose.Args!);
    }

    [Fact]
    public async Task Step脱离Stepper应抛异常()
    {
        var renderer = CreateRenderer(out _);
        var mount = renderer.MountAsync<Step>(new Dictionary<string, object?>
        {
            [nameof(Step.Title)] = "孤立步骤",
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("Step 必须在 Stepper 组件内使用。", ex.Message);
    }

    [Fact]
    public void 枚举成员应按声明序取值()
    {
        Assert.Equal(0, (int)StepperOrientation.Horizontal);
        Assert.Equal(1, (int)StepperOrientation.Vertical);
        Assert.Equal(0, (int)StepState.Pending);
        Assert.Equal(1, (int)StepState.Active);
        Assert.Equal(2, (int)StepState.Completed);
        Assert.Equal(3, (int)StepState.Error);
        Assert.Equal(4, (int)StepState.Skipped);
    }

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(
            Build(
                new StepSpec("购物车", Description: "已加入购物车"),
                new StepSpec("收货地址"),
                new StepSpec("支付", Optional: true)),
            activeStep: 1,
            clickable: true);

        await Verify(Scrub(renderer.Markup()));
    }
}
