using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// DarkModeToggle 渲染测试：亮/暗两态的图标与 aria 文案（经 ILocalizer）、
/// 点击经 ThemeService 切换主题并触发重渲染、标签与自定义图标参数、
/// 首次渲染对 ThemeService.InitializeAsync 的补初始化契约（对 NoopJavaScript 的互操作调用）。
/// </summary>
public class DarkModeToggleTests
{
    private static ComponentTestRenderer CreateRenderer(
        NoopJavaScript js,
        out ThemeService themeService)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        themeService = new ThemeService(js, new ThemeOptions());
        services.AddSingleton(themeService);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>让调度器空转一轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static Task FlushAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => { });

    private static Dictionary<string, object?> Params(
        bool? showIcon = null,
        bool? showLabel = null,
        string? lightLabel = null,
        string? darkLabel = null,
        string? @class = null,
        ButtonVariant? variant = null,
        ButtonSize? size = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (showIcon is not null)
        {
            parameters[nameof(DarkModeToggle.ShowIcon)] = showIcon.Value;
        }
        if (showLabel is not null)
        {
            parameters[nameof(DarkModeToggle.ShowLabel)] = showLabel.Value;
        }
        if (lightLabel is not null)
        {
            parameters[nameof(DarkModeToggle.LightLabel)] = lightLabel;
        }
        if (darkLabel is not null)
        {
            parameters[nameof(DarkModeToggle.DarkLabel)] = darkLabel;
        }
        if (@class is not null)
        {
            parameters[nameof(DarkModeToggle.Class)] = @class;
        }
        if (variant is not null)
        {
            parameters[nameof(DarkModeToggle.Variant)] = variant.Value;
        }
        if (size is not null)
        {
            parameters[nameof(DarkModeToggle.Size)] = size.Value;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(DarkModeToggle.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 亮色默认渲染快照应与基线一致()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out _);

        await renderer.MountAsync<DarkModeToggle>(Params());
        await FlushAsync(renderer);

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 亮色态应显示太阳图标与切换暗色的aria文案()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out _);

        await renderer.MountAsync<DarkModeToggle>(Params());
        await FlushAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("<svg", markup);
        Assert.Contains("aria-label=\"Switch to dark mode\"", markup);
    }

    [Fact]
    public async Task 首次渲染应补初始化主题服务并经JS契约派发()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out var themeService);

        await renderer.MountAsync<DarkModeToggle>(Params());
        await FlushAsync(renderer);

        Assert.True(themeService.IsInitialized);
        Assert.Contains(js.Invocations, i => i.Identifier == "import");
        Assert.Contains(js.Invocations, i => i.Identifier == "theme.initialize");
    }

    [Fact]
    public async Task 点击应切换为暗色态并更新图标与aria文案()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out var themeService);

        await renderer.MountAsync<DarkModeToggle>(Params());
        await FlushAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));
        await FlushAsync(renderer);

        Assert.True(themeService.IsDarkMode);
        Assert.Contains("aria-label=\"Switch to light mode\"", renderer.Markup());

        // 再点一次回到亮色。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));
        await FlushAsync(renderer);

        Assert.False(themeService.IsDarkMode);
        Assert.Contains("aria-label=\"Switch to dark mode\"", renderer.Markup());
    }

    [Fact]
    public async Task 外部切换主题应经OnThemeChanged驱动重渲染()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out var themeService);

        await renderer.MountAsync<DarkModeToggle>(Params());
        await FlushAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(() => themeService.SetDarkModeAsync(true));
        await FlushAsync(renderer);

        Assert.Contains("aria-label=\"Switch to light mode\"", renderer.Markup());
    }

    [Fact]
    public async Task 开启标签应显示本地化亮暗文案()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out _);

        await renderer.MountAsync<DarkModeToggle>(Params(showLabel: true));
        await FlushAsync(renderer);

        Assert.Contains(">Light<", renderer.Markup());
    }

    [Fact]
    public async Task 自定义标签应优先于本地化文案()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out _);

        await renderer.MountAsync<DarkModeToggle>(Params(showLabel: true, lightLabel: "Taghell"));
        await FlushAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("Taghell", markup);
        Assert.DoesNotContain(">Light<", markup);
    }

    [Fact]
    public async Task 关闭图标后仅剩无障碍名称()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out _);

        await renderer.MountAsync<DarkModeToggle>(Params(showIcon: false));
        await FlushAsync(renderer);

        var markup = renderer.Markup();
        Assert.DoesNotContain("<svg", markup);
        Assert.Contains("aria-label=", markup);
    }

    [Fact]
    public async Task 默认应为outline与icon尺寸()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out _);

        await renderer.MountAsync<DarkModeToggle>(Params());
        await FlushAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("atom:border-input", markup);
        Assert.Contains("atom:w-10", markup);
    }

    [Fact]
    public async Task 自定义类与额外属性应抵达按钮()
    {
        var js = new NoopJavaScript();
        var renderer = CreateRenderer(js, out _);

        await renderer.MountAsync<DarkModeToggle>(Params(
            @class: "atom:my-toggle",
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "dmt" }));
        await FlushAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("atom:my-toggle", markup);
        Assert.Contains("data-testid=\"dmt\"", markup);
    }
}
