namespace AtomBlazor;

/// <summary>
/// 用于支持键盘导航的菜单栏项接口。
/// </summary>
public interface IMenubarItem
{
    /// <summary>
    /// 获取菜单项是否被禁用。
    /// </summary>
    public bool IsDisabled { get; }

    /// <summary>
    /// 聚焦此菜单项。
    /// </summary>
    public Task FocusAsync();
}
