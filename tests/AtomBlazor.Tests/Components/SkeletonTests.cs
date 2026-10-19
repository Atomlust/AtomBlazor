using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Skeleton 渲染测试：形状类映射、Width/Height 内联样式、
/// 使用者 style 与组件样式的合并（不被 splat 覆盖）与类覆盖语义。
/// </summary>
public class SkeletonTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        SkeletonShape? shape = null,
        string? width = null,
        string? height = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (shape is not null)
        {
            parameters[nameof(Skeleton.Shape)] = shape.Value;
        }
        if (width is not null)
        {
            parameters[nameof(Skeleton.Width)] = width;
        }
        if (height is not null)
        {
            parameters[nameof(Skeleton.Height)] = height;
        }
        if (@class is not null)
        {
            parameters[nameof(Skeleton.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Skeleton.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Skeleton>(Params(@class: "h-4 w-[250px]"));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 应带脉冲动画与中性背景类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Skeleton>(Params());

        var markup = renderer.Markup();
        Assert.Contains("atom:animate-pulse", markup);
        Assert.Contains("atom:bg-muted", markup);
    }

    [Theory]
    [InlineData(SkeletonShape.Rectangular, "atom:rounded-md")]
    [InlineData(SkeletonShape.Circular, "atom:rounded-full")]
    public async Task 形状变体应映射到对应圆角类(SkeletonShape shape, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Skeleton>(Params(shape: shape));

        Assert.Contains(expectedClass, renderer.Markup());
    }

    [Fact]
    public async Task 显式宽高应产出内联样式()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Skeleton>(Params(width: "250px", height: "20px"));

        Assert.Contains("style=\"width:250px;height:20px\"", renderer.Markup());
    }

    [Fact]
    public async Task 仅设置宽度时应只产出width声明()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Skeleton>(Params(width: "50%"));

        Assert.Contains("style=\"width:50%\"", renderer.Markup());
    }

    [Fact]
    public async Task 使用者style应与组件内联样式合并而非覆盖()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Skeleton>(Params(
            width: "200px",
            additionalAttributes: new Dictionary<string, object> { ["style"] = "color:red" }));

        var markup = renderer.Markup();
        Assert.Contains("width:200px; color:red", markup);
    }

    [Fact]
    public async Task 自定义类应经TailwindMerge覆盖内置圆角()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Skeleton>(Params(@class: "rounded-none"));

        var markup = renderer.Markup();
        Assert.Contains("rounded-none", markup);
        Assert.DoesNotContain("atom:rounded-md", markup);
    }

    [Fact]
    public async Task 额外属性应展开到根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Skeleton>(Params(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "my-skeleton" }));

        Assert.Contains("data-testid=\"my-skeleton\"", renderer.Markup());
    }
}
