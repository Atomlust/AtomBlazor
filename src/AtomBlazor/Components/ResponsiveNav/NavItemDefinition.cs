using Microsoft.AspNetCore.Components.Routing;

namespace AtomBlazor;

/// <summary>
/// 定义配合 <see cref="ResponsiveNavItems"/> 使用的导航项。
/// 允许导航链接只声明一次，即可在桌面与移动两种上下文中渲染。
/// </summary>
public class NavItemDefinition
{
    /// <summary>
    /// 导航项的显示文本。
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 要导航到的 URL。为 null 时，该项配合 <see cref="Children"/> 充作分组标题。
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// 子导航项。桌面端渲染为下拉面板。
    /// 移动端渲染为分类标题下的缩进列表。
    /// </summary>
    public IReadOnlyList<NavItemDefinition>? Children { get; init; }

    /// <summary>
    /// 桌面下拉内容中显示的可选描述文本。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 用于激活态高亮的 URL 匹配方式。默认为 <see cref="NavLinkMatch.Prefix"/>。
    /// </summary>
    public NavLinkMatch Match { get; init; } = NavLinkMatch.Prefix;
}
