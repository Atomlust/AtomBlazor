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
/// Toggle（Components 层）测试：包装原语后的 aria-pressed 行为、
/// 变体与尺寸表驱动、ToggleGroup 单选/多选级联（样式经级联参数下发到项）。
/// </summary>
public class ToggleTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    // ------------------------------------------------------------------
    // Toggle 单体
    // ------------------------------------------------------------------

    private static Dictionary<string, object?> Params(
        RenderFragment? childContent = null,
        bool? pressed = null,
        EventCallback<bool>? pressedChanged = null,
        bool? defaultPressed = null,
        bool? disabled = null,
        ToggleVariant? variant = null,
        ToggleSize? size = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (childContent is not null)
        {
            parameters[nameof(Toggle.ChildContent)] = childContent;
        }
        if (pressed is not null)
        {
            parameters[nameof(Toggle.Pressed)] = pressed.Value;
        }
        if (pressedChanged is { HasDelegate: true })
        {
            parameters[nameof(Toggle.PressedChanged)] = pressedChanged.Value;
        }
        if (defaultPressed is not null)
        {
            parameters[nameof(Toggle.DefaultPressed)] = defaultPressed.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(Toggle.Disabled)] = disabled.Value;
        }
        if (variant is not null)
        {
            parameters[nameof(Toggle.Variant)] = variant.Value;
        }
        if (size is not null)
        {
            parameters[nameof(Toggle.Size)] = size.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Toggle.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Toggle.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Toggle>(Params(childContent: Text("Bold")));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 包装后应保留原语的按下状态属性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Toggle>(Params(childContent: Text("Bold")));

        var markup = renderer.Markup();
        Assert.Contains("aria-pressed=\"false\"", markup);
        Assert.Contains("data-state=\"off\"", markup);
    }

    [Fact]
    public async Task 点击应上报新值并随参数回写拨动按下态()
    {
        // 语义：包装层把 bool Pressed 直传原语的 bool? 参数，原语因此恒处于受控模式
        // 状态只能由宿主回写 Pressed 才能改变（非受控拨动仅在原语层可用）。
        var reports = new List<bool>();
        var callback = EventCallback.Factory.Create<bool>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<Toggle>(Params(pressed: false, pressedChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal([true], reports);
        Assert.Contains("aria-pressed=\"false\"", renderer.Markup());

        // 宿主回写 true 后，按下态与 data-state 样式一起翻转。
        await renderer.RerenderRootAsync(0, Params(pressed: true, pressedChanged: callback));

        Assert.Contains("aria-pressed=\"true\"", renderer.Markup());
        Assert.Contains("data-state=\"on\"", renderer.Markup());
    }

    [Fact]
    public async Task 未回写Pressed时点击不应自行拨动状态()
    {
        // 只传 Pressed（无回调）时原语仍判定为受控：点击上报后状态保持参数值。
        var renderer = CreateRenderer();

        await renderer.MountAsync<Toggle>(Params(pressed: false));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Contains("aria-pressed=\"false\"", renderer.Markup());
    }

    [Theory]
    [InlineData(ToggleVariant.Default)]
    [InlineData(ToggleVariant.Outline)]
    public async Task 每个变体应映射专属类(ToggleVariant variant)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Toggle>(Params(variant: variant));

        var markup = renderer.Markup();
        Assert.Contains("atom:bg-transparent", markup);
        if (variant == ToggleVariant.Outline)
        {
            Assert.Contains("atom:border-input", markup);
            Assert.Contains("atom:hover:bg-accent", markup);
        }
    }

    [Theory]
    [InlineData(ToggleSize.Small, "atom:h-9", "atom:px-2.5")]
    [InlineData(ToggleSize.Default, "atom:h-10", "atom:px-3")]
    [InlineData(ToggleSize.Large, "atom:h-11", "atom:px-5")]
    public async Task 每个尺寸应映射高度与内边距(ToggleSize size, string heightClass, string paddingClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Toggle>(Params(size: size));

        var markup = renderer.Markup();
        Assert.Contains(heightClass, markup);
        Assert.Contains(paddingClass, markup);
    }

    [Fact]
    public async Task 按下态数据状态样式与自定义类应同时在场()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Toggle>(Params(pressed: true, @class: "atom:my-toggle"));

        var markup = renderer.Markup();
        Assert.Contains("atom:data-[state=on]:bg-accent", markup);
        Assert.Contains("atom:my-toggle", markup);
        Assert.Contains("aria-pressed=\"true\"", markup);
    }

    // ------------------------------------------------------------------
    // ToggleGroup / ToggleGroupItem（Components 层）
    // ------------------------------------------------------------------

    private static RenderFragment Items(params (string Value, string Label)[] items) => builder =>
    {
        var seq = 0;
        foreach (var (value, label) in items)
        {
            builder.OpenComponent<ToggleGroupItem<string>>(seq++);
            builder.AddAttribute(seq++, nameof(ToggleGroupItem<string>.Value), value);
            builder.AddAttribute(seq++, nameof(ToggleGroupItem<string>.ChildContent),
                (RenderFragment)(inner => inner.AddContent(0, label)));
            builder.CloseComponent();
        }
    };

    private static Dictionary<string, object?> GroupParams(
        RenderFragment childContent,
        ToggleGroupType? type = null,
        string? value = null,
        List<string>? values = null,
        EventCallback<string?> valueChanged = default,
        EventCallback<List<string>> valuesChanged = default,
        ToggleVariant? variant = null,
        ToggleSize? size = null,
        bool? disabled = null,
        bool? required = null,
        bool? scrollable = null,
        string? @class = null,
        string? defaultValue = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ToggleGroup<string>.ChildContent)] = childContent,
        };
        if (type is not null)
        {
            parameters[nameof(ToggleGroup<string>.Type)] = type.Value;
        }
        if (value is not null)
        {
            parameters[nameof(ToggleGroup<string>.Value)] = value;
        }
        if (values is not null)
        {
            parameters[nameof(ToggleGroup<string>.Values)] = values;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(ToggleGroup<string>.ValueChanged)] = valueChanged;
        }
        if (valuesChanged.HasDelegate)
        {
            parameters[nameof(ToggleGroup<string>.ValuesChanged)] = valuesChanged;
        }
        if (variant is not null)
        {
            parameters[nameof(ToggleGroup<string>.Variant)] = variant.Value;
        }
        if (size is not null)
        {
            parameters[nameof(ToggleGroup<string>.Size)] = size.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(ToggleGroup<string>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(ToggleGroup<string>.Required)] = required.Value;
        }
        if (scrollable is not null)
        {
            parameters[nameof(ToggleGroup<string>.Scrollable)] = scrollable.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(ToggleGroup<string>.Class)] = @class;
        }
        if (defaultValue is not null)
        {
            parameters[nameof(ToggleGroup<string>.DefaultValue)] = defaultValue;
        }
        return parameters;
    }

    // 原语只在携带 Changed 委托时才把 Value(s) 当作显示来源
    // 纯展示断言因此需要一个空操作回调来进入受控分支。
    private static EventCallback<string?> NullValueChanged() =>
        EventCallback.Factory.Create<string?>(new object(), _ => { });

    private static EventCallback<List<string>> NullValuesChanged() =>
        EventCallback.Factory.Create<List<string>>(new object(), _ => { });

    [Fact]
    public async Task 单选组快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(
            Items(("a", "A"), ("b", "B")), value: "a", valueChanged: NullValueChanged()));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 组根应为radiogroup并携带弹性布局排布类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(Items(("a", "A"))));

        var markup = renderer.Markup();
        Assert.Contains("role=\"radiogroup\"", markup);
        Assert.Contains("atom:flex", markup);
        Assert.Contains("atom:justify-center", markup);
    }

    [Fact]
    public async Task 多选模式应映射为group角色与按下项()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(
            Items(("a", "A"), ("b", "B")),
            type: ToggleGroupType.Multiple,
            values: ["a"],
            valuesChanged: NullValuesChanged()));

        var markup = renderer.Markup();
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("aria-pressed=\"true\"", markup);
        Assert.Contains("aria-pressed=\"false\"", markup);
    }

    [Fact]
    public async Task 点击项应切换单选并经ValueChanged上报()
    {
        var reports = new List<string?>();
        var callback = EventCallback.Factory.Create<string?>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(
            Items(("a", "A"), ("b", "B")), value: "a", valueChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        Assert.Equal(["b"], reports);
    }

    [Fact]
    public async Task 组的变体与尺寸应级联到全部项()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(
            Items(("a", "A"), ("b", "B")),
            variant: ToggleVariant.Outline,
            size: ToggleSize.Small));

        var markup = renderer.Markup();
        Assert.Equal(2, markup.Split("atom:border-input", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, markup.Split("atom:px-2.5", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task 项自声明样式不被组级联覆盖时应保留自定义类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(
            (RenderFragment)(builder =>
            {
                builder.OpenComponent<ToggleGroupItem<string>>(0);
                builder.AddAttribute(1, nameof(ToggleGroupItem<string>.Value), "a");
                builder.AddAttribute(2, nameof(ToggleGroupItem<string>.Class), "atom:item-custom");
                builder.AddAttribute(3, nameof(ToggleGroupItem<string>.ChildContent), Text("A"));
                builder.CloseComponent();
            })));

        Assert.Contains("atom:item-custom", renderer.Markup());
    }

    [Fact]
    public async Task Scrollable应改为横向滚动排布类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(
            Items(("a", "A")), scrollable: true));

        var markup = renderer.Markup();
        Assert.Contains("atom:overflow-x-auto", markup);

        // 组根的开标签（首个元素）应改用 justify-start，项自身的居中类不受影响。
        var groupTag = markup[..(markup.IndexOf('>') + 1)];
        Assert.Contains("atom:justify-start", groupTag);
        Assert.DoesNotContain("atom:justify-center", groupTag);
    }

    [Fact]
    public async Task 组禁用应级联到项并吞掉点击()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(
            Items(("a", "A"), ("b", "B")), defaultValue: "a", disabled: true));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task Required单选点击最后选中项不应取消()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(
            Items(("a", "A")), defaultValue: "a", required: true));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 多选点击应增删值并经ValuesChanged上报()
    {
        var reports = new List<List<string>>();
        var callback = EventCallback.Factory.Create<List<string>>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<ToggleGroup<string>>(GroupParams(
            Items(("a", "A"), ("b", "B")),
            type: ToggleGroupType.Multiple,
            values: ["a"],
            valuesChanged: callback));

        // 点 b 加入。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        // 模拟宿主回写 Values 后再点 a 移除。
        await renderer.RerenderRootAsync(0, GroupParams(
            Items(("a", "A"), ("b", "B")),
            type: ToggleGroupType.Multiple,
            values: ["a", "b"],
            valuesChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal(["a", "b"], reports[0]);
        Assert.Equal(["b"], reports[1]);
    }
}
