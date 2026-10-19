using AtomBlazor.Primitives;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using PrimitiveProgress = AtomBlazor.Primitives.ProgressPrimitive;
using PrimitiveProgressIndicator = AtomBlazor.Primitives.ProgressIndicator;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// Progress 原语测试：progressbar ARIA 语义、不确定（indeterminate）状态、
/// GetValueLabel 值文本、Percentage 计算与 ProgressIndicator 级联上下文校验。
/// </summary>
public class PrimitiveProgressTests
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
        Func<double, double, string>? getValueLabel = null,
        RenderFragment? childContent = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (value is not null)
        {
            parameters[nameof(PrimitiveProgress.Value)] = value.Value;
        }
        if (max is not null)
        {
            parameters[nameof(PrimitiveProgress.Max)] = max.Value;
        }
        if (getValueLabel is not null)
        {
            parameters[nameof(PrimitiveProgress.GetValueLabel)] = getValueLabel;
        }
        if (childContent is not null)
        {
            parameters[nameof(PrimitiveProgress.ChildContent)] = childContent;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(PrimitiveProgress.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 确定态应渲染ARIA值与加载状态()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveProgress>(Params(value: 50));

        var markup = renderer.Markup();
        Assert.Contains("role=\"progressbar\"", markup);
        Assert.Contains("aria-valuenow=\"50\"", markup);
        Assert.Contains("aria-valuemax=\"100\"", markup);
        Assert.Contains("data-state=\"loading\"", markup);
        Assert.Contains("data-value=\"50\"", markup);
    }

    [Fact]
    public async Task 值为null应进入不确定状态()
    {
        var renderer = CreateRenderer();

        var component = await renderer.MountAsync<PrimitiveProgress>(Params());

        var markup = renderer.Markup();
        Assert.Contains("data-state=\"indeterminate\"", markup);
        Assert.DoesNotContain("aria-valuenow", markup);
        Assert.Null(component.Percentage);
    }

    [Fact]
    public async Task 达到最大值应进入完成状态()
    {
        var renderer = CreateRenderer();

        var component = await renderer.MountAsync<PrimitiveProgress>(Params(value: 10, max: 10));

        Assert.Contains("data-state=\"complete\"", renderer.Markup());
        Assert.Equal(100, component.Percentage!.Value);
    }

    [Fact]
    public async Task GetValueLabel应生成aria值文本()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveProgress>(Params(
            value: 50,
            getValueLabel: (v, max) => $"{v / max:0%} complete"));

        Assert.Contains("aria-valuetext=\"50% complete\"", renderer.Markup());
    }

    [Fact]
    public async Task Percentage应按最大值折算百分比()
    {
        var renderer = CreateRenderer();

        var component = await renderer.MountAsync<PrimitiveProgress>(Params(value: 3, max: 10));

        Assert.Equal(30, component.Percentage!.Value);
    }

    [Fact]
    public async Task 指示器在上下文中应继承父级状态与取值()
    {
        var renderer = CreateRenderer();

        RenderFragment indicator = builder =>
        {
            builder.OpenComponent<PrimitiveProgressIndicator>(0);
            builder.CloseComponent();
        };

        await renderer.MountAsync<PrimitiveProgress>(Params(value: 40, childContent: indicator));

        var markup = renderer.Markup();
        // 容器与指示器各携带一份 data-state：指示器的值来自级联上下文。
        Assert.Contains("data-state=\"loading\"", markup);
        Assert.Contains("data-max=\"100\"", markup);
    }

    [Fact]
    public async Task 指示器脱离Progress上下文应抛出异常()
    {
        var renderer = CreateRenderer();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => renderer.MountAsync<PrimitiveProgressIndicator>(new Dictionary<string, object?>()));

        Assert.Contains("Progress", exception.Message, StringComparison.Ordinal);
    }
}
