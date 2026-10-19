using AtomBlazor.Primitives.Services;
using AtomBlazor.Primitives;

using Microsoft.AspNetCore.Components;

using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>
/// Motion 家族共享的 JavaScript 派发通道：按电路缓存 motion.js 模块引用，
/// 把 <c>motion</c>/<c>height</c>/<c>indicator</c>/<c>play</c> 调用转发到浏览器。
/// </summary>
/// <remarks>
/// motion.js 走独立模块 URL（非 components-core.js barrel）：它并非每页必载的核心模块，
/// 与源形态一致，由 <see cref="JsModules"/> 缓存后每个调用点至多付一次电路往返。
/// 断线、取消与 disposed 竞态一律吞掉——动画是锦上添花，绝不该让渲染抛异常。
/// </remarks>
internal sealed class MotionInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? module;
    private ElementReference element;
    private bool disposed;

    /// <summary>
    /// 在指定元素上调用 motion.js 的导出函数，附带其余配置参数。
    /// </summary>
    internal async Task InvokeAsync(string method, ElementReference target, params object?[] options)
    {
        if (disposed) { return; }
        try
        {
            element = target;
            module ??= await JsModules.GetAsync(js, JsModules.Versioned("./_content/AtomBlazor/js/motion.js", typeof(MotionInterop).Assembly));
            if (disposed) { return; }
            await module.InvokeVoidAsync(method, new object?[] { target }.Concat(options).ToArray());
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException or InvalidOperationException) { }
    }

    /// <summary>
    /// 释放浏览器端动画状态；模块已不可达时静默返回。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (module is not null)
        {
            try { await module.InvokeVoidAsync("dispose", element); }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException) { }
        }
    }
}
