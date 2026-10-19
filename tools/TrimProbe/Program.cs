using Ganss.Xss;
using Markdig;
using TailwindMerge;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

// 裁剪/AOT 冒烟入口：让链接器真实走过各库代码路径，输出警告清单。
// 各调用对应组件库中的实际用法（Markdown 渲染、HTML 清洗、类名合并、日历循环规则）。
var markdown = Markdown.ToHtml("# 标题\n\n**加粗** 与 `代码`");
Console.WriteLine($"Markdig OK: {markdown.Length} chars");

var sanitizer = new HtmlSanitizer();
var clean = sanitizer.Sanitize("<p onclick=\"evil()\">内容</p>");
Console.WriteLine($"HtmlSanitizer OK: {clean}");

var twMerge = new TwMerge();
Console.WriteLine($"TailwindMerge.NET OK: {twMerge.Merge("p-2 p-4")}");

var calendar = new Calendar();
var evt = new CalendarEvent
{
    Summary = "团队会议",
    Start = new CalDateTime(new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc)),
    End = new CalDateTime(new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc))
};
// 冒烟工程有意使用被标注过时的多规则 API，以覆盖日历循环规则的裁剪路径
#pragma warning disable CS0618
evt.RecurrenceRules.Add(new RecurrencePattern(FrequencyType.Daily, 7) { Count = 3 });
calendar.Events.Add(evt);
#pragma warning restore CS0618
var occurrences = evt.GetOccurrences(evt.Start, new Ical.Net.Evaluation.EvaluationOptions { MaxUnmatchedIncrementsLimit = 1000 });
Console.WriteLine($"Ical.Net OK: {occurrences.Count()} occurrences");
