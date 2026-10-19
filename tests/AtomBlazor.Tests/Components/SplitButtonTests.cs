using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// SplitButton 家族渲染测试（波，对本族零行为测试，全自撰）：
/// 组容器（role=group/data-slot 面）、主按钮与下拉触发按钮的拼接边类、
/// DropdownAriaLabel 缺省 "More options"、Disabled 双按钮透传、
/// Size 到触发按钮尺寸映射、Outline 变体分隔边差异、附加属性 splat
/// 以及 SplitButtonItem/SplitButtonSeparator 对 DropdownMenu 封装件的参数透传。
/// </summary>
public class SplitButtonTests
{
    /// <summary>归一化实例自增 id（dropdown-menu-{n}），使快照与跑序无关（照 AccordionTests 先例）。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "dropdown-menu-\\d+", "dropdown-menu-<n>");

    /// <summary>构造带全套基元服务的渲染器（门户/定位/焦点/本地化器），与 ColorPickerTests 同构。</summary>
    private static ComponentTestRenderer CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    /// <summary>下拉弹层内容在关闭态不入 DOM（portal 判据），故主断言只覆盖面闭态。</summary>
    private static Dictionary<string, object?> DefaultParams() => new()
    {
        [nameof(SplitButton.ChildContent)] = Text("Save"),
        [nameof(SplitButton.DropdownContent)] = (RenderFragment)(inner =>
        {
            inner.OpenComponent<SplitButtonItem>(0);
            inner.AddAttribute(1, nameof(SplitButtonItem.ChildContent), Text("Save As..."));
            inner.CloseComponent();
        }),
    };

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SplitButton>(DefaultParams());

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 容器应携带group角色与内联排布类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SplitButton>(DefaultParams());

        var markup = renderer.Markup();
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("atom:inline-flex", markup);
        Assert.Equal(2, markup.Split("<button", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task 主按钮应使用右端收边类并渲染内容()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SplitButton>(DefaultParams());

        var markup = renderer.Markup();
        Assert.Contains("atom:!rounded-e-none", markup);
        Assert.Contains("atom:border-e-0", markup);
        Assert.Contains("Save", markup);
    }

    [Fact]
    public async Task 下拉触发按钮应缺省更多选项标签与左端收边类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SplitButton>(DefaultParams());

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"More options\"", markup);
        Assert.Contains("atom:!rounded-s-none", markup);
        Assert.Contains("atom:!px-2", markup);
        // 触发按钮内置 chevron-down 图标（Lucide svg 面）。
        Assert.Contains("<svg", markup);
    }

    [Fact]
    public async Task DropdownAriaLabel应覆盖触发按钮缺省标签()
    {
        var renderer = CreateRenderer();

        var parameters = DefaultParams();
        parameters[nameof(SplitButton.DropdownAriaLabel)] = "Merge options";
        await renderer.MountAsync<SplitButton>(parameters);

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Merge options\"", markup);
        Assert.DoesNotContain("aria-label=\"More options\"", markup);
    }

    [Fact]
    public async Task Outline变体触发按钮应仅带常规分隔边()
    {
        var renderer = CreateRenderer();

        var parameters = DefaultParams();
        parameters[nameof(SplitButton.Variant)] = ButtonVariant.Outline;
        await renderer.MountAsync<SplitButton>(parameters);

        var markup = renderer.Markup();
        Assert.DoesNotContain("border-s-primary-foreground/20", markup);
    }

    [Fact]
    public async Task 非Outline变体触发按钮应带前景色分隔边()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SplitButton>(DefaultParams());

        Assert.Contains("atom:border-s atom:border-s-primary-foreground/20", renderer.Markup());
    }

    [Fact]
    public async Task Disabled应同时透传两个按钮()
    {
        var renderer = CreateRenderer();

        var parameters = DefaultParams();
        parameters[nameof(SplitButton.Disabled)] = true;
        await renderer.MountAsync<SplitButton>(parameters);

        var markup = renderer.Markup();
        // Button 实物：IsDisabled 同时落 disabled 属性、tabindex=-1 与 aria-disabled=true 三面。
        Assert.Equal(2, markup.Split("aria-disabled=\"true\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, markup.Split("tabindex=\"-1\"", StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData(ButtonSize.Small, "h-9")]
    [InlineData(ButtonSize.Default, "h-10")]
    [InlineData(ButtonSize.Large, "h-11")]
    public async Task 尺寸应映射到主按钮尺寸类(ButtonSize size, string expectedHeight)
    {
        var renderer = CreateRenderer();

        var parameters = DefaultParams();
        parameters[nameof(SplitButton.Size)] = size;
        await renderer.MountAsync<SplitButton>(parameters);

        Assert.Contains($"atom:{expectedHeight}", renderer.Markup());
    }

    [Fact]
    public async Task 未匹配属性应展开到根容器()
    {
        var renderer = CreateRenderer();

        var parameters = DefaultParams();
        parameters[nameof(SplitButton.AdditionalAttributes)] = new Dictionary<string, object> { ["data-testid"] = "sb" };
        await renderer.MountAsync<SplitButton>(parameters);

        Assert.Contains("data-testid=\"sb\"", renderer.Markup());
    }

    [Fact]
    public async Task Class参数应合并进根容器类()
    {
        var renderer = CreateRenderer();

        var parameters = DefaultParams();
        parameters[nameof(SplitButton.Class)] = "atom:custom-root";
        await renderer.MountAsync<SplitButton>(parameters);

        Assert.Contains("atom:inline-flex atom:custom-root", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // SplitButtonItem / SplitButtonSeparator（DropdownMenu 封装透传面）
    // ------------------------------------------------------------------

    /// <summary>基元 DropdownMenu 根级联上下文并内联渲染子项；菜单项守卫要求不得裸挂载。</summary>
    private static Dictionary<string, object?> ItemParams(Action<RenderTreeBuilder> item) => new()
    {
        [nameof(DropdownMenu.ChildContent)] = (RenderFragment)(inner => item(inner)),
    };

    [Fact]
    public async Task 菜单项应以内层menuitem角色渲染内容()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<DropdownMenu>(ItemParams(inner =>
        {
            inner.OpenComponent<SplitButtonItem>(0);
            inner.AddAttribute(1, nameof(SplitButtonItem.ChildContent), Text("Export"));
            inner.CloseComponent();
        }));

        var markup = renderer.Markup();
        Assert.Contains("role=\"menuitem\"", markup);
        Assert.Contains("Export", markup);
    }

    [Fact]
    public async Task 菜单项禁用应落DataDisabled与AriaDisabled()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<DropdownMenu>(ItemParams(inner =>
        {
            inner.OpenComponent<SplitButtonItem>(0);
            inner.AddAttribute(1, nameof(SplitButtonItem.Disabled), true);
            inner.CloseComponent();
        }));

        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("data-disabled=\"true\"", markup);
    }

    [Fact]
    public async Task 菜单项附加属性应展开到内层元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<DropdownMenu>(ItemParams(inner =>
        {
            inner.OpenComponent<SplitButtonItem>(0);
            inner.AddAttribute(1, nameof(SplitButtonItem.AdditionalAttributes),
                new Dictionary<string, object> { ["id"] = "item-1" });
            inner.CloseComponent();
        }));

        Assert.Contains("id=\"item-1\"", renderer.Markup());
    }

    [Fact]
    public async Task 分隔项应以内层separator角色渲染()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<SplitButtonSeparator>(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("role=\"separator\"", markup);
        Assert.Contains("aria-orientation=\"horizontal\"", markup);
    }
}
