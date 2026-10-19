using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// SectionHeader 渲染测试：标题级别 1–6 的元素映射与越界校验、描述与操作插槽、
/// 分隔线开关、自定义类合并与属性展开。
/// </summary>
public class SectionHeaderTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        int? headingLevel = null,
        string? title = null,
        string? description = null,
        RenderFragment? actions = null,
        bool separator = false,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (headingLevel is not null)
        {
            parameters[nameof(SectionHeader.HeadingLevel)] = headingLevel.Value;
        }
        if (title is not null)
        {
            parameters[nameof(SectionHeader.Title)] = title;
        }
        if (description is not null)
        {
            parameters[nameof(SectionHeader.Description)] = description;
        }
        if (actions is not null)
        {
            parameters[nameof(SectionHeader.Actions)] = actions;
        }
        if (separator)
        {
            parameters[nameof(SectionHeader.Separator)] = true;
        }
        if (@class is not null)
        {
            parameters[nameof(SectionHeader.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(SectionHeader.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SectionHeader>(Params(
            title: "近期文档",
            description: "团队本周编辑过的文件。",
            actions: Text("[操作]")));

        await Verify(renderer.Markup());
    }

    [Theory]
    [InlineData(1, "<h1")]
    [InlineData(2, "<h2")]
    [InlineData(3, "<h3")]
    [InlineData(4, "<h4")]
    [InlineData(5, "<h5")]
    [InlineData(6, "<h6")]
    public async Task 标题级别应映射到对应语义标题元素(int level, string expectedTag)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SectionHeader>(Params(headingLevel: level, title: "标题"));

        Assert.Contains(expectedTag, renderer.Markup());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public async Task 标题级别越界应抛出参数越界异常(int level)
    {
        var renderer = CreateRenderer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            renderer.MountAsync<SectionHeader>(Params(headingLevel: level, title: "标题")));
    }

    [Fact]
    public async Task 未指定级别时默认渲染h2()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SectionHeader>(Params(title: "标题"));

        Assert.Contains("<h2", renderer.Markup());
    }

    [Fact]
    public async Task 描述应渲染为p且空描述应跳过()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SectionHeader>(Params(title: "标题", description: "说明文字"));
        Assert.Contains("<p class=\"atom:text-sm atom:text-muted-foreground\">说明文字</p>", renderer.Markup());

        var noDescription = CreateRenderer();
        await noDescription.MountAsync<SectionHeader>(Params(title: "标题"));
        Assert.DoesNotContain("<p", noDescription.Markup());
    }

    [Fact]
    public async Task 操作插槽应渲染在尾部收缩容器中()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SectionHeader>(Params(title: "标题", actions: Text("[新建]")));

        var markup = renderer.Markup();
        Assert.Contains("atom:shrink-0", markup);
        Assert.Contains("[新建]", markup);
    }

    [Fact]
    public async Task 分隔线开关应控制底部边框类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SectionHeader>(Params(title: "标题", separator: true));
        Assert.Contains("atom:border-b atom:pb-3", renderer.Markup());

        var plain = CreateRenderer();
        await plain.MountAsync<SectionHeader>(Params(title: "标题"));
        Assert.DoesNotContain("atom:border-b", plain.Markup());
    }

    [Fact]
    public async Task 自定义类应合并且额外属性应展开到根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SectionHeader>(Params(
            title: "标题",
            @class: "mt-6",
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "section" }));

        var markup = renderer.Markup();
        Assert.Contains("mt-6", markup);
        Assert.Contains("data-testid=\"section\"", markup);
    }
}
