using Microsoft.JSInterop;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 每个 <see cref="IJSRuntime"/> 只导入一次 JavaScript 模块，并把同一个引用交给所有调用方。
/// </summary>
/// <remarks>
/// <para>
/// 在 Blazor Server 上，从 C# 发出的 <c>import(...)</c> 是一条发往浏览器、随后由服务端等待的
/// 指令，因此它每次都要付出一次电路往返——即使浏览器自第一次导入起就把文件留在了模块注册表里。
/// 把引用缓存在实例字段里的组件，会<em>按实例</em>付出这份往返开销：一个有十三个文本输入框的
/// 表单把同一个文件导入了十三次，实测约等于页面加载时每个输入框多出四次客户端到服务器的消息。
/// </para>
/// <para>
/// 以运行时为键缓存，意味着 Server 上每电路一次、WebAssembly 上每应用一次。返回的引用是
/// 非拥有型的：它的 <c>DisposeAsync</c> 什么都不做——组件自我释放时，不能把模块从仍在使用它
/// 的组件手里拿走。电路结束时由 Blazor 释放真实模块。
/// </para>
/// </remarks>
public static class JsModules
{
    private static readonly ConditionalWeakTable<IJSRuntime, ConcurrentDictionary<string, Handle>> cache = new();

    /// <summary>
    /// 取得模块的共享引用，首次使用时才真正导入。
    /// </summary>
    /// <param name="jsRuntime">当前电路或应用的 JavaScript 运行时。</param>
    /// <param name="modulePath">模块路径，即传给 <c>import</c> 的值。</param>
    /// <returns>共享的、非拥有型的模块引用。</returns>
    public static Task<IJSObjectReference> GetAsync(IJSRuntime jsRuntime, string modulePath)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);

        var modules = cache.GetValue(jsRuntime, static _ => new ConcurrentDictionary<string, Handle>(StringComparer.Ordinal));
        return modules.GetOrAdd(modulePath, static _ => new Handle()).GetAsync(jsRuntime, modulePath);
    }

    /// <summary>
    /// 把程序集版本以 <c>v</c> 查询串附加到模块路径上，让新版本对应新 URL，
    /// 旧版本的缓存副本永远不能当作对它的合法应答。
    /// </summary>
    /// <param name="modulePath">模块路径，即传给 <c>import</c> 的值。</param>
    /// <param name="assembly">用于给 URL 盖版本戳的程序集。</param>
    /// <returns>附加了 <c>?v=&lt;version&gt;</c>（URL 编码）的路径。</returns>
    public static string Versioned(string modulePath, System.Reflection.Assembly assembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);
        ArgumentNullException.ThrowIfNull(assembly);

        // InformationalVersion 携带预发布标签，以及（MinVer 提供的）提交号——因此每次构建
        // 都是不同的 URL。取不到时退回始终存在的四段式版本号。
        var version = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = assembly.GetName().Version?.ToString() ?? "0";
        }

        var separator = modulePath.Contains('?') ? '&' : '?';
        return $"{modulePath}{separator}v={Uri.EscapeDataString(version)}";
    }

    /// <summary>
    /// 不经等待地取得已导入的模块；尚未加载时返回 false。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 供只能从同步过程发起 interop 的调用方使用——那里根本无从 await。浮层在
    /// <c>OnParametersSet</c> 里派发自己的打开动作，使该调用与承载内容的渲染批次同批送达；
    /// 若在那里等待导入，调用就会落后渲染一个往返，前面的设计就白费了。
    /// </para>
    /// <para>
    /// 返回 false 不是错误，只是"还没就绪"。调用方退回可等待的路径，为导入付出一次往返，
    /// 并为此后每次打开把本缓存焐热。
    /// </para>
    /// </remarks>
    /// <param name="jsRuntime">当前电路或应用的 JavaScript 运行时。</param>
    /// <param name="modulePath">模块路径，即传给 <c>import</c> 的值。</param>
    /// <param name="module">已加载时，共享的非拥有型模块引用。</param>
    /// <returns>模块已加载且 <paramref name="module"/> 被赋值时为 <c>true</c>。</returns>
    public static bool TryGetLoaded(IJSRuntime jsRuntime, string modulePath, out IJSObjectReference module)
    {
        module = null!;

        if (jsRuntime is null || string.IsNullOrWhiteSpace(modulePath))
        {
            return false;
        }

        if (!cache.TryGetValue(jsRuntime, out var modules) || !modules.TryGetValue(modulePath, out var handle))
        {
            return false;
        }

        var inFlight = handle.Loaded;
        if (inFlight is null || !inFlight.IsCompletedSuccessfully)
        {
            return false;
        }

        module = inFlight.Result;
        return true;
    }

    /// <summary>
    /// 把每一次调用转发给真实模块，但无视释放请求——组件自我销毁时，
    /// 不能把共享模块从仍在运行的组件那里夺走。
    /// </summary>
    private sealed class NonOwningReference : IJSObjectReference
    {
        private readonly IJSObjectReference inner;

        public NonOwningReference(IJSObjectReference inner)
        {
            this.inner = inner;
        }

        // .NET 11 分析器要求 TValue 注解与 IJSObjectReference 接口完全一致；
        // 转发垫片的调用守卫由使用方负责（BL0016 抑制）
#pragma warning disable BL0016
        public ValueTask<TValue> InvokeAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
            string identifier, object?[]? args)
            => inner.InvokeAsync<TValue>(identifier, args);

        public ValueTask<TValue> InvokeAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
            => inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
#pragma warning restore BL0016

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Handle
    {
        private readonly object sync = new();
        private Task<IJSObjectReference>? inFlight;

        /// <summary>导入任务，供"只能查看、不能等待"的调用方取用。</summary>
        public Task<IJSObjectReference>? Loaded
        {
            get
            {
                lock (sync)
                {
                    return inFlight;
                }
            }
        }

        public Task<IJSObjectReference> GetAsync(IJSRuntime jsRuntime, string modulePath)
        {
            lock (sync)
            {
                // 并发调用方共享同一个进行中的导入，因此无论多少组件同时开口，
                // 电路都只付一次往返。
                //
                // 失败的导入绝不保留。预渲染与正在断开的电路都会在这里失败，而两者都是
                // 应用能自行恢复的状态——把失败缓存下来，会让每个需要该模块的组件永久坏死。
                if (inFlight is null || (inFlight.IsCompleted && !inFlight.IsCompletedSuccessfully))
                {
                    inFlight = ImportAsync(jsRuntime, modulePath);
                }

                return inFlight;
            }
        }

        private static async Task<IJSObjectReference> ImportAsync(IJSRuntime jsRuntime, string modulePath)
        {
            // BL0016（.NET 11）要求 interop 调用带 try/catch 守卫：守卫属于消费方组件
            //（预渲染/断线在调用点可恢复），加载器本体只应把异常原样抛回调用方，
            // 由 Handle 决定失败不缓存。此处抑制该分析器，行为与源项目一致。
#pragma warning disable BL0016
            var imported = await jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath);
#pragma warning restore BL0016
            return new NonOwningReference(imported);
        }
    }
}
