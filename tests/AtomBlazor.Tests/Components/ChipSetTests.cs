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
/// ChipSet 级联与选择测试：组快照、role="group" 结构、单选切换与迁移、
/// 多选增删与 @bind-Values 上报、Required 保底选择、级联默认值
/// （变体/尺寸/移除按钮/对勾/禁用）与芯片自声明优先、集合移除回调与选择剔除。
/// </summary>
public class ChipSetTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    // 渲染若干 <Chip Value="@value">label</Chip> 子元素。
    private static RenderFragment Chips(params (string Value, string Label)[] chips) => builder =>
    {
        var seq = 0;
        foreach (var (value, label) in chips)
        {
            builder.OpenComponent<Chip>(seq++);
            builder.AddAttribute(seq++, nameof(Chip.Value), value);
            builder.AddAttribute(seq++, nameof(Chip.ChildContent), (RenderFragment)(b => b.AddContent(0, label)));
            builder.CloseComponent();
        }
    };

    private static Dictionary<string, object?> Params(
        RenderFragment childContent,
        ChipSelectionMode? mode = null,
        string? value = null,
        List<string>? values = null,
        EventCallback<string?> valueChanged = default,
        EventCallback<List<string>> valuesChanged = default,
        bool required = false,
        ChipVariant? variant = null,
        ChipSize? size = null,
        bool dismissible = false,
        bool showCheckMark = false,
        bool disabled = false,
        bool? wrap = null,
        EventCallback<string>? onDismiss = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ChipSet<string>.ChildContent)] = childContent,
        };
        if (mode is not null)
        {
            parameters[nameof(ChipSet<string>.SelectionMode)] = mode.Value;
        }
        if (value is not null)
        {
            parameters[nameof(ChipSet<string>.Value)] = value;
        }
        if (values is not null)
        {
            parameters[nameof(ChipSet<string>.Values)] = values;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(ChipSet<string>.ValueChanged)] = valueChanged;
        }
        if (valuesChanged.HasDelegate)
        {
            parameters[nameof(ChipSet<string>.ValuesChanged)] = valuesChanged;
        }
        if (required)
        {
            parameters[nameof(ChipSet<string>.Required)] = true;
        }
        if (variant is not null)
        {
            parameters[nameof(ChipSet<string>.Variant)] = variant.Value;
        }
        if (size is not null)
        {
            parameters[nameof(ChipSet<string>.Size)] = size.Value;
        }
        if (dismissible)
        {
            parameters[nameof(ChipSet<string>.Dismissible)] = true;
        }
        if (showCheckMark)
        {
            parameters[nameof(ChipSet<string>.ShowCheckMark)] = true;
        }
        if (disabled)
        {
            parameters[nameof(ChipSet<string>.Disabled)] = true;
        }
        if (wrap is not null)
        {
            parameters[nameof(ChipSet<string>.Wrap)] = wrap.Value;
        }
        if (onDismiss is not null)
        {
            parameters[nameof(ChipSet<string>.OnDismiss)] = onDismiss;
        }
        return parameters;
    }

    [Fact]
    public async Task 单选集合快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("design", "Design"), ("build", "Build"), ("ship", "Ship")),
            mode: ChipSelectionMode.Single,
            value: "build"));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 集合根应为group角色并携带数据槽标记()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(Chips(("a", "A"))));

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"chip-set\"", markup);
        Assert.Contains("role=\"group\"", markup);
    }

    [Fact]
    public async Task 点击未选中芯片应选中而点击选中芯片应取消()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A"), ("b", "B")), value: "a"));

        Assert.Contains("data-state=\"on\"", renderer.Markup());

        // occurrence 0：第一个芯片主体（已选中）——单击模式取消选择。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Contains("data-state=\"off\"", renderer.Markup());

        // occurrence 1：第二个芯片主体——选中 b。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        var markup = renderer.Markup();
        Assert.Contains("data-state=\"on\"", markup);
    }

    [Fact]
    public async Task 单选模式点击另一芯片应使选择迁移()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A"), ("b", "B")), value: "a"));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        var markup = renderer.Markup();
        // 恰好一个 on、一个 off。
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(markup, "data-state=\"on\""));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(markup, "data-state=\"off\""));
    }

    [Fact]
    public async Task ValueChanged绑定应在切换时上报选中值()
    {
        var reported = new List<string?>();
        var callback = EventCallback.Factory.Create<string?>(
            new object(), value => reported.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A"), ("b", "B")),
            value: "a",
            valueChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        Assert.Equal(["b"], reported);
    }

    [Fact]
    public async Task 多选模式应支持增删并经ValuesChanged上报()
    {
        var renderer = CreateRenderer();
        var reports = new List<List<string>>();
        var callback = EventCallback.Factory.Create<List<string>>(
            new object(), value => reports.Add(value));

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A"), ("b", "B"), ("c", "C")),
            mode: ChipSelectionMode.Multiple,
            values: ["a"],
            valuesChanged: callback));

        // 选中 b（occurrence 1）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        Assert.Equal(["a", "b"], reports[0]);

        // 再点 a（occurrence 0）取消。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal(["b"], reports[1]);
    }

    [Fact]
    public async Task Required单选时点击最后选中芯片不生效()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A")), mode: ChipSelectionMode.Single, value: "a", required: true));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Contains("data-state=\"on\"", renderer.Markup());
    }

    [Fact]
    public async Task Required多选仅剩一个选中时不可再取消()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A"), ("b", "B")),
            mode: ChipSelectionMode.Multiple,
            values: ["a"],
            required: true));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Contains("data-state=\"on\"", renderer.Markup());
    }

    [Fact]
    public async Task None模式集合仅分组而芯片不参与选择()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A")), mode: ChipSelectionMode.None, value: "a"));

        var markup = renderer.Markup();
        Assert.Contains("data-state=\"off\"", markup);
        Assert.DoesNotContain("<button", markup);
    }

    [Fact]
    public async Task 集合级Dismissible应级联到每个芯片()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A"), ("b", "B")), dismissible: true));

        var markup = renderer.Markup();
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(markup, "aria-label=\"Remove\""));
    }

    [Fact]
    public async Task 芯片移除应触发集合回调并剔除选中值()
    {
        var renderer = CreateRenderer();
        var dismissedValues = new List<string>();
        var onDismiss = EventCallback.Factory.Create<string>(new object(), v => dismissedValues.Add(v));

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A")),
            mode: ChipSelectionMode.Single,
            value: "a",
            dismissible: true,
            onDismiss: onDismiss));

        // occurrence 0 为芯片主体按钮，occurrence 1 为移除按钮。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        Assert.Equal(["a"], dismissedValues);
        Assert.Contains("data-state=\"off\"", renderer.Markup());
    }

    [Fact]
    public async Task 集合级变体尺寸与禁用应级联到芯片()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A")),
            variant: ChipVariant.Outline,
            size: ChipSize.Large,
            disabled: true));

        var markup = renderer.Markup();
        Assert.Contains("atom:border-input atom:bg-transparent", markup); // Outline 未选中
        Assert.Contains("atom:h-10 atom:text-sm", markup);                // Large
        Assert.Contains("atom:opacity-50", markup);                       // 禁用
    }

    [Fact]
    public async Task 芯片自声明变体应优先于集合默认值()
    {
        var renderer = CreateRenderer();

        var chips = (RenderFragment)(builder =>
        {
            builder.OpenComponent<Chip>(0);
            builder.AddAttribute(1, nameof(Chip.Value), "a");
            builder.AddAttribute(2, nameof(Chip.Variant), ChipVariant.Destructive);
            builder.AddAttribute(3, nameof(Chip.ChildContent), Text("A"));
            builder.CloseComponent();
        });

        await renderer.MountAsync<ChipSet<string>>(Params(chips, variant: ChipVariant.Outline));

        var markup = renderer.Markup();
        Assert.Contains("atom:bg-destructive/10", markup);
        Assert.DoesNotContain("atom:border-input", markup);
    }

    [Fact]
    public async Task Wrap关闭应切换为横向滚动而保持子项不收缩()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(Chips(("a", "A")), wrap: false));

        var markup = renderer.Markup();
        Assert.Contains("atom:overflow-x-auto", markup);
        Assert.DoesNotContain("atom:flex-wrap", markup);
    }

    [Fact]
    public async Task 集合级ShowCheckMark应级联并在选中芯片上显示对勾()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ChipSet<string>>(Params(
            Chips(("a", "A")), mode: ChipSelectionMode.Single, value: "a", showCheckMark: true));

        Assert.Contains("<svg", renderer.Markup());
    }
}
