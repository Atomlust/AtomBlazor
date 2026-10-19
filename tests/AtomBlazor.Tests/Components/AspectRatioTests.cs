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
/// AspectRatio 渲染测试：aspect-ratio 内联样式的不变区域计算、内容包装层、
/// 使用者 style 的合并语义与类名合并。
/// </summary>
public class AspectRatioTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        double? ratio = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AspectRatio.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "img");
                builder.AddAttribute(1, "alt", "样例");
                builder.CloseElement();
            }),
        };
        if (ratio is not null)
        {
            parameters[nameof(AspectRatio.Ratio)] = ratio.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(AspectRatio.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(AspectRatio.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AspectRatio>(Params());

        await Verify(renderer.Markup());
    }

    [Theory]
    [InlineData(1.0, "aspect-ratio: 1.0000;")]
    [InlineData(16.0 / 9.0, "aspect-ratio: 1.7778;")]
    [InlineData(4.0 / 3.0, "aspect-ratio: 1.3333;")]
    public async Task Ratio应按不变区域格式化进内联样式(double ratio, string expectedFragment)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AspectRatio>(Params(ratio: ratio));

        var markup = renderer.Markup();
        Assert.Contains(expectedFragment, markup);
        Assert.Contains("position: relative;", markup);
        Assert.Contains("width: 100%;", markup);
    }

    [Fact]
    public async Task 内容应包裹在ratio包装层中()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AspectRatio>(Params());

        var markup = renderer.Markup();
        Assert.Contains("class=\"aspect-ratio-content\"", markup);
        Assert.Contains("<img", markup);
    }

    [Fact]
    public async Task 使用者style应与组件样式合并且仅出现一个style属性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AspectRatio>(Params(
            ratio: 2.0,
            additionalAttributes: new Dictionary<string, object> { ["style"] = "border-radius: 8px" }));

        var markup = renderer.Markup();
        Assert.Contains("aspect-ratio: 2.0000;", markup);
        Assert.Contains("border-radius: 8px", markup);
        Assert.Equal(1, CountOccurrences(markup, "style="));
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    [Fact]
    public async Task 自定义类应合并进容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AspectRatio>(Params(@class: "atom:rounded-lg"));

        var markup = renderer.Markup();
        Assert.Contains("atom:rounded-lg", markup);
        Assert.Contains("atom:overflow-hidden", markup);
    }

    [Fact]
    public async Task 额外属性应展开到容器根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AspectRatio>(Params(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "ratio" }));

        Assert.Contains("data-testid=\"ratio\"", renderer.Markup());
    }
}
