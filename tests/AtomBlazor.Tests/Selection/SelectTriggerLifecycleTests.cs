using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Selection;

public class SelectTriggerLifecycleTests
{
    [Fact]
    public async Task KeyboardActivationDoesNotCloseTheSelectItJustOpened()
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
            var select = await renderer.MountAsync<AtomBlazor.Primitives.SelectPrimitive<string>>(new()
            {
                ["ChildContent"] = (RenderFragment)(builder =>
                {
                    builder.OpenComponent<AtomBlazor.Primitives.SelectTrigger<string>>(0);
                    builder.CloseComponent();
                })
            });
            var trigger = renderer.FindComponent<AtomBlazor.Primitives.SelectTrigger<string>>();
            var context = ComponentProbe.Field<SelectContext<string>>(select, "_context");
            ComponentProbe.Call(trigger, "HandleKeyDown", new KeyboardEventArgs { Key = "Enter" });
            Assert.True(context.IsOpen);
            ComponentProbe.Call(trigger, "HandleClick", new MouseEventArgs { Detail = 0 });
            Assert.True(context.IsOpen);
            ComponentProbe.Call(trigger, "HandleClick", new MouseEventArgs { Detail = 1 });
            Assert.False(context.IsOpen);
        });
    }
}
