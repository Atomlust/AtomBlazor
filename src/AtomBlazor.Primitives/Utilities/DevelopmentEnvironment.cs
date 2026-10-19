using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 检测宿主应用是否运行在 Development 环境，
/// 使那些只在开发应用期间有帮助的诊断在其余环境中保持静默。
/// </summary>
/// <remarks>
/// 环境抽象因渲染模式而异，且本包不引用其中任何一个类型：
/// Blazor Server（及任意泛型宿主）注册
/// <c>Microsoft.Extensions.Hosting.IHostEnvironment</c>，而 Blazor WebAssembly 注册
/// <c>Microsoft.AspNetCore.Components.WebAssembly.Hosting.IWebAssemblyHostEnvironment</c>。
/// 两者都按类型名在使用者自己的服务提供器上解析，而不是去依赖其中任何一个包。
/// 答案在进程生命周期内固定不变，因此只解析一次并缓存；之后的每次检查都是一次字段读取。
/// <para>
/// 当两个类型都找不到时——裁剪发布，或既未注册任一服务的宿主——结果为 <c>false</c>，
/// 诊断保持静默。这是安全的方向：漏掉一条开发警告只是不便，
/// 在生产环境记一条警告则是往别人的遥测数据里灌噪音。
/// </para>
/// </remarks>
internal static class DevelopmentEnvironment
{
    private const string developmentEnvironmentName = "Development";

    private const string hostEnvironmentTypeName =
        "Microsoft.Extensions.Hosting.IHostEnvironment, Microsoft.Extensions.Hosting.Abstractions";

    private const string webAssemblyHostEnvironmentTypeName =
        "Microsoft.AspNetCore.Components.WebAssembly.Hosting.IWebAssemblyHostEnvironment, Microsoft.AspNetCore.Components.WebAssembly";

    private static bool? cachedIsDevelopment;

    /// <summary>
    /// 获取或设置一个替代宿主答案的值，仅供测试使用。为 null 时向宿主询问。
    /// </summary>
    /// <remarks>
    /// 宿主的答案在进程内缓存，而测试宿主不注册任何环境，所以没有这个开关，
    /// 仅限开发环境的诊断就完全无法测试——而且无论哪个测试先问，
    /// 它的答案都会替之后所有测试定调。
    /// </remarks>
    internal static bool? OverrideForTests { get; set; }

    /// <summary>
    /// 当宿主应用的环境为 "Development" 时返回 true。
    /// </summary>
    /// <param name="services">应用的服务提供器。</param>
    public static bool IsDevelopment(IServiceProvider services)
    {
        if (OverrideForTests is { } forced)
        {
            return forced;
        }

        if (cachedIsDevelopment.HasValue)
        {
            return cachedIsDevelopment.Value;
        }

        var environmentName =
            ReadEnvironmentName(services, hostEnvironmentTypeName, "EnvironmentName")
            ?? ReadEnvironmentName(services, webAssemblyHostEnvironmentTypeName, "Environment");

        var isDevelopment = string.Equals(environmentName, developmentEnvironmentName, StringComparison.OrdinalIgnoreCase);
        cachedIsDevelopment = isDevelopment;
        return isDevelopment;
    }

    // 本方法按类型名探测本包未引用的可选宿主环境类型（见类型 remarks）；
    // 裁剪发布时 Type.GetType 返回 null，诊断保持静默，这是预期方向。
    [UnconditionalSuppressMessage("Trimming", "IL2057: 传给 Type.GetType 的参数值无法保证目标类型可用",
        Justification = "有意按程序限定名探测宿主环境类型；类型被裁剪时返回 null，诊断安全地保持静默。")]
    private static string? ReadEnvironmentName(IServiceProvider services, string assemblyQualifiedTypeName, string propertyName)
    {
        var environmentType = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
        if (environmentType is null)
        {
            return null;
        }

        var environment = services.GetService(environmentType);
        if (environment is null)
        {
            return null;
        }

        var property = environmentType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(environment) as string;
    }
}
