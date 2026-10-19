using System.Globalization;

namespace AtomBlazor;

/// <summary>
/// DateInput / TimeInput 共用的日期时间分段纯函数：分段序解析、数字解析与
/// 边界校验（随带编译依赖，位于 Components/Internal，同目录登记）。
/// </summary>
internal static class DateTimeSegments
{
    /// <summary>
    /// 依据文化的 ShortDatePattern 推导分段顺序（Day/Month/Year 子集）
    /// 模式不齐三段时回退美式 Month-Day-Year。跳过引号转义与反斜杠转义字面量。
    /// </summary>
    internal static string[] DateOrder(CultureInfo culture)
    {
        var result = new List<string>();
        var quote = '\0';
        var escaped = false;
        foreach (var character in culture.DateTimeFormat.ShortDatePattern)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (character == '\\')
            {
                escaped = true;
                continue;
            }
            if (quote != '\0')
            {
                if (character == quote) { quote = '\0'; }
                continue;
            }
            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }
            var part = character switch { 'd' => "Day", 'M' => "Month", 'y' => "Year", _ => null };
            if (part != null && !result.Contains(part)) { result.Add(part); }
        }
        return result.Count == 3 ? result.ToArray() : ["Month", "Day", "Year"];
    }

    /// <summary>不变文化解析纯数字文本；不可解析返回 null（前导零允许，负号与空格不允许）。</summary>
    internal static int? Number(string? text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>校验并合成分段为日期；分段不完整、非法或越出 min/max 界时返回 null。</summary>
    internal static DateTime? Date(string year, string month, string day, DateTime? min, DateTime? max)
    {
        if (year.Length != 4 || Number(year) is not int y || Number(month) is not int m || Number(day) is not int d
            || y is < 1 or > 9999 || m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, m))
        {
            return null;
        }
        var value = new DateTime(y, m, d);
        return (min.HasValue && value < min.Value.Date) || (max.HasValue && value > max.Value.Date) ? null : value;
    }

    /// <summary>校验并合成分段为当日时刻；12 小时制按 pm 折算，越出 min/max 界时返回 null。</summary>
    internal static TimeSpan? Time(string hour, string minute, string second, bool showSeconds, bool hour12, bool pm, TimeSpan? min, TimeSpan? max)
    {
        var h = Number(hour);
        var m = Number(minute);
        var s = showSeconds ? Number(second) : 0;
        if (h == null || m is null or < 0 or > 59 || s is null or < 0 or > 59
            || (hour12 ? h is < 1 or > 12 : h is < 0 or > 23))
        {
            return null;
        }
        var value = new TimeSpan(hour12 ? (h.Value % 12) + (pm ? 12 : 0) : h.Value, m.Value, s.Value);
        return (min.HasValue && value < min) || (max.HasValue && value > max) ? null : value;
    }
}
