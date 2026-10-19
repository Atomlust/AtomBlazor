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
/// Kbd 渲染测试：Keys 与 ChildContent 的优先级、三档尺寸类映射、
/// 自定义颜色类合并与属性展开。
/// </summary>
public class KbdTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        string? keys = null,
        RenderFragment? childContent = null,
        KbdSize? size = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (keys is not null)
        {
            parameters[nameof(Kbd.Keys)] = keys;
        }
        if (childContent is not null)
        {
            parameters[nameof(Kbd.ChildContent)] = childContent;
        }
        if (size is not null)
        {
            parameters[nameof(Kbd.Size)] = size.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Kbd.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Kbd.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task Keys渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Kbd>(Params(keys: "Ctrl"));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task Keys应渲染为原生kbd元素与文本()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Kbd>(Params(keys: "K"));

        var markup = renderer.Markup();
        Assert.Contains("<kbd", markup);
        Assert.Contains(">K<", markup);
        Assert.Contains("atom:font-mono", markup);
        Assert.Contains("atom:pointer-events-none", markup);
    }

    [Fact]
    public async Task ChildContent应优先于Keys()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Kbd>(Params(
            keys: "ignored",
            childContent: (RenderFragment)(builder => builder.AddContent(0, "⌘"))));

        var markup = renderer.Markup();
        Assert.Contains("⌘", markup);
        Assert.DoesNotContain("ignored", markup);
    }

    [Theory]
    [InlineData(KbdSize.Small, "atom:text-[10px] atom:px-1 atom:py-0.5")]
    [InlineData(KbdSize.Default, "atom:text-xs atom:px-1.5 atom:py-0.5")]
    [InlineData(KbdSize.Large, "atom:text-sm atom:px-2 atom:py-1")]
    public async Task 尺寸变体应映射到对应字号与内边距(KbdSize size, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Kbd>(Params(keys: "Ctrl", size: size));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Fact]
    public async Task 自定义颜色类应覆盖内置中性色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Kbd>(Params(keys: "Esc", @class: "text-red-500"));

        var markup = renderer.Markup();
        Assert.Contains("text-red-500", markup);
        Assert.DoesNotContain("atom:text-muted-foreground", markup);
    }

    [Fact]
    public async Task 额外属性应展开到根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Kbd>(Params(
            keys: "Tab",
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "my-kbd" }));

        Assert.Contains("data-testid=\"my-kbd\"", renderer.Markup());
    }
}
