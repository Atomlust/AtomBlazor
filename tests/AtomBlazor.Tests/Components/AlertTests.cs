using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Alert 渲染测试：变体类名、强调边框、图标定位、可关闭按钮与回调、
/// 倒计时条标记、自定义类合并与属性展开。
/// Alert 的关闭按钮 aria-label 来自 ILocalizer（键 Alert.Dismiss）
/// 因此渲染器服务需注册默认本地化器。
/// </summary>
public class AlertTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string value) => builder => builder.AddContent(0, value);

    private static RenderFragment RawMarkup(string markup) => builder => builder.AddContent(0, new MarkupString(markup));

    private static RenderFragment TitleAndDescription(string title, string description) => builder =>
    {
        builder.OpenComponent<AlertTitle>(0);
        builder.AddAttribute(1, nameof(AlertTitle.ChildContent), (RenderFragment)(b => b.AddContent(0, title)));
        builder.CloseComponent();
        builder.OpenComponent<AlertDescription>(2);
        builder.AddAttribute(3, nameof(AlertDescription.ChildContent), (RenderFragment)(b => b.AddContent(0, description)));
        builder.CloseComponent();
    };

    private static Dictionary<string, object?> Params(
        AlertVariant? variant = null,
        RenderFragment? childContent = null,
        RenderFragment? icon = null,
        bool accentBorder = false,
        bool dismissible = false,
        EventCallback? onDismiss = null,
        int? autoDismissAfter = null,
        bool showCountdown = false,
        RenderFragment? actions = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (variant is not null)
        {
            parameters[nameof(Alert.Variant)] = variant.Value;
        }
        if (childContent is not null)
        {
            parameters[nameof(Alert.ChildContent)] = childContent;
        }
        if (icon is not null)
        {
            parameters[nameof(Alert.Icon)] = icon;
        }
        if (accentBorder)
        {
            parameters[nameof(Alert.AccentBorder)] = true;
        }
        if (dismissible)
        {
            parameters[nameof(Alert.Dismissible)] = true;
        }
        if (onDismiss is not null)
        {
            parameters[nameof(Alert.OnDismiss)] = onDismiss.Value;
        }
        if (autoDismissAfter is not null)
        {
            parameters[nameof(Alert.AutoDismissAfter)] = autoDismissAfter.Value;
        }
        if (showCountdown)
        {
            parameters[nameof(Alert.ShowCountdown)] = true;
        }
        if (actions is not null)
        {
            parameters[nameof(Alert.Actions)] = actions;
        }
        if (@class is not null)
        {
            parameters[nameof(Alert.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Alert.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认变体渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(childContent: TitleAndDescription("Heads up!", "You can add components.")));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 根元素应带role属性与基础类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(childContent: TitleAndDescription("标题", "描述")));

        var markup = renderer.Markup();
        Assert.Contains("role=\"alert\"", markup);
        Assert.Contains("atom:relative", markup);
        Assert.Contains("atom:rounded-lg", markup);
        Assert.Contains("atom:bg-muted/30", markup);
    }

    [Theory]
    [InlineData(AlertVariant.Default, "atom:bg-muted/30")]
    [InlineData(AlertVariant.Success, "atom:bg-alert-success-bg")]
    [InlineData(AlertVariant.Info, "atom:bg-alert-info-bg")]
    [InlineData(AlertVariant.Warning, "atom:bg-alert-warning-bg")]
    [InlineData(AlertVariant.Danger, "atom:bg-alert-danger-bg")]
    public async Task 各变体应映射到对应的着色类(AlertVariant variant, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(variant: variant));

        Assert.Contains(expectedClass, renderer.Markup());
    }

    [Theory]
    [InlineData(AlertVariant.Success, "atom:border-alert-success/30")]
    [InlineData(AlertVariant.Info, "atom:border-alert-info/30")]
    [InlineData(AlertVariant.Warning, "atom:border-alert-warning/30")]
    [InlineData(AlertVariant.Danger, "atom:border-alert-danger/30")]
    public async Task 强调边框应加粗左边框宽度并保留变体边色(AlertVariant variant, string expectedBorderColorClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(variant: variant, accentBorder: true));

        var markup = renderer.Markup();
        Assert.Contains("atom:border-s-4", markup);
        Assert.Contains(expectedBorderColorClass, markup);
        // 说明：组件类串同时含逐边强调色 atom:border-s-alert-*，但 TailwindMerge
        // 视其与随后出现的四边色 atom:border-alert-*/30 同组冲突而将其合并掉
        // （本仓 cn 管线的既定行为，类串顺序保持与源实现 ，不魔改）。
    }

    [Fact]
    public async Task 图标片段应渲染并带定位修饰类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(icon: RawMarkup("<svg test-id=\"icon\"></svg>")));

        var markup = renderer.Markup();
        Assert.Contains("test-id=\"icon\"", markup);
        Assert.Contains("atom:[&>svg]:absolute", markup);
        Assert.Contains("atom:[&:has(svg)]:ps-11", markup);
    }

    [Fact]
    public async Task 可关闭警告应渲染带无障碍标签的关闭按钮()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(dismissible: true));

        var markup = renderer.Markup();
        Assert.Contains("<button", markup);
        Assert.Contains("aria-label=\"Dismiss\"", markup);
        Assert.Contains("atom:pe-10", markup);
    }

    [Fact]
    public async Task 点击关闭按钮应触发OnDismiss回调()
    {
        var renderer = CreateRenderer();
        var dismissed = false;
        var onDismiss = EventCallback.Factory.Create(new object(), () => dismissed = true);

        await renderer.MountAsync<Alert>(Params(dismissible: true, onDismiss: onDismiss));

        // .NET 11 渲染器强制 Dispatcher 线程亲和：事件派发也必须走 Dispatcher。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.True(dismissed);
    }

    [Fact]
    public async Task 显示倒计时应产出动画样式且时长取整毫秒()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(autoDismissAfter: 60_000, showCountdown: true));

        var markup = renderer.Markup();
        Assert.Contains("animation: atom-alert-countdown 60000ms linear forwards", markup);
        Assert.Contains("animation-play-state: running", markup);
        Assert.Contains("atom:pb-2", markup);
    }

    [Fact]
    public async Task 操作插槽应渲染在内容之后()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(
            childContent: TitleAndDescription("未保存", "有未保存的更改。"),
            actions: Text("撤销")));

        var markup = renderer.Markup();
        Assert.Contains("atom:mt-3", markup);
        Assert.Contains("撤销", markup);
        Assert.True(
            markup.IndexOf("有未保存的更改。", StringComparison.Ordinal) < markup.IndexOf("撤销", StringComparison.Ordinal),
            "操作区应位于正文内容之后。");
    }

    [Fact]
    public async Task 自定义类应经TailwindMerge覆盖内置变体背景()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(variant: AlertVariant.Default, @class: "bg-purple-500"));

        var markup = renderer.Markup();
        Assert.Contains("bg-purple-500", markup);
        Assert.DoesNotContain("atom:bg-muted/30", markup);
    }

    [Fact]
    public async Task 额外属性应展开到根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Alert>(Params(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "my-alert" }));

        Assert.Contains("data-testid=\"my-alert\"", renderer.Markup());
    }

    [Fact]
    public async Task 标题与描述应各自产出语义类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AlertTitle>(new Dictionary<string, object?>
        {
            [nameof(AlertTitle.ChildContent)] = Text("注意"),
        });
        await renderer.MountAsync<AlertDescription>(new Dictionary<string, object?>
        {
            [nameof(AlertDescription.ChildContent)] = Text("描述文本"),
        });

        var markup = renderer.Markup();
        Assert.Contains("<h5", markup);
        Assert.Contains("atom:font-medium", markup);
        Assert.Contains("atom:text-sm", markup);
    }

    [Fact]
    public async Task 标题As参数应切换实际元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AlertTitle>(new Dictionary<string, object?>
        {
            [nameof(AlertTitle.As)] = "h3",
            [nameof(AlertTitle.ChildContent)] = Text("标题"),
        });

        Assert.Contains("<h3", renderer.Markup());
    }
}
