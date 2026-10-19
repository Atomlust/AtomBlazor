namespace AtomBlazor.Primitives;

/// <summary>
/// 用于支持键盘导航的 DropdownMenu 菜单项接口。
/// </summary>
public interface IDropdownMenuItem
{
    /// <summary>
    /// 获取菜单项是否被禁用。
    /// </summary>
    public bool Disabled { get; }

    /// <summary>
    /// 使该菜单项获得焦点。
    /// </summary>
    public Task FocusAsync();

    /// <summary>
    /// 以编程方式触发该菜单项的点击。
    /// </summary>
    public Task ClickAsync();
}
