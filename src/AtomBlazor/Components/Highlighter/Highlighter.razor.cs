using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 标记字符串中与搜索词匹配的部分，让结果列表能够说明每一行为何命中。
/// </summary>
/// <remarks>
/// <para>
/// 命中的片段渲染为 <c>&lt;mark&gt;</c> 元素。浏览器和屏幕阅读器本来就把该元素理解为
/// "与搜索相关"，因此高亮并不只依赖颜色传达信息。
/// </para>
/// <para>
/// 文本始终以纯文本渲染、绝不作为标记输出：来自搜索框的词无法注入任何元素。
/// 匹配默认按序数、大小写不敏感进行，重叠的词会被合并为一段而非嵌套标记。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Highlighter Text="@row.Name" Query="@search" /&gt;
///
/// &lt;Highlighter Text="@row.Name" Queries="@terms" CaseSensitive="true" /&gt;
/// </code>
/// </example>
public partial class Highlighter : ComponentBase
{
    /// <summary>
    /// 获取或设置要渲染的文本。
    /// </summary>
    [Parameter, EditorRequired]
    public string? Text { get; set; }

    /// <summary>
    /// 获取或设置要标记的词。当设置了 <see cref="Queries"/> 时被忽略。
    /// </summary>
    [Parameter]
    public string? Query { get; set; }

    /// <summary>
    /// 获取或设置要标记的多个词。重叠的匹配会合并为一段。
    /// </summary>
    [Parameter]
    public IReadOnlyList<string>? Queries { get; set; }

    /// <summary>
    /// 获取或设置匹配是否区分大小写。默认为 <c>false</c>，这正是搜索框通常需要的行为。
    /// </summary>
    [Parameter]
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// 获取或设置词是否只在单词开头匹配。用它阻止一个短词标记到其他每个单词的中间片段上。
    /// </summary>
    [Parameter]
    public bool WholeWord { get; set; }

    /// <summary>
    /// 获取或设置为 <c>&lt;mark&gt;</c> 元素追加的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? MarkClass { get; set; }

    private string MarkCssClass => ClassNames.cn(
        "atom:rounded-sm atom:bg-primary/20 atom:px-0.5 atom:text-inherit",
        MarkClass);

    /// <summary>文本的一段，附带是否命中的标记。</summary>
    private readonly record struct Segment(string Text, bool IsMatch);

    private IEnumerable<Segment> Segments
    {
        get
        {
            if (string.IsNullOrEmpty(Text))
            {
                return [];
            }

            var terms = (Queries ?? (Query is null ? [] : [Query]))
                .Where(term => !string.IsNullOrEmpty(term))
                .ToList();

            return terms.Count == 0 ? [new Segment(Text, false)] : Build(Text, terms);
        }
    }

    private List<Segment> Build(string text, List<string> terms)
    {
        var comparison = CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        // 先收集所有匹配再合并，使两个重叠的词产出一段而不是嵌套的 mark。
        var ranges = new List<(int Start, int End)>();

        foreach (var term in terms)
        {
            var index = 0;

            while (index <= text.Length - term.Length)
            {
                var found = text.IndexOf(term, index, comparison);

                if (found < 0)
                {
                    break;
                }

                if (!WholeWord || IsWordStart(text, found))
                {
                    ranges.Add((found, found + term.Length));
                }

                index = found + 1;
            }
        }

        if (ranges.Count == 0)
        {
            return [new Segment(text, false)];
        }

        ranges.Sort((left, right) => left.Start.CompareTo(right.Start));

        var segments = new List<Segment>();
        var cursor = 0;
        var (start, end) = ranges[0];

        foreach (var (nextStart, nextEnd) in ranges.Skip(1))
        {
            if (nextStart <= end)
            {
                end = Math.Max(end, nextEnd);
                continue;
            }

            cursor = Emit(segments, text, cursor, start, end);
            (start, end) = (nextStart, nextEnd);
        }

        cursor = Emit(segments, text, cursor, start, end);

        if (cursor < text.Length)
        {
            segments.Add(new Segment(text[cursor..], false));
        }

        return segments;
    }

    private static int Emit(List<Segment> segments, string text, int cursor, int start, int end)
    {
        if (start > cursor)
        {
            segments.Add(new Segment(text[cursor..start], false));
        }

        segments.Add(new Segment(text[start..end], true));
        return end;
    }

    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity",
        Justification = "这是字符分类，不是字符串比较")]
    private static bool IsWordStart(string text, int index) =>
        index == 0 || !char.IsLetterOrDigit(text[index - 1]);
}
