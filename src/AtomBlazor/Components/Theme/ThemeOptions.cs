namespace AtomBlazor;

/// <summary>
/// 主题系统配置选项。
/// </summary>
public class ThemeOptions
{
    /// <summary>可选的聚合默认值。提供时优先于各单项 Default 属性。</summary>
    public ThemePreset? DefaultPreset { get; set; }

    /// <summary>
    /// 默认基础（灰阶）调色板。默认为 <see cref="BaseColor.Zinc"/>。
    /// </summary>
    public BaseColor DefaultBaseColor { get; set; } = BaseColor.Zinc;

    /// <summary>
    /// 默认主强调色。默认为 <see cref="PrimaryColor.Default"/>（继承基础调色板）。
    /// </summary>
    public PrimaryColor DefaultPrimaryColor { get; set; } = PrimaryColor.Default;

    /// <summary>
    /// 是否默认启用暗色模式。默认为 <c>false</c>。
    /// 当 <see cref="DetectSystemPreference"/> 为 <c>true</c> 时，系统偏好优先于此值。
    /// </summary>
    public bool DefaultDarkMode { get; set; }

    /// <summary>
    /// 为 <c>true</c> 时，首次加载探测操作系统的配色偏好。
    /// 在没有已保存偏好时覆盖 <see cref="DefaultDarkMode"/>。默认为 <c>true</c>。
    /// </summary>
    public bool DetectSystemPreference { get; set; } = true;

    /// <summary>
    /// 默认圆角半径（rem）。默认为 <c>0.5</c>（与 shadcn/ui 默认一致）。
    /// 常用值：0、0.3、0.5、0.75、1.0。
    /// </summary>
    public double DefaultRadius { get; set; } = 0.5;

    /// <summary>
    /// 为 <c>true</c> 时，把主题偏好持久化到 <c>localStorage</c> 并在加载时恢复。默认为 <c>true</c>。
    /// </summary>
    /// <remarks>
    /// 为 <c>false</c> 时，既不读取也不写入已保存偏好，并且初始化时会清除早前运行留下的条目——
    /// 因此这里的 <c>Default*</c> 值（或在启用 <see cref="DetectSystemPreference"/> 时的系统偏好）
    /// 始终生效。
    /// </remarks>
    public bool PersistToLocalStorage { get; set; } = true;
}
