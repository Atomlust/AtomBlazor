using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Theme;

/// <summary>
/// ThemeService 的行为测试：DI 生命周期契约、与 theme.js 的互操作标识符与载荷、
/// 持久化开关，以及 SSR/预渲染时 JS 不可用的守卫语义。
/// </summary>
public class ThemeServiceTests
{
    // ------------------------------------------------------------------
    // DI 生命周期契约（AddAtomBlazorComponents）
    // ------------------------------------------------------------------

    [Fact]
    public void AddAtomBlazorComponents应按源项目生命周期注册服务()
    {
        var services = new ServiceCollection();

        services.AddAtomBlazorComponents();

        Assert.Equal(ServiceLifetime.Scoped,
            services.Single(d => d.ServiceType == typeof(ThemeService)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton,
            services.Single(d => d.ServiceType == typeof(ThemeOptions)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton,
            services.Single(d => d.ServiceType == typeof(ILocalizer)).Lifetime);
        Assert.IsType<DefaultLocalizer>(services.Single(d => d.ServiceType == typeof(ILocalizer)).ImplementationInstance);
    }

    [Fact]
    public void AddAtomBlazorComponents应链式注册全部基元服务()
    {
        var services = new ServiceCollection();

        services.AddAtomBlazorComponents();

        // 基元层的代表服务在位，说明 AddAtomBlazorPrimitives 被调用。
        Assert.Contains(services, d => d.ServiceType == typeof(IPortalService));
        Assert.Contains(services, d => d.ServiceType == typeof(IFocusManager));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositioningService));
        Assert.Contains(services, d => d.ServiceType == typeof(OverlayRenderingOptions));
    }

    [Fact]
    public void TryAdd语义应尊重消费方先行注册的实现()
    {
        var services = new ServiceCollection();
        var customLocalizer = new DefaultLocalizer();
        var customOptions = new ThemeOptions { DefaultRadius = 1.0 };
        services.AddSingleton<ILocalizer>(customLocalizer);
        services.AddSingleton(customOptions);

        services.AddAtomBlazorComponents(localizer => localizer.Set("Alert.Dismiss", "Nein"));

        Assert.Same(customLocalizer, services.Single(d => d.ServiceType == typeof(ILocalizer)).ImplementationInstance);
        Assert.Same(customOptions, services.Single(d => d.ServiceType == typeof(ThemeOptions)).ImplementationInstance);
    }

    [Fact]
    public void 配置委托应作用到实际注册的实例上()
    {
        var services = new ServiceCollection();

        services.AddAtomBlazorComponents(
            configureLocalizer: localizer => localizer.Set("Alert.Dismiss", "Schließen"),
            configureTheme: theme => theme.DefaultRadius = 0.75);

        var localizer = (DefaultLocalizer)services.Single(d => d.ServiceType == typeof(ILocalizer)).ImplementationInstance!;
        var options = (ThemeOptions)services.Single(d => d.ServiceType == typeof(ThemeOptions)).ImplementationInstance!;

        Assert.Equal("Schließen", localizer["Alert.Dismiss"]);
        Assert.Equal(0.75, options.DefaultRadius);
    }

    // ------------------------------------------------------------------
    // 初始化互操作契约
    // ------------------------------------------------------------------

    [Fact]
    public async Task InitializeAsync应经共享core包一次导入并以单条theme初始化调用完成启动()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions());

        await service.InitializeAsync();

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains("components-core.js", (string)import.Args![0]!);
        Assert.Contains("assets=1", (string)import.Args![0]!);

        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "theme.initialize");
        Assert.Single(initialize.Args!);

        // 二次调用是安全的空操作——不再有新的互操作派发。
        var count = js.Invocations.Count;
        await service.InitializeAsync();
        Assert.Equal(count, js.Invocations.Count);
        Assert.True(service.IsInitialized);
    }

    [Fact]
    public async Task 初始化载荷应携带持久化开关与合法颜色清单()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions());

        await service.InitializeAsync();

        var payload = js.Invocations.Single(i => i.Identifier == "theme.initialize").Args![0]!;
        var type = payload.GetType();

        Assert.Equal(true, Read(payload, type, "persist"));
        Assert.Equal(true, Read(payload, type, "detectSystemPreference"));

        var defaults = Read(payload, type, "defaults")!;
        var defaultsType = defaults.GetType();
        Assert.Equal(false, Read(defaults, defaultsType, "isDarkMode"));
        Assert.Equal("zinc", Read(defaults, defaultsType, "baseColor"));
        Assert.Equal("default", Read(defaults, defaultsType, "primaryColor"));
        Assert.Equal(0.5, (double)Read(defaults, defaultsType, "radius")!);

        var validBase = (string[])Read(payload, type, "validBaseColors")!;
        Assert.Equal("zinc,slate,gray,neutral,stone", string.Join(',', validBase));
        var validPrimary = (string[])Read(payload, type, "validPrimaryColors")!;
        Assert.Contains("default", validPrimary);
        Assert.Contains("blue", validPrimary);
        Assert.DoesNotContain("notacolor", validPrimary);
    }

    [Fact]
    public async Task 互操作返回空时应保留配置的默认值()
    {
        // NoopJavaScript 对泛型调用返回 default——等价于浏览器无从应答的桩。
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions
        {
            DefaultPreset = new ThemePreset
            {
                DarkMode = true,
                BaseColor = BaseColor.Stone,
                PrimaryColor = PrimaryColor.Blue,
                Radius = 1.0,
            },
        });

        await service.InitializeAsync();

        Assert.True(service.IsDarkMode);
        Assert.Equal(BaseColor.Stone, service.BaseColor);
        Assert.Equal(PrimaryColor.Blue, service.PrimaryColor);
        Assert.Equal(1.0, service.Radius);
    }

    // ------------------------------------------------------------------
    // SSR / 预渲染守卫
    // ------------------------------------------------------------------

    [Fact]
    public async Task JS互操作不可用时初始化应静默完成并保留默认值()
    {
        var service = new ThemeService(new ThrowingJavaScript(typeof(InvalidOperationException)), new ThemeOptions());

        await service.InitializeAsync();

        Assert.True(service.IsInitialized);
        Assert.False(service.IsDarkMode);
        Assert.Equal(BaseColor.Zinc, service.BaseColor);
        // 默认值原封不动意味着此后 Set* 仍按配置工作。
        await service.SetDarkModeAsync(true);
        Assert.True(service.IsDarkMode);
    }

    [Fact]
    public async Task 电路断开时初始化应静默完成()
    {
        var service = new ThemeService(new ThrowingJavaScript(typeof(JSDisconnectedException)), new ThemeOptions());

        await service.InitializeAsync();

        Assert.True(service.IsInitialized);
    }

    // ------------------------------------------------------------------
    // 应用与持久化派发
    // ------------------------------------------------------------------

    [Fact]
    public async Task 变更暗色模式应依次派发apply与save并触发一次事件()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions());
        await service.InitializeAsync();

        var fired = 0;
        service.OnThemeChanged += () => fired++;

        await service.SetDarkModeAsync(true);

        Assert.Equal(1, fired);
        var identifiers = js.Invocations.Select(i => i.Identifier).ToList();
        Assert.Equal("theme.applyDarkMode", identifiers[^2]);
        Assert.Equal("theme.saveTheme", identifiers[^1]);
        Assert.Equal(true, js.Invocations[^2].Args![0]);
        Assert.Equal(true, js.Invocations[^1].Args![0]);
    }

    [Fact]
    public async Task 设置为当前值时不应产生任何派发或事件()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions());
        await service.InitializeAsync();

        var fired = 0;
        service.OnThemeChanged += () => fired++;

        await service.SetDarkModeAsync(service.IsDarkMode);
        await service.SetBaseColorAsync(service.BaseColor);
        await service.SetPrimaryColorAsync(service.PrimaryColor);
        await service.SetRadiusAsync(service.Radius);

        Assert.Equal(0, fired);
        Assert.Single(js.Invocations, i => i.Identifier == "theme.initialize");
    }

    [Fact]
    public async Task 关闭持久化后应只应用不保存且初始化载荷反映开关()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions { PersistToLocalStorage = false });
        await service.InitializeAsync();

        var payload = js.Invocations.Single(i => i.Identifier == "theme.initialize").Args![0]!;
        Assert.Equal(false, Read(payload, payload.GetType(), "persist"));

        await service.ToggleDarkModeAsync();

        Assert.Contains(js.Invocations, i => i.Identifier == "theme.applyDarkMode");
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "theme.saveTheme");
    }

    [Fact]
    public async Task 变更颜色应以小写字符串派发apply与save()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions());
        await service.InitializeAsync();
        js.Invocations.Clear();

        await service.SetBaseColorAsync(BaseColor.Slate);
        await service.SetPrimaryColorAsync(PrimaryColor.Violet);

        Assert.Equal("theme.applyBaseColor", js.Invocations[0].Identifier);
        Assert.Equal("slate", js.Invocations[0].Args![0]);
        var firstSave = js.Invocations[1];
        Assert.Equal("theme.saveTheme", firstSave.Identifier);
        Assert.Equal(false, firstSave.Args![0]);
        Assert.Equal("slate", firstSave.Args![1]);
        Assert.Equal("default", firstSave.Args![2]);
        Assert.Equal("theme.applyPrimaryColor", js.Invocations[2].Identifier);
        Assert.Equal("violet", js.Invocations[2].Args![0]);
        var secondSave = js.Invocations[3];
        Assert.Equal("theme.saveTheme", secondSave.Identifier);
        Assert.Equal("slate", secondSave.Args![1]);
        Assert.Equal("violet", secondSave.Args![2]);
    }

    [Fact]
    public async Task SetPresetAsync应一次派发applyTheme与saveTheme()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions());
        await service.InitializeAsync();
        js.Invocations.Clear();

        await service.SetPresetAsync(ThemePresets.Compact);

        Assert.Equal("theme.applyTheme", js.Invocations[0].Identifier);
        Assert.Equal(false, js.Invocations[0].Args![0]);
        Assert.Equal("zinc", js.Invocations[0].Args![1]);
        Assert.Equal("default", js.Invocations[0].Args![2]);
        Assert.Equal(0.375, js.Invocations[0].Args![3]);
        Assert.Equal("theme.saveTheme", js.Invocations[^1].Identifier);
        Assert.Equal(ThemeDensity.Compact, service.Preset.Design.Density);
        Assert.Equal(0.375, service.Radius);
    }

    [Fact]
    public async Task SetPresetAsync应拒绝非法半径()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions());
        await service.InitializeAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.SetPresetAsync(new ThemePreset { Radius = 9 }));
    }

    [Fact]
    public async Task SetDesignAsync应保留颜色与暗色模式仅更换外观()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions());
        await service.InitializeAsync();
        await service.SetDarkModeAsync(true);
        await service.SetPrimaryColorAsync(PrimaryColor.Teal);

        await service.SetDesignAsync(new ThemeDesign { Surface = ThemeSurface.Glass });

        Assert.True(service.IsDarkMode);
        Assert.Equal(PrimaryColor.Teal, service.PrimaryColor);
        Assert.Equal(ThemeSurface.Glass, service.Preset.Design.Surface);
    }

    [Fact]
    public async Task 非法半径的SetRadiusAsync应抛出且不改变状态()
    {
        var service = new ThemeService(new NoopJavaScript(), new ThemeOptions());
        await service.InitializeAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetRadiusAsync(5));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetRadiusAsync(double.NaN));
        Assert.Equal(0.5, service.Radius);
    }

    [Fact]
    public async Task DisposeAsync应为无害空操作因为模块归JsModules共享()
    {
        var js = new NoopJavaScript();
        var service = new ThemeService(js, new ThemeOptions());
        await service.InitializeAsync();
        var count = js.Invocations.Count;

        await service.DisposeAsync();

        Assert.Equal(count, js.Invocations.Count);
    }

    // ------------------------------------------------------------------
    // 辅助
    // ------------------------------------------------------------------

    private static object? Read(object target, Type type, string propertyName) =>
        type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!.GetValue(target);

    /// <summary>对任何 interop 调用抛出指定异常的伪运行时，模拟预渲染/断开的电路。</summary>
    private sealed class ThrowingJavaScript(Type exceptionType) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw (Exception)Activator.CreateInstance(exceptionType, $"Atom 测试桩：{identifier} 不可用")!;
    }
}
