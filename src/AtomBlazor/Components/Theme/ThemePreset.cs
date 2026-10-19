namespace AtomBlazor;

/// <summary>带前缀的 Atom 工具类的间距刻度。</summary>
public enum ThemeDensity
{
    /// <summary>标准组件间距。</summary>
    Standard,
    /// <summary>紧凑的应用间距。</summary>
    Compact,
    /// <summary>面向数据的密集间距。</summary>
    Dense,
    /// <summary>更宽松的组件间距。</summary>
    Spacious
}

/// <summary>字体栈。具名字体必须由应用自行提供。</summary>
public enum ThemeFont
{
    /// <summary>平台的 UI 字体。</summary>
    System,
    /// <summary>Inter，带回退到系统字体。</summary>
    Inter,
    /// <summary>Geist，带回退到系统字体。</summary>
    Geist,
    /// <summary>Roboto，带回退到系统字体。</summary>
    Roboto,
    /// <summary>Nunito Sans，带回退到系统字体。</summary>
    NunitoSans,
    /// <summary>平台的衬线字体。</summary>
    Serif,
    /// <summary>平台的等宽字体。</summary>
    Mono
}

/// <summary>卡片与菜单的表面处理方式。</summary>
public enum ThemeSurface
{
    /// <summary>标准阴影与不透明背景。</summary>
    Standard,
    /// <summary>无阴影表面。</summary>
    Flat,
    /// <summary>更强的阴影。</summary>
    Elevated,
    /// <summary>半透明菜单表面，带背景模糊。</summary>
    Glass
}

/// <summary>菜单表面的调色板。</summary>
public enum ThemeMenuColor
{
    /// <summary>标准浮层调色板。</summary>
    Default,
    /// <summary>柔和（muted）调色板。</summary>
    Muted,
    /// <summary>主色（primary）调色板。</summary>
    Primary,
    /// <summary>前景与背景反转。</summary>
    Inverse
}

/// <summary>菜单的焦点与悬停处理。</summary>
public enum ThemeMenuAccent
{
    /// <summary>微妙的前景色调。</summary>
    Subtle,
    /// <summary>主色调色板。</summary>
    Primary
}

/// <summary>可全局应用、也可应用到 <see cref="ThemeScope"/> 的外观设置。</summary>
public sealed record ThemeDesign
{
    /// <summary>组件间距密度。</summary>
    public ThemeDensity Density { get; init; }
    /// <summary>字体栈；字体永远不会被自动下载。</summary>
    public ThemeFont Font { get; init; }
    /// <summary>卡片与菜单表面处理方式。</summary>
    public ThemeSurface Surface { get; init; }
    /// <summary>菜单背景与文字颜色。</summary>
    public ThemeMenuColor MenuColor { get; init; }
    /// <summary>菜单焦点与悬停颜色。</summary>
    public ThemeMenuAccent MenuAccent { get; init; }

    /// <summary>校验各枚举取值均为已定义成员，否则抛出 <see cref="ArgumentOutOfRangeException"/>。</summary>
    internal void Validate()
    {
        if (!Enum.IsDefined(Density) || !Enum.IsDefined(Font) || !Enum.IsDefined(Surface)
            || !Enum.IsDefined(MenuColor) || !Enum.IsDefined(MenuAccent))
        {
            throw new ArgumentOutOfRangeException(nameof(ThemeDesign), "主题设计的枚举值必须是已定义成员。");
        }
    }

    /// <summary>转换为发往 JavaScript 的小写取值形状。</summary>
    internal object ToJs() => new
    {
        density = Density.ToString().ToLowerInvariant(),
        font = Font.ToString().ToLowerInvariant(),
        surface = Surface.ToString().ToLowerInvariant(),
        menuColor = MenuColor.ToString().ToLowerInvariant(),
        menuAccent = MenuAccent.ToString().ToLowerInvariant()
    };
}

/// <summary>完整、不可变的主题配置。</summary>
public sealed record ThemePreset
{
    /// <summary>暗色模式是否启用。</summary>
    public bool DarkMode { get; init; }
    /// <summary>中性调色板。</summary>
    public BaseColor BaseColor { get; init; } = BaseColor.Zinc;
    /// <summary>主色调色板。</summary>
    public PrimaryColor PrimaryColor { get; init; }
    /// <summary>圆角半径（rem）。</summary>
    public double Radius { get; init; } = 0.5;
    /// <summary>组件外观配置。</summary>
    public ThemeDesign Design { get; init; } = new();

    /// <summary>校验颜色与半径的合法范围，否则抛出 <see cref="ArgumentOutOfRangeException"/>。</summary>
    internal void Validate()
    {
        if (!Enum.IsDefined(BaseColor) || !Enum.IsDefined(PrimaryColor) || !double.IsFinite(Radius) || Radius < 0 || Radius > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(ThemePreset), "颜色必须是已定义的枚举成员，半径必须在 0 到 4 rem 之间。");
        }
        ArgumentNullException.ThrowIfNull(Design);
        Design.Validate();
    }
}

/// <summary>七个起始预设，可用 record 的 with 表达式定制。</summary>
public static class ThemePresets
{
    /// <summary>库的默认外观。</summary>
    public static ThemePreset Standard { get; } = new();
    /// <summary>均衡的专业外观。</summary>
    public static ThemePreset Balanced { get; } = new() { Radius = 0.375, Design = new() { Font = ThemeFont.Inter } };
    /// <summary>紧凑仪表盘间距。</summary>
    public static ThemePreset Compact { get; } = new() { Radius = 0.375, Design = new() { Density = ThemeDensity.Compact, Surface = ThemeSurface.Flat } };
    /// <summary>宽松的圆角控件。</summary>
    public static ThemePreset Spacious { get; } = new() { Radius = 0.875, Design = new() { Density = ThemeDensity.Spacious, Font = ThemeFont.NunitoSans } };
    /// <summary>面向开发者工具的直角控件。</summary>
    public static ThemePreset Sharp { get; } = new() { Radius = 0, Design = new() { Font = ThemeFont.Mono, Surface = ThemeSurface.Flat } };
    /// <summary>面向数据的密集间距。</summary>
    public static ThemePreset Dense { get; } = new() { Radius = 0.25, Design = new() { Density = ThemeDensity.Dense, Surface = ThemeSurface.Flat } };
    /// <summary>半透明菜单表面。</summary>
    public static ThemePreset Glass { get; } = new() { Radius = 0.75, Design = new() { Surface = ThemeSurface.Glass, Font = ThemeFont.Geist } };
}
