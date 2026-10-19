namespace AtomBlazor;

/// <summary>折叠期间桌面侧边栏的呈现方式。</summary>
public enum SidebarCollapsedMode
{
    /// <summary>保留标准图标导轨。</summary>
    Icon,
    /// <summary>隐藏侧边栏，改由独立的浮动 SidebarPillNav 呈现。</summary>
    Pill
}
