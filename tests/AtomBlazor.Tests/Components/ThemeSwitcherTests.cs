using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ThemeSwitcher 渲染测试（-1，本族零行为测试、全部自写）：
/// 关闭态仅挂触发按钮（aria-label/图标/变体类）、首渲染补初始化 ThemeService 的
/// InitializeAsync 契约、打开态 Split/Combined 两种颜色布局的分区标题与色片选中
/// 标识（aria-pressed 指纹）、点击主色/基础色/半径/明暗按钮对 ThemeService 的写回、
/// Combined 布局选基础色重置主色为 Default 的旧版语义、ShowDesignOptions 的五枚
/// NativeSelect 设计项区、外部主题变更经 OnThemeChanged 驱动的重渲染、
/// TriggerClass/PopoverContentClass/@attributes 透传沿。
/// 快照含 popover-N 自增短 id 与 NativeSelect 随机 select id，先经正则归一化。
/// </summary>
public class ThemeSwitcherTests
{
    /// <summary>
    /// 归一化浮层上下文自增短 id（popover-N 及 -trigger/-content/portal 作用域 id）
    /// 与 NativeSelect 随机 EffectiveId，使快照可确定（先例：ColorPickerTests/PopoverTests）。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "popover-[0-9]+", "popover-<n>"),
            "select-[0-9a-f]{8}", "select-<id>");

    /// <summary>
    /// 构造带全套基元服务的渲染器（门户/定位/焦点/本地化器）+ ThemeService 单例
    /// 服务注册基元面照 ColorPickerTests 先例。
    /// </summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js, ThemeService Theme) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var theme = new ThemeService(js, new ThemeOptions());
        services.AddSingleton(theme);

        var provider = services.BuildServiceProvider();
        return (new ComponentTestRenderer(provider, NullLoggerFactory.Instance), js, theme);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树（先例：ColorPickerTests）。</summary>
    private static async Task RerenderPortalAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>挂载 PortalHost（根 0）+ ThemeSwitcher（根 1），返回渲染器、JS 记录、
    /// ThemeService 与参数字典（供后续按同一参数视图重渲染根 1）。</summary>
    private static async Task<(
        ComponentTestRenderer Renderer,
        NoopJavaScript Js,
        ThemeService Theme,
        Dictionary<string, object?> Params)> MountAsync(Dictionary<string, object?> extra)
    {
        var (renderer, js, theme) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = new Dictionary<string, object?>();
        foreach (var (key, value) in extra)
        {
            dict[key] = value;
        }

        await renderer.MountAsync<ThemeSwitcher>(dict);
        await RerenderPortalAsync(renderer);
        return (renderer, js, theme, dict);
    }

    /// <summary>点击触发按钮（关闭态下唯一 onclick 携带者）打开弹层，并重渲染门户与根 1
    /// 让内容与其 OnAfterRender 沿落定（先例：ColorPickerTests 的 OpenAsync）。</summary>
    private static async Task OpenAsync(ComponentTestRenderer renderer, Dictionary<string, object?> dict)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await RerenderPortalAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await RerenderPortalAsync(renderer);
    }

    /// <summary>按渲染顺序取出全部 aria-pressed 指纹，用于逐枚按钮断言选中标识。</summary>
    private static string[] PressedFlags(string markup) =>
        Regex.Matches(markup, "aria-pressed=\"(true|false)\"")
            .Select(m => m.Groups[1].Value).ToArray();

    /// <summary>在打开态点击弹层内第 <paramref name="occurrence"/> 个 onclick 携带者
    /// （顺序：门户内容按钮在前、触发按钮居末，先例：ColorPickerTests 预设点击）。
    /// Split 布局：基础色 0..4、主色（含 Default）5..22、半径 23..27、明暗 28/29
    /// Combined 布局：基础色 0..4、主色 5..21、半径 22..26、明暗 27/28。</summary>
    private static async Task ClickAsync(
        ComponentTestRenderer renderer, int occurrence)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));
        await RerenderPortalAsync(renderer);
    }

    // ------------------------------------------------------------------
    // 关闭态触发按钮
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭态触发按钮结构快照应与基线一致()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>());

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 关闭态应仅挂触发按钮且携带无障碍文案与画笔图标()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Customize theme\"", markup);
        Assert.Contains("<svg", markup); // LucideIcon paintbrush
        Assert.Contains("atom:h-4", markup);
        Assert.Contains("atom:border-input", markup); // outline 变体默认类
        Assert.DoesNotContain("role=\"dialog\"", markup); // 关闭态弹层内容未挂载
    }

    [Fact]
    public async Task 首次渲染应补初始化主题服务并经JS契约派发()
    {
        var (renderer, js, theme, _) = await MountAsync(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.True(theme.IsInitialized);
        Assert.Contains(js.Invocations, i => i.Identifier == "import");
        Assert.Contains(js.Invocations, i => i.Identifier == "theme.initialize");
    }

    [Fact]
    public async Task 触发类与额外属性应落触发按钮且内容类落浮层面板()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ThemeSwitcher.TriggerClass)] = "atom:my-switcher",
            [nameof(ThemeSwitcher.PopoverContentClass)] = "atom:my-pop",
            [nameof(ThemeSwitcher.AdditionalAttributes)] =
                new Dictionary<string, object> { ["data-testid"] = "ts" },
        });

        Assert.Contains("atom:my-switcher", renderer.Markup());
        Assert.Contains("data-testid=\"ts\"", renderer.Markup());

        await OpenAsync(renderer, dict);
        Assert.Contains("atom:my-pop", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 打开态 Split 布局（默认）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开态Split面板结构快照应与基线一致()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 打开态Split应渲染基础色主色分区与页眉页脚标题()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        var markup = renderer.Markup();
        Assert.Contains("Customize", markup); // 页眉标题
        Assert.Contains("Pick a color and radius for your components.", markup); // 页眉描述
        Assert.Contains("Base color", markup);
        Assert.Contains("Primary color", markup);
        Assert.Contains("Radius", markup);
        Assert.Contains("Mode", markup);
        Assert.Contains("Light</button>", markup);
        Assert.Contains("Dark</button>", markup);
        Assert.DoesNotContain("Density", markup); // ShowDesignOptions 默认关闭
    }

    [Fact]
    public async Task 默认打开态应有三十个压标识且四枚按钮处于选中态()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        var flags = PressedFlags(renderer.Markup());
        Assert.Equal(30, flags.Length); // Split：5 基础 + 18 主色 + 5 半径 + 2 明暗
        Assert.Equal(4, flags.Count(f => f == "true"));
        Assert.Equal("true", flags[0]); // Zinc 基础色选中（默认）
        Assert.Equal("true", flags[5]); // Default 主色选中（默认）
        Assert.Equal("true", flags[25]); // 半径 0.5 选中（默认）
        Assert.Equal("true", flags[28]); // 亮色模式选中（默认）
    }

    [Fact]
    public async Task 点击暗色按钮应写回主题服务并翻转明暗压标识()
    {
        var (renderer, _, theme, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        await ClickAsync(renderer, 29);

        Assert.True(theme.IsDarkMode);
        var flags = PressedFlags(renderer.Markup());
        Assert.Equal("false", flags[28]);
        Assert.Equal("true", flags[29]);
    }

    [Fact]
    public async Task 点击半径零点七五应写回半径并移动选中态()
    {
        var (renderer, _, theme, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        await ClickAsync(renderer, 26); // 半径档位 [0, 0.3, 0.5, 0.75, 1.0]

        Assert.Equal(0.75, theme.Radius);
        var flags = PressedFlags(renderer.Markup());
        Assert.Equal("false", flags[25]);
        Assert.Equal("true", flags[26]);
    }

    [Fact]
    public async Task 点击主色蓝应写回主色并移动选中态()
    {
        var (renderer, _, theme, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        await ClickAsync(renderer, 17); // Default 之后第 12 枚：Blue

        Assert.Equal(PrimaryColor.Blue, theme.PrimaryColor);
        var flags = PressedFlags(renderer.Markup());
        Assert.Equal("false", flags[5]); // Default 让位
        Assert.Equal("true", flags[17]);
    }

    [Fact]
    public async Task 点击基础色石板应写回基础色且不影响主色()
    {
        var (renderer, _, theme, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        await ClickAsync(renderer, 1); // Slate

        Assert.Equal(BaseColor.Slate, theme.BaseColor);
        Assert.Equal(PrimaryColor.Default, theme.PrimaryColor); // Split 语义：永不重置主色
        var flags = PressedFlags(renderer.Markup());
        Assert.Equal("false", flags[0]);
        Assert.Equal("true", flags[1]);
    }

    // ------------------------------------------------------------------
    // 打开态 Combined 布局（旧版语义）
    // ------------------------------------------------------------------

    [Fact]
    public async Task Combined布局应合并为单网格且仅显示Color标题()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ThemeSwitcher.ColorLayout)] = ThemeSwitcherColorLayout.Combined,
        });
        await OpenAsync(renderer, dict);

        var markup = renderer.Markup();
        Assert.Contains(">Color<", markup);
        Assert.DoesNotContain("Base color", markup);
        Assert.DoesNotContain("Primary color", markup);

        var flags = PressedFlags(renderer.Markup());
        Assert.Equal(29, flags.Length); // Combined：5 基础 + 17 主色 + 5 半径 + 2 明暗
        Assert.Equal(3, flags.Count(f => f == "true")); // 主色 Default 不在网格中：仅基础/半径/明暗三枚选中
        Assert.Equal("true", flags[0]); // Zinc 基础色选中（PrimaryColor==Default 时旧版单指示器）
        Assert.Equal("true", flags[24]); // 半径档位 [0,0.3,0.5,0.75,1.0] → 22+2=24
        Assert.Equal("true", flags[27]); // 亮色模式选中（默认）
    }

    [Fact]
    public async Task Combined布局点击基础色应写回基础色并重置主色为默认()
    {
        var (renderer, _, theme, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ThemeSwitcher.ColorLayout)] = ThemeSwitcherColorLayout.Combined,
        });
        await renderer.Dispatcher.InvokeAsync(() => theme.SetPrimaryColorAsync(PrimaryColor.Blue));
        await OpenAsync(renderer, dict);

        var before = PressedFlags(renderer.Markup());
        Assert.Equal("true", before[16]); // Combined 下 Blue 为主色第 12 枚（序号 5+11）
        Assert.Equal(0, before[..5].Count(f => f == "true")); // 主色非 Default：基础色无选中态

        await ClickAsync(renderer, 3); // Gray（基础色第 4 枚）

        Assert.Equal(BaseColor.Gray, theme.BaseColor);
        Assert.Equal(PrimaryColor.Default, theme.PrimaryColor); // 旧版语义：选基础色重置主色
        var after = PressedFlags(renderer.Markup());
        Assert.Equal("true", after[3]);
        Assert.Equal(0, after[5..22].Count(f => f == "true")); // 主色 Default 不在 Combined 网格中
    }

    // ------------------------------------------------------------------
    // ShowDesignOptions 设计项区
    // ------------------------------------------------------------------

    [Fact]
    public async Task 开启设计选项应渲染五枚原生选择器与本地化标题()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ThemeSwitcher.ShowDesignOptions)] = true,
        });
        await OpenAsync(renderer, dict);

        var markup = renderer.Markup();
        Assert.Equal(5, Regex.Count(markup, "<select"));
        Assert.Contains("Density", markup);
        Assert.Contains("Font", markup);
        Assert.Contains("Surfaces", markup);
        Assert.Contains("Menu color", markup);
        Assert.Contains("Menu accent", markup);
    }

    [Fact]
    public async Task 开启设计选项的打开态结构快照应与基线一致()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ThemeSwitcher.ShowDesignOptions)] = true,
        });
        await OpenAsync(renderer, dict);

        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 外部主题变更驱动重渲染
    // ------------------------------------------------------------------

    [Fact]
    public async Task 外部变更基础色应经OnThemeChanged驱动选中态翻转()
    {
        var (renderer, _, theme, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        await renderer.Dispatcher.InvokeAsync(() => theme.SetBaseColorAsync(BaseColor.Stone));
        await RerenderPortalAsync(renderer);

        var flags = PressedFlags(renderer.Markup());
        Assert.Equal("false", flags[0]);
        Assert.Equal("true", flags[2]);
    }
}
