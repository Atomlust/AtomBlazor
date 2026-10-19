using AtomBlazor.Primitives;
using AtomBlazor.Icons.Heroicons;
using AtomBlazor.Icons.Heroicons.Data;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Icons;

/// <summary>
/// Heroicons 图标包测试——数据完整性（4 变体查找、大小写不敏感）、
/// 组件渲染（Outline 描边 / Solid 填充 / Mini 20 / Micro 16 与 viewBox、回退显示）。
/// </summary>
public class HeroIconTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    [Fact]
    public void 各变体计数应匹配并总和一致()
    {
        Assert.Equal(324, HeroIconData.OutlineIconCount);
        Assert.Equal(324, HeroIconData.SolidIconCount);
        Assert.Equal(324, HeroIconData.MiniIconCount);
        Assert.Equal(316, HeroIconData.MicroIconCount);
        Assert.Equal(1288, HeroIconData.TotalIconCount);
    }

    [Theory]
    [InlineData(HeroIconVariant.Outline)]
    [InlineData(HeroIconVariant.Solid)]
    [InlineData(HeroIconVariant.Mini)]
    [InlineData(HeroIconVariant.Micro)]
    public void 变体查找应命中同名学术帽图标且大小写不敏感(HeroIconVariant variant)
    {
        Assert.NotNull(HeroIconData.GetIcon("academic-cap", variant));
        Assert.NotNull(HeroIconData.GetIcon("ACADEMIC-CAP", variant));
        Assert.Null(HeroIconData.GetIcon("no-such-icon", variant));
    }

    [Fact]
    public async Task Outline变体应使用24x24viewBox且带stroke属性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<HeroIcon>(new Dictionary<string, object?>
        {
            [nameof(HeroIcon.Name)] = "academic-cap",
            [nameof(HeroIcon.Variant)] = HeroIconVariant.Outline,
        });

        var markup = renderer.Markup();
        Assert.Contains("viewBox=\"0 0 24 24\"", markup, StringComparison.Ordinal);
        Assert.Contains("width=\"24\"", markup, StringComparison.Ordinal);
        Assert.Contains("stroke=\"currentColor\"", markup, StringComparison.Ordinal);
        // 内层硬编码 stroke/fill 已被剥离，只剩外层各一处
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(markup, "stroke="));
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(markup, " fill="));
    }

    [Fact]
    public async Task Solid变体不应发射stroke属性且填充继承Color()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<HeroIcon>(new Dictionary<string, object?>
        {
            [nameof(HeroIcon.Name)] = "academic-cap",
            [nameof(HeroIcon.Variant)] = HeroIconVariant.Solid,
        });

        var markup = renderer.Markup();
        Assert.Contains("viewBox=\"0 0 24 24\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("stroke=\"", markup, StringComparison.Ordinal);
        Assert.Contains("fill=\"currentColor\"", markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HeroIconVariant.Mini, 20, "0 0 20 20")]
    [InlineData(HeroIconVariant.Micro, 16, "0 0 16 16")]
    public async Task Mini与Micro应按变体默认尺寸与viewBox渲染(HeroIconVariant variant, int expected, string expectedViewBox)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<HeroIcon>(new Dictionary<string, object?>
        {
            [nameof(HeroIcon.Name)] = "academic-cap",
            [nameof(HeroIcon.Variant)] = variant,
        });

        var markup = renderer.Markup();
        Assert.Contains($"width=\"{expected}\"", markup, StringComparison.Ordinal);
        Assert.Contains($"height=\"{expected}\"", markup, StringComparison.Ordinal);
        Assert.Contains($"viewBox=\"{expectedViewBox}\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 显式Size应覆盖变体默认()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<HeroIcon>(new Dictionary<string, object?>
        {
            [nameof(HeroIcon.Name)] = "academic-cap",
            [nameof(HeroIcon.Variant)] = HeroIconVariant.Mini,
            [nameof(HeroIcon.Size)] = 44,
        });

        var markup = renderer.Markup();
        Assert.Contains("width=\"44\"", markup, StringComparison.Ordinal);
        Assert.Contains("height=\"44\"", markup, StringComparison.Ordinal);
        // viewBox 仍按变体（20x20）
        Assert.Contains("viewBox=\"0 0 20 20\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 未知图标应渲染可访问的回退元素并显示变体()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<HeroIcon>(new Dictionary<string, object?>
        {
            [nameof(HeroIcon.Name)] = "no-such-icon",
            [nameof(HeroIcon.Variant)] = HeroIconVariant.Solid,
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"alert\"", markup, StringComparison.Ordinal);
        Assert.Contains("Icon not found: no-such-icon (Solid)", markup, StringComparison.Ordinal);
    }
}
