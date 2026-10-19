using System.Globalization;
using AtomBlazor;

namespace AtomBlazor.Demo.Services;

/// <summary>
/// 文档站侧的 <see cref="ILocalizer"/> 覆写（库面示例「模式 B」的落地）：
/// 中文态优先查 <see cref="DemoZhStrings"/> 点分键表，缺键回落 <see cref="DefaultLocalizer"/> 英文默认值；
/// 英文态完全走默认实现。仅影响界面外壳串，不改线程文化（宿主 en-US 钉定基线不动）。
/// </summary>
public sealed class CultureLocalizer(DemoCultureService culture) : DefaultLocalizer
{
    /// <inheritdoc />
    public override string this[string key] =>
        culture.IsChinese && DemoZhStrings.Table.TryGetValue(key, out var zh) ? zh : base[key];

    /// <inheritdoc />
    public override string this[string key, params object[] arguments] =>
        culture.IsChinese && DemoZhStrings.Table.TryGetValue(key, out var zh)
            ? string.Format(CultureInfo.CurrentCulture, zh, arguments)
            : base[key, arguments];
}
