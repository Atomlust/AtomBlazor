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
/// Badge 渲染测试：全部 12 个变体的类名映射、通知圆点的开关/位置/颜色，
/// 以及自定义类合并与属性展开。
/// </summary>
public class BadgeTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        BadgeVariant? variant = null,
        string? text = null,
        bool showDot = false,
        BadgeDotPosition? dotPosition = null,
        string? dotClass = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (variant is not null)
        {
            parameters[nameof(Badge.Variant)] = variant.Value;
        }
        if (text is not null)
        {
            parameters[nameof(Badge.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, text));
        }
        if (showDot)
        {
            parameters[nameof(Badge.ShowDot)] = true;
        }
        if (dotPosition is not null)
        {
            parameters[nameof(Badge.DotPosition)] = dotPosition.Value;
        }
        if (dotClass is not null)
        {
            parameters[nameof(Badge.DotClass)] = dotClass;
        }
        if (@class is not null)
        {
            parameters[nameof(Badge.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Badge.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认变体渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Badge>(Params(text: "New"));

        await Verify(renderer.Markup());
    }

    [Theory]
    [InlineData(BadgeVariant.Default, "atom:bg-primary")]
    [InlineData(BadgeVariant.Secondary, "atom:bg-secondary")]
    [InlineData(BadgeVariant.Destructive, "atom:bg-destructive")]
    [InlineData(BadgeVariant.Outline, "atom:text-foreground")]
    [InlineData(BadgeVariant.Success, "atom:bg-emerald-700")]
    [InlineData(BadgeVariant.Warning, "atom:bg-amber-300")]
    [InlineData(BadgeVariant.Info, "atom:bg-blue-700")]
    [InlineData(BadgeVariant.Soft, "atom:bg-primary/10")]
    [InlineData(BadgeVariant.SoftDestructive, "atom:bg-destructive/10")]
    [InlineData(BadgeVariant.SoftSuccess, "atom:dark:bg-emerald-950")]
    [InlineData(BadgeVariant.SoftWarning, "atom:dark:bg-amber-950")]
    [InlineData(BadgeVariant.SoftInfo, "atom:dark:bg-blue-950")]
    public async Task 全部变体应映射到对应的着色类(BadgeVariant variant, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Badge>(Params(variant: variant, text: "标签"));

        Assert.Contains(expectedClass, renderer.Markup());
    }

    [Fact]
    public async Task 基础类应包含圆角胶囊与内联排布形态()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Badge>(Params(text: "5"));

        var markup = renderer.Markup();
        Assert.Contains("atom:inline-flex", markup);
        Assert.Contains("atom:rounded-full", markup);
        Assert.Contains("atom:text-xs", markup);
    }

    [Fact]
    public async Task 显示圆点应加相对定位并渲染圆点span()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Badge>(Params(text: "在线", showDot: true));

        var markup = renderer.Markup();
        Assert.Contains("atom:relative", markup);
        Assert.Contains("atom:rounded-full atom:ring-2 atom:ring-background", markup);
        Assert.Contains("atom:bg-primary", markup);
    }

    [Theory]
    [InlineData(BadgeDotPosition.TopRight, "atom:-top-1 atom:-right-1")]
    [InlineData(BadgeDotPosition.TopLeft, "atom:-top-1 atom:-left-1")]
    [InlineData(BadgeDotPosition.BottomRight, "atom:-bottom-1 atom:-right-1")]
    [InlineData(BadgeDotPosition.BottomLeft, "atom:-bottom-1 atom:-left-1")]
    public async Task 圆点位置应映射到对应物理角落类(BadgeDotPosition position, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Badge>(Params(showDot: true, dotPosition: position));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Fact]
    public async Task 自定义圆点类应替换默认主色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Badge>(Params(variant: BadgeVariant.Outline, showDot: true, dotClass: "bg-green-500"));

        var markup = renderer.Markup();
        Assert.Contains("bg-green-500", markup);
        Assert.DoesNotContain("atom:bg-primary", markup);
    }

    [Fact]
    public async Task 自定义类应经TailwindMerge覆盖内置变体背景()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Badge>(Params(variant: BadgeVariant.Default, text: "品牌", @class: "bg-purple-500"));

        var markup = renderer.Markup();
        Assert.Contains("bg-purple-500", markup);
        Assert.DoesNotContain("atom:bg-primary", markup);
    }

    [Fact]
    public async Task 额外属性应展开到根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Badge>(Params(
            text: "3",
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "my-badge" }));

        Assert.Contains("data-testid=\"my-badge\"", renderer.Markup());
    }
}
