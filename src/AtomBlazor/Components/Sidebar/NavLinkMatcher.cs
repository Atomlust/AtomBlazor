using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace AtomBlazor;

/// <summary>
/// 复用 <see cref="NavLink"/> 使用的激活路由匹配，让侧边栏
/// 菜单按钮能够自行计算激活态并通过可靠的
/// <c>data-active</c> 属性暴露出来。算法刻意与
/// <see cref="NavLink"/> 保持一致，使 <c>Match</c> 参数行为完全符合用户预期。
/// </summary>
internal static class NavLinkMatcher
{
    /// <summary>
    /// 判断 <paramref name="href"/> 是否匹配当前位置，规则与
    /// <see cref="NavLink"/> 完全一致。
    /// </summary>
    /// <param name="navigationManager">用于把 <paramref name="href"/> 解析为绝对 URI。</param>
    /// <param name="currentUriAbsolute">作为匹配基准的当前绝对 URI。</param>
    /// <param name="href">链接目标。null 或空值永不匹配。</param>
    /// <param name="match">URL 的匹配方式。</param>
    public static bool IsMatch(
        NavigationManager navigationManager,
        string currentUriAbsolute,
        string? href,
        NavLinkMatch match)
    {
        if (string.IsNullOrEmpty(href))
        {
            return false;
        }

        var hrefAbsolute = navigationManager.ToAbsoluteUri(href).AbsoluteUri;

        if (EqualsHrefExactlyOrIfTrailingSlashAdded(currentUriAbsolute, hrefAbsolute))
        {
            return true;
        }

        return match == NavLinkMatch.Prefix
            && IsStrictlyPrefixWithSeparator(currentUriAbsolute, hrefAbsolute);
    }

    private static bool EqualsHrefExactlyOrIfTrailingSlashAdded(string currentUriAbsolute, string hrefAbsolute)
    {
        if (string.Equals(currentUriAbsolute, hrefAbsolute, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 特例：指向 "/path/" 的链接在 "/path"（无尾斜杠）处也激活，
        // 因为服务器通常对两者提供同一页面。
        if (currentUriAbsolute.Length == hrefAbsolute.Length - 1
            && hrefAbsolute[^1] == '/'
            && hrefAbsolute.StartsWith(currentUriAbsolute, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsStrictlyPrefixWithSeparator(string value, string prefix)
    {
        var prefixLength = prefix.Length;

        if (value.Length <= prefixLength)
        {
            return false;
        }

        // 仅当前缀末尾或紧随其后存在分隔符字符时才匹配：
        // "/abc" 是 "/abc/def" 的前缀，但不是 "/abcdef" 的前缀。
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && (prefixLength == 0
                || !IsUnreservedCharacter(prefix[prefixLength - 1])
                || !IsUnreservedCharacter(value[prefixLength]));
    }

    private static bool IsUnreservedCharacter(char c)
        => char.IsLetterOrDigit(c) || c is '-' or '.' or '_' or '~';
}
