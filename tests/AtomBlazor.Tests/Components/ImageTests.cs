using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
// Image 为组件库公开类型，别名消歧测试命名空间下的相对查找。

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Image 渲染测试：默认快照、ImageFit 五档枚举映射、懒加载开关、
/// data-state 状态沿（loading→loaded）、失败回退三形态（Fallback 内容、
/// FallbackSrc 图片、默认占位图标）、回退可访问名保留、Src 变化重置失败态，
/// 以及 OnLoad/OnError 回调触发。
/// </summary>
public class ImageTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        string? src = "https://example.com/a.jpg",
        string? alt = "Alt text",
        ImageFit? fit = null,
        bool? lazy = null,
        RenderFragment? fallback = null,
        string? fallbackSrc = null,
        EventCallback? onLoad = null,
        EventCallback? onError = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Image.Src)] = src,
            [nameof(Image.Alt)] = alt,
        };
        if (fit is not null)
        {
            parameters[nameof(Image.Fit)] = fit.Value;
        }
        if (lazy is not null)
        {
            parameters[nameof(Image.Lazy)] = lazy.Value;
        }
        if (fallback is not null)
        {
            parameters[nameof(Image.Fallback)] = fallback;
        }
        if (fallbackSrc is not null)
        {
            parameters[nameof(Image.FallbackSrc)] = fallbackSrc;
        }
        if (onLoad is not null)
        {
            parameters[nameof(Image.OnLoad)] = onLoad.Value;
        }
        if (onError is not null)
        {
            parameters[nameof(Image.OnError)] = onError.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Image.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Image.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params());

        await Verify(renderer.Markup());
    }

    [Theory]
    [InlineData(ImageFit.None, null)]
    [InlineData(ImageFit.Cover, "atom:object-cover")]
    [InlineData(ImageFit.Contain, "atom:object-contain")]
    [InlineData(ImageFit.Fill, "atom:object-fill")]
    [InlineData(ImageFit.ScaleDown, "atom:object-scale-down")]
    public async Task Fit枚举应映射到对应ObjectFit类(ImageFit fit, string? expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params(fit: fit));

        var markup = renderer.Markup();
        if (expectedClass is not null)
        {
            Assert.Contains(expectedClass, markup);
        }
        else
        {
            Assert.DoesNotContain("atom:object-", markup);
        }
    }

    [Fact]
    public async Task 默认懒加载而关闭后移除loading属性()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<Image>(Params());
        Assert.Contains("loading=\"lazy\"", renderer.Markup());

        var eager = CreateRenderer();
        await eager.MountAsync<Image>(Params(lazy: false));
        Assert.DoesNotContain("loading=", eager.Markup());
    }

    [Fact]
    public async Task 状态标记应随加载事件从loading变为loaded()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params());
        Assert.Contains("data-state=\"loading\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onload", new EventArgs()));

        Assert.Contains("data-state=\"loaded\"", renderer.Markup());
    }

    [Fact]
    public async Task 加载失败无回退时渲染默认占位图标()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onerror", new EventArgs()));

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"image-fallback\"", markup);
        Assert.Contains("role=\"img\"", markup);
        Assert.Contains("aria-label=\"Alt text\"", markup);
        Assert.Contains("<svg", markup); // image-off 占位图标
        Assert.DoesNotContain("<img", markup);
    }

    [Fact]
    public async Task 装饰性图片回退应标记隐藏而非img角色()
    {
        var renderer = CreateRenderer();

        // 用自定义回退内容，避免默认占位 LucideIcon 自身 svg 的 role="img" 干扰断言。
        await renderer.MountAsync<Image>(Params(alt: "", fallback: Text("FB")));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onerror", new EventArgs()));

        var markup = renderer.Markup();
        Assert.Contains("aria-hidden=\"true\"", markup);
        Assert.DoesNotContain("role=\"img\"", markup);
        Assert.Contains(">FB</div>", markup);
    }

    [Fact]
    public async Task 自定义Fallback内容优先于FallbackSrc()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params(
            fallback: Text("AB"),
            fallbackSrc: "https://example.com/fb.jpg"));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onerror", new EventArgs()));

        var markup = renderer.Markup();
        Assert.Contains(">AB</div>", markup);
        Assert.DoesNotContain("fb.jpg", markup);
    }

    [Fact]
    public async Task FallbackSrc回退渲染第二张图片且不再重试()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params(fallbackSrc: "https://example.com/fb.jpg"));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onerror", new EventArgs()));

        var markup = renderer.Markup();
        Assert.Contains("src=\"https://example.com/fb.jpg\"", markup);
        Assert.Contains("data-state=\"fallback\"", markup);
        // 回退图片自身没有 onerror 处理器：损坏的回退不再重试，避免循环。
        Assert.DoesNotContain("onerror", markup);
    }

    [Fact]
    public async Task Src变化应重置失败态重新尝试加载()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params(src: "https://bad.example/x.jpg"));
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onerror", new EventArgs()));
        Assert.Contains("data-slot=\"image-fallback\"", renderer.Markup());

        await renderer.RerenderRootAsync(0, Params(src: "https://good.example/y.jpg"));

        var markup = renderer.Markup();
        Assert.Contains("src=\"https://good.example/y.jpg\"", markup);
        Assert.DoesNotContain("image-fallback", markup);
    }

    [Fact]
    public async Task 加载与失败回调应各自触发()
    {
        var loaded = false;
        var errored = false;
        var onLoad = EventCallback.Factory.Create(new object(), () => loaded = true);
        var onError = EventCallback.Factory.Create(new object(), () => errored = true);
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params(onLoad: onLoad));
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onload", new EventArgs()));
        Assert.True(loaded);

        var failing = CreateRenderer();
        await failing.MountAsync<Image>(Params(onError: onError));
        await failing.Dispatcher.InvokeAsync(async () =>
            await failing.DispatchAsync("onerror", new EventArgs()));
        Assert.True(errored);
    }

    [Fact]
    public async Task 类应同时作用于图片与回退保持形状一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params(@class: "rounded-full"));
        Assert.Contains("atom:block atom:max-w-full rounded-full", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onerror", new EventArgs()));

        // 回退 div 同时携带库的居中着色类与使用者的形状类。
        Assert.Contains("atom:flex atom:items-center", renderer.Markup());
        Assert.Contains("atom:justify-center", renderer.Markup());
        Assert.Contains("rounded-full", renderer.Markup());
    }

    [Fact]
    public async Task 额外属性应展开到图片元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Image>(Params(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "hero" }));

        Assert.Contains("data-testid=\"hero\"", renderer.Markup());
    }
}
