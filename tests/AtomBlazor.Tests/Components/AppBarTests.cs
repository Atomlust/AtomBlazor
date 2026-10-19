using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// AppBar 组件层测试：header 容器与内排行结构、z-30/吸顶/安全区默认类串、
/// Sticky 与 SafeArea 开关剥类、Class 末位追加、标题与描述渲染及空描述省略、
/// TitleContent 整替默认标题、ShowBack 返回按钮（本地化兜底 "Go back" 与 BackLabel 覆盖、
/// LeadingContent 压返回钮）、返回点击转调、Actions 尾槽有无两态、额外属性 splat 落根、
/// 默认结构快照。 AppBar 零专属测试，本文件按 ScrollToTopTests/TimelineTests 先例自写。
/// </summary>
public class AppBarTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Task<AppBar> MountAsync(
        ComponentTestRenderer renderer,
        string? title = null,
        string? description = null,
        RenderFragment? titleContent = null,
        RenderFragment? leadingContent = null,
        RenderFragment? actions = null,
        bool showBack = false,
        string? backLabel = null,
        EventCallback<MouseEventArgs>? onBack = null,
        bool sticky = true,
        bool safeArea = true,
        string? @class = null,
        Dictionary<string, object>? extra = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AppBar.Title)] = title,
            [nameof(AppBar.Description)] = description,
            [nameof(AppBar.TitleContent)] = titleContent,
            [nameof(AppBar.LeadingContent)] = leadingContent,
            [nameof(AppBar.Actions)] = actions,
            [nameof(AppBar.ShowBack)] = showBack,
            [nameof(AppBar.BackLabel)] = backLabel,
            [nameof(AppBar.Sticky)] = sticky,
            [nameof(AppBar.SafeArea)] = safeArea,
            [nameof(AppBar.Class)] = @class,
        };

        if (onBack is not null)
        {
            parameters[nameof(AppBar.OnBack)] = onBack.Value;
        }

        if (extra is not null)
        {
            parameters[nameof(AppBar.AdditionalAttributes)] = extra;
        }

        return renderer.MountAsync<AppBar>(parameters);
    }

    private static RenderFragment TextFragment(string text) => builder => builder.AddContent(0, text);

    [Fact]
    public async Task 根为header地标且内行容器齐备()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "首页");

        var markup = renderer.Markup();
        Assert.StartsWith("<header", markup, StringComparison.Ordinal);
        Assert.Contains("</header>", markup, StringComparison.Ordinal);
        Assert.Contains("atom:flex atom:min-h-14 atom:items-center atom:gap-2 atom:px-3", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 默认类串含层级吸顶与顶部安全区()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "首页");

        var markup = renderer.Markup();
        Assert.Contains("atom:z-30", markup, StringComparison.Ordinal);
        Assert.Contains("atom:sticky atom:top-0", markup, StringComparison.Ordinal);
        Assert.Contains("atom:pt-[env(safe-area-inset-top)]", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sticky与SafeArea关闭应剥去对应类段()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "首页", sticky: false, safeArea: false);

        var markup = renderer.Markup();
        Assert.DoesNotContain("atom:sticky", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("safe-area-inset-top", markup, StringComparison.Ordinal);
        Assert.Contains("atom:z-30", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 自定义Class应末位追加于基础类串之后()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "首页", @class: "x-custom");

        var markup = renderer.Markup();
        var custom = markup.IndexOf("x-custom", StringComparison.Ordinal);
        var baseClass = markup.IndexOf("atom:z-30", StringComparison.Ordinal);
        Assert.True(custom > baseClass, $"自定义类应排在基础类之后：{markup}");
    }

    [Fact]
    public async Task 标题与描述同现且空描述省略段落()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "标题", description: "副行");
        var markup = renderer.Markup();
        Assert.Contains("<div class=\"atom:truncate atom:font-semibold\">标题</div>", markup, StringComparison.Ordinal);
        Assert.Contains("<div class=\"atom:truncate atom:text-xs atom:text-muted-foreground\">副行</div>", markup, StringComparison.Ordinal);

        var bare = CreateRenderer();
        await MountAsync(bare, title: "只有标题");
        Assert.DoesNotContain("atom:text-muted-foreground", bare.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TitleContent整体替换默认标题与描述()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "被替换", description: "也被替换", titleContent: TextFragment("自定义标题区"));

        var markup = renderer.Markup();
        Assert.Contains("自定义标题区", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("被替换", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("atom:truncate atom:font-semibold", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShowBack渲染返回钮且标签走本地化兜底与显式覆盖()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "详情", showBack: true);
        var markup = renderer.Markup();
        Assert.Contains("<button", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Go back\"", markup, StringComparison.Ordinal); // AppBar.Back 键在册兜底
        Assert.Contains("<svg", markup, StringComparison.Ordinal); // arrow-left 图标
        Assert.Contains("atom:rtl:rotate-180", markup, StringComparison.Ordinal);

        var labeled = CreateRenderer();
        await MountAsync(labeled, title: "详情", showBack: true, backLabel: "返回上一页");
        Assert.Contains("aria-label=\"返回上一页\"", labeled.Markup(), StringComparison.Ordinal);
        Assert.DoesNotContain("Go back", labeled.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LeadingContent在场时压掉默认返回钮()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "详情", showBack: true, leadingContent: TextFragment("先导区"));

        var markup = renderer.Markup();
        Assert.Contains("先导区", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<button", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Go back", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 点击返回钮应转调OnBack回调()
    {
        var clicks = 0;
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "详情", showBack: true,
            onBack: EventCallback.Factory.Create<MouseEventArgs>(new object(), _ => clicks++));

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => { });
        Assert.Equal(1, clicks);
    }

    [Fact]
    public async Task Actions尾槽有则渲染无则整段省略()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "首页", actions: TextFragment("操作"));
        var markup = renderer.Markup();
        Assert.Contains("<div class=\"atom:flex atom:shrink-0 atom:items-center atom:gap-1\">操作</div>", markup, StringComparison.Ordinal);

        var bare = CreateRenderer();
        await MountAsync(bare, title: "首页");
        Assert.DoesNotContain("atom:shrink-0", bare.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 额外属性应透传到根header节点()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "首页", extra: new Dictionary<string, object> { ["data-test"] = "appbar" });

        var markup = renderer.Markup();
        Assert.Contains("<header data-test=\"appbar\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();
        await MountAsync(renderer, title: "标题", description: "描述", showBack: true,
            actions: TextFragment("操作"));

        await Verify(renderer.Markup());
    }
}
