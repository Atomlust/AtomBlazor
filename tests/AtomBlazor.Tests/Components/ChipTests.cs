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
/// Chip 渲染测试：默认快照、非交互时渲染 span 而非 button、Selectable 点击切换
/// 本地选中态与 aria-pressed、@bind-Selected 双向绑定、移除按钮的本地化标签与
/// 回调、禁用态拦截交互、变体/尺寸表驱动映射、选中对勾与 Icon 前导内容。
/// </summary>
public class ChipTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        ChipVariant? variant = null,
        ChipSize? size = null,
        string? label = "Design",
        bool? selectable = null,
        bool selected = false,
        EventCallback<bool>? selectedChanged = null,
        bool? dismissible = null,
        string? dismissLabel = null,
        EventCallback<MouseEventArgs>? onDismiss = null,
        EventCallback<MouseEventArgs>? onClick = null,
        bool disabled = false,
        bool? showCheckMark = null,
        RenderFragment? icon = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (variant is not null)
        {
            parameters[nameof(Chip.Variant)] = variant.Value;
        }
        if (size is not null)
        {
            parameters[nameof(Chip.Size)] = size.Value;
        }
        if (label is not null)
        {
            parameters[nameof(Chip.ChildContent)] = Text(label);
        }
        if (selectable is not null)
        {
            parameters[nameof(Chip.Selectable)] = selectable.Value;
        }
        if (selected)
        {
            parameters[nameof(Chip.Selected)] = true;
        }
        if (selectedChanged is not null)
        {
            parameters[nameof(Chip.SelectedChanged)] = selectedChanged.Value;
        }
        if (dismissible is not null)
        {
            parameters[nameof(Chip.Dismissible)] = dismissible.Value;
        }
        if (dismissLabel is not null)
        {
            parameters[nameof(Chip.DismissLabel)] = dismissLabel;
        }
        if (onDismiss is not null)
        {
            parameters[nameof(Chip.OnDismiss)] = onDismiss.Value;
        }
        if (onClick is not null)
        {
            parameters[nameof(Chip.OnClick)] = onClick.Value;
        }
        if (disabled)
        {
            parameters[nameof(Chip.Disabled)] = true;
        }
        if (showCheckMark is not null)
        {
            parameters[nameof(Chip.ShowCheckMark)] = showCheckMark.Value;
        }
        if (icon is not null)
        {
            parameters[nameof(Chip.Icon)] = icon;
        }
        if (@class is not null)
        {
            parameters[nameof(Chip.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Chip.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Chip>(Params(selectable: true, dismissible: true));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 静态芯片应渲染为span而非button()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Chip>(Params());

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"chip\"", markup);
        Assert.DoesNotContain("<button", markup);
        Assert.DoesNotContain("aria-pressed", markup);
    }

    [Fact]
    public async Task 点击可选芯片应切换本地选中态与状态属性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Chip>(Params(selectable: true));
        Assert.Contains("data-state=\"off\"", renderer.Markup());
        Assert.Contains("aria-pressed=\"false\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        var markup = renderer.Markup();
        Assert.Contains("data-state=\"on\"", markup);
        Assert.Contains("aria-pressed=\"true\"", markup);
    }

    [Fact]
    public async Task 绑定Selected应经回调上报新值()
    {
        var renderer = CreateRenderer();
        var reported = new List<bool>();
        var callback = EventCallback.Factory.Create<bool>(
            new object(), value => reported.Add(value));

        await renderer.MountAsync<Chip>(Params(selectable: true, selectedChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        // 绑定委托存在时选中态以 Selected 参数为准，点击后回调上报取反值。
        Assert.Equal([true], reported);
    }

    [Fact]
    public async Task Selected参数在绑定委托存在时决定初始状态()
    {
        var renderer = CreateRenderer();
        var callback = EventCallback.Factory.Create<bool>(new object(), _ => { });

        await renderer.MountAsync<Chip>(Params(selected: true, selectedChanged: callback));

        Assert.Contains("data-state=\"on\"", renderer.Markup());
    }

    [Fact]
    public async Task 移除按钮应携带本地化标签并触发回调()
    {
        var renderer = CreateRenderer();
        var dismissed = false;
        var onDismiss = EventCallback.Factory.Create<MouseEventArgs>(new object(), _ => dismissed = true);

        await renderer.MountAsync<Chip>(Params(dismissible: true, onDismiss: onDismiss));

        Assert.Contains("aria-label=\"Remove\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.True(dismissed);
    }

    [Fact]
    public async Task DismissLabel应覆盖本地化默认值()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Chip>(Params(dismissible: true, dismissLabel: "删掉它"));

        Assert.Contains("aria-label=\"删掉它\"", renderer.Markup());
        Assert.DoesNotContain("aria-label=\"Remove\"", renderer.Markup());
    }

    [Fact]
    public async Task 禁用芯片应呈现半透明并拦截点击()
    {
        var renderer = CreateRenderer();
        var clicked = false;
        var onDismiss = EventCallback.Factory.Create<MouseEventArgs>(new object(), _ => clicked = true);

        await renderer.MountAsync<Chip>(Params(selectable: true, dismissible: true, disabled: true, onDismiss: onDismiss));

        var markup = renderer.Markup();
        Assert.Contains("atom:opacity-50", markup);
        Assert.Contains("disabled", markup);
        Assert.Contains("aria-disabled=\"true\"", markup);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.False(clicked);
        Assert.Contains("data-state=\"off\"", renderer.Markup());
    }

    [Theory]
    [InlineData(ChipVariant.Default, "atom:bg-secondary")]
    [InlineData(ChipVariant.Secondary, "atom:bg-muted")]
    [InlineData(ChipVariant.Outline, "atom:border-input atom:bg-transparent")]
    [InlineData(ChipVariant.Destructive, "atom:bg-destructive/10")]
    [InlineData(ChipVariant.Soft, "atom:bg-primary/10")]
    public async Task 五种变体未选中态应映射到对应配色(ChipVariant variant, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Chip>(Params(variant: variant));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Theory]
    [InlineData(ChipVariant.Default, "atom:bg-primary atom:text-primary-foreground")]
    [InlineData(ChipVariant.Secondary, "atom:border-primary/30 atom:bg-primary/15")]
    [InlineData(ChipVariant.Outline, "atom:border-primary atom:bg-accent")]
    [InlineData(ChipVariant.Destructive, "atom:bg-destructive atom:text-destructive-foreground")]
    [InlineData(ChipVariant.Soft, "atom:bg-primary atom:text-primary-foreground")]
    public async Task 五种变体选中态应映射到对应配色(ChipVariant variant, string expectedClasses)
    {
        var renderer = CreateRenderer();

        // Selectable 使芯片可交互；无绑定委托时初始选中取自 Selected 参数。
        await renderer.MountAsync<Chip>(Params(variant: variant, selectable: true, selected: true));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Theory]
    [InlineData(ChipSize.Small, "atom:h-6 atom:text-xs")]
    [InlineData(ChipSize.Default, "atom:h-8 atom:text-sm")]
    [InlineData(ChipSize.Large, "atom:h-10 atom:text-sm")]
    public async Task 三种尺寸应映射到对应高度类(ChipSize size, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Chip>(Params(size: size));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Fact]
    public async Task 选中且开启对勾时应渲染check图标()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Chip>(Params(selectable: true, showCheckMark: true));
        Assert.DoesNotContain("<svg", renderer.Markup()); // 未选中：无对勾图标

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Contains("<svg", renderer.Markup());
    }

    [Fact]
    public async Task Icon前导内容应包裹在收缩容器中()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Chip>(Params(icon: Text("*")));

        Assert.Contains("atom:inline-flex atom:shrink-0 atom:items-center", renderer.Markup());
    }

    [Fact]
    public async Task OnClick委托使芯片可交互但不产生选中态()
    {
        var renderer = CreateRenderer();
        var clicked = false;
        var onClick = EventCallback.Factory.Create<MouseEventArgs>(new object(), _ => clicked = true);

        await renderer.MountAsync<Chip>(Params(onClick: onClick));
        Assert.Contains("<button", renderer.Markup());
        Assert.DoesNotContain("aria-pressed", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.True(clicked);
        Assert.Contains("data-state=\"off\"", renderer.Markup());
    }

    [Fact]
    public async Task 额外属性应展开到根元素且Class并入根类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Chip>(Params(
            @class: "font-bold",
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "chip" }));

        var markup = renderer.Markup();
        Assert.Contains("data-testid=\"chip\"", markup);
        Assert.Contains("font-bold", markup);
    }
}
