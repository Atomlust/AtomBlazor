using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 使用 JavaScript interop 的焦点管理服务实现。
/// </summary>
public class FocusManager : IFocusManager, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="FocusManager"/> 类的新实例。
    /// </summary>
    /// <param name="jsRuntime">用于调用焦点管理函数的 JavaScript 运行时。</param>
    public FocusManager(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    // bundle 在整个电路内共享，原先住在这里的缓存与锁现在归 PrimitiveModules 所有。
    // 本服务不得释放它取回的引用。
    private Task<IJSObjectReference> GetModuleAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(FocusManager));
        return PrimitiveModules.GetAsync(_jsRuntime);
    }

    /// <inheritdoc />
    public Task<IAsyncDisposable> TrapFocus(ElementReference container)
        => TrapFocus(container, FocusTrapInitialFocus.FirstFocusable);

    /// <inheritdoc />
    public async Task<IAsyncDisposable> TrapFocus(
        ElementReference container,
        FocusTrapInitialFocus initialFocus,
        ElementReference? initialFocusElement = null)
    {
        var module = await GetModuleAsync();

        // BL0016（.NET 11）：断线/预渲染守卫留在调用方组件（与源项目各浮层组件的
        // try/catch 形态一致），服务本体只做忠实转发。
#pragma warning disable BL0016
        var cleanupFunction = await module.InvokeAsync<IJSObjectReference>(
            "focusTrap.createFocusTrap", container, ToJsMode(initialFocus), initialFocusElement);
#pragma warning restore BL0016
        return new FocusTrapHandle(cleanupFunction);
    }

    private static string ToJsMode(FocusTrapInitialFocus initialFocus) => initialFocus switch
    {
        FocusTrapInitialFocus.Container => "container",
        FocusTrapInitialFocus.None => "none",
        _ => "first",
    };

    /// <inheritdoc />
    public async Task RestoreFocus(ElementReference? previousElement)
    {
        if (previousElement.HasValue)
        {
            try
            {
                // 使用 Blazor 内建的 FocusAsync 而非 eval，出于安全考虑
                await previousElement.Value.FocusAsync();
            }
            catch
            {
                // 元素可能已不存在，忽略
            }
        }
    }

    /// <inheritdoc />
    public async Task FocusFirst(ElementReference container)
    {
        var module = await GetModuleAsync();
        // BL0016（.NET 11）：守卫在调用方组件，服务只做转发
#pragma warning disable BL0016
        await module.InvokeVoidAsync("focusTrap.focusFirst", container);
#pragma warning restore BL0016
    }

    /// <inheritdoc />
    public async Task FocusLast(ElementReference container)
    {
        var module = await GetModuleAsync();
        // BL0016（.NET 11）：守卫在调用方组件，服务只做转发
#pragma warning disable BL0016
        await module.InvokeVoidAsync("focusTrap.focusLast", container);
#pragma warning restore BL0016
    }

    /// <summary>
    /// 释放焦点管理器，归还 JavaScript 模块资源。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        _disposed = true;

        // 无可释放之物。模块引用归 PrimitiveModules 所有，与电路上所有组件共享；
        // 电路结束时由 Blazor 释放它。
        await Task.CompletedTask;
    }

    private sealed class FocusTrapHandle : IAsyncDisposable
    {
        private readonly IJSObjectReference _cleanupFunction;

        public FocusTrapHandle(IJSObjectReference cleanupFunction)
        {
            _cleanupFunction = cleanupFunction;
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                await _cleanupFunction.InvokeVoidAsync("apply");
            }
            catch
            {
                // 清理函数可能已被释放
            }
        }
    }
}
