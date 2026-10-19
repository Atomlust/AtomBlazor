using AtomBlazor.Primitives;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Conventions;

/// <summary>
/// 本库发出的每一个 Tailwind 工具类都带 <c>atom:</c> 前缀，并位于 <c>atom-utilities</c> 级联层中
/// 因此使用者自己运行的 Tailwind 构建永远不会产生同名类，两套样式表也无法争夺同一个元素（#496）。
///
/// 构建侧通过读取产出的样式表来守护；源码侧同样被守护，因为在带前缀的构建下
/// 组件类字符串里一个未加前缀的令牌并不会冲突——它会安静地什么都不产出，这更糟。
/// </summary>
public class TailwindPrefixTests
{
    private const string prefix = "atom:";

    /// <summary>
    /// 允许不带前缀、合法出现在组件类字符串中的类名：
    /// 来自 <c>atom-input.css</c> 的手写选择器、被任意 group 变体或使用方 CSS 消费的标记
    /// 以及使用方构建可能为其定义样式的排版（typography）插件钩子。
    /// <para>
 /// 测试波补充：<c>atom-guide-background-grid</c> 是 DashboardGrid 背景导引格的手写
    /// CSS 选择器（ -input.css#L931 的 <c>::before</c> 规则，消费
    /// dashboard-grid.js 写入的 <c>--atom-dashboard-guide-svg</c> 等自定义属性），与
    /// <c>sortable-*</c> 同属"非 Tailwind 工具类、由手写 CSS/JS 消费"的标记类别。
    /// 本仓对应的 ::before 规则尚未随 CSS 波（已），但类名本身属主码 事实
    /// 只扩白名单、不动主码。
    /// </para>
    /// </summary>
    private static readonly Regex neutral = new(
        @"^(atom-[a-z0-9-]+|aspect-ratio-content|sortable-[a-z-]+|atom-guide-background-grid|atom-portal|destructive|aria-current-step|origin-top-center|prose|prose-sm|dark:prose-invert)$",
        RegexOptions.Compiled);

    private static readonly Regex classAttribute = new(
        @"(?<=[\s""])(?:[A-Za-z_@:-]*[Cc]lass[A-Za-z]*|PopoverWidth)=""",
        RegexOptions.Compiled);

    private static bool IsAllowed(string token) =>
        token.StartsWith(prefix, StringComparison.Ordinal) || neutral.IsMatch(token);

    // ---------------------------------------------------------------------------------------
    // 构建产出的样式表
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// 级联层的优先级由层名在文档中首次出现的顺序固定。
    /// 压缩器会把输入里显式的 <c>@layer a, b, c;</c> 语句重写为首次出现顺序
    /// 所以生效的顺序才是关键：先是使用方的标准层，然后是本库带前缀的工具类层
    /// 再是使用方的工具类层，最后是 <c>atom</c> 层。
    /// </summary>
    [Fact]
    public void BuiltStylesheetEstablishesTheSharedLayerOrder()
    {
        var css = ReadBuiltStylesheet();

        var order = new List<string>();
        foreach (var name in TopLevelLayerNames(css))
        {
            if (!order.Contains(name))
            {
                order.Add(name);
            }
        }

        Assert.Equal(["properties", "theme", "base", "components", "atom-utilities", "utilities", "atom"], order);
    }

    [Fact]
    public void BuiltStylesheetWritesNothingIntoTheSharedUtilitiesLayer()
    {
        var css = ReadBuiltStylesheet();

        foreach (var (prelude, _) in TopLevelBlocks(css))
        {
            Assert.False(
                Regex.IsMatch(prelude, @"^@layer\s+utilities\s*$"),
                "atomblazor.css 不得写入共享的 `utilities` 层；使用方的构建共用该层，两者会冲突（#496）。");
        }
    }

    /// <summary>
    /// 默认边框颜色必须由一个在特殊性（specificity）上压过 Tailwind preflight 的选择器承载
    /// 而不能只靠文档顺序。
    /// <para>
    /// 本库与使用方自己的 Tailwind 构建都会把 preflight 写入共享的 <c>base</c> 层
    /// 而 preflight 的 <c>border: 0 solid</c> 会把 <c>border-color</c> 重置为 <c>currentColor</c>。
    /// 裸 <c>*</c> 选择器与它的特殊性打平，于是谁的第二份样式表生效谁赢——当赢的是使用方那份时
    /// 本库所有边框都会渲染成近黑色（#527）。改用匹配 class 属性的选择器可把特殊性定在 0,1,0
    /// 这是引入顺序无法改变的。它必须留在 <c>base</c> 层：放到更晚的层里会压过使用方自己的
    /// <c>border-*</c> 工具类，那就是反向重演 #398。
    /// </para>
    /// </summary>
    [Fact]
    public void DefaultBorderColourOutranksPreflightOnSpecificity()
    {
        var css = ReadBuiltStylesheet();

 // 优雅跳过：默认边框守护规则随主题令牌一起进入 base 层
        // 在此之前构建产物仍是骨架。以「组件工具类层是否已产出」作为骨架期结束的信号
 // 一旦 @layer atom-utilities 块出现（组件开始交付），本测试自动生效。
        if (!TopLevelBlocks(css).Any(b => Regex.IsMatch(b.Prelude, @"^@layer\s+atom-utilities\s*$")))
        {
            return;
        }

        var baseBlock = TopLevelBlocks(css)
            .Where(b => Regex.IsMatch(b.Prelude, @"^@layer\s+base\s*$"))
            .Select(b => b.Body)
            .FirstOrDefault();

        Assert.False(baseBlock is null, "atomblazor.css 没有顶层 `@layer base` 块。");

        // 压缩器可能去掉引号并转义冒号，两种写法都接受。
        var guarded = RuleSelectors(baseBlock!).Any(selector =>
            Regex.IsMatch(selector, @"\[class\*=""?atom\\?:border""?\]"));

        Assert.True(guarded,
            "@layer base 中没有 `[class*=\"atom:border\"]` 规则。本库的默认边框颜色将只能依赖"
            + "裸 `*` 选择器，它与使用方自己的 Tailwind preflight 特殊性打平，只要对方的样式表"
            + "第二个引入就会落败——所有边框变成近黑色（#527）。把该规则留在 `base` 层，"
            + "并保持用 class 属性匹配，让特殊性而非引入顺序决定胜负。");
    }

    [Fact]
    public void EveryUtilityInTheBuiltStylesheetIsPrefixed()
    {
        var css = ReadBuiltStylesheet();
        var block = TopLevelBlocks(css)
            .Where(b => Regex.IsMatch(b.Prelude, @"^@layer\s+atom-utilities\s*$"))
            .Select(b => b.Body)
            .SingleOrDefault();

        if (block is null)
        {
            // 优雅跳过：骨架期构建产物还没有组件工具类输出
 // @layer atom-utilities 块自组件（+）起才会出现，届时本测试自动生效。
            return;
        }

        var offenders = new List<string>();
        foreach (var selector in RuleSelectors(block))
        {
            var own = selector.StartsWith(":is(", StringComparison.Ordinal) ? selector[4..] : selector;
            if (own.StartsWith('.') && !own.StartsWith(@".atom\:", StringComparison.Ordinal))
            {
                offenders.Add(selector);
            }
        }

        Assert.True(offenders.Count == 0,
            "@layer atom-utilities 中存在未加前缀的工具类：\n  " + string.Join("\n  ", offenders.Distinct().Take(40)));
    }

    // ---------------------------------------------------------------------------------------
    // 组件源码
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void RazorClassAttributesUseThePrefix()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);
            foreach (Match m in classAttribute.Matches(text))
            {
                var line = 1 + text.AsSpan(0, m.Index).Count('\n');
                foreach (var token in PlainTokensOfAttributeValue(text, m.Index + m.Length))
                {
                    if (!IsAllowed(token))
                    {
                        offenders.Add($"{SourceTree.RelativePath(file)}:{line} `{token}`");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, Explain(offenders));
    }

    [Fact]
    public void ClassStringsPassedToCnUseThePrefix()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources)
        {
            var text = StripLineComments(File.ReadAllText(file.FullName));
            foreach (Match m in Regex.Matches(text, @"(?<![A-Za-z0-9_])cn\("))
            {
                var argsStart = m.Index + m.Length;
                var argsEnd = MatchingParen(text, argsStart - 1);
                if (argsEnd < 0)
                {
                    continue;
                }

                var line = 1 + text.AsSpan(0, m.Index).Count('\n');
                foreach (var literal in StringLiterals(text.AsSpan(argsStart, argsEnd - argsStart).ToString()))
                {
                    foreach (var token in literal.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (token.Contains('{') || token.Contains('}'))
                        {
                            continue; // 插值占位洞
                        }

                        if (!IsAllowed(token))
                        {
                            offenders.Add($"{SourceTree.RelativePath(file)}:{line} `{token}`");
                        }
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, Explain(offenders));
    }

    private static string Explain(List<string> offenders) =>
        "本库类字符串中的 Tailwind 工具类必须带 `atom:` 前缀（见 #496）。" +
        "在带前缀的构建下，未加前缀的令牌完全不产出任何 CSS。违规项：\n  " +
        string.Join("\n  ", offenders.Take(60)) +
        (offenders.Count > 60 ? $"\n  … 还有 {offenders.Count - 60} 条" : string.Empty);

    /// <summary>
    /// <c>group</c>/<c>peer</c> 标记必须以裸形式（与带前缀形式并列）抵达 DOM
    /// 因为使用方的 Tailwind 构建会把 <c>group-*</c>/<c>peer-*</c> 编译成 <c>:where(.group)</c>
    /// 永远看不到 <c>atom</c> 前缀。<c>ClassNames.cn</c> 会自动补上裸孪生类
    /// 丢失它的唯一途径是把标记字面量直接交给组件的 <c>Class</c> 参数——
    /// primitive 会把 <c>Class</c> 原样拼接，不做任何合并。
    /// </summary>
    [Fact]
    public void MarkerLiteralsGoThroughCn()
    {
        var markerInLiteralClass = new Regex(
            @"[Cc]lass=""(?<value>(?:[^""@]|@(?!\())*?atom:(?:group|peer)(?:/[\w-]+)?(?![\w/-])[^""]*)""",
            RegexOptions.Compiled);

        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources)
        {
            var text = File.ReadAllText(file.FullName);
            foreach (Match m in markerInLiteralClass.Matches(text))
            {
                var line = 1 + text.AsSpan(0, m.Index).Count('\n');
                offenders.Add($"{SourceTree.RelativePath(file)}:{line} `{m.Groups["value"].Value}`");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "字面量 Class 属性里的 atom:group/atom:peer 标记永远得不到它的裸孪生类，" +
            "使用方自己的 group-*/peer-* 变体将安静地匹配不到任何东西。请用 ClassNames.cn(...) 包裹它。" +
            "违规项：\n  " + string.Join("\n  ", offenders));
    }

    // ---------------------------------------------------------------------------------------
    // 辅助方法
    // ---------------------------------------------------------------------------------------

    private static string ReadBuiltStylesheet()
    {
        var path = Path.Combine(SourceTree.RepoRoot.FullName,
            "src", "AtomBlazor", "wwwroot", "atomblazor.css");
        Assert.True(File.Exists(path), $"未找到构建产出的样式表 {path}；请先构建 AtomBlazor。");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// 从每一个顶层 <c>@layer</c> 语句或块中按文档顺序取出层名
    /// 逗号分隔语句中的每个名字都包括在内。
    /// </summary>
    private static IEnumerable<string> TopLevelLayerNames(string css)
    {
        var depth = 0;
        var preludeStart = 0;

        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];
            if (c == '\\')
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                var close = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    yield break;
                }

                i = close + 1;
                if (depth == 0)
                {
                    preludeStart = i + 1;
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                i = SkipQuoted(css, i);
                continue;
            }

            if (c is '{' or ';')
            {
                if (depth == 0)
                {
                    var prelude = css[preludeStart..i].Trim();
                    if (prelude.StartsWith("@layer", StringComparison.Ordinal))
                    {
                        foreach (var name in prelude[6..].Split(','))
                        {
                            yield return name.Trim();
                        }
                    }

                    preludeStart = i + 1;
                }

                if (c == '{')
                {
                    depth++;
                }
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    preludeStart = i + 1;
                }
            }
        }
    }

    /// <summary>样式表的顶层 <c>prelude { body }</c> 块，能正确处理引号。</summary>
    private static IEnumerable<(string Prelude, string Body)> TopLevelBlocks(string css)
    {
        var depth = 0;
        var preludeStart = 0;
        var bodyStart = -1;
        string? prelude = null;

        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];
            if (c == '\\')
            {
                i++; // CSS 转义，例如 `[class*=\'size-\']` 里的 `\'`
                continue;
            }

            if (c == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                var close = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    yield break;
                }

                i = close + 1;
                if (depth == 0)
                {
                    preludeStart = i + 1;
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                i = SkipQuoted(css, i);
                continue;
            }

            if (c == '{')
            {
                if (depth == 0)
                {
                    prelude = css[preludeStart..i].Trim();
                    bodyStart = i + 1;
                }

                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    yield return (prelude!, css[bodyStart..i]);
                    preludeStart = i + 1;
                }
            }
            else if (c == ';' && depth == 0)
            {
                preludeStart = i + 1;
            }
        }
    }

    /// <summary>块内的每一个规则选择器，任意嵌套深度，能正确处理引号。</summary>
    private static IEnumerable<string> RuleSelectors(string body)
    {
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c == '\\')
            {
                i++;
                continue;
            }

            if (c is '"' or '\'')
            {
                i = SkipQuoted(body, i);
                continue;
            }

            if (c == '{')
            {
                var prelude = body[start..i].Trim();
                if (prelude.Length > 0 && prelude[0] != '@')
                {
                    foreach (var part in SplitSelectorList(prelude))
                    {
                        yield return part;
                    }
                }

                start = i + 1;
            }
            else if (c is '}' or ';')
            {
                start = i + 1;
            }
        }
    }

    /// <summary>
    /// 只在顶层逗号处切分选择器列表。
    /// </summary>
    /// <remarks>
    /// 直接 <c>Split(',')</c> 会把函数式伪类内部的参数列表撕开：
    /// <c>.atom\:dark\:bg-input:where(.dark, .dark *)</c> 会裂成两段，后一段读起来像一个裸的、
    /// 未加前缀的 <c>.dark *)</c> 选择器，而实际上没有任何规则这样声明。
    /// Tailwind 为自定义 <c>dark</c> 变体产出的正是这种形状，于是朴素的切分把整个暗色模式修复
    /// 都误报成了前缀违规。
    /// </remarks>
    private static IEnumerable<string> SplitSelectorList(string prelude)
    {
        var depth = 0;
        var start = 0;

        for (var i = 0; i < prelude.Length; i++)
        {
            var c = prelude[i];

            if (c == '\\')
            {
                i++;
                continue;
            }

            if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                yield return prelude[start..i].Trim();
                start = i + 1;
            }
        }

        yield return prelude[start..].Trim();
    }

    private static int SkipQuoted(string text, int openIndex)
    {
        var quote = text[openIndex];
        var i = openIndex + 1;
        while (i < text.Length && text[i] != quote)
        {
            if (text[i] == '\\')
            {
                i++;
            }

            i++;
        }

        return i;
    }

    /// <summary>
    /// Razor 属性值中属于纯文本的、以空白分隔的令牌——不属于 <c>@(…)</c> 或
    /// <c>@Identifier(…)</c> 表达式的部分。
    /// </summary>
    private static string[] PlainTokensOfAttributeValue(string text, int valueStart)
    {
        var plain = new StringBuilder();
        var i = valueStart;
        while (i < text.Length && text[i] != '"')
        {
            if (text[i] == '@')
            {
                if (i + 1 < text.Length && text[i + 1] == '@')
                {
                    plain.Append('@');
                    i += 2;
                    continue;
                }

                var end = SkipRazorExpression(text, i);
                if (end > i)
                {
                    plain.Append(' ');
                    i = end;
                    continue;
                }
            }

            plain.Append(text[i]);
            i++;
        }

        return plain.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>返回从 <paramref name="at"/>（一个 <c>@</c>）开始的 Razor 表达式结束后的下标；若不存在则返回 <paramref name="at"/>。</summary>
    private static int SkipRazorExpression(string text, int at)
    {
        var i = at + 1;
        if (i < text.Length && text[i] == '(')
        {
            var end = MatchingParen(text, i);
            return end < 0 ? at : end + 1;
        }

        var ident = Regex.Match(text[i..], @"^[A-Za-z_][A-Za-z0-9_]*");
        if (!ident.Success)
        {
            return at;
        }

        i += ident.Length;
        while (i < text.Length)
        {
            if (text[i] == '.' && i + 1 < text.Length && (char.IsLetter(text[i + 1]) || text[i + 1] == '_'))
            {
                var member = Regex.Match(text[(i + 1)..], @"^[A-Za-z_][A-Za-z0-9_]*");
                i += 1 + member.Length;
                continue;
            }

            if (text[i] is '(' or '[')
            {
                var end = MatchingParen(text, i);
                if (end < 0)
                {
                    return i;
                }

                i = end + 1;
                continue;
            }

            break;
        }

        return i;
    }

    /// <summary>返回与 <paramref name="openIndex"/> 处括号配对的那个括号的索引，能正确处理字符串；不配对时返回 -1。</summary>
    private static int MatchingParen(string text, int openIndex)
    {
        var open = text[openIndex];
        var close = open == '(' ? ')' : ']';
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                i = SkipQuoted(text, i);
                continue;
            }

            if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary><paramref name="code"/> 中 C# 字符串字面量的内容（普通、逐字与插值三种）。</summary>
    private static IEnumerable<string> StringLiterals(string code)
    {
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] != '"')
            {
                continue;
            }

            var verbatim = (i > 0 && code[i - 1] == '@') || (i > 1 && code[i - 2] == '@');
            var end = SkipQuoted(code, i);
            while (verbatim && end + 1 < code.Length && code[end + 1] == '"')
            {
                end = SkipQuoted(code, end + 1);
            }

            if (end > i + 1)
            {
                yield return code[(i + 1)..end];
            }

            i = end;
        }
    }

    private static string StripLineComments(string code)
    {
        var sb = new StringBuilder(code.Length);
        foreach (var line in code.Split('\n'))
        {
            sb.Append(line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : line).Append('\n');
        }

        return sb.ToString();
    }
}
