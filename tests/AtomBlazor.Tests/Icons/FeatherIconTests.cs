using AtomBlazor.Primitives;
using AtomBlazor.Icons.Feather;
using AtomBlazor.Icons.Feather.Data;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Icons;

/// <summary>
/// Feather 图标包测试——数据完整性（大小写不敏感查找）、
/// 组件渲染（内联 SVG / 内层描边剔除 / 回退显示）与设计尺寸语义。
/// </summary>
public class FeatherIconTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    [Fact]
    public void 图标数据应完整且大小写不敏感()
    {
        Assert.Equal(286, FeatherIconData.IconCount);
        Assert.NotNull(FeatherIconData.GetIcon("HOME"));
        Assert.NotNull(FeatherIconData.GetIcon("camera"));
        Assert.Null(FeatherIconData.GetIcon("definitely-not-an-icon"));
    }

    [Fact]
    public void 可用图标集合应与字典键一致() =>
        Assert.Equal(FeatherIconData.IconCount, FeatherIconData.GetAvailableIcons().Count());

    [Fact]
    public async Task 已存在的图标应渲染为内联SVG并仅保留外层描边()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FeatherIcon>(new Dictionary<string, object?> { [nameof(FeatherIcon.Name)] = "camera" });

        var markup = renderer.Markup();
        Assert.Contains("<svg", markup, StringComparison.Ordinal);
        // 内层 path/g 的硬编码 stroke 必须被剥离，只留外层 SVG 一处，让 Color 参数生效
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(markup, "stroke="));
    }

    [Fact]
    public async Task 尺寸与描边宽度参数应作用到外层SVG()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FeatherIcon>(new Dictionary<string, object?>
        {
            [nameof(FeatherIcon.Name)] = "home",
            [nameof(FeatherIcon.Size)] = 40,
            [nameof(FeatherIcon.StrokeWidth)] = 1.0,
        });

        var markup = renderer.Markup();
        Assert.Contains("width=\"40\"", markup, StringComparison.Ordinal);
        Assert.Contains("height=\"40\"", markup, StringComparison.Ordinal);
        Assert.Contains("stroke-width=\"1\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 未知图标应渲染可访问的回退元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FeatherIcon>(new Dictionary<string, object?> { [nameof(FeatherIcon.Name)] = "no-such-icon" });

        var markup = renderer.Markup();
        Assert.Contains("role=\"alert\"", markup, StringComparison.Ordinal);
        Assert.Contains("Icon not found: no-such-icon", markup, StringComparison.Ordinal);
    }
}
