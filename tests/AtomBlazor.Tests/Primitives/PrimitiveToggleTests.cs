using AtomBlazor.Primitives;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using PrimitiveToggle = AtomBlazor.Primitives.TogglePrimitive;
using PrimitiveToggleGroup = AtomBlazor.Primitives.ToggleGroup<string>;
using PrimitiveToggleGroupItem = AtomBlazor.Primitives.ToggleGroupItem<string>;
using PrimitiveToggleGroupType = AtomBlazor.Primitives.ToggleGroupType;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// Toggle 原语测试：aria-pressed/data-state 状态映射、受控与非受控拨动、
/// 禁用吞点击；组原语的 radiogroup/group 角色、radio/button 角色配对、
/// 漫游 tabindex、单选/多选级联与方向键导航。
/// </summary>
public class PrimitiveToggleTests
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

    [Fact]
    public async Task 原语默认应未按下并输出false按下态()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggle>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveToggle.ChildContent)] = Text("Bold"),
        });

        var markup = renderer.Markup();
        Assert.Contains("type=\"button\"", markup);
        Assert.Contains("aria-pressed=\"false\"", markup);
        Assert.Contains("data-state=\"off\"", markup);
    }

    [Fact]
    public async Task 非受控点击应在开与关之间拨动()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggle>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveToggle.ChildContent)] = Text("Bold"),
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        var markup = renderer.Markup();
        Assert.Contains("aria-pressed=\"true\"", markup);
        Assert.Contains("data-state=\"on\"", markup);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Contains("aria-pressed=\"false\"", renderer.Markup());
    }

    [Fact]
    public async Task DefaultPressed应在初始化时置为按下()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggle>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveToggle.DefaultPressed)] = true,
        });

        Assert.Contains("aria-pressed=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 受控点击应只上报新值不自行改变状态()
    {
        var reports = new List<bool>();
        var callback = EventCallback.Factory.Create<bool>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggle>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveToggle.Pressed)] = true,
            [nameof(PrimitiveToggle.PressedChanged)] = callback,
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal([false], reports);
        // 状态仍由外部参数决定。
        Assert.Contains("aria-pressed=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 禁用原语应输出disabled并吞掉点击()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggle>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveToggle.Disabled)] = true,
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        var markup = renderer.Markup();
        Assert.Contains("disabled", markup);
        Assert.Contains("aria-pressed=\"false\"", markup);
    }

    [Fact]
    public async Task 原语应把未匹配属性展开到按钮()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggle>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveToggle.AdditionalAttributes)] = new Dictionary<string, object> { ["id"] = "t" },
        });

        Assert.Contains("id=\"t\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // ToggleGroup 原语（级联）
    // ------------------------------------------------------------------

    // 渲染若干 <ToggleGroupItem Value="value">label</ToggleGroupItem> 子项。
    private static RenderFragment Items(params (string Value, string Label)[] items) => builder =>
    {
        var seq = 0;
        foreach (var (value, label) in items)
        {
            builder.OpenComponent<PrimitiveToggleGroupItem>(seq++);
            builder.AddAttribute(seq++, nameof(PrimitiveToggleGroupItem.Value), value);
            builder.AddAttribute(seq++, nameof(PrimitiveToggleGroupItem.ChildContent),
                (RenderFragment)(inner => inner.AddContent(0, label)));
            builder.CloseComponent();
        }
    };

    private static Dictionary<string, object?> GroupParams(
        RenderFragment childContent,
        PrimitiveToggleGroupType? type = null,
        string? value = null,
        List<string>? values = null,
        EventCallback<string?> valueChanged = default,
        EventCallback<List<string>> valuesChanged = default,
        bool? disabled = null,
        bool? required = null,
        string? ariaLabel = null,
        string? orientation = null,
        string? defaultValue = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(PrimitiveToggleGroup.ChildContent)] = childContent,
        };
        if (type is not null)
        {
            parameters[nameof(PrimitiveToggleGroup.Type)] = type.Value;
        }
        if (value is not null)
        {
            parameters[nameof(PrimitiveToggleGroup.Value)] = value;
        }
        if (values is not null)
        {
            parameters[nameof(PrimitiveToggleGroup.Values)] = values;
        }
        if (valueChanged.HasDelegate)
        {
            parameters[nameof(PrimitiveToggleGroup.ValueChanged)] = valueChanged;
        }
        if (valuesChanged.HasDelegate)
        {
            parameters[nameof(PrimitiveToggleGroup.ValuesChanged)] = valuesChanged;
        }
        if (disabled is not null)
        {
            parameters[nameof(PrimitiveToggleGroup.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(PrimitiveToggleGroup.Required)] = required.Value;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(PrimitiveToggleGroup.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(PrimitiveToggleGroup.Orientation)] = orientation;
        }
        if (defaultValue is not null)
        {
            parameters[nameof(PrimitiveToggleGroup.DefaultValue)] = defaultValue;
        }
        return parameters;
    }

    // 原语只在携带 Changed 委托时才把 Value(s) 当作受控来源；
    // 下面的纯展示断言因此需要一个空操作回调来进入受控分支。
    private static EventCallback<string?> NullValueChanged() =>
        EventCallback.Factory.Create<string?>(new object(), _ => { });

    private static EventCallback<List<string>> NullValuesChanged() =>
        EventCallback.Factory.Create<List<string>>(new object(), _ => { });

    [Fact]
    public async Task 单选组应使用radiogroup角色且radio项配ariaChecked()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggleGroup>(GroupParams(
            Items(("a", "A"), ("b", "B")), value: "a", valueChanged: NullValueChanged(), ariaLabel: "Align"));

        var markup = renderer.Markup();
        Assert.Contains("role=\"radiogroup\"", markup);
        Assert.Contains("aria-label=\"Align\"", markup);
        Assert.Contains("role=\"radio\"", markup);
        Assert.Contains("aria-checked=\"true\"", markup);
        Assert.Contains("aria-checked=\"false\"", markup);
        Assert.DoesNotContain("aria-pressed", markup);
    }

    [Fact]
    public async Task 多选组应使用group角色且button项配ariaPressed()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggleGroup>(GroupParams(
            Items(("a", "A"), ("b", "B")),
            type: PrimitiveToggleGroupType.Multiple,
            values: ["a"],
            valuesChanged: NullValuesChanged()));

        var markup = renderer.Markup();
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("role=\"button\"", markup);
        Assert.Contains("aria-pressed=\"true\"", markup);
        Assert.Contains("aria-pressed=\"false\"", markup);
        Assert.DoesNotContain("aria-checked", markup);
    }

    [Fact]
    public async Task 单选非受控点击应使选择迁移并保留唯一按下项()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggleGroup>(GroupParams(
            Items(("a", "A"), ("b", "B")), value: null));

        // 无初始选择：第一项可 Tab（漫游 tabindex）。
        var markup = renderer.Markup();
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(markup, "tabindex=\"0\""));

        // occurrence 0/1：两个项各自的 onclick（组的 onkeydown 不是 onclick，不计入）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        markup = renderer.Markup();
        Assert.Contains("aria-checked=\"true\"", markup);
        Assert.Contains("data-state=\"on\"", markup);
    }

    [Fact]
    public async Task 受控单选应经ValueChanged上报且点击相同项时上报null()
    {
        var reports = new List<string?>();
        var callback = EventCallback.Factory.Create<string?>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggleGroup>(GroupParams(
            Items(("a", "A"), ("b", "B")), value: "a", valueChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal(["b", null], reports);
    }

    [Fact]
    public async Task Required单选点击最后选中项不应生效()
    {
        var renderer = CreateRenderer();

        // 非受控：initial 选择来自 DefaultValue，点击唯一选中项应被 Required 拦住。
        await renderer.MountAsync<PrimitiveToggleGroup>(GroupParams(
            Items(("a", "A")), defaultValue: "a", required: true));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 多选经ValuesChanged增删上报()
    {
        var reports = new List<List<string>>();
        var callback = EventCallback.Factory.Create<List<string>>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggleGroup>(GroupParams(
            Items(("a", "A"), ("b", "B")),
            type: PrimitiveToggleGroupType.Multiple,
            values: ["a"],
            valuesChanged: callback));

        // 点 b 加入。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        // 受控组的显示来源是 Values 参数：模拟宿主回写 ["a","b"] 后再点 a 移除。
        await renderer.RerenderRootAsync(0, GroupParams(
            Items(("a", "A"), ("b", "B")),
            type: PrimitiveToggleGroupType.Multiple,
            values: ["a", "b"],
            valuesChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal(["a", "b"], reports[0]);
        Assert.Equal(["b"], reports[1]);
    }

    [Fact]
    public async Task 组禁用应级联到全部项并吞掉点击()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveToggleGroup>(GroupParams(
            Items(("a", "A"), ("b", "B")), defaultValue: "a", disabled: true));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));

        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup);
        // 两个项都渲染了裸 disabled 特性。
        Assert.Equal(2, markup.Split(" disabled", StringSplitOptions.None).Length - 1);
        Assert.Contains("aria-checked=\"true\"", markup);
    }
}
