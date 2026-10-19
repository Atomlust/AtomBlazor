namespace AtomBlazor;

/// <summary>
/// 把 SidebarMenuButton 相关枚举转换为字符串表示的扩展方法。
/// </summary>
public static class SidebarMenuButtonExtensions
{
    /// <summary>
    /// 将 SidebarMenuButtonSize 转为小写字符串表示。
    /// </summary>
    public static string ToValue(this SidebarMenuButtonSize size) => size switch
    {
        SidebarMenuButtonSize.Small => "sm",
        SidebarMenuButtonSize.Default => "default",
        SidebarMenuButtonSize.Large => "lg",
        _ => "default"
    };

    /// <summary>
    /// 将 SidebarMenuButtonVariant 转为小写字符串表示。
    /// </summary>
    public static string ToValue(this SidebarMenuButtonVariant variant) => variant switch
    {
        SidebarMenuButtonVariant.Default => "default",
        SidebarMenuButtonVariant.Outline => "outline",
        _ => "default"
    };

    /// <summary>
    /// 将 SidebarMenuButtonElement 转为小写字符串表示。
    /// </summary>
    public static string ToValue(this SidebarMenuButtonElement element) => element switch
    {
        SidebarMenuButtonElement.Button => "button",
        SidebarMenuButtonElement.Anchor => "a",
        _ => "button"
    };

    /// <summary>
    /// 将 SidebarGroupLabelElement 转为小写字符串表示。
    /// </summary>
    public static string ToValue(this SidebarGroupLabelElement element) => element switch
    {
        SidebarGroupLabelElement.Div => "div",
        SidebarGroupLabelElement.Button => "button",
        _ => "div"
    };

    /// <summary>
    /// 将 SidebarMenuSubButtonSize 转为小写字符串表示。
    /// </summary>
    public static string ToValue(this SidebarMenuSubButtonSize size) => size switch
    {
        SidebarMenuSubButtonSize.Small => "sm",
        SidebarMenuSubButtonSize.Medium => "md",
        _ => "md"
    };
}
