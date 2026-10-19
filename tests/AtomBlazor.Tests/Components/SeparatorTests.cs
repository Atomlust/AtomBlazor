using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Separator 渲染测试：装饰/语义两种 ARIA 模式、水平与垂直方向的类映射、
/// 线条样式（实线/虚线/点线）与自定义类合并。
/// </summary>
public class SeparatorTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        SeparatorOrientation? orientation = null,
        bool? decorative = null,
        SeparatorLineStyle? lineStyle = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (orientation is not null)
        {
            parameters[nameof(Separator.Orientation)] = orientation.Value;
        }
        if (decorative is not null)
        {
            parameters[nameof(Separator.Decorative)] = decorative.Value;
        }
        if (lineStyle is not null)
        {
            parameters[nameof(Separator.LineStyle)] = lineStyle.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Separator.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Separator.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Separator>(Params());

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 默认应为装饰模式并渲染none角色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Separator>(Params());

        var markup = renderer.Markup();
        Assert.Contains("role=\"none\"", markup);
        Assert.DoesNotContain("aria-orientation", markup);
        Assert.Contains("data-orientation=\"horizontal\"", markup);
    }

    [Fact]
    public async Task 语义模式应携带separator角色与aria方向()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Separator>(Params(decorative: false));

        var markup = renderer.Markup();
        Assert.Contains("role=\"separator\"", markup);
        Assert.Contains("aria-orientation=\"horizontal\"", markup);
    }

    [Fact]
    public async Task 垂直方向应切换尺寸类与aria方向()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Separator>(Params(orientation: SeparatorOrientation.Vertical, decorative: false));

        var markup = renderer.Markup();
        Assert.Contains("data-orientation=\"vertical\"", markup);
        Assert.Contains("aria-orientation=\"vertical\"", markup);
        Assert.Contains("atom:h-full", markup);
        Assert.Contains("atom:w-[1px]", markup);
    }

    [Theory]
    [InlineData(SeparatorLineStyle.Dashed, "atom:border-dashed")]
    [InlineData(SeparatorLineStyle.Dotted, "atom:border-dotted")]
    public async Task 非实线样式应映射边框类(SeparatorLineStyle lineStyle, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Separator>(Params(lineStyle: lineStyle));

        var markup = renderer.Markup();
        Assert.Contains(expectedClass, markup);
        Assert.Contains("atom:bg-transparent", markup);
        Assert.Contains("atom:border-t", markup);
    }

    [Fact]
    public async Task 垂直虚线应使用逻辑边框起始类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Separator>(Params(orientation: SeparatorOrientation.Vertical, lineStyle: SeparatorLineStyle.Dashed));

        var markup = renderer.Markup();
        Assert.Contains("atom:border-s", markup);
        Assert.Contains("atom:w-0", markup);
    }

    [Fact]
    public async Task 自定义类应合并进分隔符()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Separator>(Params(@class: "atom:my-4"));

        Assert.Contains("atom:my-4", renderer.Markup());
    }

    [Fact]
    public async Task 额外属性应展开到分隔符根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Separator>(Params(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "divider" }));

        Assert.Contains("data-testid=\"divider\"", renderer.Markup());
    }
}
