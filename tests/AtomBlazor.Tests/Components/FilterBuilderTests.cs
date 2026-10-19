using AtomBlazor.Primitives;
using System.Collections.Generic;
using AtomBlazor;
using AtomBlazor.Primitives.Extensions;
using AtomBlazor.Primitives.Filtering;
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
/// FilterBuilder 组件层测试。覆盖：容器与预设/应用按钮渲染、应用预设时对原预设做深拷贝
/// （偿还：编辑草稿不污染原始预设）、HandleApply 将草稿复制进绑定 Filter、以及
/// MaxConditions 低于预设条件数时 ApplyPresetAsync 抛 InvalidOperationException。
/// </summary>
public class FilterBuilderTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddAtomBlazorPrimitives();
        services.AddAtomBlazorComponents();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static FilterPreset MakePreset(string id, string field)
    {
        var filter = new FilterDefinition
        {
            Conditions =
            {
                new AtomBlazor.Primitives.Filtering.FilterCondition { Field = field, Operator = FilterOperator.Equals, Value = "alpha" },
            },
        };
        return new FilterPreset(id, $"预设{id}", filter);
    }

    private static async Task<(ComponentTestRenderer Renderer, FilterBuilder Builder)> MountAsync(Dictionary<string, object?> parameters)
    {
        var renderer = CreateRenderer();
        parameters.TryAdd(nameof(FilterBuilder.Filter), new FilterDefinition());
        parameters.TryAdd(nameof(FilterBuilder.Fields), new List<FilterField>
        {
            new() { Name = "Name", Label = "名称", Type = FilterFieldType.Text },
        });
        var builder = await renderer.MountAsync<FilterBuilder>(parameters);
        return (renderer, builder);
    }

    [Fact]
    public async Task 含预设且显示应用按钮时应渲染容器与按钮()
    {
        var (renderer, builder) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FilterBuilder.Presets)] = new List<FilterPreset> { MakePreset("p1", "Name") },
            [nameof(FilterBuilder.ShowApplyButton)] = true,
            [nameof(FilterBuilder.ApplyButtonText)] = "应用筛选",
            [nameof(FilterBuilder.ClearButtonText)] = "清空",
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:space-y-3", markup);     // 容器样式指纹
        Assert.Contains("预设p1", markup);               // 预设按钮标签
        Assert.Contains("应用筛选", markup);             // 应用按钮
        Assert.Contains("清空", markup);                 // 清空按钮
    }

    [Fact]
    public async Task 应用预设时应深拷贝原始预设不被草稿污染()
    {
        var preset = MakePreset("p1", "Name");
        var (_, builder) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FilterBuilder.Presets)] = new List<FilterPreset> { preset },
            [nameof(FilterBuilder.ShowApplyButton)] = true,
        });

        await builder.ApplyPresetAsync("p1");

        var draft = ComponentProbe.Field<FilterDefinition?>(builder, "draftFilter")!;
        Assert.Single(draft.Conditions);
        Assert.Equal("alpha", draft.Conditions[0].Value);

 // 编辑草稿不应影响原始预设（克隆语义）。
        draft.Conditions[0].Value = "mutated";
        Assert.Equal("alpha", preset.Filter.Conditions[0].Value);
    }

    [Fact]
    public async Task 显式应用应将草稿复制进绑定Filter()
    {
        var preset = MakePreset("p1", "Name");
        var (_, builder) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FilterBuilder.Presets)] = new List<FilterPreset> { preset },
            [nameof(FilterBuilder.ShowApplyButton)] = true,
        });

        await builder.ApplyPresetAsync("p1");
        Assert.True(builder.Filter.IsEmpty); // 显式应用前绑定 Filter 仍为空

        await (Task)ComponentProbe.Call(builder, "HandleApply")!;

        Assert.False(builder.Filter.IsEmpty);
        Assert.Single(builder.Filter.Conditions);
        Assert.Equal("alpha", builder.Filter.Conditions[0].Value);
    }

    [Fact]
    public async Task MaxConditions低于预设条件数时应用预设应抛异常()
    {
        var preset = MakePreset("p1", "Name");
        var (_, builder) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(FilterBuilder.Presets)] = new List<FilterPreset> { preset },
            [nameof(FilterBuilder.ShowApplyButton)] = true,
            [nameof(FilterBuilder.MaxConditions)] = 0,
        });

        await Assert.ThrowsAsync<System.InvalidOperationException>(async () => await builder.ApplyPresetAsync("p1"));
    }
}
