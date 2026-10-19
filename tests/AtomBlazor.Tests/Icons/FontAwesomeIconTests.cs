using AtomBlazor.Primitives;
using AtomBlazor.Icons.FontAwesome;
using AtomBlazor.Icons.FontAwesome.Data;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Icons;

/// <summary>
/// Font Awesome 图标包测试——数据完整性（3 变体条目查找）、
/// 组件渲染（固有宽高比等比缩放高度、viewBox 推导、内层填充剔除、回退显示）。
/// </summary>
public class FontAwesomeIconTests
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
        Assert.Equal(1407, FontAwesomeIconData.SolidIconCount);
        Assert.Equal(164, FontAwesomeIconData.RegularIconCount);
        Assert.Equal(495, FontAwesomeIconData.BrandsIconCount);
        Assert.Equal(2066, FontAwesomeIconData.TotalIconCount);
    }

    [Fact]
    public void 条目应携带固有宽高且查找大小写不敏感()
    {
        var entry = FontAwesomeIconData.GetIcon("0", FontAwesomeIconVariant.Solid);
        Assert.NotNull(entry);
        Assert.Equal(320, entry!.Width);
        Assert.Equal(512, entry.Height);
        Assert.NotNull(FontAwesomeIconData.GetIcon("GITHUB", FontAwesomeIconVariant.Brands));
        Assert.Null(FontAwesomeIconData.GetIcon("no-such-icon", FontAwesomeIconVariant.Solid));
    }

    [Fact]
    public async Task 应按固有宽高比等比缩放高度并推导viewBox()
    {
        var renderer = CreateRenderer();

        // solid "0"：固有 320x512，默认宽度 16 → 高度 round(16*512/320)=26
        await renderer.MountAsync<FontAwesomeIcon>(new Dictionary<string, object?>
        {
            [nameof(FontAwesomeIcon.Name)] = "0",
            [nameof(FontAwesomeIcon.Variant)] = FontAwesomeIconVariant.Solid,
        });

        var markup = renderer.Markup();
        Assert.Contains("width=\"16\"", markup, StringComparison.Ordinal);
        Assert.Contains("height=\"26\"", markup, StringComparison.Ordinal);
        Assert.Contains("viewBox=\"0 0 320 512\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 品牌图标非方形应保持宽高比且仅保留外层填充()
    {
        var renderer = CreateRenderer();

        // brands "github"：固有 496x512，宽度 16 → 高度 round(16*512/496)=17
        await renderer.MountAsync<FontAwesomeIcon>(new Dictionary<string, object?>
        {
            [nameof(FontAwesomeIcon.Name)] = "github",
            [nameof(FontAwesomeIcon.Variant)] = FontAwesomeIconVariant.Brands,
        });

        var markup = renderer.Markup();
        Assert.Contains("height=\"17\"", markup, StringComparison.Ordinal);
        Assert.Contains("viewBox=\"0 0 496 512\"", markup, StringComparison.Ordinal);
        // 内层 path 的硬编码 fill 被剥离，只留外层 SVG 一处
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(markup, " fill="));
        Assert.Equal(0, System.Text.RegularExpressions.Regex.Count(markup, "stroke="));
    }

    [Fact]
    public async Task 未知图标应渲染可访问的回退元素并显示变体()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FontAwesomeIcon>(new Dictionary<string, object?>
        {
            [nameof(FontAwesomeIcon.Name)] = "no-such-icon",
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"alert\"", markup, StringComparison.Ordinal);
        Assert.Contains("Icon not found: no-such-icon (Solid)", markup, StringComparison.Ordinal);
    }
}
