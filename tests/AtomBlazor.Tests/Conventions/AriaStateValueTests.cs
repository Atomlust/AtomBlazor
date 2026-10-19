using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// ARIA 状态属性的取值必须是字符串 <c>"true"</c> 或 <c>"false"</c>，绝不能是裸布尔值。
/// <para>
/// Blazor 把 <see cref="bool"/> 类型的属性值当作 HTML 布尔属性处理：<c>true</c> 渲染为属性名带空值，
/// 而 <c>false</c> 干脆把整个属性去掉。于是 <c>aria-expanded="@IsOpen"</c> 在打开时渲染成
/// <c>aria-expanded=""</c>、关闭时什么都不渲染——一个两种状态都不播报的控件。
/// 源码里的标记看起来是对的，无障碍树里却是错的，这正是它要以文本方式检查的原因。
/// </para>
/// </summary>
public partial class AriaStateValueTests
{
    /// <summary>
    /// 取值必须是字面字符串 "true" 或 "false" 的 ARIA 属性。三态成员
    /// （<c>aria-checked="mixed"</c>、<c>aria-current="page"</c>）也在其中：
    /// 它们可以取别的字符串，但绝不取布尔值。
    /// </summary>
    private static readonly string[] stateAttributes =
    [
        "aria-expanded", "aria-hidden", "aria-selected", "aria-checked", "aria-disabled",
        "aria-pressed", "aria-required", "aria-invalid", "aria-busy", "aria-readonly",
        "aria-multiselectable", "aria-multiline", "aria-modal", "aria-atomic", "aria-current",
    ];

    [Fact]
    public void AriaStateAttributesRenderAStringNotABoolean()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);
            var component = text + ReadCodeBehind(file.FullName);

            foreach (var (attribute, expression, index) in Bindings(text))
            {
                if (RendersAString(expression, component))
                {
                    continue;
                }

                var line = text.Take(index).Count(c => c == '\n') + 1;
                offenders.Add($"{SourceTree.RelativePath(file)}:{line} {attribute}=\"@{expression}\"");
            }
        }

        Assert.True(offenders.Count == 0,
            "这些 ARIA 状态属性绑定到了布尔值，于是为 true 时渲染出空值、为 false 时干脆消失。"
            + "请把状态显式写出来："
            + $"@(condition ? \"true\" : \"false\")。{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>标记中每一个 <c>aria-state="@expression"</c>，并把表达式解包。</summary>
    private static IEnumerable<(string Attribute, string Expression, int Index)> Bindings(string text)
    {
        foreach (var attribute in stateAttributes)
        {
            var needle = attribute + "=\"@";
            var at = text.IndexOf(needle, StringComparison.Ordinal);

            while (at >= 0)
            {
                var start = at + needle.Length;
                yield return (attribute, Expression(text, start), at);
                at = text.IndexOf(needle, start, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// 读取一个 Razor 表达式：要么是带括号的块——跟踪嵌套与字符串字面量，
    /// 使内部的 <c>? "true" : "false"</c> 不会提前终止它；要么是一个普通的成员访问。
    /// </summary>
    private static string Expression(string text, int start)
    {
        if (text[start] != '(')
        {
            // 成员访问或方法调用，在属性值结束处终止。
            // 里面不可能有字符串字面量，否则那个引号早就把属性值关掉了。
            var end = text.IndexOf('"', start);
            return end < 0 ? text[start..] : text[start..end];
        }

        var depth = 0;
        var inString = false;

        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                inString = !inString;
            }
            else if (!inString && text[i] == '(')
            {
                depth++;
            }
            else if (!inString && text[i] == ')' && --depth == 0)
            {
                return text[start..(i + 1)];
            }
        }

        return text[start..];
    }

    /// <summary>
    /// 表达式是否只可能产出字符串：它把状态写成字面量、显式转换，
    /// 或读取组件声明为 string 的成员。
    /// </summary>
    private static bool RendersAString(string expression, string component)
    {
        if (expression.Contains('"', StringComparison.Ordinal)
            || expression.Contains("ToString", StringComparison.Ordinal)
            || expression.Contains("Localizer", StringComparison.Ordinal))
        {
            return true;
        }

        var member = expression.TrimStart('(').Split('.')[^1].Trim(')', '?', ' ');

        return StringMember(member).IsMatch(component);
    }

    private static Regex StringMember(string member) =>
        new(@"\bstring\??\s+" + Regex.Escape(member) + @"\b", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static string ReadCodeBehind(string razorPath)
    {
        var behind = razorPath + ".cs";
        return File.Exists(behind) ? File.ReadAllText(behind) : string.Empty;
    }
}
