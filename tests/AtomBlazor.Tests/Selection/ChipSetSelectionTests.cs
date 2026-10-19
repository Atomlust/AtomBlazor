using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Selection;

/// <summary>
/// <see cref="ChipSet{TValue}"/> 为其 chip 掌管选择，chip 则把值以
/// <see cref="object"/> 承载，从而可以不带类型参数地使用。
/// <para>装箱正是这些测试存在的理由：
/// 集合在每次操作时都把值回拆成 <c>TValue</c>，
/// 所以与集合类型不符的值、或压根没有值的 chip，
/// 都必须从选择中落出而不是抛异常。
/// 受控与非受控两条路都走同一个 toggle，
/// 并由 <c>Required</c> 把关——
/// 这些单看渲染标记都看不出来。</para>
/// </summary>
public class ChipSetSelectionTests
{
    [Fact]
    public async Task ClickingAChipReportsItsValue()
    {
        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["beta"]);

            Assert.Equal("beta", sink.Value);
        });
    }

    [Fact]
    public async Task ClickingTheSelectedChipClearsTheSelection()
    {
        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["alpha"]);
            await Activate(renderer, chips["alpha"]);

            Assert.Null(sink.Value);
        });
    }

    [Fact]
    public async Task RequiredKeepsTheLastSelectedChip()
    {
        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["alpha"]);
            await Activate(renderer, chips["alpha"]);

            Assert.Equal("alpha", sink.Value);
        },
        configure: parameters => parameters[nameof(ChipSet<string>.Required)] = true);
    }

    [Fact]
    public async Task MultipleModeAddsAndRemovesValues()
    {
        await RunAsync(["alpha", "beta", "gamma"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["alpha"]);
            await Activate(renderer, chips["gamma"]);

            Assert.Equal(["alpha", "gamma"], sink.Values);

            await Activate(renderer, chips["alpha"]);

            Assert.Equal(["gamma"], sink.Values);
        },
        configure: parameters =>
            parameters[nameof(ChipSet<string>.SelectionMode)] = ChipSelectionMode.Multiple);
    }

    [Fact]
    public async Task MultipleModeReportsANewListRatherThanMutatingTheBoundOne()
    {
        var bound = new List<string> { "alpha" };

        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["beta"]);

            Assert.Equal(["alpha", "beta"], sink.Values);

            // 调用方交来的列表原封未动，所以以其重渲染的组件
            // 不会看到一份从未被告知过的选择。
            Assert.Equal(["alpha"], bound);
        },
        configure: parameters =>
        {
            parameters[nameof(ChipSet<string>.SelectionMode)] = ChipSelectionMode.Multiple;
            parameters[nameof(ChipSet<string>.Values)] = bound;
        });
    }

    [Fact]
    public async Task DismissingAChipDropsItsValueAndReportsIt()
    {
        var dismissed = new List<string>();

        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["alpha"]);
            await Dismiss(renderer, chips["alpha"]);

            Assert.Null(sink.Value);
            Assert.Equal(["alpha"], dismissed);
        },
        configure: parameters =>
        {
            parameters[nameof(ChipSet<string>.Dismissible)] = true;
            parameters[nameof(ChipSet<string>.OnDismiss)] =
                EventCallback.Factory.Create<string>(dismissed, dismissed.Add);
        });
    }

    [Fact]
    public async Task WithoutABindingTheSetKeepsItsOwnSelection()
    {
        await RunAsync(["alpha", "beta"], async (renderer, _, chips) =>
        {
            await Activate(renderer, chips["beta"]);

            // 什么都没绑定，点击唯一的记录就是集合自身的状态，
            // 经由自称处于按下态的 chip 回读出来。
            Assert.Contains("data-state=\"on\"", renderer.Markup());
            Assert.Contains("aria-pressed=\"true\"", renderer.Markup());
        },
        bindValue: false);
    }

    [Fact]
    public async Task AChipWithNoValueIsNotInteractive()
    {
        await RunAsync([], async (renderer, _, _) =>
        {
            var markup = renderer.Markup();

            // 无法参与选择的 chip 渲染成 span，
            // 读屏软件因此不会播报一个什么都不做的按钮。
            Assert.DoesNotContain("aria-pressed", markup);
            Assert.DoesNotContain("<button", markup);
        },
        valuelessChip: true);
    }

    [Fact]
    public async Task AValueOfTheWrongTypeIsIgnored()
    {
        await RunAsync([], async (renderer, sink, chips) =>
        {
            // chip 以 object 装箱，拦不住调用方往字符串集合里塞一个 int。
            // 它必须被排除在外而非抛异常。
            await Activate(renderer, chips["wrong"]);

            Assert.Null(sink.Value);
        },
        wrongTypedChip: true);
    }

    // --- 私有辅助 ----------------------------------------------------------------------------

    private sealed class SelectionSink
    {
        public string? Value { get; private set; }
        public IReadOnlyList<string> Values { get; private set; } = [];

        public void SetValue(string? value) => Value = value;
        public void SetValues(List<string> values) => Values = values;
    }

    private static Task Activate(ComponentTestRenderer renderer, Chip chip) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await (ComponentProbe.Call(chip, "HandleActivateAsync", new MouseEventArgs()) as Task
                   ?? Task.CompletedTask));

    private static Task Dismiss(ComponentTestRenderer renderer, Chip chip) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await (ComponentProbe.Call(chip, "HandleDismissAsync", new MouseEventArgs()) as Task
                   ?? Task.CompletedTask));

    private static async Task RunAsync(
        string[] values,
        Func<ComponentTestRenderer, SelectionSink, Dictionary<string, Chip>, Task> body,
        Action<Dictionary<string, object?>>? configure = null,
        bool bindValue = true,
        bool valuelessChip = false,
        bool wrongTypedChip = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var sink = new SelectionSink();
        var chips = new Dictionary<string, Chip>(StringComparer.Ordinal);

        var parameters = new Dictionary<string, object?>
        {
            [nameof(ChipSet<string>.ChildContent)] =
                (RenderFragment)(builder => BuildChips(builder, values, chips, valuelessChip, wrongTypedChip)),
        };

        if (bindValue)
        {
            parameters[nameof(ChipSet<string>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(sink, sink.SetValue);
            parameters[nameof(ChipSet<string>.ValuesChanged)] =
                EventCallback.Factory.Create<List<string>>(sink, sink.SetValues);
        }

        configure?.Invoke(parameters);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.MountAsync<ChipSet<string>>(parameters));

        await body(renderer, sink, chips);
    }

    private static void BuildChips(
        RenderTreeBuilder builder,
        string[] values,
        Dictionary<string, Chip> chips,
        bool valuelessChip,
        bool wrongTypedChip)
    {
        var sequence = 0;

        foreach (var value in values)
        {
            var key = value;
            builder.OpenComponent<Chip>(sequence++);
            builder.AddComponentParameter(sequence++, nameof(Chip.Value), value);
            builder.AddComponentReferenceCapture(sequence++, captured => chips[key] = (Chip)captured);
            builder.CloseComponent();
        }

        if (valuelessChip)
        {
            builder.OpenComponent<Chip>(sequence++);
            builder.AddComponentReferenceCapture(sequence++, captured => chips["none"] = (Chip)captured);
            builder.CloseComponent();
        }

        if (wrongTypedChip)
        {
            builder.OpenComponent<Chip>(sequence++);
            builder.AddComponentParameter(sequence++, nameof(Chip.Value), 42);
            builder.AddComponentReferenceCapture(sequence++, captured => chips["wrong"] = (Chip)captured);
            builder.CloseComponent();
        }
    }
}
