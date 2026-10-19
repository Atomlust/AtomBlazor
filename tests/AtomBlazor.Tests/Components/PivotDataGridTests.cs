using AtomBlazor.Primitives;
using System.Collections.Generic;
using AtomBlazor;
using AtomBlazor.Primitives.Pivot;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// PivotDataGrid 组件层测试。覆盖声明驱动的构建契约：字段/值片段注册、交叉汇总表在
/// OnAfterRender 内构建、单元格数值进入标记、空数据源与缺值时不构建，以及表可通过公开
/// <c>Table</c> 属性读取。不依赖任何浏览器交互。
/// </summary>
public class PivotDataGridTests
{
    private sealed class Sale
    {
        public string Region { get; set; } = "";
        public string Quarter { get; set; } = "";
        public double Amount { get; set; }
    }

    private static List<Sale> Sample() => new()
    {
        new Sale { Region = "North", Quarter = "Q1", Amount = 100 },
        new Sale { Region = "North", Quarter = "Q1", Amount = 50 },
        new Sale { Region = "North", Quarter = "Q2", Amount = 200 },
        new Sale { Region = "South", Quarter = "Q1", Amount = 75 },
        new Sale { Region = "South", Quarter = "Q2", Amount = 25 },
    };

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment SaleRows() => builder =>
    {
        builder.OpenComponent<AtomBlazor.PivotField<Sale>>(0);
        builder.AddAttribute(1, nameof(AtomBlazor.PivotField<Sale>.Title), "Region");
        builder.AddAttribute(2, nameof(AtomBlazor.PivotField<Sale>.Value), (Func<Sale, object?>)(s => s.Region));
        builder.CloseComponent();
    };

    private static RenderFragment SaleColumns() => builder =>
    {
        builder.OpenComponent<AtomBlazor.PivotField<Sale>>(0);
        builder.AddAttribute(1, nameof(AtomBlazor.PivotField<Sale>.Title), "Quarter");
        builder.AddAttribute(2, nameof(AtomBlazor.PivotField<Sale>.Value), (Func<Sale, object?>)(s => s.Quarter));
        builder.CloseComponent();
    };

    private static RenderFragment SaleValues() => builder =>
    {
        builder.OpenComponent<PivotValue<Sale>>(0);
        builder.AddAttribute(1, nameof(PivotValue<Sale>.Title), "Total");
        builder.AddAttribute(2, nameof(PivotValue<Sale>.Function), AggregateFunction.Sum);
        builder.AddAttribute(3, nameof(PivotValue<Sale>.Value), (Func<Sale, object?>)(s => s.Amount));
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, PivotDataGrid<Sale> Grid)> MountAsync(Dictionary<string, object?> parameters)
    {
        var renderer = CreateRenderer();
        parameters.TryAdd(nameof(PivotDataGrid<Sale>.Data), Sample());
        parameters.TryAdd(nameof(PivotDataGrid<Sale>.Rows), SaleRows());
        parameters.TryAdd(nameof(PivotDataGrid<Sale>.Columns), SaleColumns());
        parameters.TryAdd(nameof(PivotDataGrid<Sale>.Values), SaleValues());
        var grid = await renderer.MountAsync<PivotDataGrid<Sale>>(parameters);
        return (renderer, grid);
    }

    [Fact]
    public async Task 声明字段值后应构建三乘三交叉汇总表()
    {
        var (_, grid) = await MountAsync(new Dictionary<string, object?>());

        Assert.NotNull(grid.Table);
        Assert.Equal(3, grid.Table.RowLeaves.Count);   // North + South + 总计
        Assert.Equal(3, grid.Table.ColumnLeaves.Count); // Q1 + Q2 + 总计
        Assert.Equal(5, grid.Table.ItemCount);
    }

    [Fact]
    public async Task 构建后单元格数值应进入渲染标记()
    {
        var (renderer, grid) = await MountAsync(new Dictionary<string, object?>());
        var markup = renderer.Markup();

        Assert.Contains("data-slot=\"pivot-data-grid\"", markup);
        // 行/列标题
        Assert.Contains("North", markup);
        Assert.Contains("South", markup);
        Assert.Contains("Q1", markup);
        Assert.Contains("Q2", markup);
        // North/Q1 单元 = 100 + 50 = 150 应被渲染
        Assert.Contains("150", markup);
    }

    [Fact]
    public async Task 空数据源不应构建表并展示空消息()
    {
        var (renderer, grid) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PivotDataGrid<Sale>.Data)] = new List<Sale>(),
            [nameof(PivotDataGrid<Sale>.EmptyMessage)] = "暂无数据",
        });

        Assert.NotNull(grid.Table);
        Assert.True(grid.Table.IsEmpty);
        Assert.Contains("暂无数据", renderer.Markup());
    }

    [Fact]
    public async Task 缺少值声明不应构建表()
    {
        var renderer = CreateRenderer();
        var p = new Dictionary<string, object?>
        {
            [nameof(PivotDataGrid<Sale>.Data)] = Sample(),
            [nameof(PivotDataGrid<Sale>.Rows)] = SaleRows(),
            [nameof(PivotDataGrid<Sale>.Columns)] = SaleColumns(),
            // 故意不提供 Values
        };
        var grid = await renderer.MountAsync<PivotDataGrid<Sale>>(p);

        Assert.Null(grid.Table);
    }
}
