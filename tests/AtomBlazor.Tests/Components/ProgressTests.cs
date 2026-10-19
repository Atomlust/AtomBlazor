using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Progress 渲染测试：progressbar 无障碍语义、百分比钳位（0-100）、
/// data-state 状态机、标签开关与格式、自定义类合并与属性展开。
/// </summary>
public class ProgressTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static Dictionary<string, object?> Params(
        double? value = null,
        double? max = null,
        bool showLabel = false,
        string? labelFormat = null,
        string? @class = null,
        string? indicatorClass = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (value is not null)
        {
            parameters[nameof(Progress.Value)] = value.Value;
        }
        if (max is not null)
        {
            parameters[nameof(Progress.Max)] = max.Value;
        }
        if (showLabel)
        {
            parameters[nameof(Progress.ShowLabel)] = true;
        }
        if (labelFormat is not null)
        {
            parameters[nameof(Progress.LabelFormat)] = labelFormat;
        }
        if (@class is not null)
        {
            parameters[nameof(Progress.Class)] = @class;
        }
        if (indicatorClass is not null)
        {
            parameters[nameof(Progress.IndicatorClass)] = indicatorClass;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Progress.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Progress>(Params(value: 33));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 应带progressbar角色与aria取值范围()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Progress>(Params(value: 50));

        var markup = renderer.Markup();
        Assert.Contains("role=\"progressbar\"", markup);
        Assert.Contains("aria-valuemin=\"0\"", markup);
        Assert.Contains("aria-valuemax=\"100\"", markup);
        Assert.Contains("aria-valuenow=\"50\"", markup);
    }

    [Fact]
    public async Task 指示器应通过translateX按百分比钳位()
    {
        var cases = new (double Value, double Max, string Expected)[]
        {
            (50, 100, "translateX(-50%)"),
            (-10, 100, "translateX(-100%)"),
            (150, 100, "translateX(-0%)"),
            (3, 10, "translateX(-70%)"),
        };

        foreach (var (value, max, expected) in cases)
        {
            var renderer = CreateRenderer();
            await renderer.MountAsync<Progress>(Params(value: value, max: max));

            Assert.Contains(expected, renderer.Markup());
        }
    }

    [Fact]
    public async Task DataState应区分加载态与完成状态()
    {
        var loadingRenderer = CreateRenderer();
        await loadingRenderer.MountAsync<Progress>(Params(value: 50));
        Assert.Contains("data-state=\"loading\"", loadingRenderer.Markup());

        var completeRenderer = CreateRenderer();
        await completeRenderer.MountAsync<Progress>(Params(value: 100));
        Assert.Contains("data-state=\"complete\"", completeRenderer.Markup());
    }

    [Fact]
    public async Task ShowLabel应显示百分比标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Progress>(Params(value: 60, showLabel: true));

        Assert.Contains("60%", renderer.Markup());
    }

    [Fact]
    public async Task LabelFormat应自定义标签格式()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Progress>(Params(value: 3, max: 5, showLabel: true, labelFormat: "{0}% done"));

        Assert.Contains("60% done", renderer.Markup());
    }

    [Fact]
    public async Task 自定义类应合并进容器与指示器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Progress>(Params(value: 40, @class: "atom:h-6", indicatorClass: "atom:bg-blue-500"));

        var markup = renderer.Markup();
        Assert.Contains("atom:h-6", markup);
        Assert.Contains("atom:bg-blue-500", markup);
    }

    [Fact]
    public async Task 额外属性应展开到progressbar根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Progress>(Params(
            value: 20,
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "progress" }));

        var markup = renderer.Markup();
        Assert.Contains("role=\"progressbar\"", markup);
        Assert.Contains("data-testid=\"progress\"", markup);
    }
}
