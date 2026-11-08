namespace AtomBlazor.Tests;

/// <summary>
/// 固定时间源：让依赖"今天"的组件渲染获得确定性，消灭随日历漂移的测试。
/// </summary>
/// <remarks>
/// 组件经 <c>Clock</c> 参数（<see cref="TimeProvider"/>）消费"今天"。
/// 固定点选 2026-10-15 12:00 UTC——正午保证任何时区换算后日期仍是 15 日。
/// </remarks>
internal sealed class FixedTimeProvider(DateTimeOffset fixedNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => fixedNow;

    /// <summary>2026 年 10 月（与日期选择器测试写死的期望区间一致）。</summary>
    public static FixedTimeProvider October2026 { get; } =
        new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
}
