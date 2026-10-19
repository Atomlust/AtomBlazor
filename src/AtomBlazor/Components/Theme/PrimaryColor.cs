namespace AtomBlazor;

/// <summary>
/// 主强调色，用于按钮、链接、焦点环与交互元素。
/// 每个取值对应一组独立的 OKLCH CSS 自定义属性，映射到 <c>--primary</c> 与 <c>--primary-foreground</c>。
/// </summary>
public enum PrimaryColor
{
    /// <summary>主色继承基础调色板（不做覆盖）。</summary>
    Default,

    /// <summary>蓝色主色——oklch 色相约 260。</summary>
    Blue,

    /// <summary>紫罗兰主色——oklch 色相约 280。</summary>
    Violet,

    /// <summary>紫色主色——oklch 色相约 290。</summary>
    Purple,

    /// <summary>玫瑰主色——oklch 色相约 350。</summary>
    Rose,

    /// <summary>红色主色——oklch 色相约 25。</summary>
    Red,

    /// <summary>橙色主色——oklch 色相约 45。</summary>
    Orange,

    /// <summary>琥珀主色——oklch 色相约 75。</summary>
    Amber,

    /// <summary>黄色主色——oklch 色相约 90。</summary>
    Yellow,

    /// <summary>青柠主色——oklch 色相约 125。</summary>
    Lime,

    /// <summary>绿色主色——oklch 色相约 145。</summary>
    Green,

    /// <summary>翡翠主色——oklch 色相约 160。</summary>
    Emerald,

    /// <summary>蓝绿主色——oklch 色相约 180。</summary>
    Teal,

    /// <summary>青色主色——oklch 色相约 200。</summary>
    Cyan,

    /// <summary>天蓝主色——oklch 色相约 220。</summary>
    Sky,

    /// <summary>靛蓝主色——oklch 色相约 270。</summary>
    Indigo,

    /// <summary>品红主色——oklch 色相约 320。</summary>
    Fuchsia,

    /// <summary>粉红主色——oklch 色相约 340。</summary>
    Pink
}
