using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Rating 组件测试：默认/半星/心形与圆形图标/尺寸快照（Scrub 随机渐变 id）、
/// radiogroup/radio 结构与 aria（checked、label "n out of max"）、只读与禁用呈现
/// （tabindex=-1、点击与键盘空操作）、点击沿（AllowClear 清零、AllowHalf 半星↔整星
/// 切换）、键盘左右步进（半星步长 0.5、Home/End、数字键直达、0 清空）、
/// hover 预览沿（mouseenter/mouseleave 派发驱动填充预览）、IconTemplate 载荷断言。
/// 组件无 JS interop（纯 Blazor 渲染），无导入断言。
/// </summary>
public class RatingTests
{
    /// <summary>
    /// 归一化渐变 id 中自生成的 8 位十六进制随机实例段，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "gradient-[0-9a-f]{8}-", "gradient-<id>-");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static async Task<(ComponentTestRenderer Renderer, Rating Rating)> MountAsync(
        Dictionary<string, object?>? parameters = null)
    {
        var renderer = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(Rating), parameters ?? []));
        return (renderer, (Rating)component);
    }

    /// <summary>派发一次键盘事件（处理器挂在 radiogroup 根上）。</summary>
    private static Task KeyAsync(ComponentTestRenderer renderer, string key) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = key }));

    /// <summary>按渲染顺序派发某颗星上的 onclick（occurrence = index - 1）。</summary>
    private static Task ClickStarAsync(ComponentTestRenderer renderer, int index) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: index - 1));

    /// <summary>读取 aria-checked="true" 的数量（即当前整星数）。</summary>
    private static int CheckedCount(ComponentTestRenderer renderer) =>
        Regex.Count(renderer.Markup(), "aria-checked=\"true\"");

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 3.0,
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 半星与心形图标渲染快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 2.5,
            [nameof(Rating.AllowHalf)] = true,
            [nameof(Rating.Icon)] = RatingIcon.Heart,
        });
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 圆形图标与大尺寸渲染快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 1.0,
            [nameof(Rating.Icon)] = RatingIcon.Circle,
            [nameof(Rating.Size)] = RatingSize.Large,
        });
        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 结构与 aria
    // ------------------------------------------------------------------

    [Fact]
    public async Task 根元素应为radiogroup且每颗星为radio()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 3.0,
            [nameof(Rating.Max)] = 4,
            [nameof(Rating.AriaLabel)] = "满意度",
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"radiogroup\"", markup);
        Assert.Contains("aria-label=\"满意度\"", markup);
        Assert.Equal(4, Regex.Count(markup, "role=\"radio\""));
        // 前三颗选中、第四颗未选中；aria-label 逐一报数
        Assert.Equal(3, CheckedCount(renderer));
        Assert.Contains("aria-checked=\"false\"", markup);
        Assert.Contains("aria-label=\"1 out of 4\"", markup);
        Assert.Contains("aria-label=\"4 out of 4\"", markup);
        // 可编辑态根元素是 Tab 停靠点
        Assert.Contains("tabindex=\"0\"", markup);
    }

    [Fact]
    public async Task 自定义颜色应落到渐变与描边上()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 2.0,
            [nameof(Rating.ActiveColor)] = "#f97316",
            [nameof(Rating.InactiveColor)] = "#e5e7eb",
        });

        var markup = renderer.Markup();
        Assert.Contains("stop-color=\"#f97316\"", markup);
        Assert.Contains("stop-color=\"#e5e7eb\"", markup);
        Assert.Contains("stroke=\"#e5e7eb\"", markup);
    }

    // ------------------------------------------------------------------
    // 点击沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 点击应设置评分并上报()
    {
        var reports = new List<double>();
        var valueChanged = EventCallback.Factory.Create<double>(new object(), v => reports.Add(v));

        var (renderer, rating) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.ValueChanged)] = valueChanged,
        });

        await ClickStarAsync(renderer, 4);
        Assert.Equal(4.0, rating.Value);
        Assert.Equal([4.0], reports);
        Assert.Equal(4, CheckedCount(renderer));
    }

    [Fact]
    public async Task AllowClear时点击当前值应清零()
    {
        var reports = new List<double>();
        var valueChanged = EventCallback.Factory.Create<double>(new object(), v => reports.Add(v));

        var (renderer, rating) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 3.0,
            [nameof(Rating.ValueChanged)] = valueChanged,
        });

        await ClickStarAsync(renderer, 3);
        Assert.Equal(0.0, rating.Value);
        Assert.Equal([0.0], reports);
    }

    [Fact]
    public async Task AllowHalf时同星点击应在半星与整星间切换()
    {
        var reports = new List<double>();
        var valueChanged = EventCallback.Factory.Create<double>(new object(), v => reports.Add(v));

        var (renderer, rating) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.AllowHalf)] = true,
            [nameof(Rating.AllowClear)] = false,
            [nameof(Rating.ValueChanged)] = valueChanged,
        });

        // 首点整星；再点同星回到整星（AllowClear 关闭时不清零）
        await ClickStarAsync(renderer, 3);
        Assert.Equal(3.0, rating.Value);
        await ClickStarAsync(renderer, 3);
        Assert.Equal(3.0, rating.Value);
        Assert.Equal([3.0], reports);

        // 半星 → 整星：点击当前半星所在星（2.5 → 点第 3 颗）
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowLeft" }));
        Assert.Equal(2.5, rating.Value);
        await ClickStarAsync(renderer, 3);
        Assert.Equal(3.0, rating.Value);
    }

    [Fact]
    public async Task 半星态键盘下探后填充应为百分之五十()
    {
        var (renderer, rating) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.AllowHalf)] = true,
            [nameof(Rating.Value)] = 3.0,
        });

        await KeyAsync(renderer, "ArrowLeft");

        Assert.Equal(2.5, rating.Value);
        // 第三颗星渲染半填充渐变（停点 50%）
        Assert.Contains("stop offset=\"50%\"", renderer.Markup());
        Assert.Equal(2, CheckedCount(renderer));
    }

    [Fact]
    public async Task 只读与禁用时点击与键盘应为空操作()
    {
        foreach (var flag in new[] { nameof(Rating.ReadOnly), nameof(Rating.Disabled) })
        {
            var reports = new List<double>();
            var valueChanged = EventCallback.Factory.Create<double>(new object(), v => reports.Add(v));

            var (renderer, rating) = await MountAsync(new Dictionary<string, object?>
            {
                [flag] = true,
                [nameof(Rating.Value)] = 2.0,
                [nameof(Rating.ValueChanged)] = valueChanged,
            });

            var markup = renderer.Markup();
            Assert.Contains("tabindex=\"-1\"", markup);
            Assert.Contains("cursor-default", markup);

            await ClickStarAsync(renderer, 5);
            await KeyAsync(renderer, "ArrowRight");
            Assert.Equal(2.0, rating.Value);
            Assert.Empty(reports);
            Assert.Equal(2, CheckedCount(renderer));
        }
    }

    // ------------------------------------------------------------------
    // 键盘步进
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("ArrowRight", 3.0)]
    [InlineData("ArrowUp", 3.0)]
    [InlineData("ArrowLeft", 1.0)]
    [InlineData("ArrowDown", 1.0)]
    [InlineData("Home", 0.0)]
    [InlineData("End", 5.0)]
    [InlineData("4", 4.0)]
    [InlineData("9", 2.0)] // 超过 Max 的数字键被忽略
    public async Task 键盘按键应按语义表步进(string key, double expected)
    {
        var (renderer, rating) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 2.0,
        });

        await KeyAsync(renderer, key);
        Assert.Equal(expected, rating.Value);
    }

    [Fact]
    public async Task 半星模式步长为半星()
    {
        var (renderer, rating) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.AllowHalf)] = true,
            [nameof(Rating.Value)] = 2.0,
        });

        await KeyAsync(renderer, "ArrowRight");
        Assert.Equal(2.5, rating.Value);
        await KeyAsync(renderer, "ArrowRight");
        Assert.Equal(3.0, rating.Value);
        await KeyAsync(renderer, "ArrowLeft");
        Assert.Equal(2.5, rating.Value);
    }

    [Fact]
    public async Task 步进应在边界钳制()
    {
        var (renderer, rating) = await MountAsync();

        await KeyAsync(renderer, "ArrowLeft");
        Assert.Equal(0.0, rating.Value);
        await KeyAsync(renderer, "ArrowDown");
        Assert.Equal(0.0, rating.Value);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "End" }));
        Assert.Equal(5.0, rating.Value);
        await KeyAsync(renderer, "ArrowRight");
        Assert.Equal(5.0, rating.Value);
    }

    [Fact]
    public async Task 数字零应受AllowClear约束()
    {
        var (renderer, rating) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.AllowClear)] = false,
            [nameof(Rating.Value)] = 2.0,
        });

        await KeyAsync(renderer, "0");
        Assert.Equal(2.0, rating.Value);

        var (renderer2, rating2) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 2.0,
        });
        await renderer2.Dispatcher.InvokeAsync(async () =>
            await renderer2.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "0" }));
        Assert.Equal(0.0, rating2.Value);
    }

    [Fact]
    public async Task 键盘沿应上报值变化()
    {
        var reports = new List<double>();
        var valueChanged = EventCallback.Factory.Create<double>(new object(), v => reports.Add(v));

        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 2.0,
            [nameof(Rating.ValueChanged)] = valueChanged,
        });

        await KeyAsync(renderer, "ArrowRight");
        Assert.Equal([3.0], reports);
    }

    // ------------------------------------------------------------------
    // hover 预览沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 悬停应驱动填充预览且离开后回落()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 1.0,
        });

        // 悬停第 4 颗星：其填充提前点亮
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onmouseenter", new MouseEventArgs(), occurrence: 3));

        // 每颗整星有两处 100% 停点（激活 + 未激活 stop）；悬停后 4 颗整星 → 8 处
        Assert.Equal(8, Regex.Count(renderer.Markup(), "offset=\"100%\""));

        // 离开后回落到实际值：仅第 1 颗整星
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onmouseleave", new MouseEventArgs()));
        Assert.Equal(2, Regex.Count(renderer.Markup(), "offset=\"100%\""));
        Assert.Equal(1, CheckedCount(renderer));
    }

    [Fact]
    public async Task 只读时悬停不应驱动预览()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 1.0,
            [nameof(Rating.ReadOnly)] = true,
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onmouseenter", new MouseEventArgs(), occurrence: 3));

        // 只读时悬停不驱动预览：填充仍锚定实际值 1（一处整星 = 两处 100% 停点）
        Assert.Equal(2, Regex.Count(renderer.Markup(), "offset=\"100%\""));
        Assert.Equal(1, CheckedCount(renderer));
    }

    // ------------------------------------------------------------------
    // IconTemplate 载荷
    // ------------------------------------------------------------------

    [Fact]
    public async Task 自定义图标模板应收到索引与填充载荷()
    {
        var contexts = new List<RatingIconContext>();
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Rating.Value)] = 2.5,
            [nameof(Rating.AllowHalf)] = true,
            [nameof(Rating.ActiveColor)] = "#f00",
            [nameof(Rating.InactiveColor)] = "#ccc",
            [nameof(Rating.IconTemplate)] = (RenderFragment<RatingIconContext>)(ctx => builder =>
            {
                contexts.Add(ctx);
                builder.OpenElement(0, "i");
                builder.AddAttribute(1, "data-index", ctx.Index);
                builder.AddAttribute(2, "data-fill", ctx.Fill);
                builder.CloseElement();
            }),
        });

        var markup = renderer.Markup();
        Assert.Equal(5, contexts.Count);
        Assert.Equal(1, contexts[0].Index);
        Assert.Equal(5, contexts[4].Index);
        Assert.Equal(1.0, contexts[0].Fill);
        Assert.Equal(0.5, contexts[2].Fill);
        Assert.Equal(0.0, contexts[4].Fill);
        Assert.Equal("#f00", contexts[0].ActiveColor);
        Assert.Equal("#ccc", contexts[0].InactiveColor);
        Assert.Contains("data-index=\"1\"", markup);
        Assert.Contains("data-fill=\"0.5\"", markup);
    }
}
