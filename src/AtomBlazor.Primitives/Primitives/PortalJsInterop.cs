using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor.Primitives;

/// <summary>
/// portal.js（primitive bundle 中的 <c>portal</c> 命名空间）的类型化 interop 门面。
/// </summary>
/// <remarks>
/// <para>
/// 试点：把散落在各组件里的 <c>module.InvokeAsync</c> 魔法字符串收敛为
/// 编译期可查名的方法。模块引用一律经 <see cref="PrimitiveModules"/> 的按电路缓存取得，
/// 行为与源项目逐调用点一致；标识符仍按 bundle 约定写成 portal.函数名（不带引号，
/// 以免被 PrimitiveJsModuleTests 的源码扫描误判为真实调用点），
/// 由该测试与 portal.js 的实际导出逐一核对。
/// </para>
/// <para>
/// portal.js 其余导出（ensurePortalContainer、setupPortal、getComputedZIndex、
/// isElementInViewport、triggerAutofocus、redispatchContextMenu）由 portal.js 自身与 JS 侧消费：
/// 本仓 C# 走 <see cref="PortalService"/> 的内存登记加
/// <c>PortalHost</c> 渲染，不经 interop，故历轮复核后依旧无 C# 调用点、不预置包装。
/// </para>
/// </remarks>
public static class PortalJsInterop
{
    /// <summary>
    /// 确保 primitive bundle（含 portal.js 的自动聚焦观察者等模块级副作用）已导入。
    /// 复用 <see cref="PrimitiveModules"/> 的缓存：首个调用付一次电路往返，其后免费。
    /// </summary>
    /// <param name="jsRuntime">当前电路或应用的 JavaScript 运行时。</param>
    /// <returns>共享的、非拥有型的 bundle 引用。</returns>
    public static Task<IJSObjectReference> EnsureLoadedAsync(IJSRuntime jsRuntime) =>
        PrimitiveModules.GetAsync(jsRuntime);

    /// <summary>
    /// 锁定 body 滚动（引用计数，模态浮层用）。嵌套浮层共享同一把底层锁，
    /// 最后一把锁释放时页面才恢复原有滚动状态。
    /// </summary>
    /// <param name="jsRuntime">当前电路或应用的 JavaScript 运行时。</param>
    /// <returns>JS 侧的解锁句柄，交给 <see cref="UnlockBodyScrollAsync"/> 释放。</returns>
    public static async ValueTask<IJSObjectReference> LockBodyScrollAsync(IJSRuntime jsRuntime)
    {
        // BL0016（.NET 11）：断线/预渲染守卫留在调用方组件（与源项目 Dialog/Sheet 的
        // try/catch 一致），门面只负责把强类型调用转发到 bundle。
#pragma warning disable BL0016
        var module = await PrimitiveModules.GetAsync(jsRuntime);
        return await module.InvokeAsync<IJSObjectReference>("portal.lockBodyScroll");
#pragma warning restore BL0016
    }

    /// <summary>
    /// 释放 <see cref="LockBodyScrollAsync"/> 取得的一次滚动锁。
    /// JS 侧对同一句柄的重复释放有防重入保护（如关闭+释放两条路径）。
    /// </summary>
    /// <param name="cleanupHandle"><see cref="LockBodyScrollAsync"/> 返回的句柄。</param>
    public static ValueTask UnlockBodyScrollAsync(IJSObjectReference cleanupHandle) =>
        // 句柄作用域上的 "apply" 不带命名空间：它是 JS 返回对象的方法，不属于 bundle 导出。
#pragma warning disable BL0016
        cleanupHandle.InvokeVoidAsync("apply");
#pragma warning restore BL0016
}
