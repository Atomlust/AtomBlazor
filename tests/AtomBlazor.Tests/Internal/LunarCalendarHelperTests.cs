using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Internal;

/// <summary>
/// LunarCalendarHelper 农历换算测试：已知日期向量（春节/端午/中秋/除夕）、
/// 闰月语义（2025 闰六月、2023 闰二月）、BCL 支持范围守卫（Min/Max 边界内外）、
/// 副标签优先级（节日 &gt; 初一月名 &gt; 日名）。
/// </summary>
public class LunarCalendarHelperTests
{
    [Fact]
    public void 春节应为正月初一并命中节日()
    {
        // 2026 年春节 = 2 月 17 日
        var ok = LunarCalendarHelper.TryGetLunar(new DateTime(2026, 2, 17), out var info);

        Assert.True(ok);
        Assert.Equal(2026, info.Year);
        Assert.Equal(1, info.Month);
        Assert.Equal(1, info.Day);
        Assert.False(info.IsLeapMonth);
        Assert.Equal("正月", info.MonthName);
        Assert.Equal("初一", info.DayName);
        Assert.Equal("春节", info.Festival);
    }

    [Fact]
    public void 端午与中秋应命中农历节日()
    {
        // 2025 端午 = 5 月 31 日（五月初五）
        Assert.True(LunarCalendarHelper.TryGetLunar(new DateTime(2025, 5, 31), out var duanwu));
        Assert.Equal("端午节", duanwu.Festival);
        Assert.Equal(5, duanwu.Month);
        Assert.Equal(5, duanwu.Day);

        // 2025 中秋 = 10 月 6 日（八月十五）
        Assert.True(LunarCalendarHelper.TryGetLunar(new DateTime(2025, 10, 6), out var zhongqiu));
        Assert.Equal("中秋节", zhongqiu.Festival);
        Assert.Equal(8, zhongqiu.Month);
        Assert.Equal(15, zhongqiu.Day);
    }

    [Fact]
    public void 除夕应动态判定为腊月最后一天()
    {
        // 2026 春节前一日 = 2 月 16 日；该腊月只有 29 天，除夕日名为廿九而非三十
        Assert.True(LunarCalendarHelper.TryGetLunar(new DateTime(2026, 2, 16), out var info));
        Assert.Equal(12, info.Month);
        Assert.False(info.IsLeapMonth);
        Assert.Equal("廿九", info.DayName);
        Assert.Equal("除夕", info.Festival);
    }

    [Fact]
    public void 闰月应带闰前缀且名义月序正确()
    {
        // 2025 闰六月：闰六月初一 = 7 月 25 日
        Assert.True(LunarCalendarHelper.TryGetLunar(new DateTime(2025, 7, 25), out var runLiuYue));
        Assert.True(runLiuYue.IsLeapMonth);
        Assert.Equal(6, runLiuYue.Month);
        Assert.Equal("闰六月", runLiuYue.MonthName);
        Assert.Equal("初一", runLiuYue.DayName);

        // 2023 闰二月：闰二月初一 = 3 月 22 日
        Assert.True(LunarCalendarHelper.TryGetLunar(new DateTime(2023, 3, 22), out var runErYue));
        Assert.True(runErYue.IsLeapMonth);
        Assert.Equal(2, runErYue.Month);
        Assert.Equal("闰二月", runErYue.MonthName);
    }

    [Fact]
    public void 闰月内不应命中农历节日()
    {
        // 闰六月初一不得命中六月初一之后的任何固定节日（节日语义只在正月子月有效）
        Assert.True(LunarCalendarHelper.TryGetLunar(new DateTime(2025, 7, 25), out var info));
        Assert.Null(info.Festival);
    }

    [Fact]
    public void 超出BCL支持范围的日期应返回False()
    {
        var calendar = new System.Globalization.ChineseLunisolarCalendar();

        // Min/Max 支持端点内换算成功，端点外一天失败
        Assert.True(LunarCalendarHelper.TryGetLunar(calendar.MinSupportedDateTime, out _));
        Assert.False(LunarCalendarHelper.TryGetLunar(calendar.MinSupportedDateTime.AddDays(-1), out _));
        Assert.True(LunarCalendarHelper.TryGetLunar(calendar.MaxSupportedDateTime, out _));
        Assert.False(LunarCalendarHelper.TryGetLunar(calendar.MaxSupportedDateTime.AddDays(1), out _));
    }

    [Fact]
    public void 副标签应按节日优先于初一月名再日名()
    {
        // 有节日 → 节日
        var withFestival = new LunarInfo(2026, 1, 1, false, "正月", "初一", "春节");
        Assert.Equal("春节", LunarCalendarHelper.GetSubLabel(withFestival));

        // 无节日的初一 → 月名
        var firstDay = new LunarInfo(2026, 2, 1, false, "二月", "初一", null);
        Assert.Equal("二月", LunarCalendarHelper.GetSubLabel(firstDay));

        // 其余日 → 日名
        var ordinaryDay = new LunarInfo(2026, 2, 15, false, "二月", "十五", null);
        Assert.Equal("十五", LunarCalendarHelper.GetSubLabel(ordinaryDay));

        // 闰月初一 → 闰月名
        var leapFirstDay = new LunarInfo(2025, 6, 1, true, "闰六月", "初一", null);
        Assert.Equal("闰六月", LunarCalendarHelper.GetSubLabel(leapFirstDay));
    }
}
