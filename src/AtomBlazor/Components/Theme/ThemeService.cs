using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 管理应用的视觉主题——暗色模式、基础色与主色。
/// 偏好持久化到 <c>localStorage</c>，并通过 JS 模块应用到 DOM。
/// </summary>
/// <remarks>
/// <para>
/// 通过 <see cref="ServiceCollectionExtensions.AddAtomBlazorComponents(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{DefaultLocalizer}?, Action{ThemeOptions}?, Action{AtomBlazor.Primitives.Services.OverlayRenderingOptions}?)"/>
/// 注册，可选传入 <see cref="ThemeOptions"/> 配置委托。
/// </para>
/// <para>
/// 在首次可交互渲染之后（通常在 <c>OnAfterRenderAsync(firstRender)</c> 中）调用一次
/// <see cref="InitializeAsync"/>，以加载已保存的偏好并应用主题。
/// </para>
/// </remarks>
public class ThemeService : IAsyncDisposable
{
    private readonly IJSRuntime jsRuntime;
    private readonly ThemeOptions options;
    private IJSObjectReference? module;
    private bool isDarkMode;
    private BaseColor baseColor;
    private PrimaryColor primaryColor;
    private double radius;
    private bool isInitialized;
    private ThemeDesign design;

    /// <summary>当前完整配置。</summary>
    public ThemePreset Preset => new() { DarkMode = isDarkMode, BaseColor = baseColor, PrimaryColor = primaryColor, Radius = radius, Design = design };

    /// <summary>
    /// 任一主题属性变更后触发。订阅它以便消费组件触发 <c>StateHasChanged</c>。
    /// </summary>
    public event Action? OnThemeChanged;

    /// <summary>
    /// 获取当前是否处于暗色模式。
    /// </summary>
    public bool IsDarkMode => isDarkMode;

    /// <summary>
    /// 获取当前基础（灰阶）颜色。
    /// </summary>
    public BaseColor BaseColor => baseColor;

    /// <summary>
    /// 获取当前主强调色。
    /// </summary>
    public PrimaryColor PrimaryColor => primaryColor;

    /// <summary>
    /// 获取当前圆角半径（rem）。
    /// </summary>
    public double Radius => radius;

    /// <summary>
    /// 获取服务是否已通过 <see cref="InitializeAsync"/> 完成初始化。
    /// </summary>
    public bool IsInitialized => isInitialized;

    /// <summary>
    /// 创建 <see cref="ThemeService"/> 实例。
    /// </summary>
    /// <param name="jsRuntime">Blazor 的 JS 互操作运行时。</param>
    /// <param name="options">主题配置选项。</param>
    public ThemeService(IJSRuntime jsRuntime, ThemeOptions options)
    {
        this.jsRuntime = jsRuntime;
        this.options = options;
        var preset = options.DefaultPreset ?? new ThemePreset
        {
            DarkMode = options.DefaultDarkMode, BaseColor = options.DefaultBaseColor,
            PrimaryColor = options.DefaultPrimaryColor, Radius = options.DefaultRadius
        };
        preset.Validate();
        isDarkMode = preset.DarkMode;
        baseColor = preset.BaseColor;
        primaryColor = preset.PrimaryColor;
        radius = preset.Radius;
        design = preset.Design;
    }

    /// <summary>
    /// 从 <c>localStorage</c> 加载已保存的偏好（若启用持久化），探测系统配色偏好，
    /// 并把主题应用到 DOM。可安全地多次调用——只有第一次会真正执行初始化。
    /// </summary>
    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        try
        {
            module = await ComponentModules.GetCoreAsync(jsRuntime);
        }
        catch (JSDisconnectedException)
        {
            isInitialized = true;
            return;
        }
        catch (InvalidOperationException)
        {
            // JS 互操作不可用（SSR 预渲染）——保留默认值
            isInitialized = true;
            return;
        }

        try
        {
            // 一次调用，而不是四次。读取 localStorage、清除过期条目、询问操作系统的暗色偏好
            // 并应用结果，全都是浏览器侧的决策；而在 Blazor Server 上，此前每一次分别 await 的
            // 调用都是页面加载时的一次电路往返。随请求带上合法的颜色名，使损坏的条目在浏览器
            // 里就回落到配置默认值——与这里 ParseEnum 的行为一致，而不是晚一个往返才被纠正。
            var applied = await module.InvokeAsync<ThemeState?>("theme.initialize", new ThemeInitializeJsConfig
            {
                persist = options.PersistToLocalStorage,
                detectSystemPreference = options.DetectSystemPreference,
                defaults = new ThemeDefaultsJsConfig
                {
                    isDarkMode = this.isDarkMode,
                    baseColor = this.baseColor.ToString().ToLowerInvariant(),
                    primaryColor = this.primaryColor.ToString().ToLowerInvariant(),
                    radius = this.radius,
                    design = this.design.ToJs()
                },
                validBaseColors = Enum.GetNames<BaseColor>().Select(n => n.ToLowerInvariant()).ToArray(),
                validPrimaryColors = Enum.GetNames<PrimaryColor>().Select(n => n.ToLowerInvariant()).ToArray()
            });

            // 互操作被桩掉、或浏览器无法应答时为 null。配置默认值已经在字段里，
            // 保持不动正是正确的结果。
            if (applied is null)
            {
                isInitialized = true;
                return;
            }

            isDarkMode = applied.IsDarkMode;
            baseColor = ParseEnum(applied.BaseColor, baseColor);
            primaryColor = ParseEnum(applied.PrimaryColor, primaryColor);
            radius = applied.Radius is >= 0 and <= 4 ? applied.Radius.Value : radius;
            if (applied.Design is { } restored)
            {
                design = new ThemeDesign
                {
                    Density = ParseEnum(restored.Density, design.Density),
                    Font = ParseEnum(restored.Font, design.Font),
                    Surface = ParseEnum(restored.Surface, design.Surface),
                    MenuColor = ParseEnum(restored.MenuColor, design.MenuColor),
                    MenuAccent = ParseEnum(restored.MenuAccent, design.MenuAccent)
                };
            }
        }
        catch (JSDisconnectedException)
        {
            // 初始化期间电路断开
        }

        isInitialized = true;
    }

    /// <summary>
    /// 在暗色与亮色模式之间切换。
    /// </summary>
    public async Task ToggleDarkModeAsync() =>
        await SetDarkModeAsync(!isDarkMode);

    /// <summary>
    /// 设置暗色模式为指定值。
    /// </summary>
    /// <param name="value"><c>true</c> 为暗色模式，<c>false</c> 为亮色模式。</param>
    public async Task SetDarkModeAsync(bool value)
    {
        if (isDarkMode == value)
        {
            return;
        }

        isDarkMode = value;
        await ApplyDarkModeAsync();
        await SaveAsync();
        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// 设置基础（灰阶）调色板。
    /// </summary>
    /// <param name="color">要应用的基础色。</param>
    public async Task SetBaseColorAsync(BaseColor color)
    {
        if (baseColor == color)
        {
            return;
        }

        baseColor = color;
        await ApplyBaseColorAsync();
        await SaveAsync();
        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// 设置主强调色。
    /// </summary>
    /// <param name="color">要应用的主色。</param>
    public async Task SetPrimaryColorAsync(PrimaryColor color)
    {
        if (primaryColor == color)
        {
            return;
        }

        primaryColor = color;
        await ApplyPrimaryColorAsync();
        await SaveAsync();
        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// 设置圆角半径。
    /// </summary>
    /// <param name="value">半径（rem），例如 0、0.3、0.5、0.75、1.0。</param>
    public async Task SetRadiusAsync(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "半径必须在 0 到 4 rem 之间。");
        }
        if (Math.Abs(radius - value) < 0.001)
        {
            return;
        }

        radius = value;
        await ApplyRadiusAsync();
        await SaveAsync();
        OnThemeChanged?.Invoke();
    }

    /// <summary>在一次更新内应用并持久化完整预设。</summary>
    public async Task SetPresetAsync(ThemePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        preset.Validate();
        await InitializeAsync();
        isDarkMode = preset.DarkMode;
        baseColor = preset.BaseColor;
        primaryColor = preset.PrimaryColor;
        radius = preset.Radius;
        design = preset.Design;
        await ApplyAllAsync();
        await SaveAsync();
        OnThemeChanged?.Invoke();
    }

    /// <summary>更改外观设置，同时保留当前颜色、半径与暗色模式。</summary>
    public async Task SetDesignAsync(ThemeDesign value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        await InitializeAsync();
        await SetPresetAsync(Preset with { Design = value });
    }

    private async Task ApplyAllAsync()
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("theme.applyTheme",
                isDarkMode,
                baseColor.ToString().ToLowerInvariant(),
                primaryColor.ToString().ToLowerInvariant(),
                radius, design.ToJs());
        }
        catch
        {
            // JS 不可用时忽略
        }
    }

    private async Task ApplyDarkModeAsync()
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("theme.applyDarkMode", isDarkMode);
        }
        catch
        {
            // JS 不可用时忽略
        }
    }

    private async Task ApplyBaseColorAsync()
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("theme.applyBaseColor", baseColor.ToString().ToLowerInvariant());
        }
        catch
        {
            // JS 不可用时忽略
        }
    }

    private async Task ApplyPrimaryColorAsync()
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("theme.applyPrimaryColor", primaryColor.ToString().ToLowerInvariant());
        }
        catch
        {
            // JS 不可用时忽略
        }
    }

    private async Task ApplyRadiusAsync()
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("theme.applyRadius", radius);
        }
        catch
        {
            // JS 不可用时忽略
        }
    }

    private async Task SaveAsync()
    {
        if (module is null || !options.PersistToLocalStorage)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("theme.saveTheme",
                isDarkMode,
                baseColor.ToString().ToLowerInvariant(),
                primaryColor.ToString().ToLowerInvariant(),
                radius, design.ToJs());
        }
        catch
        {
            // localStorage 不可用时忽略
        }
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result) ? result : fallback;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // 没有需要释放的东西：主题模块是共享的，归 JsModules 所有。
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 表示从 <c>localStorage</c> 恢复的主题状态。
    /// </summary>
    private sealed class ThemeState
    {
        /// <summary>获取或设置是否启用暗色模式。</summary>
        public bool IsDarkMode { get; set; }

        /// <summary>获取或设置基础色名称。</summary>
        public string? BaseColor { get; set; }

        /// <summary>获取或设置主色名称。</summary>
        public string? PrimaryColor { get; set; }

        /// <summary>获取或设置圆角半径（rem）。</summary>
        public double? Radius { get; set; }

        /// <summary>获取或设置外观设置。</summary>
        public ThemeDesignState? Design { get; set; }
    }

    /// <summary>外观设置的持久化形状。</summary>
    private sealed class ThemeDesignState
    {
        /// <summary>获取或设置密度。</summary>
        public string? Density { get; set; }

        /// <summary>获取或设置字体。</summary>
        public string? Font { get; set; }

        /// <summary>获取或设置表面处理。</summary>
        public string? Surface { get; set; }

        /// <summary>获取或设置菜单颜色。</summary>
        public string? MenuColor { get; set; }

        /// <summary>获取或设置菜单强调色。</summary>
        public string? MenuAccent { get; set; }
    }
}
