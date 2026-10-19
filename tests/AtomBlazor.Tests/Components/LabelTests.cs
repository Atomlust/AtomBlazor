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
/// Label 家族渲染测试：包装层经 Primitives.Label 基元渲染语义化 label、
/// For 映射为 for 特性、默认类串含 peer-disabled 禁用态、自定义类合并与属性展开；
/// 基元侧守护无样式的裸 label 形态。
/// </summary>
public class LabelTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        string? @for = null,
        string? @class = null,
        RenderFragment? childContent = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (@for is not null)
        {
            parameters[nameof(Label.For)] = @for;
        }
        if (@class is not null)
        {
            parameters[nameof(Label.Class)] = @class;
        }
        if (childContent is not null)
        {
            parameters[nameof(Label.ChildContent)] = childContent;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Label.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    // ------------------------------------------------------------------
    // 包装层
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Label>(Params(@for: "email", childContent: Text("Email Address")));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task For参数应映射为label的for特性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Label>(Params(@for: "email", childContent: Text("Email")));

        var markup = renderer.Markup();
        Assert.Contains("<label", markup);
        Assert.Contains("for=\"email\"", markup);
    }

    [Fact]
    public async Task 默认类串应包含排版与peer禁用态样式()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Label>(Params(childContent: Text("Email")));

        var markup = renderer.Markup();
        Assert.Contains("atom:text-sm", markup);
        Assert.Contains("atom:font-medium", markup);
        Assert.Contains("atom:leading-none", markup);
        Assert.Contains("atom:peer-disabled:cursor-not-allowed", markup);
        Assert.Contains("atom:peer-disabled:opacity-70", markup);
    }

    [Fact]
    public async Task 自定义类应与默认类合并()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Label>(Params(@class: "atom:font-bold", childContent: Text("Email")));

        var markup = renderer.Markup();
        Assert.Contains("atom:font-bold", markup);
        Assert.Contains("atom:text-sm", markup);
    }

    [Fact]
    public async Task 属性展开应落到label元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Label>(Params(
            childContent: Text("Email"),
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "label-root" }));

        Assert.Contains("data-testid=\"label-root\"", renderer.Markup());
    }

    [Fact]
    public async Task 必填标识等嵌套内容应原样渲染()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Label>(Params(@for: "username", childContent: builder =>
        {
            builder.AddContent(0, "Username ");
            builder.OpenElement(1, "span");
            builder.AddAttribute(2, "class", "atom:text-destructive");
            builder.AddContent(3, "*");
            builder.CloseElement();
        }));

        var markup = renderer.Markup();
        Assert.Contains("for=\"username\"", markup);
        Assert.Contains("<span class=\"atom:text-destructive\">*</span>", markup);
    }

    // ------------------------------------------------------------------
    // Primitives 基元层
    // ------------------------------------------------------------------

    [Fact]
    public async Task 基元Label应渲染裸label不带样式类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.LabelPrimitive>(new Dictionary<string, object?>
        {
            [nameof(AtomBlazor.Primitives.LabelPrimitive.For)] = "terms",
            [nameof(AtomBlazor.Primitives.LabelPrimitive.ChildContent)] = Text("Accept terms"),
        });

        var markup = renderer.Markup();
        Assert.Equal("<label for=\"terms\">Accept terms</label>", markup);
    }

    [Fact]
    public async Task 基元Label属性展开直通元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.LabelPrimitive>(new Dictionary<string, object?>
        {
            [nameof(AtomBlazor.Primitives.LabelPrimitive.AdditionalAttributes)] =
                new Dictionary<string, object> { ["id"] = "raw-label" },
        });

        Assert.Contains("id=\"raw-label\"", renderer.Markup());
    }
}
