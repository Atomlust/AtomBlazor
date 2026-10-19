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
/// Avatar 家族渲染测试：默认快照、四档尺寸映射、圆点指示器与 DotClass 覆盖、
/// AvatarGroup 级联边框、头像组快照，以及 AvatarImage 加载失败自动隐藏
/// 让位 AvatarFallback 的行为与 Source 变化时的错误态重置。
/// </summary>
public class AvatarTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> AvatarParams(
        AvatarSize? size = null,
        bool showDot = false,
        string? dotClass = null,
        string? @class = null,
        RenderFragment? childContent = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (size is not null)
        {
            parameters[nameof(Avatar.Size)] = size.Value;
        }
        if (showDot)
        {
            parameters[nameof(Avatar.ShowDot)] = true;
        }
        if (dotClass is not null)
        {
            parameters[nameof(Avatar.DotClass)] = dotClass;
        }
        if (@class is not null)
        {
            parameters[nameof(Avatar.Class)] = @class;
        }
        parameters[nameof(Avatar.ChildContent)] = childContent ?? Text("JD");
        if (additionalAttributes is not null)
        {
            parameters[nameof(Avatar.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    private static Dictionary<string, object?> ImageParams(
        string? source = null,
        string? alt = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (source is not null)
        {
            parameters[nameof(AvatarImage.Source)] = source;
        }
        if (alt is not null)
        {
            parameters[nameof(AvatarImage.Alt)] = alt;
        }
        if (@class is not null)
        {
            parameters[nameof(AvatarImage.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(AvatarImage.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    // 用渲染树片段构造 <Avatar><AvatarImage/><AvatarFallback/></Avatar> 形态。
    private static RenderFragment AvatarChildren(bool withImage = true) => builder =>
    {
        if (withImage)
        {
            builder.OpenComponent<AvatarImage>(0);
            builder.AddAttribute(1, nameof(AvatarImage.Source), "https://example.com/user.jpg");
            builder.AddAttribute(2, nameof(AvatarImage.Alt), "John Doe");
            builder.CloseComponent();
        }
        builder.OpenComponent<AvatarFallback>(3);
        builder.AddAttribute(4, nameof(AvatarFallback.ChildContent), (RenderFragment)(b => b.AddContent(0, "JD")));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Avatar>(AvatarParams(childContent: AvatarChildren()));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 头像组快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AvatarGroup>(new Dictionary<string, object?>
        {
            [nameof(AvatarGroup.ChildContent)] = GroupAvatars(),
        });

        await Verify(renderer.Markup());
    }

    // 组内三个纯回退头像。
    private static RenderFragment GroupAvatars() => builder =>
    {
        var initials = new[] { "AB", "CD", "EF" };
        for (var i = 0; i < initials.Length; i++)
        {
            var initialsText = initials[i]; // 循环内取值，避免延迟执行的片段闭包捕获越界索引
            builder.OpenComponent<Avatar>(i * 3);
            builder.AddAttribute((i * 3) + 1, nameof(Avatar.ChildContent),
                (RenderFragment)(b =>
                {
                    b.OpenComponent<AvatarFallback>(0);
                    b.AddAttribute(1, nameof(AvatarFallback.ChildContent), Text(initialsText));
                    b.CloseComponent();
                }));
            builder.CloseComponent();
        }
    };

    [Theory]
    [InlineData(AvatarSize.Small, "atom:h-8 atom:w-8 atom:text-xs")]
    [InlineData(AvatarSize.Default, "atom:h-10 atom:w-10 atom:text-sm")]
    [InlineData(AvatarSize.Large, "atom:h-12 atom:w-12 atom:text-base")]
    [InlineData(AvatarSize.ExtraLarge, "atom:h-16 atom:w-16 atom:text-lg")]
    public async Task 四档尺寸应映射到对应尺寸类(AvatarSize size, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Avatar>(AvatarParams(size: size));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Fact]
    public async Task ShowDot应渲染右下角指示点且默认主色着色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Avatar>(AvatarParams(showDot: true));

        var markup = renderer.Markup();
        Assert.Contains("atom:absolute atom:bottom-0 atom:end-0", markup);
        Assert.Contains("atom:ring-2 atom:ring-background", markup);
        Assert.Contains("atom:bg-primary", markup);
    }

    [Theory]
    [InlineData(AvatarSize.Small, "atom:h-2 atom:w-2")]
    [InlineData(AvatarSize.Default, "atom:h-2.5 atom:w-2.5")]
    [InlineData(AvatarSize.Large, "atom:h-3 atom:w-3")]
    [InlineData(AvatarSize.ExtraLarge, "atom:h-3.5 atom:w-3.5")]
    public async Task 指示点尺寸应随头像尺寸缩放(AvatarSize size, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Avatar>(AvatarParams(size: size, showDot: true));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Fact]
    public async Task DotClass应替换默认着色类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Avatar>(AvatarParams(showDot: true, dotClass: "atom:bg-green-500"));

        var markup = renderer.Markup();
        Assert.Contains("atom:bg-green-500", markup);
        Assert.DoesNotContain("atom:bg-primary", markup);
    }

    [Fact]
    public async Task 组内头像应获得分隔边框而组外没有()
    {
        var standalone = CreateRenderer();
        await standalone.MountAsync<Avatar>(AvatarParams());
        Assert.DoesNotContain("atom:border-2 atom:border-background", standalone.Markup());

        var grouped = CreateRenderer();
        await grouped.MountAsync<AvatarGroup>(new Dictionary<string, object?>
        {
            [nameof(AvatarGroup.ChildContent)] = GroupAvatars(),
        });

        var markup = grouped.Markup();
        Assert.Contains("atom:border-2 atom:border-background", markup);
        // 组容器使用负向间距形成叠放。
        Assert.Contains("atom:flex atom:-space-x-3", markup);
    }

    [Fact]
    public async Task 额外属性应展开到头像根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Avatar>(AvatarParams(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "avatar" }));

        Assert.Contains("data-testid=\"avatar\"", renderer.Markup());
    }

    [Fact]
    public async Task AvatarImage正常渲染img并携带alt与object覆盖类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AvatarImage>(ImageParams(source: "https://example.com/a.jpg", alt: "Ann"));

        var markup = renderer.Markup();
        Assert.Contains("<img", markup);
        Assert.Contains("src=\"https://example.com/a.jpg\"", markup);
        Assert.Contains("alt=\"Ann\"", markup);
        Assert.Contains("atom:absolute atom:inset-0 atom:aspect-square", markup);
    }

    [Fact]
    public async Task Source为空时不渲染图片元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AvatarImage>(ImageParams());

        Assert.DoesNotContain("<img", renderer.Markup());
    }

    [Fact]
    public async Task 图片加载失败后应自我隐藏()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AvatarImage>(ImageParams(source: "https://bad.example/x.jpg", alt: "X"));
        Assert.Contains("<img", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onerror", new EventArgs()));

        Assert.DoesNotContain("<img", renderer.Markup());
    }

    [Fact]
    public async Task Source变化应重置错误态并恢复渲染()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AvatarImage>(ImageParams(source: "https://bad.example/x.jpg", alt: "X"));
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onerror", new EventArgs()));
        Assert.DoesNotContain("<img", renderer.Markup());

        await renderer.RerenderRootAsync(0, ImageParams(source: "https://good.example/y.jpg", alt: "Y"));

        var markup = renderer.Markup();
        Assert.Contains("src=\"https://good.example/y.jpg\"", markup);
    }

    [Fact]
    public async Task 回退内容应居中并采用弱化色着色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AvatarFallback>(new Dictionary<string, object?>
        {
            [nameof(AvatarFallback.ChildContent)] = Text("JD"),
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:flex atom:h-full atom:w-full atom:items-center atom:justify-center", markup);
        Assert.Contains("atom:bg-muted atom:text-muted-foreground atom:font-medium", markup);
        Assert.Contains(">JD</span>", markup);
    }
}
