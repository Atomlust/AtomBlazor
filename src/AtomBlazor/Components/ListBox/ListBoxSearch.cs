namespace AtomBlazor;

/// <summary>
/// 列表搜索匹配规则的唯一权威定义。
/// </summary>
/// <remarks>
/// <c>PickList</c> 必须知道某个窗格的搜索留下了哪些可见选项，
/// 这样它的"移动全部"按钮只作用于这些可见项而非被隐藏的其余项。
/// 规则若存在两份拷贝就会各自漂移，症状是按钮悄悄移动了用户看不到的行。
/// </remarks>
internal static class ListBoxSearch
{
    public static bool Matches(string text, string? query) =>
        string.IsNullOrEmpty(query)
        || text.Contains(query, StringComparison.CurrentCultureIgnoreCase);
}
