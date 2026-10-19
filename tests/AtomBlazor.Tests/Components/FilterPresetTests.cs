using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Primitives.Filtering;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace AtomBlazor.Tests.Components;

public class FilterPresetTests
{
    [Fact]
    public async Task PresetsAreClonedAndExplicitApplyKeepsEditsInTheDraft()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var saved = new FilterDefinition { Conditions = [new() { Field = "Status", Value = "Active", Operator = FilterOperator.Equals }] };
            var original = new FilterDefinition();
            var builder = await renderer.MountAsync<FilterBuilder>(new()
            {
                [nameof(FilterBuilder.Filter)] = original,
                [nameof(FilterBuilder.ShowApplyButton)] = true,
                [nameof(FilterBuilder.Fields)] = new FilterField[] { new() { Name = "Status", Label = "Status", Type = FilterFieldType.Text } },
                [nameof(FilterBuilder.Presets)] = new FilterPreset[] { new("active", "Active", saved) }
            });
            await builder.ApplyPresetAsync("active");
            Assert.True(original.IsEmpty);
            var draft = ComponentProbe.Field<FilterDefinition>(builder, "draftFilter");
            Assert.NotSame(saved.Conditions[0], draft.Conditions[0]);
            var editor = new FilterValueEditorContext(new() { Name = "Status", Label = "Status" }, draft.Conditions[0], EventCallback.Empty);
            await editor.SetValueAsync("Pending");
            Assert.Equal("Active", saved.Conditions[0].Value);
            Assert.True(original.IsEmpty);
            await (Task)ComponentProbe.Call(builder, "HandleApply")!;
            Assert.Equal("Pending", original.Conditions[0].Value);
            await builder.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(builder.MaxConditions)] = 0 }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => builder.ApplyPresetAsync("active"));
            Assert.Equal("Pending", original.Conditions[0].Value);
        });
    }
}
