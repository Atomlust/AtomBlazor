using Microsoft.Extensions.DependencyInjection;
using AtomBlazor.Demo.Services;
using AtomBlazor;

namespace AtomBlazor.Demo.Extensions;

/// <summary>
/// 文档站服务注册扩展（自源 Demo.Shared/Extensions，AddDemo→AddAtomBlazorDemo）。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册文档站所需的全部服务（Primitives + Components + 主题）。</summary>
    public static IServiceCollection AddAtomBlazorDemo(this IServiceCollection services)
    {
        // 注册全部 AtomBlazor 服务（Primitives + Components + Theme）
        services.AddAtomBlazorComponents(configureTheme: options =>
        {
            options.DefaultBaseColor = BaseColor.Zinc;
            options.DetectSystemPreference = true;
        });

        // 折叠菜单状态持久化服务
        services.AddScoped<CollapsibleStateService>();

        // 演示数据生成服务
        services.AddSingleton<MockDataService>();

        // 文档站显示语言（默认中文；SSR 宿主经中间件从 atom-lang cookie 初始化）
        services.AddScoped<DemoCultureService>();

        // 以 Scoped 覆写库的 ILocalizer 注册（后注册者生效）：中文态查词条表、缺键回落英文
        services.AddScoped<ILocalizer, CultureLocalizer>();

        return services;
    }
}
