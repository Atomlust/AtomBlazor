using AtomBlazor.Primitives;
using System.Linq.Expressions;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace AtomBlazor.Tests.Inputs;

public class MobileInputTests
{
    private static readonly double[] drawerSnaps = [.3, .6, .9];
    [Fact]
    public async Task QuantityButtonsClampWithoutOverflowAndRemoveOnlyAtMinimum()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var removed = 0;
            var stepper = await renderer.MountAsync<QuantityStepper>(new()
            {
                [nameof(QuantityStepper.Value)] = int.MaxValue - 1,
                [nameof(QuantityStepper.Step)] = int.MaxValue,
                [nameof(QuantityStepper.OnRemove)] = EventCallback.Factory.Create(this, () => removed++)
            });
            await (Task)ComponentProbe.Call(stepper, "IncreaseAsync")!;
            Assert.Equal(int.MaxValue, stepper.Value);
            await (Task)ComponentProbe.Call(stepper, "DecreaseAsync")!;
            Assert.Equal(1, stepper.Value);
            Assert.Equal(0, removed);
            await (Task)ComponentProbe.Call(stepper, "DecreaseAsync")!;
            Assert.Equal(1, removed);
            await stepper.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(stepper.ReadOnly)] = true }));
            await (Task)ComponentProbe.Call(stepper, "IncreaseAsync")!;
            await (Task)ComponentProbe.Call(stepper, "DecreaseAsync")!;
            Assert.Equal(1, stepper.Value);
            Assert.Equal(1, removed);
        });
    }

    [Fact]
    public async Task QuantityButtonChangesNotifyTheBoundEditFormFieldOnce()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var model = new QuantityModel();
            var context = new EditContext(model);
            var changedFields = new List<FieldIdentifier>();
            context.OnFieldChanged += (_, args) => changedFields.Add(args.FieldIdentifier);
            RenderFragment content = builder =>
            {
                builder.OpenComponent<QuantityStepper>(0);
                builder.AddAttribute(1, nameof(QuantityStepper.Value), model.Quantity);
                builder.AddAttribute(2, nameof(QuantityStepper.ValueExpression), (Expression<Func<int>>)(() => model.Quantity));
                builder.AddAttribute(3, nameof(QuantityStepper.ValueChanged), EventCallback.Factory.Create<int>(model, value => model.Quantity = value));
                builder.AddAttribute(4, nameof(QuantityStepper.Max), 2);
                builder.CloseComponent();
            };
            await renderer.MountAsync<CascadingValue<EditContext>>(new()
            {
                [nameof(CascadingValue<EditContext>.Value)] = context,
                [nameof(CascadingValue<EditContext>.ChildContent)] = content
            });
            var stepper = renderer.FindComponent<QuantityStepper>();
            await (Task)ComponentProbe.Call(stepper, "IncreaseAsync")!;
            await (Task)ComponentProbe.Call(stepper, "IncreaseAsync")!;
            Assert.Equal(2, model.Quantity);
            Assert.Equal(new FieldIdentifier(model, nameof(model.Quantity)), Assert.Single(changedFields));
        });
    }

    [Fact]
    public async Task DrawerSnapChangesIgnoreOutOfRangeIndicesAndNotifyOnlyOnChange()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var indices = new List<int>();
            var drawer = await renderer.MountAsync<Drawer>(new()
            {
                [nameof(Drawer.SnapPoints)] = drawerSnaps,
                [nameof(Drawer.SnapIndexChanged)] = EventCallback.Factory.Create<int>(this, index => indices.Add(index))
            });
            await drawer.SetSnapIndexAsync(2);
            await drawer.SetSnapIndexAsync(2);
            await drawer.SetSnapIndexAsync(3);
            await drawer.SetSnapIndexAsync(-1);
            Assert.Equal(2, drawer.SnapIndex);
            Assert.Equal([2], indices);
        });
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();
        return services;
    }

    private sealed class QuantityModel
    {
        public int Quantity { get; set; } = 1;
    }
}
