namespace AtomBlazor.Primitives;

/// <summary>
/// 从 <see cref="MenubarPrimitive"/> 向下传递的共享上下文，用于协调各菜单之间打开/关闭状态
/// 以及水平方向的键盘导航。
/// </summary>
public class MenubarContext
{
    private readonly List<MenubarMenuContext> menus = new();
    private readonly Action stateChanged;

    /// <summary>
    /// 当前打开菜单的 ID；若所有菜单都已关闭则为 <c>null</c>。
    /// </summary>
    public string? ActiveMenu { get; private set; }

    /// <summary>
    /// 创建新的 <see cref="MenubarContext"/> 实例。
    /// </summary>
    /// <param name="stateChanged">活动菜单发生变化时调用的回调（触发重新渲染）。</param>
    public MenubarContext(Action stateChanged)
    {
        this.stateChanged = stateChanged;
    }

    /// <summary>
    /// 向此上下文注册一个菜单。
    /// </summary>
    public void RegisterMenu(MenubarMenuContext menu)
    {
        if (!menus.Contains(menu))
        {
            menus.Add(menu);
        }
    }

    /// <summary>
    /// 从此上下文注销一个菜单。
    /// </summary>
    public void UnregisterMenu(MenubarMenuContext menu) =>
        menus.Remove(menu);

    /// <summary>
    /// 设置活动（打开）菜单。传入 <c>null</c> 可关闭所有菜单。
    /// </summary>
    public void SetActiveMenu(string? menuId)
    {
        ActiveMenu = menuId;
        stateChanged();
    }

    /// <summary>
    /// 导航到 Menubar 中的下一个菜单（循环回绕）。
    /// </summary>
    public void NavigateToNextMenu()
    {
        if (menus.Count == 0 || ActiveMenu == null)
        {
            return;
        }

        var currentIndex = menus.FindIndex(m => m.MenuId == ActiveMenu);
        if (currentIndex == -1)
        {
            return;
        }

        var nextIndex = (currentIndex + 1) % menus.Count;
        menus[nextIndex].Open();
    }

    /// <summary>
    /// 导航到 Menubar 中的上一个菜单（循环回绕）。
    /// </summary>
    public void NavigateToPreviousMenu()
    {
        if (menus.Count == 0 || ActiveMenu == null)
        {
            return;
        }

        var currentIndex = menus.FindIndex(m => m.MenuId == ActiveMenu);
        if (currentIndex == -1)
        {
            return;
        }

        var prevIndex = currentIndex == 0 ? menus.Count - 1 : currentIndex - 1;
        menus[prevIndex].Open();
    }
}

/// <summary>
/// 每个菜单各自的上下文，保存菜单的标识并提供打开/关闭方法。
/// </summary>
public class MenubarMenuContext
{
    internal Func<Task>? FocusTrigger { get; set; }

    private readonly MenubarContext parent;

    /// <summary>
    /// 此菜单的唯一标识符。
    /// </summary>
    public string MenuId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 此菜单当前是否处于打开状态。
    /// </summary>
    public bool IsOpen => parent.ActiveMenu == MenuId;

    /// <summary>
    /// 创建新的 <see cref="MenubarMenuContext"/> 实例。
    /// </summary>
    public MenubarMenuContext(MenubarContext parent)
    {
        this.parent = parent;
    }

    /// <summary>打开此菜单（会关闭其他已打开的菜单）。</summary>
    public void Open() => parent.SetActiveMenu(MenuId);

    /// <summary>若此菜单当前处于打开状态则将其关闭。</summary>
    public void Close()
    {
        if (IsOpen)
        {
            parent.SetActiveMenu(null);
        }
    }

    /// <summary>切换此菜单的打开/关闭状态。</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }
}
