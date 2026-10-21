using System.Linq.Expressions;
using AtomBlazor;
using AtomBlazor.Primitives;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// DataGrid 列注册表的生命周期面：条件渲染移除列时列组件必须注销，
/// 重新添加时不得留下旧实例或重复表头；层级列与组列同样随销毁撤销其网格侧状态。
/// </summary>
public class DataGridColumnLifecycleTests
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

    private static RenderFragment PropertyColumns(bool includeAge) => builder =>
    {
        builder.OpenComponent<DataGridPropertyColumn<Row, string>>(0);
        builder.AddAttribute(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
        builder.AddAttribute(2, "Title", "NameHeader");
        builder.CloseComponent();

        if (!includeAge)
        {
            return;
        }

        builder.OpenComponent<DataGridPropertyColumn<Row, int>>(3);
        builder.AddAttribute(4, "Property", (Expression<Func<Row, int>>)(r => r.Age));
        builder.AddAttribute(5, "Title", "AgeHeader");
        builder.CloseComponent();
    };

    private static RenderFragment HierarchyColumns(bool includeHierarchy) => builder =>
    {
        builder.OpenComponent<DataGridPropertyColumn<Row, string>>(0);
        builder.AddAttribute(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
        builder.AddAttribute(2, "Title", "NameHeader");
        builder.CloseComponent();

        if (!includeHierarchy)
        {
            return;
        }

        builder.OpenComponent<DataGridHierarchyColumn<Row, int>>(3);
        builder.AddAttribute(4, "Property", (Expression<Func<Row, int>>)(r => r.Age));
        builder.AddAttribute(5, "Title", "TreeHeader");
        builder.CloseComponent();
    };

    private static RenderFragment GroupColumns(bool includeGroup) => builder =>
    {
        if (includeGroup)
        {
            builder.OpenComponent<DataGridGroupColumn<Row, int>>(0);
            builder.AddAttribute(1, "Property", (Expression<Func<Row, int>>)(r => r.Age));
            builder.AddAttribute(2, "HeaderTemplate",
                (RenderFragment<DataGridGroupContext<Row>>)(_ => groupBuilder => groupBuilder.AddContent(0, "Group")));
            builder.CloseComponent();
        }

        builder.OpenComponent<DataGridPropertyColumn<Row, string>>(3);
        builder.AddAttribute(4, "Property", (Expression<Func<Row, string>>)(r => r.Name));
        builder.AddAttribute(5, "Title", "NameHeader");
        builder.CloseComponent();
    };

    private static Task<DataGrid<Row>> MountGridAsync(ComponentTestRenderer renderer, RenderFragment columns) =>
        renderer.MountAsync<DataGrid<Row>>(new Dictionary<string, object?>
        {
            [nameof(DataGrid<Row>.Items)] = new[] { new Row { Name = "根", Age = 1 } },
            [nameof(DataGrid<Row>.Columns)] = columns,
        });

    private static List<IDataGridColumn<Row>> RegisteredColumns(DataGrid<Row> grid) =>
        ComponentProbe.Field<List<IDataGridColumn<Row>>>(grid, "_registeredColumns");

    [Fact]
    public async Task 条件列移除后应注销且重新添加不产生重复登记()
    {
        await WithRendererAsync(async renderer =>
        {
            var grid = await MountGridAsync(renderer, PropertyColumns(includeAge: true));
            Assert.Equal(2, RegisteredColumns(grid).Count);

            await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(DataGrid<Row>.Columns)] = PropertyColumns(includeAge: false),
            });
            Assert.Single(RegisteredColumns(grid));

            await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(DataGrid<Row>.Columns)] = PropertyColumns(includeAge: true),
            });

            var registered = RegisteredColumns(grid);
            Assert.Equal(2, registered.Count);
            Assert.Equal(2, registered.Distinct().Count());
            Assert.Single(registered.OfType<DataGridPropertyColumn<Row, int>>());
        });
    }

    [Fact]
    public async Task 层级列移除再添加应可重新注册且仅登记一次()
    {
        await WithRendererAsync(async renderer =>
        {
            var grid = await renderer.MountAsync<DataGrid<Row>>(new Dictionary<string, object?>
            {
                [nameof(DataGrid<Row>.Items)] = new[] { new Row { Name = "根", Age = 1 } },
                [nameof(DataGrid<Row>.ItemValueSelector)] = (Func<Row, string>)(r => r.Name),
                [nameof(DataGrid<Row>.ChildrenSelector)] = (Func<Row, IEnumerable<Row>?>)(_ => null),
                [nameof(DataGrid<Row>.Columns)] = HierarchyColumns(includeHierarchy: true),
            });

            Assert.NotNull(ComponentProbe.Field<IDataGridColumn<Row>?>(grid, "_hierarchyColumn"));
            Assert.Equal(2, RegisteredColumns(grid).Count);

            await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(DataGrid<Row>.Columns)] = HierarchyColumns(includeHierarchy: false),
            });
            Assert.Null(ComponentProbe.Field<IDataGridColumn<Row>?>(grid, "_hierarchyColumn"));
            Assert.Single(RegisteredColumns(grid));

            await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(DataGrid<Row>.Columns)] = HierarchyColumns(includeHierarchy: true),
            });

            var registered = RegisteredColumns(grid);
            Assert.Equal(2, registered.Count);
            Assert.Single(registered.OfType<DataGridHierarchyColumn<Row, int>>());
            Assert.Same(registered.OfType<DataGridHierarchyColumn<Row, int>>().Single(),
                ComponentProbe.Field<IDataGridColumn<Row>?>(grid, "_hierarchyColumn"));
        });
    }

    [Fact]
    public async Task 条件组列移除后应撤销已配置的分组()
    {
        await WithRendererAsync(async renderer =>
        {
            var grid = await MountGridAsync(renderer, GroupColumns(includeGroup: true));
            Assert.Equal("age", ComponentProbe.Field<string?>(grid, "_groupByColumnId"));
            Assert.NotNull(ComponentProbe.Field<Func<Row, object?>?>(grid, "_groupByAccessor"));

            await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
            {
                [nameof(DataGrid<Row>.Columns)] = GroupColumns(includeGroup: false),
            });

            Assert.Null(ComponentProbe.Field<string?>(grid, "_groupByColumnId"));
            Assert.Null(ComponentProbe.Field<Func<Row, object?>?>(grid, "_groupByAccessor"));
        });
    }
}
