using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Motion 家族渲染测试：渲染态级联提供（预渲染 false → 交互 true）、
/// Motion 可见性属性组合与参数校验表、HeightAnimation/SelectionIndicator 结构，
/// ScreenTransition/PageTransition 的转场触发沿，以及 motion.js 的 interop 契约
/// （NoopJavaScript 调用断言：import 独立模块 URL、motion/height/indicator/play/dispose）。
/// </summary>
public class MotionTests
{
    private static ComponentTestRenderer CreateRenderer(NoopJavaScript? js = null, NavigationManager? navigation = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js ?? new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        if (navigation is not null)
        {
            services.AddSingleton(navigation);
        }
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>让调度器空转若干轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    /// <summary>读取 motion.js 配置匿名对象上的属性（匿名类型为编译产物，只能反射取值）。</summary>
    private static object? ConfigValue((string Identifier, object?[]? Args) invocation, string property)
    {
        var config = invocation.Args!.Last()!;
        return config.GetType().GetProperty(property)!.GetValue(config);
    }

    [Fact]
    public async Task 全家族结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<RenderStateProvider>(new Dictionary<string, object?>
        {
            [nameof(RenderStateProvider.ChildContent)] = (RenderFragment)(builder =>
            {
                var seq = 0;

                // 隐藏的 Motion：hidden/inert/aria-hidden 三件套。
                builder.OpenComponent<Motion>(seq++);
                builder.AddAttribute(seq++, nameof(Motion.Visible), false);
                builder.AddAttribute(seq++, nameof(Motion.Preset), MotionPreset.SlideUp);
                builder.AddAttribute(seq++, nameof(Motion.Trigger), MotionTrigger.InView);
                builder.AddAttribute(seq++, nameof(Motion.ChildContent), Text("入场内容"));
                builder.CloseComponent();

                // 折叠的 HeightAnimation：overflow-hidden 包裹 + flow-root 测量内层。
                builder.OpenComponent<HeightAnimation>(seq++);
                builder.AddAttribute(seq++, nameof(HeightAnimation.Expanded), false);
                builder.AddAttribute(seq++, nameof(HeightAnimation.ChildContent), Text("折叠内容"));
                builder.CloseComponent();

                // 指示器：默认隐藏的绝对定位胶囊。
                builder.OpenComponent<SelectionIndicator>(seq++);
                builder.AddAttribute(seq++, nameof(SelectionIndicator.HoverSelector), "[role=tab]");
                builder.CloseComponent();

                // 屏幕转场：内部是一个 Manual 触发的 Motion。
                builder.OpenComponent<ScreenTransition>(seq++);
                builder.AddAttribute(seq++, nameof(ScreenTransition.TransitionKey), "home");
                builder.AddAttribute(seq++, nameof(ScreenTransition.Preset), MotionPreset.Fade);
                builder.AddAttribute(seq++, nameof(ScreenTransition.ChildContent), Text("正文"));
                builder.CloseComponent();
            }),
        });
        await FlushAsync(renderer);

        await Verify(renderer.Markup());
    }

    private static Task<Motion> MountMotionAsync(
        ComponentTestRenderer renderer,
        bool? visible = null,
        MotionPreset? preset = null,
        MotionTrigger? trigger = null,
        int? duration = null,
        int? delay = null,
        double? distance = null,
        double? threshold = null,
        bool? animateOnFirstRender = null)
    {
        return renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object?>
            {
                [nameof(Motion.ChildContent)] = Text("内容"),
            };
            if (visible is not null) { parameters[nameof(Motion.Visible)] = visible.Value; }
            if (preset is not null) { parameters[nameof(Motion.Preset)] = preset.Value; }
            if (trigger is not null) { parameters[nameof(Motion.Trigger)] = trigger.Value; }
            if (duration is not null) { parameters[nameof(Motion.Duration)] = duration.Value; }
            if (delay is not null) { parameters[nameof(Motion.Delay)] = delay.Value; }
            if (distance is not null) { parameters[nameof(Motion.Distance)] = distance.Value; }
            if (threshold is not null) { parameters[nameof(Motion.Threshold)] = threshold.Value; }
            if (animateOnFirstRender is not null) { parameters[nameof(Motion.AnimateOnFirstRender)] = animateOnFirstRender.Value; }

            var motion = await renderer.MountAsync<Motion>(parameters);
            await FlushAsync(renderer);
            return motion;
        });
    }

    [Fact]
    public async Task 默认可见的Motion不应带隐藏与惯性感()
    {
        var renderer = CreateRenderer();
        await MountMotionAsync(renderer);

        var markup = renderer.Markup();
        Assert.DoesNotContain("hidden", markup);
        Assert.DoesNotContain("inert", markup);
        Assert.Contains("内容", markup);
    }

    [Fact]
    public async Task 隐藏的Motion应叠加hidden与inert并标注AriaHidden()
    {
        var renderer = CreateRenderer();
        await MountMotionAsync(renderer, visible: false);

        var markup = renderer.Markup();
        Assert.Contains("hidden", markup);
        Assert.Contains("inert", markup);
        Assert.Contains("aria-hidden=\"true\"", markup);
        // 隐藏内容仍保留在 DOM 中。
        Assert.Contains("内容", markup);
    }

    [Theory]
    [InlineData(-1, null, null, null)]   // 负时长
    [InlineData(null, -1, null, null)]   // 负延迟
    [InlineData(null, null, -1.0, null)] // 负距离
    [InlineData(null, null, null, 1.5)]  // 阈值越界
    public async Task Motion非法参数应抛出ArgumentOutOfRangeException(int? duration, int? delay, double? distance, double? threshold)
    {
        var renderer = CreateRenderer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => MountMotionAsync(
            renderer, duration: duration, delay: delay, distance: distance, threshold: threshold));
    }

    [Fact]
    public async Task interop契约应导入motion模块并派发motion与play与dispose()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);
        var motion = await MountMotionAsync(renderer, preset: MotionPreset.Spring, trigger: MotionTrigger.Hover);

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        var url = import.Args![0]!.ToString()!;
        Assert.Contains("./_content/AtomBlazor/js/motion.js", url);
        Assert.Contains("?v=", url);

        // 配置匿名对象与 JS 端约定同名小写字段。
        var configure = js.Invocations.First(i => i.Identifier == "motion");
        Assert.Equal("Spring", ConfigValue(configure, "preset"));
        Assert.Equal("Hover", ConfigValue(configure, "trigger"));
        Assert.Equal(250, ConfigValue(configure, "duration"));

        // PlayAsync 先重新配置再派发 play。
        await renderer.Dispatcher.InvokeAsync(() => motion.PlayAsync());
        Assert.Contains(js.Invocations, i => i.Identifier == "play");

        await renderer.Dispatcher.InvokeAsync(async () => await motion.DisposeAsync());
        Assert.Contains(js.Invocations, i => i.Identifier == "dispose");
    }

    [Fact]
    public async Task HeightAnimation折叠时应隐藏容器并保留测量内层()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<HeightAnimation>(new Dictionary<string, object?>
            {
                [nameof(HeightAnimation.Expanded)] = false,
                [nameof(HeightAnimation.ChildContent)] = Text("折叠"),
            });
            await FlushAsync(renderer);
        });

        var markup = renderer.Markup();
        Assert.Contains("hidden", markup);
        Assert.Contains("inert", markup);
        Assert.Contains("aria-hidden=\"true\"", markup);
        Assert.Contains("atom:overflow-hidden", markup);
        Assert.Contains("atom:flow-root", markup);

        Assert.Contains("./_content/AtomBlazor/js/motion.js",
            Assert.Single(js.Invocations, i => i.Identifier == "import").Args![0]!.ToString()!);
        var configure = js.Invocations.First(i => i.Identifier == "height");
        Assert.Equal(false, ConfigValue(configure, "expanded"));
        Assert.Equal(250, ConfigValue(configure, "duration"));
    }

    [Fact]
    public async Task HeightAnimation负时长应抛出ArgumentOutOfRangeException()
    {
        var renderer = CreateRenderer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            renderer.Dispatcher.InvokeAsync(async () =>
            {
                await renderer.MountAsync<HeightAnimation>(new Dictionary<string, object?>
                {
                    [nameof(HeightAnimation.Duration)] = -1,
                    [nameof(HeightAnimation.ChildContent)] = Text("x"),
                });
            }));
    }

    [Fact]
    public async Task SelectionIndicator应为默认隐藏的活动胶囊并派发indicator配置()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<SelectionIndicator>(new Dictionary<string, object?>
            {
                [nameof(SelectionIndicator.HoverSelector)] = "[role=tab]",
            });
            await FlushAsync(renderer);
        });

        var markup = renderer.Markup();
        Assert.Contains("<span", markup);
        Assert.Contains("hidden", markup);
        Assert.Contains("aria-hidden=\"true\"", markup);
        Assert.Contains("atom:pointer-events-none atom:absolute atom:left-0 atom:top-0", markup);
        Assert.Contains("atom:rounded-md atom:bg-accent", markup);

        var configure = js.Invocations.First(i => i.Identifier == "indicator");
        Assert.Equal("[data-state=active], [aria-selected=true], [aria-current=page]", ConfigValue(configure, "selector"));
        Assert.Equal("[role=tab]", ConfigValue(configure, "hoverSelector"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            renderer.Dispatcher.InvokeAsync(async () =>
            {
                await renderer.MountAsync<SelectionIndicator>(new Dictionary<string, object?>
                {
                    [nameof(SelectionIndicator.Duration)] = -1,
                });
            }));
    }

    [Fact]
    public async Task RenderStateProvider应在首次交互渲染后级联true()
    {
        var renderer = CreateRenderer();
        var observed = new List<bool?>();

        await renderer.MountAsync<RenderStateProvider>(new Dictionary<string, object?>
        {
            [nameof(RenderStateProvider.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<RenderStateConsumer>(0);
                builder.AddAttribute(1, nameof(RenderStateConsumer.OnState),
                    (Action<RenderState?>)(state => observed.Add(state?.IsInteractive)));
                builder.CloseComponent();
            }),
        });

        // 级联初始为预渲染态，首次 OnAfterRender 后翻转为交互态。
        Assert.Contains(false, observed);

        await FlushAsync(renderer);

        Assert.Contains(true, observed);
        Assert.Contains("interactive", renderer.Markup());
    }

    /// <summary>消费级联 RenderState 的探针组件。</summary>
    private sealed class RenderStateConsumer : ComponentBase
    {
        [CascadingParameter] internal RenderState? State { get; set; }
        [Parameter] public Action<RenderState?>? OnState { get; set; }

        protected override void OnInitialized() => OnState?.Invoke(State);
        protected override void OnParametersSet() => OnState?.Invoke(State);

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, State?.IsInteractive == true ? "interactive" : "prerender");
    }

    [Fact]
    public async Task ScreenTransition在交互后键变化应触发入场播放()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<ScreenTransition>(new Dictionary<string, object?>
            {
                [nameof(ScreenTransition.TransitionKey)] = "home",
                [nameof(ScreenTransition.ChildContent)] = Text("首页"),
            });
            await FlushAsync(renderer);
        });

        // 首次渲染不播放。
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "play");
        Assert.Contains("首页", renderer.Markup());

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(ScreenTransition.TransitionKey)] = "about",
            [nameof(ScreenTransition.ChildContent)] = Text("关于"),
        });
        await FlushAsync(renderer);

        Assert.Contains(js.Invocations, i => i.Identifier == "play");
        Assert.Contains("关于", renderer.Markup());
    }

    [Fact]
    public async Task ScreenTransition禁用转场时应以零时长配置Motion()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<ScreenTransition>(new Dictionary<string, object?>
            {
                [nameof(ScreenTransition.Enabled)] = false,
                [nameof(ScreenTransition.TransitionKey)] = "a",
                [nameof(ScreenTransition.ChildContent)] = Text("内容"),
            });
            await FlushAsync(renderer);
        });

        var configure = js.Invocations.First(i => i.Identifier == "motion");
        Assert.Equal(0, ConfigValue(configure, "duration"));
        Assert.Equal("Manual", ConfigValue(configure, "trigger"));
    }

    [Fact]
    public async Task PageTransition应随导航事件更新转场键并播放()
    {
        var js = new NoopJavaScript();
        var navigation = new TestNavigationManager("https://localhost/", "https://localhost/home");
        var renderer = CreateRenderer(js, navigation);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<PageTransition>(new Dictionary<string, object?>
            {
                [nameof(PageTransition.ChildContent)] = Text("页面"),
            });
            await FlushAsync(renderer);
        });

        Assert.Contains("页面", renderer.Markup());
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "play");

        await renderer.Dispatcher.InvokeAsync(() => navigation.SimulateNavigation("https://localhost/about"));
        await FlushAsync(renderer);

        Assert.Contains(js.Invocations, i => i.Identifier == "play");
    }

    /// <summary>
    /// 最小可驱动的 NavigationManager：.NET 11 中抽象成员已收敛为可继承的具体实现，
    /// 只需 Initialize 播种、再用受保护的 Uri 赋值与 NotifyLocationChanged 触发事件。
    /// </summary>
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string baseUri, string uri)
        {
            Initialize(baseUri, uri);
        }

        public void SimulateNavigation(string absoluteUri)
        {
            Uri = absoluteUri;
            NotifyLocationChanged(false);
        }
    }
}
