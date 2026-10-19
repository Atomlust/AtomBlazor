using AtomBlazor.Primitives;
using AtomBlazor.Icons.Lucide;
using AtomBlazor.Icons.Lucide.Data;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Icons;

/// <summary>
/// Lucide 图标包测试——数据完整性（FrozenDictionary 查找）、
/// 组件渲染（内联 SVG / 回退显示 / 参数生效）与大小写不敏感语义。
/// </summary>
public class LucideIconTests
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
        Assert.Equal(1753, LucideIconData.IconCount);
        Assert.NotNull(LucideIconData.GetIcon("HOUSE"));
        Assert.NotNull(LucideIconData.GetIcon("a-arrow-down"));
        Assert.Null(LucideIconData.GetIcon("definitely-not-an-icon"));
    }

    [Fact]
    public async Task 已存在的图标应渲染为内联SVG并剔除内层描边填充()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<LucideIcon>(new Dictionary<string, object?> { [nameof(LucideIcon.Name)] = "house" });

        var markup = renderer.Markup();
        Assert.Contains("<svg", markup, StringComparison.Ordinal);
        Assert.Contains("lucide-icon", markup, StringComparison.Ordinal);
        // 内层 path 的 stroke/fill 硬编码属性必须被移除（只剩外层 SVG 各一个），让组件参数生效
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(markup, "stroke="));
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(markup, " fill="));
    }

    [Fact]
    public async Task 未知图标应渲染可访问的回退元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<LucideIcon>(new Dictionary<string, object?> { [nameof(LucideIcon.Name)] = "no-such-icon" });

        var markup = renderer.Markup();
        Assert.Contains("role=\"alert\"", markup, StringComparison.Ordinal);
        Assert.Contains("Icon not found: no-such-icon", markup, StringComparison.Ordinal);
    }
}
