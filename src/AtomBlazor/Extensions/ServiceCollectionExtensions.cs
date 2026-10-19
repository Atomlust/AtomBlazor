using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AtomBlazor.Primitives.Extensions;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 把 AtomBlazor.Components 的服务注册进依赖注入容器的扩展方法。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 向服务集合添加 AtomBlazor 的全部服务（Primitives + Components）。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configureLocalizer">可选：通过 <see cref="DefaultLocalizer.Set"/> 配置本地化键。</param>
    /// <param name="configureTheme">可选：通过 <see cref="ThemeOptions"/> 配置主题系统。</param>
    /// <param name="configureOverlays">可选：配置浮层渲染选项（例如让整个应用改用原生
    /// <c>&lt;dialog&gt;</c> 渲染）。等价于把同一个委托传给本方法内部调用的
    /// <c>AddAtomBlazorPrimitives</c>。</param>
    /// <returns>便于链式调用的服务集合。</returns>
    /// <remarks>
    /// <para>
    /// 本地化器默认经 <c>TryAddSingleton</c> 注册为单例。要启用动态文化切换
    /// （如 Blazor Server 的按电路本地化），请在调用本方法之前把自定义
    /// <see cref="ILocalizer"/> 实现注册为 Scoped。
    /// </para>
    /// <para>
    /// <see cref="ThemeService"/> 始终注册（Scoped）。用 <paramref name="configureTheme"/>
    /// 设置暗色模式、基础色、主色与持久化行为的默认值。
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAtomBlazorComponents(
        this IServiceCollection services,
        Action<DefaultLocalizer>? configureLocalizer = null,
        Action<ThemeOptions>? configureTheme = null,
        Action<OverlayRenderingOptions>? configureOverlays = null)
    {
        // 注册全部基元服务（portal、focus、positioning、dropdown manager、keyboard shortcuts）
        services.AddAtomBlazorPrimitives(configureOverlays);

        // 把 ToastService 注册为 Scoped，用于 Blazor Server 的用户隔离
        // 每个用户会话都拥有自己的 toast 通知状态
        services.AddScoped<ToastService>();

        // 注册 DialogService 为 Scoped（编程式确认/提示/自定义对话框，Blazor Server 按会话隔离）
        services.AddScoped<DialogService>();

        // ThemeService 注册为 Scoped（每个电路/会话拥有独立的主题状态）
        var themeOptions = new ThemeOptions();
        configureTheme?.Invoke(themeOptions);
        services.TryAddSingleton(themeOptions);
        services.TryAddScoped<ThemeService>();

        // 注册本地化器（默认单例；消费方可先把它注册为 Scoped 来替换）
        var localizer = new DefaultLocalizer();
        configureLocalizer?.Invoke(localizer);
        services.TryAddSingleton<ILocalizer>(localizer);

        return services;
    }
}
