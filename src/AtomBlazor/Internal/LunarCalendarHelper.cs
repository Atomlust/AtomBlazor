namespace AtomBlazor;

/// <summary>
/// 单个公历日期的农历换算结果。
/// </summary>
/// <param name="Year">农历年（公历年）。</param>
/// <param name="Month">名义月序（1–12，闰月返回其本体月序）。</param>
/// <param name="Day">农历日（1–30）。</param>
/// <param name="IsLeapMonth">该日是否落在闰月。</param>
/// <param name="MonthName">月名，闰月带「闰」前缀（如「闰六月」）。</param>
/// <param name="DayName">日名（初一…三十）。</param>
/// <param name="Festival">命中的节日名；无节日为 null。</param>
internal readonly record struct LunarInfo(
    int Year,
    int Month,
    int Day,
    bool IsLeapMonth,
    string MonthName,
    string DayName,
    string? Festival);

/// <summary>
/// 公历 → 农历换算与命名，基于 BCL <see cref="System.Globalization.ChineseLunisolarCalendar"/>。
/// </summary>
/// <remarks>
/// 覆盖范围即 BCL 支持范围（1901-02-19 ~ 2100-12-31，取自
/// <c>MinSupportedDateTime</c>/<c>MaxSupportedDateTime</c>）；越界日期经
/// <see cref="TryGetLunar"/> 返回 false，由调用方隐藏农历副标签。
/// 节日表只含日期固定的农历节日与公历节日；除夕无固定日期，按
/// 「腊月最后一天」动态判定。节气不在本实现范围（BCL 不提供，需独立数据表）。
/// </remarks>
internal static class LunarCalendarHelper
{
    private static readonly System.Globalization.ChineseLunisolarCalendar Calendar = new();

    private static readonly string[] MonthNames =
        ["正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "冬月", "腊月"];

    private static readonly string[] DayNames =
    [
        "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
        "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
        "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十"
    ];

    /// <summary>日期固定的农历节日，键 =（名义月 &lt;&lt; 8）| 日。除夕动态判定，不入此表。</summary>
    private static readonly Dictionary<int, string> LunarFestivals = new()
    {
        [(1 << 8) | 1] = "春节",
        [(1 << 8) | 15] = "元宵节",
        [(2 << 8) | 2] = "龙抬头",
        [(5 << 8) | 5] = "端午节",
        [(7 << 8) | 7] = "七夕节",
        [(7 << 8) | 15] = "中元节",
        [(8 << 8) | 15] = "中秋节",
        [(9 << 8) | 9] = "重阳节",
        [(12 << 8) | 8] = "腊八节",
    };

    /// <summary>公历节日，键 =（月 &lt;&lt; 8）| 日。</summary>
    private static readonly Dictionary<int, string> SolarFestivals = new()
    {
        [(1 << 8) | 1] = "元旦",
        [(5 << 8) | 1] = "劳动节",
        [(10 << 8) | 1] = "国庆节",
    };

    /// <summary>
    /// 换算指定公历日期的农历信息。
    /// </summary>
    /// <returns>超出 BCL 支持范围或换算失败时返回 false。</returns>
    public static bool TryGetLunar(DateTime date, out LunarInfo info)
    {
        info = default;
        if (date < Calendar.MinSupportedDateTime || date > Calendar.MaxSupportedDateTime)
        {
            return false;
        }

        try
        {
            var lunarYear = Calendar.GetYear(date);
            var lunarMonth = Calendar.GetMonth(date);
            var lunarDay = Calendar.GetDayOfMonth(date);

            // GetLeapMonth 返回闰月自身在 13 月序列里的 1 基序号（0 = 无闰月），
            // 例如 2025 闰六月返回 7、2023 闰二月返回 3：
            // 闰月自身以及其后所有月序都需减一才是名义月序。
            var leapMonth = Calendar.GetLeapMonth(lunarYear);
            var isLeap = leapMonth > 0 && lunarMonth == leapMonth;
            var nominalMonth = leapMonth > 0 && lunarMonth >= leapMonth ? lunarMonth - 1 : lunarMonth;

            var monthName = (isLeap ? "闰" : string.Empty) + MonthNames[nominalMonth - 1];
            var dayName = DayNames[lunarDay - 1];

            string? festival = null;
            if (!isLeap && LunarFestivals.TryGetValue((nominalMonth << 8) | lunarDay, out var lunarFestival))
            {
                festival = lunarFestival;
            }
            else if (!isLeap && nominalMonth == 12 && lunarDay == GetLastDayOfTwelfthMonth(lunarYear))
            {
                // 除夕 = 腊月最后一天；腊月可能有 29 或 30 天，故不能进静态表。
                festival = "除夕";
            }
            else if (SolarFestivals.TryGetValue((date.Month << 8) | date.Day, out var solarFestival))
            {
                festival = solarFestival;
            }

            info = new LunarInfo(lunarYear, nominalMonth, lunarDay, isLeap, monthName, dayName, festival);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // 个别 BCL 边界日期换算仍可能抛出（支持范围声明与实现存在细微出入）。
            return false;
        }
    }

    /// <summary>
    /// 取日格副标签：节日优先；无节日时初一显示月名、其余显示日名。
    /// </summary>
    public static string GetSubLabel(in LunarInfo info) =>
        info.Festival ?? (info.Day == 1 ? info.MonthName : info.DayName);

    /// <summary>农历某年腊月的天数（29 或 30），用于除夕判定。</summary>
    private static int GetLastDayOfTwelfthMonth(int lunarYear)
    {
        // 闰年有 13 个月：GetLeapMonth 返回闰月的 BCL 月序（2..13），
        // 名义月 12（腊月）的 BCL 月序在闰年为 13、平年为 12。
        var twelfthMonthIndex = Calendar.GetLeapMonth(lunarYear) > 0 ? 13 : 12;
        return Calendar.GetDaysInMonth(lunarYear, twelfthMonthIndex);
    }
}
