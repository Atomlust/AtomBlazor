using Microsoft.JSInterop;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// AtomBlazor 基元 JavaScript bundle 的共享、带缓存的加载器。
/// </summary>
/// <remarks>
/// <para>
/// 在 Blazor Server 上，从 C# 发出的 <c>import(...)</c> 是一条发往浏览器、随后由服务端等待的
/// 指令，所以每个懒加载模块都要付出一次完整的电路往返。无论文件是否已在浏览器 HTTP 缓存里，
/// 这份开销都按模块、按页面加载计，并且随用户延迟放大，而与文件大小无关。
/// </para>
/// <para>
/// 因此所有基元模块都从单一 bundle 再导出，本类型按 <see cref="IJSRuntime"/> 缓存对它的唯一
/// 引用——Server 上即每电路一次，WebAssembly 上即每应用一次。第一个调用方付一次往返，此后
/// 任何组件中的后续调用方都免费拿到同一个引用。
/// </para>
/// <para>
/// 函数以 <c>命名空间.函数</c> 的点分标识符寻址，命名空间即模块文件名（去连字符）的 camelCase
/// 形式：
/// <code>
/// var module = await PrimitiveModules.GetAsync(JSRuntime);
/// await module.InvokeVoidAsync("elementUtils.scrollIntoView", elementId);
/// </code>
/// </para>
/// <para>
/// 返回的引用是非拥有型的：它的 <c>DisposeAsync</c> 什么都不做。否则一个组件释放它，就会拖垮
/// 电路上其余所有组件，而持有它的组件无从得知自己是否是最后一个。电路结束时由 Blazor 释放底层
/// 模块。
/// </para>
/// </remarks>
public static class PrimitiveModules
{
    /// <summary>
    /// bundle 的路径，即从 C# 导入时使用的值。
    /// </summary>
    public const string ModulePath = "./_content/AtomBlazor.Primitives/js/primitives/primitives.js";

    /// <summary>
    /// 真正被导入的 URL：在 <see cref="ModulePath"/> 之后带上库版本作为查询串。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 正是这个查询串阻止浏览器（或它前面的 CDN）把上周的 bundle 当作本周的 C# 的合法应答。
    /// .NET 8 的静态资源在 .js 上没有指纹，没有它的话，缓存里的旧 <c>primitives.js</c> 在缓存
    /// 有效期内都是合法响应——而一个早于某个导出重命名的 bundle，会在第一次调用时报
    /// <c>Could not find 'x.y'</c>；在 Blazor Server 上，这曾拖垮整个电路。
    /// </para>
    /// <para>
    /// 它只驱逐入口文件本身。入口文件所导入的模块由浏览器按相对路径取回、不带查询串，其中
    /// 过期的一份仍可能从缓存端出；bundle 在加载时自检这种情况，并抛出点明文件名的错误。
    /// </para>
    /// </remarks>
    // 资源版本号从 1 起算（本工程首次交付 bundle）。当 bundle 相对导入的某个模块的契约变化时
    // 递增此号——即使本地构建保留了相同的 assembly informational version，也能让入口 URL 翻新。
    public static string ModuleUrl { get; } = JsModules.Versioned($"{ModulePath}?assets=1", typeof(PrimitiveModules).Assembly);

    /// <summary>
    /// 取得指定运行时的共享 primitive bundle，首次使用时才导入。
    /// </summary>
    /// <param name="jsRuntime">当前电路或应用的 JavaScript 运行时。</param>
    /// <returns>共享的、非拥有型的模块引用。</returns>
    public static Task<IJSObjectReference> GetAsync(IJSRuntime jsRuntime)
        => JsModules.GetAsync(jsRuntime, ModuleUrl);

    /// <summary>
    /// 不经等待地取得共享 primitive bundle；尚未导入时返回 false。
    /// 供只能从同步过程发起 interop 的调用方使用。
    /// </summary>
    /// <param name="jsRuntime">当前电路或应用的 JavaScript 运行时。</param>
    /// <param name="module">已加载时，共享的非拥有型模块引用。</param>
    /// <returns>bundle 已加载且 <paramref name="module"/> 被赋值时为 <c>true</c>。</returns>
    public static bool TryGetLoaded(IJSRuntime jsRuntime, out IJSObjectReference module)
        => JsModules.TryGetLoaded(jsRuntime, ModuleUrl, out module);
}
