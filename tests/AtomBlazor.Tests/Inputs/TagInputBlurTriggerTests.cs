using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Inputs;

/// <summary>
/// <see cref="TagInputTrigger.Blur"/> 在焦点离开输入框时提交已键入的内容，
/// 免得半途的条目被别处的一击弄丢。
/// <para>这次提交搭的是那条已在推迟关闭建议列表的延迟的便车，
/// 这正是安全所在，
/// 也是这些测试真正检查的点：点击建议与焦点回还都会取消延迟，
/// 于是两者都不会再顺手追加
/// 键入的那半截。</para>
/// </summary>
public class TagInputBlurTriggerTests
{
    // 组件在失焦后先等这么长时间再动作。
    private static readonly TimeSpan pastBlurDelay = TimeSpan.FromMilliseconds(400);

    [Fact]
    public async Task BlurIsNotATriggerByDefault()
    {
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "unfinished");
            await Blur(renderer, input);

            Assert.Empty(tags.Current);
        });
    }

    [Fact]
    public async Task WithBlurSetLeavingTheInputAddsTheTypedText()
    {
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "unfinished");
            await Blur(renderer, input);

            Assert.Equal(["unfinished"], tags.Current);
        }, TagInputTrigger.Enter | TagInputTrigger.Blur);
    }

    [Fact]
    public async Task FocusReturningBeforeTheDelayCancelsTheCommit()
    {
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "still typing");

            // 失焦后立刻回焦——是控件内部的一次点击，不是离开。
            var blur = renderer.Dispatcher.InvokeAsync(() => ComponentProbe.Call(input, "HandleBlur") as Task ?? Task.CompletedTask);
            await renderer.Dispatcher.InvokeAsync(() => ComponentProbe.Call(input, "HandleFocus"));
            await Task.Delay(pastBlurDelay);
            await blur;

            Assert.Empty(tags.Current);
        }, TagInputTrigger.Enter | TagInputTrigger.Blur);
    }

    [Fact]
    public async Task EmptyInputAddsNothing()
    {
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "   ");
            await Blur(renderer, input);

            Assert.Empty(tags.Current);
        }, TagInputTrigger.Enter | TagInputTrigger.Blur);
    }

    [Fact]
    public async Task RejectedTextIsReportedAndKeptInTheInput()
    {
        var rejected = new List<string>();

        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "nope");
            await Blur(renderer, input);

            Assert.Empty(tags.Current);
            Assert.Equal(["nope"], rejected);

            // 留在框内而非静默丢弃，与按键提交时的留法完全一致。
            Assert.Equal("nope", ComponentProbe.Field<string>(input, "_inputText"));
        },
        TagInputTrigger.Enter | TagInputTrigger.Blur,
        parameters => parameters[nameof(TagInput.Validate)] = (Func<string, bool>)(_ => false),
        onRejected: rejected.Add);
    }

    [Fact]
    public async Task ADuplicateIsRejectedRatherThanAdded()
    {
        var rejected = new List<string>();

        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "alpha");
            await Blur(renderer, input);

            // 被拒绝，所以列表从未改变，TagsChanged 也从未触发。
            Assert.Equal(["alpha"], rejected);
            Assert.Empty(tags.Current);
        },
        TagInputTrigger.Enter | TagInputTrigger.Blur,
        parameters => parameters[nameof(TagInput.Tags)] = (IReadOnlyList<string>)["alpha"],
        onRejected: rejected.Add);
    }

    [Fact]
    public async Task TabCommittingFirstDoesNotAddTheTagTwice()
    {
        // Tab 在击键当下就提交并清空文本。随后的失焦必须发现
        // 已没有任何可追加的东西。
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "once");
            await renderer.Dispatcher.InvokeAsync(async () => await input.JsTriggerAdd());
            await Blur(renderer, input);

            Assert.Equal(["once"], tags.Current);
        }, TagInputTrigger.Tab | TagInputTrigger.Blur);
    }

    // --- 私有辅助 ----------------------------------------------------------------------------

    private sealed class TagSink
    {
        public IReadOnlyList<string> Current { get; private set; } = [];

        public void Set(IReadOnlyList<string>? tags) => Current = tags ?? [];
    }

    private static Task Type(ComponentTestRenderer renderer, TagInput input, string text) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await (ComponentProbe.Call(input, "HandleInput", new ChangeEventArgs { Value = text }) as Task
                   ?? Task.CompletedTask));

    private static async Task Blur(ComponentTestRenderer renderer, TagInput input)
    {
        var blur = renderer.Dispatcher.InvokeAsync(() =>
            ComponentProbe.Call(input, "HandleBlur") as Task ?? Task.CompletedTask);
        await Task.Delay(pastBlurDelay);
        await blur;
    }

    private static async Task RunAsync(
        Func<ComponentTestRenderer, TagInput, TagSink, Task> body,
        TagInputTrigger? trigger = null,
        Action<Dictionary<string, object?>>? configure = null,
        Action<string>? onRejected = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var sink = new TagSink();
        var parameters = new Dictionary<string, object?>
        {
            [nameof(TagInput.TagsChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<string>?>(sink, sink.Set),
        };

        if (trigger is not null)
        {
            parameters[nameof(TagInput.AddTrigger)] = trigger.Value;
        }

        if (onRejected is not null)
        {
            parameters[nameof(TagInput.OnTagRejected)] =
                EventCallback.Factory.Create<string>(sink, onRejected);
        }

        configure?.Invoke(parameters);

        var input = await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<TagInput>(parameters));
        await body(renderer, input, sink);
    }
}
