namespace AtomBlazor;

/// <summary>
/// 响应式导航组件的状态容器。
/// </summary>
public class ResponsiveNavState
{
    /// <summary>
    /// 移动端菜单是否打开。
    /// </summary>
    public bool OpenMobile { get; set; }

    /// <summary>
    /// 当前视口是否为移动端。
    /// </summary>
    public bool IsMobile { get; set; }
}

/// <summary>
/// 管理响应式导航状态的上下文。
/// 提供状态管理与响应式行为。
/// </summary>
public class ResponsiveNavContext
{
    private ResponsiveNavState _state = new();

    /// <summary>
    /// 获取当前导航状态。
    /// </summary>
    public ResponsiveNavState State => _state;

    /// <summary>
    /// 获取移动端菜单当前是否打开。
    /// </summary>
    public bool OpenMobile => _state.OpenMobile;

    /// <summary>
    /// 获取当前视口是否为移动端。
    /// </summary>
    public bool IsMobile => _state.IsMobile;

    /// <summary>
    /// 导航状态变化时触发的事件。
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// 切换移动端菜单的开/关状态。
    /// </summary>
    public void Toggle() =>
        SetOpenMobile(!_state.OpenMobile);

    /// <summary>
    /// 设置移动端菜单的打开状态。
    /// </summary>
    public void SetOpenMobile(bool open)
    {
        if (_state.OpenMobile != open)
        {
            _state.OpenMobile = open;
            OnStateChanged();
        }
    }

    /// <summary>
    /// 设置视口是否为移动端。
    /// 由 ResponsiveNavProvider 经 JS interop 调用。
    /// </summary>
    public void SetIsMobile(bool isMobile)
    {
        if (_state.IsMobile != isMobile)
        {
            _state.IsMobile = isMobile;
            // 切回桌面端时关闭移动端菜单
            if (!isMobile && _state.OpenMobile)
            {
                _state.OpenMobile = false;
            }
            OnStateChanged();
        }
    }

    private void OnStateChanged() =>
        StateChanged?.Invoke(this, EventArgs.Empty);
}
