using System.Collections;
using System.Text.RegularExpressions;
using TailwindMerge;

namespace AtomBlazor;

/// <summary>
/// 智能合并 CSS 类名的工具。等价于 shadcn 的 cn() 工具（clsx + tailwind-merge）。
/// </summary>
/// <example>
/// 基本用法：
/// <code>
/// ClassNames.cn("px-4", "py-2") // "px-4 py-2"
/// </code>
///
/// 条件类：
/// <code>
/// ClassNames.cn("btn", isActive &amp;&amp; "btn-active") // "btn btn-active" 或 "btn"
/// ClassNames.cn("btn", isActive ? "active" : "inactive") // "btn active" 或 "btn inactive"
/// </code>
///
/// Tailwind 冲突消解：
/// <code>
/// ClassNames.cn("px-4", "px-2") // "px-2"（后者胜出）
/// ClassNames.cn("p-4", "pr-10") // "p-4 pr-10"（长写细化简写）
/// ClassNames.cn("pr-10", "p-4") // "p-4"（简写覆盖在先的长写）
/// ClassNames.cn("md:p-4", "md:p-8") // "md:p-8"（冲突按变体隔离）
/// ClassNames.cn("text-red-500", "text-primary-foreground") // "text-primary-foreground"（支持语义色）
/// </code>
///
/// 库前缀。本库发出的每个工具类都带 <c>atom:</c> 前缀，永远不会与使用者的 Tailwind 构建冲突。
/// 使用者不带前缀的类仍能替换库同属性的带前缀类——合并时剥离前缀，存活的类名再戴回前缀：
/// <code>
/// ClassNames.cn("atom:p-4 atom:rounded-md", "p-6") // "p-6 atom:rounded-md"
/// ClassNames.cn("atom:p-4", "atom:p-8") // "atom:p-8"
/// </code>
///
/// 数组支持：
/// <code>
/// ClassNames.cn(new[] { "a", "b" }) // "a b"
/// </code>
///
/// 空值处理：
/// <code>
/// ClassNames.cn("a", null, "b") // "a b"
/// ClassNames.cn("a", false &amp;&amp; "b", "c") // "a c"
/// </code>
/// </example>
public static partial class ClassNames
{
    /// <summary>
    /// <c>atomblazor.css</c> 中每个工具类携带的变体前缀，使本库的 Tailwind 产物
    /// 永远不会与使用者自己的构建冲突（#496）。
    /// </summary>
    internal const string UtilityPrefix = "atom:";

    private static readonly char[] whitespaceSeparators = [' ', '\t', '\n', '\r'];
    private static readonly TwMerge twMerge = new();

    // 校验 CSS 类名只含安全字符——允许字母数字、连字符、下划线、冒号、斜杠、方括号、点、
    // 百分号与 CSS 组合字符，覆盖形如 "w-1/2"、"hover:bg-blue-500"、"data-[state=open]:block"、
    // "text-[14px]"、"[&>svg]:absolute" 的 Tailwind 类。
    [GeneratedRegex(@"^[a-zA-Z0-9_\-:/.[\]()%!@#&>+~=*,' ]+$")]
    private static partial Regex ValidClassNameRegex();

    /// <summary>
    /// 校验 CSS 类名仅包含安全字符，拒绝可能被用于 CSS 注入攻击的输入。
    /// </summary>
    private static bool IsValidClassName(string className)
    {
        if (string.IsNullOrWhiteSpace(className) || className.Length > 200)
        {
            return false;
        }

        // 检查潜在的危险模式
        if (className.Contains("expression", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("url(", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("import", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ValidClassNameRegex().IsMatch(className);
    }

    /// <summary>
    /// 智能合并多个类名，并消解 Tailwind 冲突。等价于 shadcn 的 cn() 工具。
    /// </summary>
    /// <param name="inputs">
    /// 类名可以是字符串、布尔值、数组或条件表达式。
    /// - 字符串原样纳入
    /// - true/false 布尔值被忽略（条件短路产生的 false 正由此被丢弃）
    /// - null 值被忽略
    /// - 数组与 IEnumerable 会被递归处理
    /// - 形如 (condition &amp;&amp; "class") 的条件表达式自然生效（false 被忽略）
    /// </param>
    /// <returns>合并后、Tailwind 冲突已消解的类字符串。</returns>
    /// <example>
    /// <code>
    /// cn("btn", isActive &amp;&amp; "active", "px-4", "px-2") // "btn active px-2"
    /// </code>
    /// </example>
    public static string cn(params object?[] inputs)
    {
        if (inputs == null || inputs.Length == 0)
        {
            return string.Empty;
        }

        var classes = new List<string>();

        foreach (var input in inputs)
        {
            ProcessInput(input, classes);
        }

        if (classes.Count == 0)
        {
            return string.Empty;
        }

        return WithBareMarkers(MergeWithPrefix(classes));
    }

    /// <summary>
    /// 在每个被合并保留的带前缀标记旁，携带其裸形态的 <c>group</c>/<c>peer</c> 标记。
    /// </summary>
    /// <remarks>
    /// Tailwind 依据字面标记类解析 <c>group-*</c> 与 <c>peer-*</c>，使用者自己的构建产出的是
    /// <c>:where(.group)</c> 与 <c>:where(.peer\/menu-button)</c>——它永远看不到 <c>atom</c>
    /// 前缀，也绝不会产出 <c>:where(.atom\:group)</c>。只发带前缀的标记，使用者的
    /// <c>group-data-[state=open]:rotate-180</c> 作用在库标记上就会安静地匹配不到任何东西。
    /// <para>
    /// 这段逻辑放在这里而不是各组件的类字符串里，因为裸 token 出现在 <c>cn(…)</c> 字面量中
    /// 正是 <c>TailwindPrefixTests</c> 约定所禁止的，而且合并过程会把它改写回带前缀形态。
    /// 标记不是工具类——不携带声明、也从不冲突——所以两种形态同时发出没有任何代价。
    /// </para>
    /// </remarks>
    private static string WithBareMarkers(string merged)
    {
        if (merged.Length == 0 || !merged.Contains(UtilityPrefix, StringComparison.Ordinal))
        {
            return merged;
        }

        List<string>? extra = null;
        foreach (var token in merged.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!token.StartsWith(UtilityPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var bare = token[UtilityPrefix.Length..];
            var name = bare.Split('/')[0];
            if (name is not ("group" or "peer"))
            {
                continue;
            }

            (extra ??= []).Add(bare);
        }

        return extra is null ? merged : string.Join(' ', extra) + " " + merged;
    }

    /// <summary>
    /// 把库的 <c>atom:</c> 带前缀 token 与使用者的无前缀 token 视作同一词汇表来消解 Tailwind
    /// 冲突。TailwindMerge 把前缀当作普通变体，若不处理，<c>atom:p-4 p-6</c> 会两者都保留，
    /// 使用者的 <c>Class="p-6"</c> 便再也替换不掉库的内边距。做法是剥离前缀、合并裸 token，
    /// 存活者按其在输入中最后一次出现的形态（带或不带前缀）还原。
    /// </summary>
    private static string MergeWithPrefix(List<string> classes)
    {
        var anyPrefixed = false;
        foreach (var c in classes)
        {
            if (c.StartsWith(UtilityPrefix, StringComparison.Ordinal))
            {
                anyPrefixed = true;
                break;
            }
        }

        if (!anyPrefixed)
        {
            return twMerge.Merge(string.Join(" ", classes)) ?? string.Empty;
        }

        var bare = new string[classes.Count];
        for (var i = 0; i < classes.Count; i++)
        {
            var c = classes[i];
            bare[i] = c.StartsWith(UtilityPrefix, StringComparison.Ordinal) ? c[UtilityPrefix.Length..] : c;
        }

        var merged = twMerge.Merge(string.Join(" ", bare));
        if (string.IsNullOrEmpty(merged))
        {
            return string.Empty;
        }

        // TailwindMerge 在两个冲突 token 中保留较晚的那个，所以裸 token 的最后一次出现
        // 决定了存活者属于库还是属于使用者。
        var survivors = merged.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < survivors.Length; i++)
        {
            var token = survivors[i];
            for (var j = bare.Length - 1; j >= 0; j--)
            {
                if (bare[j] == token)
                {
                    survivors[i] = classes[j];
                    break;
                }
            }
        }

        return string.Join(" ", survivors);
    }

    /// <summary>
    /// 递归处理输入值并提取类名。
    /// </summary>
    private static void ProcessInput(object? input, List<string> classes)
    {
        if (input == null)
        {
            return;
        }

        // 处理字符串（最常见的情形）
        if (input is string str)
        {
            if (!string.IsNullOrWhiteSpace(str))
            {
                // 按空白拆分，以处理多类名字符串
                var parts = str.Split(whitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (IsValidClassName(part))
                    {
                        classes.Add(part);
                    }
                }
            }
            return;
        }

        // 处理布尔值（来自条件表达式）
        // 注意：C# 中 `condition && "class"` 返回 "class" 或 false，而非 true/false；
        // 若调用方显式传入 true/false，则一律忽略。
        if (input is bool)
        {
            // 忽略独立的布尔值
            return;
        }

        // 处理数组与 IEnumerable（递归处理）
        if (input is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                ProcessInput(item, classes);
            }
            return;
        }

        // 其余类型按字符串转换处理（数字、枚举等）
        var strValue = input.ToString();
        if (!string.IsNullOrWhiteSpace(strValue) && IsValidClassName(strValue))
        {
            classes.Add(strValue);
        }
    }

    /// <summary>
    /// 条件为真时返回类字符串，否则返回 null。便于在 cn() 中书写条件类。
    /// </summary>
    /// <param name="condition">要判断的条件。</param>
    /// <param name="className">条件为真时返回的类字符串。</param>
    /// <returns>条件为真返回类字符串，否则返回 null。</returns>
    /// <example>
    /// <code>
    /// cn("btn", when(isActive, "btn-active"), "px-4")
    /// // 替代：
    /// cn("btn", isActive ? "btn-active" : null, "px-4")
    /// </code>
    /// </example>
    public static string? when(bool condition, string className) =>
        condition ? className : null;
}
