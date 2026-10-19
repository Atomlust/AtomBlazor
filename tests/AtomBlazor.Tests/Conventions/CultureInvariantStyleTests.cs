using AtomBlazor.Primitives;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// 防止数值经由当前区域设置（culture）流入内联 <c>style</c> 属性。
/// <para>
/// Razor 用 <see cref="System.Globalization.CultureInfo.CurrentCulture"/> 渲染裸的 <c>@someDouble</c>，
/// 所以在用逗号作小数点的区域设置下，标记会变成 <c>left: 33,33%</c>。这是 CSS 语法错误，
/// 浏览器会安静地丢弃整条声明——没有异常、没有控制台警告，只有一个不再移动的元素。
/// </para>
/// <para>
/// 这个问题出现过两次：<c>ColorPicker</c>（#436）与 <c>RangeSlider</c>（#443），前后相隔两周。
/// 第二次是修完第一个之后手工横扫源码树才发现的——而这正是值得自动化的那类检查。
/// </para>
/// </summary>
public class CultureInvariantStyleTests
{
    /// <summary>
    /// 紧跟在 CSS 单位前面的插值——<c>@(x * 100)%</c>、<c>@Foo px</c>。
    /// 单位后面不允许再跟单词字符，否则 <c>@ItemContainerStyle</c> 会被匹配成 <c>@It</c> 加 <c>em</c> 单位。
    /// </summary>
    private const string unit = @"(?:%|px|deg|em|rem|vh|vw|fr)(?![\w-])";

    private static readonly Regex interpolation = new(
        @"@\((?<expr>[^()]*(?:\([^()]*\)[^()]*)*)\)" + unit + @"|@(?<expr2>[A-Za-z_][\w.]*)" + unit,
        RegexOptions.Compiled);

    [Fact]
    public void NumericValuesInStyleAttributesAreCultureInvariant()
    {
        var violations = new List<string>();

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var lines = File.ReadAllLines(file.FullName);

            for (var i = 0; i < lines.Length; i++)
            {
                // 只有内联样式要紧。同一个值出现在 class 属性或文本内容里不会被当作 CSS 解析，
                // 那里的逗号是无害的。
                if (!lines[i].Contains("style=", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in interpolation.Matches(lines[i]))
                {
                    var expression = match.Groups["expr"].Success
                        ? match.Groups["expr"].Value
                        : match.Groups["expr2"].Value;

                    if (expression.Contains("InvariantCulture", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    violations.Add(
                        $"{SourceTree.RelativePath(file)}:{i + 1}  ->  @{expression.Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    private static string BuildMessage(List<string> violations)
    {
        var message = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture,
                $"{violations.Count} 处数值经由当前区域设置进入了内联 style。")
            .AppendLine()
            .AppendLine("Razor 用 CurrentCulture 渲染裸 @value，于是逗号小数的区域设置")
            .AppendLine("会产出非法 CSS（left: 33,33%），浏览器一言不发地把它整条丢掉。")
            .AppendLine()
            .AppendLine("请改用 InvariantCulture 格式化：")
            .AppendLine("  style=\"left: @(pct.ToString(\"0.##\", CultureInfo.InvariantCulture))%\"")
            .AppendLine()
            .AppendLine("优先用 \"0.##\" 而不是默认的 \"G\" 格式：G 在小于 1e-5 时会切换成科学计数法，")
            .AppendLine("那是以另一条路径产出的非法 CSS。")
            .AppendLine();

        foreach (var violation in violations)
        {
            message.AppendLine("  " + violation);
        }

        return message.ToString();
    }
}
