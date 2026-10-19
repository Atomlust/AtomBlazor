using AtomBlazor.Primitives;
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
/// Carousel 渲染与状态机测试：全结构快照、项目注册与指示器数量、
/// GoToAsync 边界钳制与无限循环、Next/Previous 命令与回调上报、
/// 可导航枚举（CanGoPrevious/CanGoNext）驱动按钮禁用态、参数校验、
/// 不变文化内联样式，以及 carousel.js 的 interop 契约（NoopJavaScript 调用断言）。
/// </summary>
public class CarouselTests
{
    private static ComponentTestRenderer CreateRenderer(NoopJavaScript? js = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js ?? new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>让调度器空转一轮，冲刷 OnAfterRender/StateHasChanged 队列（可多次）。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    /// <summary>构建 Carousel 的 ChildContent：视口 + 若干幻灯片 + 可选的前后导航按钮。</summary>
    private static RenderFragment CarouselBody(int itemCount, bool withNavButtons = false) => builder =>
    {
        var seq = 0;

        builder.OpenComponent<CarouselContent>(seq++);
        builder.AddAttribute(seq++, nameof(CarouselContent.ChildContent), (RenderFragment)(items =>
        {
            for (var i = 0; i < itemCount; i++)
            {
                var n = i;
                items.OpenComponent<CarouselItem>(n * 2);
                items.AddAttribute((n * 2) + 1, nameof(CarouselItem.ChildContent), Text($"幻灯片 {n + 1}"));
                items.CloseComponent();
            }
        }));
        builder.CloseComponent();

        if (withNavButtons)
        {
            builder.OpenComponent<CarouselPrevious>(seq++);
            builder.CloseComponent();
            builder.OpenComponent<CarouselNext>(seq++);
            builder.CloseComponent();
        }
    };

    private static Dictionary<string, object?> CarouselParams(
        int itemCount = 3,
        bool withNavButtons = false,
        bool? loop = null,
        bool? showIndicators = null,
        bool? autoplay = null,
        bool? paused = null,
        int? activeIndex = null,
        double? slidesPerView = null,
        int? gap = null,
        CarouselOrientation? orientation = null,
        CarouselIndicatorPosition? indicatorPosition = null,
        string? ariaLabel = null,
        EventCallback<int>? activeIndexChanged = null,
        EventCallback<int>? onSlideChanged = null,
        RenderFragment? childContent = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Carousel.ChildContent)] = childContent ?? CarouselBody(itemCount, withNavButtons),
        };
        if (loop is not null) { parameters[nameof(Carousel.Loop)] = loop.Value; }
        if (showIndicators is not null) { parameters[nameof(Carousel.ShowIndicators)] = showIndicators.Value; }
        if (autoplay is not null) { parameters[nameof(Carousel.Autoplay)] = autoplay.Value; }
        if (paused is not null) { parameters[nameof(Carousel.Paused)] = paused.Value; }
        if (activeIndex is not null) { parameters[nameof(Carousel.ActiveIndex)] = activeIndex.Value; }
        if (slidesPerView is not null) { parameters[nameof(Carousel.SlidesPerView)] = slidesPerView.Value; }
        if (gap is not null) { parameters[nameof(Carousel.Gap)] = gap.Value; }
        if (orientation is not null) { parameters[nameof(Carousel.Orientation)] = orientation.Value; }
        if (indicatorPosition is not null) { parameters[nameof(Carousel.IndicatorPosition)] = indicatorPosition.Value; }
        if (ariaLabel is not null) { parameters[nameof(Carousel.AriaLabel)] = ariaLabel; }
        if (activeIndexChanged is not null) { parameters[nameof(Carousel.ActiveIndexChanged)] = activeIndexChanged; }
        if (onSlideChanged is not null) { parameters[nameof(Carousel.OnSlideChanged)] = onSlideChanged; }
        return parameters;
    }

    private static Task<Carousel> MountAsync(ComponentTestRenderer renderer, Dictionary<string, object?> parameters)
        => renderer.Dispatcher.InvokeAsync(async () =>
        {
            var carousel = await renderer.MountAsync<Carousel>(parameters);
            await FlushAsync(renderer);
            return carousel;
        });

    [Fact]
    public async Task 全结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<Carousel>(CarouselParams(
                itemCount: 3,
                withNavButtons: true,
                showIndicators: true,
                autoplay: true,
                ariaLabel: "产品图集"));
            await FlushAsync(renderer);
        });

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 三个项目应注册并渲染三个指示器()
    {
        var renderer = CreateRenderer();
        var carousel = await MountAsync(renderer, CarouselParams(
            itemCount: 3, showIndicators: true));

        Assert.Equal(3, carousel.ItemCount);
        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Slide 1\"", markup);
        Assert.Contains("aria-label=\"Slide 2\"", markup);
        Assert.Contains("aria-label=\"Slide 3\"", markup);
        Assert.Contains("aria-current=\"true\"", markup);
        Assert.Contains("role=\"region\"", markup);
        Assert.Contains("aria-roledescription=\"carousel\"", markup);
    }

    [Theory]
    [InlineData(false, -1, 0)]   // 无循环：越界向前钳到 0
    [InlineData(false, 9, 2)]    // 无循环：越界向后钳到末索引
    [InlineData(true, -1, 2)]    // 循环：向前回绕到末索引
    [InlineData(true, 9, 0)]     // 循环：向后回绕到 0
    [InlineData(true, 1, 1)]     // 界内直达
    public async Task GoToAsync应按边界与循环钳制索引(bool loop, int requested, int expected)
    {
        var renderer = CreateRenderer();
        var carousel = await MountAsync(renderer, CarouselParams(itemCount: 3, loop: loop));

        await renderer.Dispatcher.InvokeAsync(() => carousel.GoToAsync(requested));

        Assert.Equal(expected, carousel.ActiveIndex);
    }

    [Fact]
    public async Task Next与Previous应驱动索引并上报回调()
    {
        var reported = new List<int>();
        var changed = new List<int>();
        var callback = EventCallback.Factory.Create<int>(new object(), i => reported.Add(i));
        var onSlide = EventCallback.Factory.Create<int>(new object(), i => changed.Add(i));
        var renderer = CreateRenderer();
        var carousel = await MountAsync(renderer, CarouselParams(
            itemCount: 3, activeIndexChanged: callback, onSlideChanged: onSlide));

        await renderer.Dispatcher.InvokeAsync(() => carousel.Next());
        Assert.Equal(1, carousel.CurrentIndex);

        await renderer.Dispatcher.InvokeAsync(() => carousel.Previous());
        Assert.Equal(0, carousel.CurrentIndex);

        await renderer.Dispatcher.InvokeAsync(() => carousel.Next());
        Assert.Equal([1, 0, 1], reported);
        Assert.Equal([1, 0, 1], changed);
    }

    [Theory]
    [InlineData(false, 0, false, true)]   // 无循环首张：不能向前
    [InlineData(false, 2, true, false)]   // 无循环末张：不能向后
    [InlineData(true, 0, true, true)]     // 循环且多于一张：双向皆可
    public async Task 可导航枚举应随循环与索引变化(bool loop, int activeIndex, bool canPrevious, bool canNext)
    {
        var renderer = CreateRenderer();
        var carousel = await MountAsync(renderer, CarouselParams(
            itemCount: 3, loop: loop, activeIndex: activeIndex, withNavButtons: true));

        Assert.Equal(canPrevious, carousel.CanGoPrevious);
        Assert.Equal(canNext, carousel.CanGoNext);

        var markup = renderer.Markup();
        Assert.Equal(canPrevious, !markup.Contains("disabled aria-label=\"Previous slide\"", StringComparison.Ordinal));
        Assert.Equal(canNext, !markup.Contains("disabled aria-label=\"Next slide\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 点击指示器应导航到对应幻灯片()
    {
        var renderer = CreateRenderer();
        var carousel = await MountAsync(renderer, CarouselParams(
            itemCount: 3, showIndicators: true));

        // ChildContent 无导航按钮时，onclick 处理器按渲染顺序依次为 3 个指示器。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 2));
        await FlushAsync(renderer);

        Assert.Equal(2, carousel.ActiveIndex);
    }

    [Fact]
    public async Task 小数可见数应换算最大索引与指示器数量()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, CarouselParams(itemCount: 4, showIndicators: true, slidesPerView: 2));

        // MaxIndex = min(3, ceil(4-2)=2) → 渲染 3 个指示器、缺席第 4 个。
        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Slide 3\"", markup);
        Assert.DoesNotContain("aria-label=\"Slide 4\"", markup);
    }

    [Theory]
    [InlineData(CarouselIndicatorPosition.Bottom, "atom:pt-2")]
    [InlineData(CarouselIndicatorPosition.Top, "atom:absolute atom:bottom-full atom:start-0 atom:w-full atom:pb-2")]
    [InlineData(CarouselIndicatorPosition.Inside, "atom:absolute atom:bottom-2 atom:start-0 atom:w-full")]
    public async Task 指示器位置枚举应映射容器定位类(CarouselIndicatorPosition position, string expected)
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, CarouselParams(
            itemCount: 2, showIndicators: true, indicatorPosition: position));

        Assert.Contains(expected, renderer.Markup());
    }

    [Theory]
    [InlineData(CarouselOrientation.Horizontal, "atom:-ms-(--atom-carousel-gap)", "atom:ps-(--atom-carousel-gap)")]
    [InlineData(CarouselOrientation.Vertical, "atom:h-full atom:flex-col atom:-mt-(--atom-carousel-gap)", "atom:pt-(--atom-carousel-gap)")]
    public async Task 方向枚举应映射轨道与项目的轴向类(CarouselOrientation orientation, string track, string item)
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, CarouselParams(itemCount: 2, orientation: orientation));

        var markup = renderer.Markup();
        Assert.Contains(track, markup);
        Assert.Contains(item, markup);
    }

    [Fact]
    public async Task 轮播按钮的aria文案应随暂停态切换()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, CarouselParams(itemCount: 2, autoplay: true));

        Assert.Contains("aria-label=\"Stop automatic rotation\"", renderer.Markup());

        await renderer.RerenderRootAsync(0, CarouselParams(itemCount: 2, autoplay: true, paused: true));
        await FlushAsync(renderer);

        Assert.Contains("aria-label=\"Start automatic rotation\"", renderer.Markup());
    }

    [Fact]
    public async Task JS回调入口应驱动步进暂停与布局钳制()
    {
        var renderer = CreateRenderer();
        var carousel = await MountAsync(renderer, CarouselParams(
            itemCount: 3, showIndicators: true, autoplay: true));

        await renderer.Dispatcher.InvokeAsync(() => carousel.JsOnStep(1));
        Assert.Equal(1, carousel.ActiveIndex);

        await renderer.Dispatcher.InvokeAsync(() => carousel.JsOnStep(-1));
        Assert.Equal(0, carousel.ActiveIndex);

        await renderer.Dispatcher.InvokeAsync(() => carousel.JsOnPause());
        Assert.True(carousel.Paused);

        // 布局上报最大索引 1：指示器收敛为 2 个，活动索引被钳制。
        await renderer.Dispatcher.InvokeAsync(() => carousel.JsOnLayout(1));
        await FlushAsync(renderer);
        Assert.Contains("aria-label=\"Slide 2\"", renderer.Markup());
        Assert.DoesNotContain("aria-label=\"Slide 3\"", renderer.Markup());
    }

    [Theory]
    [InlineData(0.5, null, null)]   // SlidesPerView 小于 1
    [InlineData(null, -1, null)]    // 负间距
    [InlineData(null, null, 999)]   // 间隔小于 1000ms
    public async Task 非法参数应抛出ArgumentOutOfRangeException(double? slidesPerView, int? gap, int? interval)
    {
        var renderer = CreateRenderer();

        var parameters = CarouselParams(itemCount: 2, slidesPerView: slidesPerView, gap: gap);
        if (interval is not null)
        {
            parameters[nameof(Carousel.AutoplayInterval)] = interval.Value;
        }

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => renderer.MountAsync<Carousel>(parameters));
    }

    [Fact]
    public async Task 根样式应输出不变文化的basis与gap并保留用户样式()
    {
        var renderer = CreateRenderer();

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<Carousel>(CarouselParams(
                itemCount: 2, slidesPerView: 2, gap: 24));
        });

        var markup = renderer.Markup();
        Assert.Contains("--atom-carousel-basis:50%;--atom-carousel-gap:24px", markup);
        Assert.DoesNotContain("--bb-carousel", markup);

        var withUser = CreateRenderer();
        await withUser.Dispatcher.InvokeAsync(async () =>
        {
            await withUser.MountAsync<Carousel>(new Dictionary<string, object?>
            {
                [nameof(Carousel.ChildContent)] = CarouselBody(2),
                [nameof(Carousel.SlidesPerView)] = 2.5,
                [nameof(Carousel.AdditionalAttributes)] = new Dictionary<string, object> { ["style"] = "color:red" },
            });
        });

        var merged = withUser.Markup();
        Assert.Contains("--atom-carousel-basis:40%;--atom-carousel-gap:16px;color:red", merged);
    }

    [Fact]
    public async Task 卸载项目应回收计数并钳制活动索引()
    {
        var renderer = CreateRenderer();
        var carousel = await MountAsync(renderer, CarouselParams(
            itemCount: 3, activeIndex: 2));

        await renderer.RerenderRootAsync(0, CarouselParams(itemCount: 1, activeIndex: 2));
        await FlushAsync(renderer);

        Assert.Equal(1, carousel.ItemCount);
        Assert.Equal(0, carousel.ActiveIndex);
    }

    [Fact]
    public async Task 互操作契约应导入carousel模块并派发configure与dispose()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js);

        var carousel = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.MountAsync<Carousel>(CarouselParams(itemCount: 3));
            await FlushAsync(renderer);
            return component;
        });

        // 模块 URL：独立文件（非 barrel），经 JsModules 按电路缓存并带版本查询串。
        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        var url = import.Args![0]!.ToString()!;
        Assert.Contains("./_content/AtomBlazor/js/carousel.js", url);
        Assert.Contains("?v=", url);

        // configure 以 (元素, dotnet 引用, 配置对象) 派发；dispose 在释放时回收浏览器端状态。
        // 模块本身只导入一次（JsModules 按电路缓存），configure 可随重渲染重复派发。
        var configure = js.Invocations.First(i => i.Identifier == "configure");
        Assert.Equal(3, configure.Args!.Length);
        Assert.Single(js.Invocations, i => i.Identifier == "import");

        await renderer.Dispatcher.InvokeAsync(async () => await carousel.DisposeAsync());
        Assert.Contains(js.Invocations, i => i.Identifier == "dispose");
    }
}
