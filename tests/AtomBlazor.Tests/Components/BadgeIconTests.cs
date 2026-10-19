using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// BadgeIcon 渲染测试：Name 经 Lucide 管线渲染内联 SVG、
/// ChildContent 优先于 Name、wrapper 对辅助技术隐藏且类可合并。
/// </summary>
public class BadgeIconTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task 图标名应经Lucide管线渲染为内联svg()
    {
        var renderer = CreateRenderer();

        await renderer.MountTypeAsync(typeof(BadgeIcon), new Dictionary<string, object?>
        {
            [nameof(BadgeIcon.Name)] = "check",
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-hidden=\"true\"", markup);
        Assert.Contains("<svg", markup);
        Assert.Contains("width=\"12\"", markup);
    }

    [Fact]
    public async Task ChildContent应优先于Name()
    {
        var renderer = CreateRenderer();

        await renderer.MountTypeAsync(typeof(BadgeIcon), new Dictionary<string, object?>
        {
            [nameof(BadgeIcon.Name)] = "check",
            [nameof(BadgeIcon.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, "★")),
        });

        var markup = renderer.Markup();
        Assert.Contains("★", markup);
        Assert.DoesNotContain("<svg", markup);
    }

    [Fact]
    public async Task 自定义类应并入包裹层且保留基础尺寸类()
    {
        var renderer = CreateRenderer();

        await renderer.MountTypeAsync(typeof(BadgeIcon), new Dictionary<string, object?>
        {
            [nameof(BadgeIcon.Name)] = "star",
            [nameof(BadgeIcon.Class)] = "me-1",
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:inline-flex", markup);
        Assert.Contains("atom:[&>svg]:size-3", markup);
        Assert.Contains("me-1", markup);
    }
}
