using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 基于 Floating UI 库的定位服务实现。
/// </summary>
public class PositioningService : IPositioningService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="PositioningService"/> 类的新实例。
    /// </summary>
    /// <param name="jsRuntime">用于调用定位函数的 JavaScript 运行时。</param>
    public PositioningService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    // bundle 在整个电路内共享，原先住在这里的缓存与锁现在归 PrimitiveModules 所有。
    // 本服务不得释放它取回的引用。
    private Task<IJSObjectReference> GetModuleAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(PositioningService));
        return PrimitiveModules.GetAsync(_jsRuntime);
    }

    /// <inheritdoc />
    public async Task<PositionResult> ComputePositionAsync(
        ElementReference reference,
        ElementReference floating,
        PositioningOptions? options = null)
    {
        var module = await GetModuleAsync();
        options ??= new PositioningOptions();

        var jsOptions = new
        {
            placement = options.Placement,
            offset = options.Offset,
            flip = options.Flip,
            shift = options.Shift,
            padding = options.Padding,
            strategy = options.Strategy.ToValue(),
            matchReferenceWidth = options.MatchReferenceWidth
        };

        // BL0016（.NET 11）：断线/预渲染守卫留在调用方组件（与源项目各浮层组件的
        // try/catch 一致），服务只把强类型调用转发到 bundle。
#pragma warning disable BL0016
        var result = await module.InvokeAsync<JsonElement>(
            "positioning.computePosition", reference, floating, jsOptions);
#pragma warning restore BL0016

        return new PositionResult
        {
            X = result.GetProperty("x").GetDouble(),
            Y = result.GetProperty("y").GetDouble(),
            Placement = result.GetProperty("placement").GetString() ?? options.Placement,
            TransformOrigin = result.TryGetProperty("transformOrigin", out var origin)
                ? origin.GetString()
                : null,
            Strategy = result.TryGetProperty("strategy", out var strategy)
                ? strategy.GetString() ?? options.Strategy.ToValue()
                : options.Strategy.ToValue()
        };
    }

    /// <inheritdoc />
    public async Task ApplyPositionAsync(ElementReference floating, PositionResult position, bool makeVisible = false)
    {
        var module = await GetModuleAsync();
#pragma warning disable BL0016 // 同上：守卫在调用方组件，服务只做转发
        await module.InvokeVoidAsync("positioning.applyPosition", floating, position, makeVisible);
#pragma warning restore BL0016
    }

    /// <inheritdoc />
    public async Task HidePositionAsync(ElementReference floating)
    {
        var module = await GetModuleAsync();
#pragma warning disable BL0016 // 同上：守卫在调用方组件，服务只做转发
        await module.InvokeVoidAsync("positioning.hidePosition", floating);
#pragma warning restore BL0016
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> AutoUpdateAsync(
        ElementReference reference,
        ElementReference floating,
        PositioningOptions? options = null)
    {
        var module = await GetModuleAsync();
        options ??= new PositioningOptions();

        var jsOptions = new
        {
            placement = options.Placement,
            offset = options.Offset,
            flip = options.Flip,
            shift = options.Shift,
            padding = options.Padding,
            strategy = options.Strategy.ToValue(),
            matchReferenceWidth = options.MatchReferenceWidth
        };

#pragma warning disable BL0016 // 同上：守卫在调用方组件，服务只做转发
        var cleanup = await module.InvokeAsync<IJSObjectReference>(
            "positioning.autoUpdate", reference, floating, jsOptions);
#pragma warning restore BL0016

        return new AutoUpdateHandle(cleanup);
    }

    /// <summary>
    /// 释放定位服务。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        _disposed = true;

        // 无物可释放。模块引用属于 PrimitiveModules，与电路上所有其他组件共享；
        // 电路结束时由 Blazor 释放它。
        await Task.CompletedTask;
    }

    private sealed class AutoUpdateHandle : IAsyncDisposable
    {
        private readonly IJSObjectReference _cleanup;

        public AutoUpdateHandle(IJSObjectReference cleanup)
        {
            _cleanup = cleanup;
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                // 句柄作用域上的 apply 不带命名空间：它是 JS 返回对象的方法，
                // 不属于 bundle 导出。
                await _cleanup.InvokeVoidAsync("apply");
            }
#pragma warning disable CA1031 // 保留源项目的兜底捕获：断线/释放路径众多，清理失败应保持静默。
            catch
            {
                // 清理句柄可能已被释放。
            }
#pragma warning restore CA1031
        }
    }
}
