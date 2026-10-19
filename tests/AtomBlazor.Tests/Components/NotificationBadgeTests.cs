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
/// NotificationBadge 渲染测试：计数格式化与 Maximum 溢出封顶（可访问标签保留完整计数）、
/// 零计数与显隐规则、圆点模式独立性、aria-label 组装与覆盖、四角位置映射、
/// 徽章配色变体、BadgeClass 覆盖、参数越界校验与属性展开。
/// </summary>
public class NotificationBadgeTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        int? count = null,
        int? maximum = null,
        bool showZero = false,
        bool dot = false,
        string? ariaLabel = null,
        BadgeDotPosition? position = null,
        BadgeVariant? variant = null,
        RenderFragment? childContent = null,
        string? @class = null,
        string? badgeClass = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (count is not null)
        {
            parameters[nameof(NotificationBadge.Count)] = count.Value;
        }
        if (maximum is not null)
        {
            parameters[nameof(NotificationBadge.Maximum)] = maximum.Value;
        }
        if (showZero)
        {
            parameters[nameof(NotificationBadge.ShowZero)] = true;
        }
        if (dot)
        {
            parameters[nameof(NotificationBadge.Dot)] = true;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(NotificationBadge.AriaLabel)] = ariaLabel;
        }
        if (position is not null)
        {
            parameters[nameof(NotificationBadge.Position)] = position.Value;
        }
        if (variant is not null)
        {
            parameters[nameof(NotificationBadge.Variant)] = variant.Value;
        }
        parameters[nameof(NotificationBadge.ChildContent)] = childContent ?? Text("[icon]");
        if (@class is not null)
        {
            parameters[nameof(NotificationBadge.Class)] = @class;
        }
        if (badgeClass is not null)
        {
            parameters[nameof(NotificationBadge.BadgeClass)] = badgeClass;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(NotificationBadge.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 5));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 计数应显示且带status角色与本地化标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 5));

        var markup = renderer.Markup();
        Assert.Contains("role=\"status\"", markup);
        Assert.Contains("aria-label=\"5 notifications\"", markup);
        Assert.Contains(">5</span>", markup);
    }

    [Fact]
    public async Task 超出Maximum应封顶显示但标签保留完整计数()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 128, maximum: 99));

        var markup = renderer.Markup();
        Assert.Contains(">99+</span>", markup);
        Assert.Contains("aria-label=\"128 notifications\"", markup);
        Assert.DoesNotContain(">128<", markup);
    }

    [Fact]
    public async Task 恰等于Maximum的计数不应加加号()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 99));

        var markup = renderer.Markup();
        Assert.Contains(">99</span>", markup);
        Assert.DoesNotContain("99+", markup);
    }

    [Fact]
    public async Task 零计数默认隐藏而ShowZero使其可见()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 0));
        Assert.DoesNotContain("role=\"status\"", renderer.Markup());

        var showZero = CreateRenderer();
        await showZero.MountAsync<NotificationBadge>(Params(count: 0, showZero: true));
        var markup = showZero.Markup();
        Assert.Contains(">0</span>", markup);
        Assert.Contains("aria-label=\"0 notifications\"", markup);
    }

    [Fact]
    public async Task Count为空且非圆点时不渲染徽章()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params());

        var markup = renderer.Markup();
        Assert.Contains("[icon]", markup);
        Assert.DoesNotContain("role=\"status\"", markup);
    }

    [Fact]
    public async Task 圆点模式不显示计数并采用本地化未读标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(dot: true));

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Unread notifications\"", markup);
        Assert.Contains("atom:size-2", markup);
        Assert.DoesNotContain("atom:h-4 atom:min-w-4", markup);
    }

    [Fact]
    public async Task 圆点模式与计数相互独立可同现()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 7, dot: true));

        var markup = renderer.Markup();
        Assert.Contains("atom:size-2", markup);
        // Dot 时可见文本为空——计数只在 aria-label 里。
        Assert.Contains("aria-label=\"Unread notifications\"", markup);
    }

    [Fact]
    public async Task 自定义AriaLabel应覆盖默认描述()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 3, ariaLabel: "新账号动态"));

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"新账号动态\"", markup);
        Assert.DoesNotContain("3 notifications", markup);
    }

    [Theory]
    [InlineData(BadgeDotPosition.TopRight, "atom:-top-1 atom:-right-1")]
    [InlineData(BadgeDotPosition.TopLeft, "atom:-top-1 atom:-left-1")]
    [InlineData(BadgeDotPosition.BottomRight, "atom:-bottom-1 atom:-right-1")]
    [InlineData(BadgeDotPosition.BottomLeft, "atom:-bottom-1 atom:-left-1")]
    public async Task 四个角落应映射到对应物理定位类(BadgeDotPosition position, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 1, position: position));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Theory]
    [InlineData(BadgeVariant.Destructive, "atom:bg-destructive")]
    [InlineData(BadgeVariant.Default, "atom:bg-primary")]
    [InlineData(BadgeVariant.Secondary, "atom:bg-secondary")]
    [InlineData(BadgeVariant.Outline, "atom:border atom:bg-background")]
    public async Task 徽章配色变体应映射到对应着色类(BadgeVariant variant, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 1, variant: variant));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Fact]
    public async Task BadgeClass应并入徽章而Class并入外层容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(count: 2, @class: "align-middle", badgeClass: "shadow-md"));

        var markup = renderer.Markup();
        Assert.Contains("atom:relative atom:inline-flex align-middle", markup);
        Assert.Contains("shadow-md", markup);
    }

    [Fact]
    public async Task 额外属性应展开到外层容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<NotificationBadge>(Params(
            count: 1,
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "notif" }));

        Assert.Contains("data-testid=\"notif\"", renderer.Markup());
    }

    [Theory]
    [InlineData(-1)]
    public async Task 负计数应抛出参数越界异常(int count)
    {
        var renderer = CreateRenderer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            renderer.MountAsync<NotificationBadge>(Params(count: count)));
    }

    [Fact]
    public async Task Maximum小于1应抛出参数越界异常()
    {
        var renderer = CreateRenderer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            renderer.MountAsync<NotificationBadge>(Params(count: 1, maximum: 0)));
    }
}
