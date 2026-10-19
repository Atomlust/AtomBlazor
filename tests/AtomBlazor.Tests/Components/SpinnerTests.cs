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
/// Spinner 渲染测试：role/aria-label 无障碍语义、三档尺寸类、
/// 自定义颜色（Class 含 text- 时去掉默认中性色）与属性展开。
/// </summary>
public class SpinnerTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        SpinnerSize? size = null,
        string? ariaLabel = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (size is not null)
        {
            parameters[nameof(Spinner.Size)] = size.Value;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(Spinner.AriaLabel)] = ariaLabel;
        }
        if (@class is not null)
        {
            parameters[nameof(Spinner.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Spinner.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Spinner>(Params());

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 应带status角色与默认无障碍标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Spinner>(Params());

        var markup = renderer.Markup();
        Assert.Contains("role=\"status\"", markup);
        Assert.Contains("aria-label=\"Loading\"", markup);
        Assert.Contains("atom:animate-spin", markup);
    }

    [Fact]
    public async Task 自定义AriaLabel应替换默认标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Spinner>(Params(ariaLabel: "正在保存"));

        Assert.Contains("aria-label=\"正在保存\"", renderer.Markup());
    }

    [Theory]
    [InlineData(SpinnerSize.Small, "atom:h-4 atom:w-4")]
    [InlineData(SpinnerSize.Default, "atom:h-6 atom:w-6")]
    [InlineData(SpinnerSize.Large, "atom:h-10 atom:w-10")]
    public async Task 尺寸变体应映射到对应宽高类(SpinnerSize size, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Spinner>(Params(size: size));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Fact]
    public async Task Class含颜色类时应去掉默认中性色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Spinner>(Params(@class: "text-blue-500"));

        var markup = renderer.Markup();
        Assert.Contains("text-blue-500", markup);
        Assert.DoesNotContain("atom:text-muted-foreground", markup);
    }

    [Fact]
    public async Task Class不含颜色类时应保留默认中性色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Spinner>(Params(@class: "h-8"));

        Assert.Contains("atom:text-muted-foreground", renderer.Markup());
    }

    [Fact]
    public async Task 额外属性应展开到根svg()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Spinner>(Params(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "my-spinner" }));

        Assert.Contains("data-testid=\"my-spinner\"", renderer.Markup());
    }
}
