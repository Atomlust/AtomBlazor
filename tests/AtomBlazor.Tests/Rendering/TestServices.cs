using AtomBlazor;
using AtomBlazor.Primitives.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace AtomBlazor.Tests.Rendering;

/// <summary>
/// 测试用最小服务装配助手。
/// <para>
/// 组件库通过 <c>AddAtomBlazorComponents</c> / <c>AddAtomBlazorPrimitives</c> 一次性注册全部服务，
/// 但多数渲染测试只需要一个能工作的 <see cref="IJSRuntime"/> 桩件；直接自建
/// <see cref="ServiceCollection"/> 时，组件必需的下列服务往往被漏掉，表现为
/// "There is no registered service of type ..." 异常，或 Portal 类组件静默不渲染内容。
/// </para>
/// <para>
/// 组件新增必需依赖时，请在此处补齐，避免 191 个各自建容器的测试文件逐个踩坑。
/// </para>
/// </summary>
internal static class TestServices
{
    /// <summary>
    /// 注册渲染测试所需的最小服务集：日志、本地化器，以及基元层完整服务。
    /// JS 运行时桩件由各测试自行注册（类型与生命周期因测试而异）。
    /// </summary>
    public static IServiceCollection AddAtomBlazorTestDefaults(this IServiceCollection services)
    {
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // 与 ServiceCollectionExtensions 中的注册保持一致：DefaultLocalizer 默认单例
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());

        // 基元层服务整体注册（Portal / NativeOverlay / Focus / Positioning 等）。
        // 手工逐个补注册极易漏项——漏掉的每一个都会让对应组件子树抛
        // InvalidOperationException，表现为快照里内容/遮罩/定位类整体消失。
        services.AddAtomBlazorPrimitives();

        return services;
    }

    /// <summary>
    /// 注册渲染测试所需的最小服务集，并一并注册给定的 JS 运行时桩件。
    /// </summary>
    public static IServiceCollection AddAtomBlazorTestDefaults(this IServiceCollection services, IJSRuntime jsRuntime)
    {
        services.AddSingleton(jsRuntime);
        return services.AddAtomBlazorTestDefaults();
    }
}
