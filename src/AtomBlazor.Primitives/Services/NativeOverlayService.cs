using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// <see cref="INativeOverlayService"/> 的默认实现，由 native-dialog 模块支撑。
/// </summary>
public class NativeOverlayService : INativeOverlayService, IAsyncDisposable
{
    private readonly IJSRuntime jsRuntime;
    private readonly OverlayRenderingOptions options;
    private bool? dialogSupported;
    private bool nativeUnavailable;
    private bool disposed;


    public NativeOverlayService(IJSRuntime jsRuntime, OverlayRenderingOptions options)
    {
        this.jsRuntime = jsRuntime;
        this.options = options;
    }

    // bundle 在整个电路内共享，原先住在这里的缓存与锁现在归 PrimitiveModules 所有。
    // 本服务不得释放它取回的引用。
    private Task<IJSObjectReference> GetModuleAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, nameof(NativeOverlayService));
        return PrimitiveModules.GetAsync(jsRuntime);
    }

    /// <inheritdoc />
    public async Task<bool> IsDialogSupportedAsync()
    {
        // 只缓存肯定结果。false 可能只是 JS interop 暂不可用（预渲染/断线）时的
        // 瞬时失败；缓存它会让"浏览器不支持"的警告在其实支持的浏览器里永远响下去。
        if (dialogSupported == true)
        {
            return true;
        }

        try
        {
            var objectReference = await GetModuleAsync();
            // BL0016（.NET 11）：下面的 try/catch 正是该分析器要求的断线守卫
            var supported = await objectReference.InvokeAsync<bool>("nativeDialog.supportsNativeDialog");

            if (supported)
            {
                dialogSupported = true;
            }
            else
            {
                // 探测确实跑通了、浏览器也答了"不支持"，那就是定论而非瞬时 interop 失败：
                // 此后不再把任何请求解析为原生策略。
                nativeUnavailable = true;
            }

            return supported;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // JS interop 不可用（预渲染/断线）。暂时按不支持处理。
            return false;
        }
    }

    /// <inheritdoc />
    public void FallBackToJavaScript() => nativeUnavailable = true;

    /// <inheritdoc />
    public OverlayRenderingStrategy ResolveStrategy(OverlayRenderingStrategy? requested)
    {
        var strategy = requested ?? options.DefaultStrategy;

        return nativeUnavailable && strategy == OverlayRenderingStrategy.Native
            ? OverlayRenderingStrategy.JavaScript
            : strategy;
    }

    /// <inheritdoc />
    public async Task ShowDialogAsync(ElementReference element)
    {
        try
        {
            var objectReference = await GetModuleAsync();
            await objectReference.InvokeVoidAsync("nativeDialog.showModal", element);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // 预渲染/断线期间属预期情况。
        }
    }

    /// <inheritdoc />
    public async Task CloseDialogAsync(ElementReference element, string? returnValue = null)
    {
        try
        {
            var objectReference = await GetModuleAsync();
            await objectReference.InvokeVoidAsync("nativeDialog.closeDialog", element, returnValue);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // 预渲染/断线期间属预期情况。
        }
    }

    /// <inheritdoc />
    public async Task FocusDialogAsync(ElementReference element)
    {
        try
        {
            var objectReference = await GetModuleAsync();
            await objectReference.InvokeVoidAsync("nativeDialog.focusDialog", element);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // 预渲染/断线期间属预期情况。
        }
    }

    /// <inheritdoc />
    public async Task FocusTriggerAsync(ElementReference element)
    {
        try
        {
            var objectReference = await GetModuleAsync();
            await objectReference.InvokeVoidAsync("nativeDialog.focusElement", element);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // 预渲染/断线期间属预期情况。
        }
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> SetupDialogAsync(ElementReference element, object dotNetRef)
    {
        var objectReference = await GetModuleAsync();

        // BL0016（.NET 11）：此调用刻意不加守卫——setup 失败必须让组件感知并改用
        // JavaScript 策略；断线恢复的语义由调用方组件决定（与源项目一致）。
#pragma warning disable BL0016
        var cleanup = await objectReference.InvokeAsync<IJSObjectReference>("nativeDialog.setupDialog", element, dotNetRef);
#pragma warning restore BL0016
        return new NativeDialogHandle(cleanup);
    }

    private sealed class NativeDialogHandle : IAsyncDisposable
    {
        private readonly IJSObjectReference cleanup;

        public NativeDialogHandle(IJSObjectReference cleanup)
        {
            this.cleanup = cleanup;
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                await cleanup.InvokeVoidAsync("dispose");
                await cleanup.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 清理句柄可能已释放，或电路已断开。
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        disposed = true;

        // 无可释放之物。模块引用归 PrimitiveModules 所有，与电路上所有组件共享；
        // 电路结束时由 Blazor 释放它。
        await Task.CompletedTask;
    }

}
