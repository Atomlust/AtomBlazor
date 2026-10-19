using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Fab 渲染测试：变体映射到 Button 变体、形状/尺寸（图标与带标签）类矩阵、
/// 五种停靠方位与安全区类、fixed/absolute 锚定，以及图标与标签结构。
/// </summary>
public class FabTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        FabVariant? variant = null,
        FabSize? size = null,
        FabPlacement? placement = null,
        FabShape? shape = null,
        bool? @fixed = null,
        bool? safeArea = null,
        RenderFragment? icon = null,
        RenderFragment? childContent = null,
        string? ariaLabel = null,
        bool? disabled = null,
        EventCallback<MouseEventArgs> onClick = default,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (variant is not null)
        {
            parameters[nameof(Fab.Variant)] = variant.Value;
        }
        if (size is not null)
        {
            parameters[nameof(Fab.Size)] = size.Value;
        }
        if (placement is not null)
        {
            parameters[nameof(Fab.Placement)] = placement.Value;
        }
        if (shape is not null)
        {
            parameters[nameof(Fab.Shape)] = shape.Value;
        }
        if (@fixed is not null)
        {
            parameters[nameof(Fab.Fixed)] = @fixed.Value;
        }
        if (safeArea is not null)
        {
            parameters[nameof(Fab.SafeArea)] = safeArea.Value;
        }
        if (icon is not null)
        {
            parameters[nameof(Fab.Icon)] = icon;
        }
        if (childContent is not null)
        {
            parameters[nameof(Fab.ChildContent)] = childContent;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(Fab.AriaLabel)] = ariaLabel;
        }
        if (disabled is not null)
        {
            parameters[nameof(Fab.Disabled)] = disabled.Value;
        }
        if (onClick.HasDelegate)
        {
            parameters[nameof(Fab.OnClick)] = onClick;
        }
        if (@class is not null)
        {
            parameters[nameof(Fab.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Fab.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(
            icon: Text("+"),
            ariaLabel: "New message"));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 应渲染为带fab数据槽的按钮()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(ariaLabel: "Compose"));

        var markup = renderer.Markup();
        Assert.Contains("<button", markup);
        Assert.Contains("data-slot=\"fab\"", markup);
        Assert.Contains("aria-label=\"Compose\"", markup);
    }

    [Theory]
    [InlineData(FabVariant.Primary, "atom:bg-primary")]
    [InlineData(FabVariant.Secondary, "atom:bg-secondary")]
    [InlineData(FabVariant.Surface, "atom:border-input")]
    [InlineData(FabVariant.Destructive, "atom:bg-destructive")]
    public async Task 每个变体应映射到底层按钮变体(FabVariant variant, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(variant: variant));

        Assert.Contains(expectedClass, renderer.Markup());
    }

    [Theory]
    [InlineData(FabShape.Rounded, "atom:rounded-md", "atom:rounded-full")]
    [InlineData(FabShape.Circle, "atom:rounded-full", "atom:rounded-md")]
    public async Task 形状应切换圆角类(FabShape shape, string present, string absent)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(shape: shape));

        var markup = renderer.Markup();
        Assert.Contains(present, markup);
        Assert.DoesNotContain(absent, markup);
    }

    [Theory]
    [InlineData(FabSize.Small, "atom:h-10", "atom:w-10")]
    [InlineData(FabSize.Default, "atom:h-14", "atom:w-14")]
    [InlineData(FabSize.Large, "atom:h-16", "atom:w-16")]
    public async Task 图标形态尺寸应映射正方形(FabSize size, string heightClass, string widthClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(size: size, icon: Text("+")));

        var markup = renderer.Markup();
        Assert.Contains(heightClass, markup);
        Assert.Contains(widthClass, markup);
        Assert.Contains("atom:p-0", markup);
    }

    [Theory]
    [InlineData(FabSize.Small, "atom:px-4")]
    [InlineData(FabSize.Default, "atom:px-6")]
    [InlineData(FabSize.Large, "atom:px-8")]
    public async Task 带标签形态尺寸应映射水平内边距(FabSize size, string paddingClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(size: size, childContent: Text("New")));

        var markup = renderer.Markup();
        Assert.Contains(paddingClass, markup);
        Assert.Contains("atom:truncate", markup);
    }

    [Theory]
    [InlineData(FabPlacement.BottomEnd, "atom:bottom-6", "atom:end-6")]
    [InlineData(FabPlacement.BottomStart, "atom:bottom-6", "atom:start-6")]
    [InlineData(FabPlacement.TopEnd, "atom:top-6", "atom:end-6")]
    [InlineData(FabPlacement.TopStart, "atom:top-6", "atom:start-6")]
    public async Task 角落方位应使用逻辑边锚定(FabPlacement placement, string crossClass, string edgeClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(placement: placement));

        var markup = renderer.Markup();
        Assert.Contains(crossClass, markup);
        Assert.Contains(edgeClass, markup);
        Assert.Contains("atom:fixed", markup);
    }

    [Fact]
    public async Task 底部居中应使用对称平移居中()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(placement: FabPlacement.BottomCenter));

        var markup = renderer.Markup();
        Assert.Contains("atom:bottom-6", markup);
        Assert.Contains("atom:left-1/2", markup);
        Assert.Contains("atom:-translate-x-1/2", markup);
    }

    [Fact]
    public async Task Inline方位应退出锚定保留文档流()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(placement: FabPlacement.Inline));

        var markup = renderer.Markup();
        Assert.DoesNotContain("atom:fixed", markup);
        Assert.DoesNotContain("atom:absolute", markup);
        Assert.DoesNotContain("atom:bottom-6", markup);
    }

    [Fact]
    public async Task 取消Fixed应改为钉在定位祖先上()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(@fixed: false));

        var markup = renderer.Markup();
        Assert.Contains("atom:absolute", markup);
        Assert.DoesNotContain("atom:fixed", markup);
    }

    [Fact]
    public async Task 安全区默认按方位加边距且可关闭()
    {
        var bottomRenderer = CreateRenderer();
        await bottomRenderer.MountAsync<Fab>(Params());
        Assert.Contains("atom:mb-[env(safe-area-inset-bottom)]", bottomRenderer.Markup());

        var topRenderer = CreateRenderer();
        await topRenderer.MountAsync<Fab>(Params(placement: FabPlacement.TopEnd));
        Assert.Contains("atom:mt-[env(safe-area-inset-top)]", topRenderer.Markup());

        var offRenderer = CreateRenderer();
        await offRenderer.MountAsync<Fab>(Params(safeArea: false));
        Assert.DoesNotContain("safe-area-inset", offRenderer.Markup());
    }

    [Fact]
    public async Task 图标应包在收缩容器中标签应独立截断()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(
            icon: Text("+"),
            childContent: Text("New task")));

        var markup = renderer.Markup();
        Assert.Contains("<span class=\"atom:inline-flex atom:shrink-0 atom:items-center\">+</span>", markup);
        Assert.Contains("<span class=\"atom:truncate\">New task</span>", markup);
    }

    [Fact]
    public async Task 禁用与点击应经由底层按钮()
    {
        var clicks = 0;
        var callback = EventCallback.Factory.Create<MouseEventArgs>(new object(), _ => clicks++);
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(onClick: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.Equal(1, clicks);

        var disabledRenderer = CreateRenderer();
        await disabledRenderer.MountAsync<Fab>(Params(disabled: true, onClick: callback));

        await disabledRenderer.Dispatcher.InvokeAsync(async () =>
            await disabledRenderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.Equal(1, clicks);
    }

    [Fact]
    public async Task 自定义类与额外属性应抵达按钮元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Fab>(Params(
            @class: "atom:bottom-2",
            additionalAttributes: new Dictionary<string, object> { ["data-x"] = "1" }));

        var markup = renderer.Markup();
        Assert.Contains("atom:bottom-2", markup);
        Assert.Contains("data-x=\"1\"", markup);
    }
}
