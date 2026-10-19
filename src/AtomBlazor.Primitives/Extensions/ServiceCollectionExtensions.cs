using Microsoft.Extensions.DependencyInjection;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor.Primitives.Extensions;

/// <summary>
/// 把 AtomBlazor.Primitives 的服务注册进依赖注入容器的扩展方法。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 向服务集合添加 AtomBlazor.Primitives 的基元服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configureOverlays">可选：配置浮层渲染选项
    ///（例如让整个应用改用原生 <c>&lt;dialog&gt;</c> 渲染）。</param>
    /// <returns>便于链式调用的服务集合。</returns>
    public static IServiceCollection AddAtomBlazorPrimitives(
        this IServiceCollection services,
        Action<OverlayRenderingOptions>? configureOverlays = null)
    {
        // 浮层渲染选项（全局默认策略）。注册为单例，保证解析到的默认值
        // 跨所有渲染模式作用域一致。
        //
        // 若已存在注册，则就地配置——这样先于 AddAtomBlazorComponents 调用本方法、
        // 或在其之后调用都同样有效。过去是追加第二个注册，而容器返回最后那一个，
        // 于是 AddAtomBlazorComponents——它自己也会以无配置方式调用本方法——
        // 只要跑得比调用方晚，就会悄悄把调用方的选项换回默认值。
        var overlayOptions = ExistingOverlayOptions(services);

        if (overlayOptions is null)
        {
            overlayOptions = new OverlayRenderingOptions();
            configureOverlays?.Invoke(overlayOptions);
            services.AddSingleton(overlayOptions);
        }
        else
        {
            configureOverlays?.Invoke(overlayOptions);
        }

        // 原生浮层服务（能力检测 + 原生 <dialog> 驱动）。
        services.AddScoped<INativeOverlayService, NativeOverlayService>();

        // PortalService 注册为 Scoped，供 Blazor Server 的用户隔离：
        // 每个用户会话得到自己的门户注册表。
        services.AddScoped<IPortalService, PortalService>();

        // FocusManager 注册为 Scoped（组件级状态）：焦点捕获与管理。
        services.AddScoped<IFocusManager, FocusManager>();

        // PositioningService 注册为 Scoped（组件级状态）：浮动定位服务。
        services.AddScoped<IPositioningService, PositioningService>();

        // DropdownManagerService 注册为 Scoped（每个用户会话内同时只开一个下拉）。
        services.AddScoped<DropdownManagerService>();

        // KeyboardShortcutService 注册为 Scoped（按用户会话的全局键盘快捷键）。
        services.AddScoped<IKeyboardShortcutService, KeyboardShortcutService>();

        return services;
    }

    /// <summary>
    /// 服务集合中已注册的 <see cref="OverlayRenderingOptions"/> 实例，若存在。
    /// </summary>
    private static OverlayRenderingOptions? ExistingOverlayOptions(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(OverlayRenderingOptions)
                && services[i].ImplementationInstance is OverlayRenderingOptions options)
            {
                return options;
            }
        }

        return null;
    }
}
