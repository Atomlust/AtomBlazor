using AtomBlazor.Primitives;
using System.Collections.Generic;
using AtomBlazor;
using AtomBlazor.Primitives.Extensions;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Sortable 组件层测试。覆盖可独立验证的渲染契约：列表项渲染、data-atom-sortable-item
/// 契约属性、SortableHandle 按钮与 data-atom-sortable-handle 属性。JS 实际拖拽在 headless 下不触发，
/// 但 OnAfterRenderAsync 的模块加载经 NoopJavaScript 静默成功。
/// </summary>
public class SortableTests
{
    private sealed class Person
    {
        public string Name { get; set; } = "";
    }

    private static List<Person> Sample() => new()
    {
        new Person { Name = "Charlie" },
        new Person { Name = "Alice" },
        new Person { Name = "Bob" },
    };

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddAtomBlazorPrimitives();
        services.AddAtomBlazorComponents();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task 列表应随数据渲染各项并带可排序项属性()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(Sortable<Person>.Items)] = Sample(),
            [nameof(Sortable<Person>.ItemTemplate)] = (RenderFragment<Person>)(item => b => b.AddContent(0, item.Name)),
        };
        await renderer.MountAsync<Sortable<Person>>(p);
        var markup = renderer.Markup();

        Assert.Contains("Charlie", markup);
        Assert.Contains("Alice", markup);
        Assert.Contains("Bob", markup);
        // 每个项由 Primitives Sortable 包裹 data-atom-sortable-item（与 untransformed JS 契约一致）
        Assert.Contains("data-atom-sortable-item", markup);
    }

    [Fact]
    public async Task 空列表不应渲染可排序项()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(Sortable<Person>.Items)] = new List<Person>(),
            [nameof(Sortable<Person>.ItemTemplate)] = (RenderFragment<Person>)(item => b => b.AddContent(0, item.Name)),
        };
        await renderer.MountAsync<Sortable<Person>>(p);
        var markup = renderer.Markup();

        Assert.DoesNotContain("data-atom-sortable-item", markup);
    }

    [Fact]
    public async Task SortableHandle应渲染拖拽手柄按钮()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(SortableHandle.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "grip")),
        };
        await renderer.MountAsync<SortableHandle>(p);
        var markup = renderer.Markup();

        Assert.Contains("data-atom-sortable-handle", markup);
        Assert.Contains("grip", markup);
        // aria-label 来自 Localizer["Sortable.Handle"] = "Reorder item"
        Assert.Contains("Reorder item", markup);
    }
}
