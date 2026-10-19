using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Rendering;

/// <summary>
/// 冒烟测试：验证的 ComponentTestRenderer 能挂载组件、驱动渲染批次
/// 并把渲染树序列化为可断言的类 HTML 标记。后续所有组件渲染测试都构建在此之上。
/// </summary>
public class ComponentTestRendererSmokeTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task 渲染器应挂载组件并序列化出标记()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<HelloProbeComponent>(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("atom-probe", markup);
        Assert.Contains("你好", markup);
    }

    [Fact]
    public async Task 渲染器应能按类型挂载并取回组件实例()
    {
        var renderer = CreateRenderer();

        var component = await renderer.MountTypeAsync(typeof(HelloProbeComponent), new Dictionary<string, object?>());

        Assert.IsType<HelloProbeComponent>(component);
    }

    /// <summary>探针组件：只写出一个带类名与文本的元素，用于验证渲染树序列化路径。</summary>
    private sealed class HelloProbeComponent : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "atom-probe");
            builder.AddContent(2, "你好");
            builder.CloseElement();
        }
    }
}
