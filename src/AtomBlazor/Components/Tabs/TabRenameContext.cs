namespace AtomBlazor;

/// <summary>
/// 阅读者刚刚重命名的一个标签。
/// </summary>
/// <remarks>
/// 标签不会自行改名。存下新标签并交回一个改动过的集合，或置
/// <see cref="Cancel"/> 拒绝它——一个列表就是把"这个名字已被占用"的规则放在这里。
/// 被拒绝的重命名会重新打开编辑器，且已输入的内容仍在其中，
/// 于是名字可以被修正，而不必从头重打。
/// </remarks>
public sealed class TabRenameContext
{
    /// <summary>获取标识该标签的值。</summary>
    public required string Value { get; init; }

    /// <summary>获取编辑之前该标签所带的标签文本。</summary>
    public required string OldLabel { get; init; }

    /// <summary>获取阅读者输入的标签文本，已去除首尾空格。</summary>
    /// <remarks>绝不为空。什么都不填就提交的编辑被视为一次取消。</remarks>
    public required string NewLabel { get; init; }

    /// <summary>
    /// 获取或设置是否拒绝新标签并重新打开编辑器。
    /// </summary>
    public bool Cancel { get; set; }
}
