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
/// Typography 家族渲染测试：11 个排版组件各自渲染正确的语义标签与基础类，
/// 自定义类合并与属性展开。
/// </summary>
public class TypographyTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(TypographyH1.ChildContent)] = Text("排版样例"),
        };
        if (@class is not null)
        {
            parameters[nameof(TypographyH1.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(TypographyH1.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 全家族渲染快照应与基线一致()
    {
        var types = new[]
        {
            typeof(TypographyH1), typeof(TypographyH2), typeof(TypographyH3), typeof(TypographyH4),
            typeof(TypographyP), typeof(TypographyLead), typeof(TypographyLarge),
            typeof(TypographySmall), typeof(TypographyMuted), typeof(TypographyInlineCode),
            typeof(TypographyBlockquote),
        };

        var markup = new System.Text.StringBuilder();
        foreach (var type in types)
        {
            var renderer = CreateRenderer();
            await renderer.MountTypeAsync(type, Params());
            markup.AppendLine(renderer.Markup());
        }

        await Verify(markup.ToString());
    }

    [Theory]
    [InlineData(typeof(TypographyH1), "<h1", "atom:text-4xl")]
    [InlineData(typeof(TypographyH2), "<h2", "atom:text-3xl")]
    [InlineData(typeof(TypographyH3), "<h3", "atom:text-2xl")]
    [InlineData(typeof(TypographyH4), "<h4", "atom:text-xl")]
    [InlineData(typeof(TypographyP), "<p", "atom:leading-7")]
    [InlineData(typeof(TypographyLead), "<p", "atom:text-xl")]
    [InlineData(typeof(TypographyMuted), "<p", "atom:text-sm")]
    [InlineData(typeof(TypographySmall), "<small", "atom:leading-none")]
    [InlineData(typeof(TypographyLarge), "<div", "atom:text-lg")]
    [InlineData(typeof(TypographyInlineCode), "<code", "atom:font-mono")]
    [InlineData(typeof(TypographyBlockquote), "<blockquote", "atom:italic")]
    public async Task 每个组件应渲染正确的标签与基础类(Type type, string expectedTag, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountTypeAsync(type, Params());

        var markup = renderer.Markup();
        Assert.Contains(expectedTag, markup);
        Assert.Contains(expectedClass, markup);
        Assert.Contains("排版样例", markup);
    }

    [Fact]
    public async Task 自定义类应合并进排版元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountTypeAsync(typeof(TypographyH1), Params(@class: "atom:text-red-500"));

        var markup = renderer.Markup();
        Assert.Contains("atom:text-red-500", markup);
        Assert.Contains("atom:scroll-m-20", markup);
    }

    [Fact]
    public async Task 额外属性应展开到排版元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountTypeAsync(typeof(TypographyH2), Params(
            additionalAttributes: new Dictionary<string, object> { ["id"] = "section-title" }));

        Assert.Contains("id=\"section-title\"", renderer.Markup());
    }

    [Fact]
    public async Task 二级标题应带下边框类()
    {
        var renderer = CreateRenderer();

        await renderer.MountTypeAsync(typeof(TypographyH2), Params());

        Assert.Contains("atom:border-b", renderer.Markup());
    }

    [Fact]
    public async Task 引用块应使用逻辑内联起始边框()
    {
        var renderer = CreateRenderer();

        await renderer.MountTypeAsync(typeof(TypographyBlockquote), Params());

        var markup = renderer.Markup();
        Assert.Contains("atom:border-s-2", markup);
        Assert.Contains("atom:ps-6", markup);
    }
}
