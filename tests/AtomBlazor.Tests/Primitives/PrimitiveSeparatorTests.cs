using AtomBlazor.Primitives;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using PrimitiveSeparator = AtomBlazor.Primitives.SeparatorPrimitive;
using PrimitiveSeparatorOrientation = AtomBlazor.Primitives.SeparatorOrientation;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// Separator 原语测试：装饰/语义模式的 role 与 aria-orientation 映射、
/// data-orientation 与属性展开。
/// </summary>
public class PrimitiveSeparatorTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        PrimitiveSeparatorOrientation? orientation = null,
        bool? decorative = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (orientation is not null)
        {
            parameters[nameof(PrimitiveSeparator.Orientation)] = orientation.Value;
        }
        if (decorative is not null)
        {
            parameters[nameof(PrimitiveSeparator.Decorative)] = decorative.Value;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(PrimitiveSeparator.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认装饰模式应渲染none角色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveSeparator>(Params());

        var markup = renderer.Markup();
        Assert.Contains("role=\"none\"", markup);
        Assert.Contains("data-orientation=\"horizontal\"", markup);
        Assert.DoesNotContain("aria-orientation", markup);
    }

    [Fact]
    public async Task 语义模式应携带separator角色与方向()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveSeparator>(Params(
            orientation: PrimitiveSeparatorOrientation.Vertical,
            decorative: false));

        var markup = renderer.Markup();
        Assert.Contains("role=\"separator\"", markup);
        Assert.Contains("aria-orientation=\"vertical\"", markup);
        Assert.Contains("data-orientation=\"vertical\"", markup);
    }

    [Fact]
    public async Task 额外属性应展开到分隔符元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveSeparator>(Params(
            additionalAttributes: new Dictionary<string, object> { ["id"] = "sep" }));

        Assert.Contains("id=\"sep\"", renderer.Markup());
    }
}
