namespace AtomBlazor;

/// <summary>
/// 表示侧边栏的变体样式。
/// </summary>
public enum SidebarVariant
{
    /// <summary>
    /// 默认侧边栏，推开内容。
    /// </summary>
    Sidebar,

    /// <summary>
    /// 浮动侧边栏，覆盖在内容之上。
    /// </summary>
    Floating,

    /// <summary>
    /// 内嵌侧边栏，带内边距。
    /// </summary>
    Inset
}

/// <summary>
/// 表示侧边栏出现的一侧。
/// </summary>
public enum SidebarSide
{
    /// <summary>
    /// 侧边栏出现在左侧。
    /// </summary>
    Left,

    /// <summary>
    /// 侧边栏出现在右侧。
    /// </summary>
    Right
}

/// <summary>
/// 侧边栏组件的状态容器。
/// </summary>
public class SidebarState
{
    /// <summary>
    /// 侧边栏在桌面端是否打开。
    /// </summary>
    public bool Open { get; set; } = true;

    /// <summary>
    /// 侧边栏在移动端是否打开。
    /// </summary>
    public bool OpenMobile { get; set; }

    /// <summary>
    /// 当前视口是否为移动端。
    /// </summary>
    public bool IsMobile { get; set; }

    /// <summary>
    /// 侧边栏的变体/样式。
    /// </summary>
    public SidebarVariant Variant { get; set; } = SidebarVariant.Sidebar;

    /// <summary>
    /// 侧边栏出现的一侧。
    /// </summary>
    public SidebarSide Side { get; set; } = SidebarSide.Left;
}

/// <summary>
/// 在所有侧边栏组件之间管理侧边栏状态的上下文。
/// 提供状态管理、响应式行为与键盘快捷键。
/// </summary>
public class SidebarContext
{
    private SidebarState _state = new();

    /// <summary>桌面端折叠呈现方式。</summary>
    public SidebarCollapsedMode CollapsedMode { get; private set; }

    /// <summary>更改折叠呈现方式并通知订阅者。</summary>
    public void SetCollapsedMode(SidebarCollapsedMode mode)
    {
        if (CollapsedMode == mode) { return; }
        CollapsedMode = mode;
        OnStateChanged();
    }


    /// <summary>
    /// 获取当前侧边栏状态。
    /// </summary>
    public SidebarState State => _state;

    /// <summary>
    /// 获取侧边栏当前是否打开（依视口取桌面端或移动端）。
    /// </summary>
    public bool IsOpen => _state.IsMobile ? _state.OpenMobile : _state.Open;

    /// <summary>
    /// 获取侧边栏在桌面端是否打开。
    /// </summary>
    public bool Open => _state.Open;

    /// <summary>
    /// 获取侧边栏在移动端是否打开。
    /// </summary>
    public bool OpenMobile => _state.OpenMobile;

    /// <summary>
    /// 获取当前视口是否为移动端。
    /// </summary>
    public bool IsMobile => _state.IsMobile;

    /// <summary>
    /// 获取侧边栏变体。
    /// </summary>
    public SidebarVariant Variant => _state.Variant;

    /// <summary>
    /// 获取侧边栏所在的一侧。
    /// </summary>
    public SidebarSide Side => _state.Side;

    /// <summary>
    /// 侧边栏状态变化时发出的事件。
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// 切换侧边栏开合状态。
    /// 移动端切换 OpenMobile，桌面端切换 Open。
    /// </summary>
    public void ToggleSidebar()
    {
        if (_state.IsMobile)
        {
            SetOpenMobile(!_state.OpenMobile);
        }
        else
        {
            SetOpen(!_state.Open);
        }
    }

    /// <summary>
    /// 设置桌面端打开状态。
    /// </summary>
    public void SetOpen(bool open)
    {
        if (_state.Open != open)
        {
            _state.Open = open;
            OnStateChanged();
        }
    }

    /// <summary>
    /// 设置移动端打开状态。
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
    /// 由 SidebarProvider 经 JS interop 调用。
    /// </summary>
    public void SetIsMobile(bool isMobile)
    {
        if (_state.IsMobile != isMobile)
        {
            _state.IsMobile = isMobile;
            OnStateChanged();
        }
    }

    /// <summary>
    /// 设置侧边栏变体。
    /// </summary>
    public void SetVariant(SidebarVariant variant)
    {
        if (_state.Variant != variant)
        {
            _state.Variant = variant;
            OnStateChanged();
        }
    }

    /// <summary>
    /// 设置侧边栏所在的一侧。
    /// </summary>
    public void SetSide(SidebarSide side)
    {
        if (_state.Side != side)
        {
            _state.Side = side;
            OnStateChanged();
        }
    }

    /// <summary>
    /// 以给定值初始化状态（通常来自 cookie 或默认值）。
    /// </summary>
    public void Initialize(bool? open = null, SidebarVariant? variant = null, SidebarSide? side = null)
    {
        var changed = false;

        if (open.HasValue && _state.Open != open.Value)
        {
            _state.Open = open.Value;
            changed = true;
        }

        if (variant.HasValue && _state.Variant != variant.Value)
        {
            _state.Variant = variant.Value;
            changed = true;
        }

        if (side.HasValue && _state.Side != side.Value)
        {
            _state.Side = side.Value;
            changed = true;
        }

        if (changed)
        {
            OnStateChanged();
        }
    }

    private void OnStateChanged() =>
        StateChanged?.Invoke(this, EventArgs.Empty);
}
