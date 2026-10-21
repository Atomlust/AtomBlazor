using System.Globalization;
using System.Runtime.CompilerServices;

namespace AtomBlazor.Tests;

// 日历类快照按 zh-CN 记录（月份/星期名、日期格式）；Linux CI runner 默认 en-US
// 会让这些基线跨平台漂移，因此在测试程序集加载时固定区域性与本机基线一致。
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var culture = CultureInfo.GetCultureInfo("zh-CN");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
