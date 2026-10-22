using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Focus;

/// <summary>
/// 守护 <see cref="FocusManager"/> 交给 <c>focus-trap.js</c> 的那份契约。
/// 初始聚焦目标由 JS 决定，所以此处可测的是模式与显式元素是否原样送达。
/// 静默回归恰恰藏在那里：陷阱照样生效、照样锁住 Tab，
/// 却悄悄退回聚焦第一个可 tab 的子元素——这正是所报告的缺陷 #504，
/// 而且它不产生任何错误。
/// </summary>
public class FocusTrapInitialFocusTests
{
    [Fact]
    public async Task TrapFocusDefaultsToTheFirstTabbableDescendant()
    {
        var module = new RecordingModule();
        var manager = new FocusManager(new StubJsRuntime(module));

        await manager.TrapFocus(default);

        Assert.Equal("focusTrap.createFocusTrap", module.LastIdentifier);
        Assert.Equal("first", module.LastArgs![1]);
        Assert.Null(module.LastArgs[2]);
    }

    [Theory]
    [InlineData(FocusTrapInitialFocus.FirstFocusable, "first")]
    [InlineData(FocusTrapInitialFocus.Container, "container")]
    [InlineData(FocusTrapInitialFocus.None, "none")]
    public async Task TrapFocusSendsTheModeTheCallerAskedFor(
        FocusTrapInitialFocus initialFocus, string expected)
    {
        var module = new RecordingModule();
        var manager = new FocusManager(new StubJsRuntime(module));

        await manager.TrapFocus(default, initialFocus);

        Assert.Equal(expected, module.LastArgs![1]);
    }

    [Fact]
    public async Task TrapFocusForwardsAnExplicitElement()
    {
        var module = new RecordingModule();
        var manager = new FocusManager(new StubJsRuntime(module));
        var target = new ElementReference("the-target");

        await manager.TrapFocus(default, FocusTrapInitialFocus.Container, target);

        // 模式仍会随行：当显式元素最终不在容器内时，JS 会回落到它。
        // 因此这一路也须断言模式未被丢掉。
        Assert.Equal("container", module.LastArgs![1]);
        Assert.Equal(target, Assert.IsType<ElementReference>(module.LastArgs[2]));
    }

    /// <summary>只交出那一个模块；其余任何调用都是管理器不该发起的。</summary>
    private sealed class StubJsRuntime(RecordingModule module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != "import")
            {
                throw new InvalidOperationException(
                    $"FocusManager should only call 'import' on IJSRuntime, not '{identifier}'.");
            }

            return ValueTask.FromResult((TValue)(object)module);
        }
    }

    /// <summary>记录最后一次调用，好让测试断言真正抵达 JS 的参数。</summary>
    private sealed class RecordingModule : IJSObjectReference
    {
        public string? LastIdentifier { get; private set; }

        public object?[]? LastArgs { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            LastIdentifier = identifier;
            LastArgs = args;

            // 真实模块会返回的清理句柄。
            if (typeof(TValue) == typeof(IJSObjectReference))
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
