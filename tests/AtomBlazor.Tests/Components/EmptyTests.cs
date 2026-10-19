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
/// Empty 渲染测试：标题/描述/图标/额外内容的插槽渲染、三档尺寸类映射、
/// 自定义类合并与属性展开。
/// </summary>
public class EmptyTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        string? title = null,
        string? description = null,
        EmptySize? size = null,
        RenderFragment? icon = null,
        RenderFragment? childContent = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (title is not null)
        {
            parameters[nameof(Empty.Title)] = title;
        }
        if (description is not null)
        {
            parameters[nameof(Empty.Description)] = description;
        }
        if (size is not null)
        {
            parameters[nameof(Empty.Size)] = size.Value;
        }
        if (icon is not null)
        {
            parameters[nameof(Empty.Icon)] = icon;
        }
        if (childContent is not null)
        {
            parameters[nameof(Empty.ChildContent)] = childContent;
        }
        if (@class is not null)
        {
            parameters[nameof(Empty.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Empty.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Empty>(Params(
            title: "暂无数据",
            description: "这里还没有任何内容。"));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 标题应渲染为h3描述应渲染为p()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Empty>(Params(title: "标题", description: "描述"));

        var markup = renderer.Markup();
        Assert.Contains("<h3", markup);
        Assert.Contains("标题", markup);
        Assert.Contains("<p", markup);
        Assert.Contains("描述", markup);
    }

    [Fact]
    public async Task 空标题或空描述应跳过对应元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Empty>(Params());

        var markup = renderer.Markup();
        Assert.DoesNotContain("<h3", markup);
        Assert.DoesNotContain("atom:font-semibold", markup);
    }

    [Fact]
    public async Task 图标插槽应包裹在图标容器中()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Empty>(Params(icon: Text("[icon]")));

        var markup = renderer.Markup();
        Assert.Contains("[icon]", markup);
        Assert.Contains("atom:mb-4", markup);
    }

    [Fact]
    public async Task 额外内容应渲染在上边距容器中()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Empty>(Params(childContent: Text("操作按钮")));

        var markup = renderer.Markup();
        Assert.Contains("atom:mt-4", markup);
        Assert.Contains("操作按钮", markup);
    }

    [Theory]
    [InlineData(EmptySize.Small, "atom:py-6", "atom:h-8", "atom:text-sm")]
    [InlineData(EmptySize.Default, "atom:py-12", "atom:h-12", "atom:text-lg")]
    [InlineData(EmptySize.Large, "atom:py-20", "atom:h-16", "atom:text-xl")]
    public async Task 尺寸枚举应映射内边距图标与标题类(EmptySize size, string expectedPadding, string expectedIconSize, string expectedTitleSize)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Empty>(Params(title: "标题", size: size, icon: Text("[icon]")));

        var markup = renderer.Markup();
        Assert.Contains(expectedPadding, markup);
        Assert.Contains(expectedIconSize, markup);
        Assert.Contains(expectedTitleSize, markup);
    }

    [Fact]
    public async Task 自定义类应合并进容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Empty>(Params(@class: "atom:border"));

        var markup = renderer.Markup();
        Assert.Contains("atom:border", markup);
        Assert.Contains("atom:flex-col", markup);
    }

    [Fact]
    public async Task 额外属性应展开到容器根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Empty>(Params(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "empty" }));

        Assert.Contains("data-testid=\"empty\"", renderer.Markup());
    }
}
