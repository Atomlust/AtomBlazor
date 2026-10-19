using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// <see cref="Stepper"/> 依据每个步骤相对活动步骤的位置推导其状态，
/// 而它的步骤在子内容渲染时自行注册。
/// <para>
/// 这两点都值得钉死。步骤状态从不落仓存储，所以比较里的一个差一错误
/// 就会把错误的步骤标成已完成；又因为注册发生在指示器渲染之后，
/// 落在尚未完成注册的列表末端之外的活动索引必须被钳制
/// 而非抛异常。
/// </para>
/// </summary>
public class StepperStateTests
{
    [Fact]
    public async Task 活动步骤之前的步骤为已完成之后的为待处理()
    {
        await RunAsync(3, activeStep: 1, stepper =>
        {
            Assert.Equal(StepState.Completed, State(stepper, 0));
            Assert.Equal(StepState.Active, State(stepper, 1));
            Assert.Equal(StepState.Pending, State(stepper, 2));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task 显式状态应覆盖位置推导的状态()
    {
        // 步骤 0 排在活动步骤之前，不然它会被读成已完成。
        await RunAsync(3, activeStep: 2, stepper =>
        {
            Assert.Equal(StepState.Error, State(stepper, 0));
            return Task.CompletedTask;
        },
        stateOverrides: new Dictionary<int, StepState> { [0] = StepState.Error });
    }

    [Fact]
    public async Task 显式状态也应覆盖活动步骤()
    {
        await RunAsync(3, activeStep: 1, stepper =>
        {
            Assert.Equal(StepState.Skipped, State(stepper, 1));
            return Task.CompletedTask;
        },
        stateOverrides: new Dictionary<int, StepState> { [1] = StepState.Skipped });
    }

    [Fact]
    public async Task 超出末端的活动索引应被钳制()
    {
        await RunAsync(3, activeStep: 9, stepper =>
        {
            Assert.Equal(StepState.Active, State(stepper, 2));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task 步骤仅在步进器可点击时才可导航()
    {
        await RunAsync(3, activeStep: 0, stepper =>
        {
            Assert.False(CanNavigate(stepper, 2));
            return Task.CompletedTask;
        });

        await RunAsync(3, activeStep: 0, stepper =>
        {
            Assert.True(CanNavigate(stepper, 2));
            return Task.CompletedTask;
        },
        clickable: true);
    }

    [Fact]
    public async Task 点击步骤应上报新索引()
    {
        var reported = new List<int>();

        await RunAsync(3, activeStep: 0, async stepper =>
        {
            await GoTo(stepper, 2);

            Assert.Equal([2], reported);
            Assert.Equal(StepState.Active, State(stepper, 2));
        },
        clickable: true,
        onStepChanged: reported.Add);
    }

    [Fact]
    public async Task 被禁用的步骤不能被激活()
    {
        var reported = new List<int>();

        await RunAsync(3, activeStep: 0, async stepper =>
        {
            await GoTo(stepper, 1);

            Assert.Empty(reported);
            Assert.Equal(StepState.Active, State(stepper, 0));
        },
        clickable: true,
        disabledSteps: [1],
        onStepChanged: reported.Add);
    }

    [Fact]
    public async Task 活动步骤不能被重复激活()
    {
        var reported = new List<int>();

        await RunAsync(3, activeStep: 1, async stepper =>
        {
            await GoTo(stepper, 1);

            Assert.Empty(reported);
        },
        clickable: true,
        onStepChanged: reported.Add);
    }

    // -------------------------------------------------------------------------------------

    private static StepState State(Stepper stepper, int index) =>
        (StepState)ComponentProbe.Call(stepper, "GetStepState", index)!;

    private static bool CanNavigate(Stepper stepper, int index) =>
        (bool)ComponentProbe.Call(stepper, "CanNavigateToStep", index)!;

    private static Task GoTo(Stepper stepper, int index) =>
        (Task)ComponentProbe.Call(stepper, "GoToStepAsync", index)!;

    private static async Task RunAsync(
        int stepCount,
        int activeStep,
        Func<Stepper, Task> body,
        bool clickable = false,
        int[]? disabledSteps = null,
        Dictionary<int, StepState>? stateOverrides = null,
        Action<int>? onStepChanged = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var parameters = new Dictionary<string, object?>
        {
            [nameof(Stepper.ActiveStep)] = activeStep,
            [nameof(Stepper.Clickable)] = clickable,
            [nameof(Stepper.ChildContent)] = (RenderFragment)(builder =>
                BuildSteps(builder, stepCount, disabledSteps ?? [], stateOverrides)),
        };

        if (onStepChanged is not null)
        {
            parameters[nameof(Stepper.ActiveStepChanged)] =
                EventCallback.Factory.Create(onStepChanged.Target ?? new object(), onStepChanged);
        }

        var stepper = await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.MountAsync<Stepper>(parameters));

        await renderer.Dispatcher.InvokeAsync(async () => await body(stepper));
    }

    private static void BuildSteps(
        RenderTreeBuilder builder,
        int stepCount,
        int[] disabledSteps,
        Dictionary<int, StepState>? stateOverrides)
    {
        var sequence = 0;

        for (var index = 0; index < stepCount; index++)
        {
            builder.OpenComponent<Step>(sequence++);
            builder.AddComponentParameter(sequence++, nameof(Step.Title), $"Step {index + 1}");

            if (disabledSteps.Contains(index))
            {
                builder.AddComponentParameter(sequence++, nameof(Step.Disabled), true);
            }

            if (stateOverrides is not null && stateOverrides.TryGetValue(index, out var state))
            {
                builder.AddComponentParameter(sequence++, nameof(Step.State), state);
            }

            builder.CloseComponent();
        }
    }
}
