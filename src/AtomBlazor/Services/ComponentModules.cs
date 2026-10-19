using AtomBlazor.Primitives.Services;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>
/// AtomBlazor 组件核心 bundle 的共享、带缓存的加载器——即几乎每个页面都会加载的那组模块。
/// </summary>
/// <remarks>
/// <para>
/// 路径只写一处，避免出现十份字符串字面量副本；版本查询串也只写一处——它阻止浏览器
/// （或它前面的 CDN）用旧副本应答新构建，缘由详见 <see cref="PrimitiveModules.ModuleUrl"/>。
/// </para>
/// <para>
/// 当前 只打包 theme 模块；sidebar、text-input、composition-guard 等其余核心模块
/// TODO：随对应组件交付时补入 bundle 并追加新鲜性校验。
/// </para>
/// <para>
/// 函数以 <c>命名空间.函数</c> 的点分标识符寻址，命名空间即模块文件名（去连字符）的
/// camelCase 形式：<c>sidebarInset.scrollToTop</c>、<c>theme.initialize</c>。
/// </para>
/// </remarks>
public static class ComponentModules
{
    /// <summary>
    /// 核心 bundle 的路径，即从 C# 导入时使用的值。
    /// </summary>
    public const string CorePath = "./_content/AtomBlazor/js/components-core.js";

    /// <summary>
    /// 真正被导入的 URL：在 <see cref="CorePath"/> 之后带上库版本作为查询串。
    /// </summary>
    // 对于程序集 informational version 未变的本地构建，以及已发布包的升级，
    // 都要同步修订入口与其依赖的 URL。
    public static string CoreUrl { get; } = JsModules.Versioned($"{CorePath}?assets=1", typeof(ComponentModules).Assembly);

    /// <summary>
    /// 取得指定运行时的共享核心 bundle，首次使用时才导入。
    /// </summary>
    /// <param name="jsRuntime">当前电路或应用的 JavaScript 运行时。</param>
    /// <returns>共享的、非拥有型的模块引用。</returns>
    public static Task<IJSObjectReference> GetCoreAsync(IJSRuntime jsRuntime)
        => JsModules.GetAsync(jsRuntime, CoreUrl);

    /// <summary>
    /// 不经等待地取得共享核心 bundle；尚未导入时返回 false。
    /// </summary>
    /// <param name="jsRuntime">当前电路或应用的 JavaScript 运行时。</param>
    /// <param name="module">已加载时，共享的非拥有型模块引用。</param>
    /// <returns>bundle 已加载且 <paramref name="module"/> 被赋值时为 <c>true</c>。</returns>
    public static bool TryGetCoreLoaded(IJSRuntime jsRuntime, out IJSObjectReference module)
        => JsModules.TryGetLoaded(jsRuntime, CoreUrl, out module);
}
