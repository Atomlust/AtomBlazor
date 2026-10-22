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
/// DataView 组件层测试。 DataView 断言依赖 FilterBuilder，不可机械
/// 故覆盖可独立验证的渲染契约与状态面：列表/网格模板渲染、空态自定义模板、加载态。
/// </summary>
public class DataViewTests
{
    private sealed class Person
    {
        public string Name { get; set; } = "";

        public int Age { get; set; }
    }

    private static List<Person> Sample() => new()
    {
        new Person { Name = "Charlie", Age = 30 },
        new Person { Name = "Alice", Age = 25 },
        new Person { Name = "Bob", Age = 35 },
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
    public async Task 列表模板应随数据渲染各项()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(DataView<Person>.Data)] = Sample(),
            [nameof(DataView<Person>.ListTemplate)] = (RenderFragment<Person>)(item => b => b.AddContent(0, item.Name)),
            [nameof(DataView<Person>.ShowToolbar)] = false,
            [nameof(DataView<Person>.ShowPagination)] = false,
        };
        await renderer.MountAsync<DataView<Person>>(p);
        var markup = renderer.Markup();

        Assert.Contains("Charlie", markup);
        Assert.Contains("Alice", markup);
        Assert.Contains("Bob", markup);
    }

    [Fact]
    public async Task 网格模板应在网格布局下渲染各项()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(DataView<Person>.Data)] = Sample(),
            [nameof(DataView<Person>.Layout)] = DataViewLayout.Grid,
            [nameof(DataView<Person>.GridTemplate)] = (RenderFragment<Person>)(item => b => b.AddContent(0, $"G:{item.Name}")),
            [nameof(DataView<Person>.ShowToolbar)] = false,
            [nameof(DataView<Person>.ShowPagination)] = false,
        };
        await renderer.MountAsync<DataView<Person>>(p);
        var markup = renderer.Markup();

        Assert.Contains("G:Charlie", markup);
        Assert.Contains("G:Alice", markup);
        Assert.Contains("G:Bob", markup);
    }

    [Fact]
    public async Task 空数据应回退到自定义空模板()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(DataView<Person>.Data)] = new List<Person>(),
            [nameof(DataView<Person>.EmptyTemplate)] = (RenderFragment)(b => b.AddContent(0, "NO-DATA-MARKER")),
            [nameof(DataView<Person>.ShowToolbar)] = false,
            [nameof(DataView<Person>.ShowPagination)] = false,
        };
        await renderer.MountAsync<DataView<Person>>(p);
        var markup = renderer.Markup();

        Assert.Contains("NO-DATA-MARKER", markup);
    }

    [Fact]
    public async Task 加载态应渲染加载占位()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(DataView<Person>.Data)] = Sample(),
            [nameof(DataView<Person>.IsLoading)] = true,
            [nameof(DataView<Person>.ShowToolbar)] = false,
            [nameof(DataView<Person>.ShowPagination)] = false,
        };
        await renderer.MountAsync<DataView<Person>>(p);
        var markup = renderer.Markup();

        Assert.Contains("Loading...", markup);
    }

    [Fact]
    public async Task 工具栏应渲染全局搜索占位()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(DataView<Person>.Data)] = Sample(),
            [nameof(DataView<Person>.ListTemplate)] = (RenderFragment<Person>)(item => b => b.AddContent(0, item.Name)),
        };
        await renderer.MountAsync<DataView<Person>>(p);
        var markup = renderer.Markup();

        // 工具栏全局搜索框占位来自 Localizer["DataView.SearchPlaceholder"] = "Search..."
        Assert.Contains("Search...", markup);
    }
}
