using System.Linq.Expressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// DataGrid 虚拟化参数沿：InitialItemIndex 必须转发给框架 Virtualize，
/// ItemSize 非法时交由框架抛出显式异常（不再静默退回非虚拟化），
/// 且 ScrollToItemAsync 在未启用/未完成交互渲染时给出可读错误。
/// </summary>
public class DataGridVirtualizationTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";

        public int Age { get; set; }
    }

    private static async Task WithRendererAsync(Func<ComponentTestRenderer, Task> test)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAtomBlazorComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await test(renderer);
    }

    private static RenderFragment Columns() => builder =>
    {
        builder.OpenComponent<DataGridPropertyColumn<Row, string>>(0);
        builder.AddAttribute(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
        builder.AddAttribute(2, "Title", "NameHeader");
        builder.CloseComponent();
    };

    private static Task<DataGrid<Row>> MountAsync(
        ComponentTestRenderer renderer, Dictionary<string, object?> extra)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(DataGrid<Row>.Items)] = Enumerable.Range(1, 100)
                .Select(i => new Row { Name = $"行{i}", Age = i })
                .ToArray(),
            [nameof(DataGrid<Row>.Columns)] = Columns(),
        };
        foreach (var (key, value) in extra)
        {
            parameters[key] = value;
        }

        return renderer.MountAsync<DataGrid<Row>>(parameters);
    }

    [Fact]
    public async Task 初始索引应转发给虚拟化组件()
    {
        await WithRendererAsync(async renderer =>
        {
            await MountAsync(renderer, new Dictionary<string, object?>
            {
                [nameof(DataGrid<Row>.Virtualize)] = true,
                [nameof(DataGrid<Row>.InitialItemIndex)] = 25,
                [nameof(DataGrid<Row>.ItemSize)] = 32f,
            });

            var virtualize = renderer.FindComponent<Virtualize<Row>>();
            Assert.Equal(25, virtualize.InitialItemIndex);
            Assert.Equal(32f, virtualize.ItemSize);
        });
    }

    [Fact]
    public async Task 非正ItemSize应由框架抛出显式异常()
    {
        await WithRendererAsync(async renderer =>
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                MountAsync(renderer, new Dictionary<string, object?>
                {
                    [nameof(DataGrid<Row>.Virtualize)] = true,
                    [nameof(DataGrid<Row>.ItemSize)] = 0f,
                }));

            Assert.Contains("ItemSize", error.Message);
        });
    }

    [Fact]
    public async Task 未启用虚拟化时ScrollToItemAsync应抛错()
    {
        await WithRendererAsync(async renderer =>
        {
            var grid = await MountAsync(renderer, new Dictionary<string, object?>());

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => grid.ScrollToItemAsync(10));
            Assert.Contains(nameof(DataGrid<Row>.ScrollToItemAsync), error.Message);
        });
    }
}
