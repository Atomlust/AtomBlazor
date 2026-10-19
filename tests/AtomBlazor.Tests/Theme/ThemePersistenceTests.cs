using AtomBlazor.Primitives;
using System.Text.Json;
using AtomBlazor;
using Microsoft.JSInterop;

namespace AtomBlazor.Tests.Theme;

/// <summary>
/// 守护 <see cref="ThemeOptions.PersistToLocalStorage"/>，防止它读回
/// 一份自己承诺过不写的主题。
/// <para><c>InitializeAsync</c> 过去会无条件调用 <c>loadTheme</c>。
/// 谁只要开过一次持久化再关掉，
/// 此后拿到的就仍是存下的主题而非配置的默认值，
/// 除非手动清除站点数据，
/// 别无出路（问题号 #481）。</para>
/// <para>这个判断如今就发生在 <c>theme.initialize</c> 里，
/// 因为读取 localStorage、清除过期条目、询问系统的暗色模式偏好，
/// 曾是四次分别 await 的调用——每次页面加载就是四趟线路往返。
/// C# 仍然负责的（也正是这些测试覆盖的）是它交出去的配置，
/// 以及对回答的处理方式。
/// 而 localStorage 是否真的没被碰过，属于浏览器行为，
/// 由驱动 demo 验证。</para>
/// </summary>
public class ThemePersistenceTests
{
    /// <summary>
    /// 在每个维度上都刻意取配置默认值的反面，
    /// 好让从存储里漏出的值只会让测试失败，而非碰巧撞上期望答案。
    /// </summary>
    private const string restoredTheme =
        """{"isDarkMode":true,"baseColor":"Slate","primaryColor":"Blue","radius":1.0}""";

    [Fact]
    public async Task PersistenceDisabledTellsTheBrowserNotToRead()
    {
        var module = new RecordingModule();
        var service = new ThemeService(
            new StubJsRuntime(module),
            new ThemeOptions { PersistToLocalStorage = false });

        await service.InitializeAsync();

        Assert.False(module.Config.GetProperty("persist").GetBoolean());
    }

    [Fact]
    public async Task PersistenceEnabledTellsTheBrowserToRead()
    {
        var module = new RecordingModule();
        var service = new ThemeService(
            new StubJsRuntime(module),
            new ThemeOptions { PersistToLocalStorage = true });

        await service.InitializeAsync();

        Assert.True(module.Config.GetProperty("persist").GetBoolean());
    }

    [Fact]
    public async Task ConfiguredDefaultsAreSentAsTheFallback()
    {
        var module = new RecordingModule();
        var service = new ThemeService(
            new StubJsRuntime(module),
            new ThemeOptions
            {
                PersistToLocalStorage = false,
                DetectSystemPreference = false,
                DefaultDarkMode = false,
                DefaultBaseColor = BaseColor.Zinc,
                DefaultPrimaryColor = PrimaryColor.Default,
                DefaultRadius = 0.5,
            });

        await service.InitializeAsync();

        var defaults = module.Config.GetProperty("defaults");
        Assert.False(module.Config.GetProperty("detectSystemPreference").GetBoolean());
        Assert.False(defaults.GetProperty("isDarkMode").GetBoolean());
        Assert.Equal("zinc", defaults.GetProperty("baseColor").GetString());
        Assert.Equal("default", defaults.GetProperty("primaryColor").GetString());
        Assert.Equal(0.5, defaults.GetProperty("radius").GetDouble());
    }

    /// <summary>
    /// 浏览器在应用存储的颜色名之前会先校验，所以它可能接受的名字清单
    /// 必须随请求同行——否则本构建已不认识的值会先被应用，
    /// 再过一趟往返才被纠正。
    /// </summary>
    [Fact]
    public async Task ValidColourNamesAreSentForTheBrowserToCheckAgainst()
    {
        var module = new RecordingModule();
        var service = new ThemeService(new StubJsRuntime(module), new ThemeOptions());

        await service.InitializeAsync();

        var baseColors = module.Config.GetProperty("validBaseColors")
            .EnumerateArray().Select(v => v.GetString()).ToList();

        Assert.Equal(Enum.GetNames<BaseColor>().Length, baseColors.Count);
        Assert.Contains("zinc", baseColors);
        Assert.All(baseColors, name => Assert.Equal(name, name!.ToLowerInvariant()));
    }

    [Fact]
    public async Task TheStateTheBrowserAppliedBecomesTheServiceState()
    {
        var module = new RecordingModule { AppliedThemeJson = restoredTheme };
        var service = new ThemeService(
            new StubJsRuntime(module),
            new ThemeOptions { PersistToLocalStorage = true });

        await service.InitializeAsync();

        Assert.True(service.IsDarkMode);
        Assert.Equal(BaseColor.Slate, service.BaseColor);
        Assert.Equal(PrimaryColor.Blue, service.PrimaryColor);
        Assert.Equal(1.0, service.Radius);
    }

    /// <summary>
    /// 预渲染和桩运行时都不给任何回答。配置的默认值此时已经就位，
    /// 所以服务必须沿用它们而不是倒下。
    /// </summary>
    [Fact]
    public async Task NoAnswerFromTheBrowserLeavesTheConfiguredDefaults()
    {
        var module = new RecordingModule { AppliedThemeJson = null };
        var service = new ThemeService(
            new StubJsRuntime(module),
            new ThemeOptions { DefaultBaseColor = BaseColor.Zinc, DefaultRadius = 0.5 });

        await service.InitializeAsync();

        Assert.Equal(BaseColor.Zinc, service.BaseColor);
        Assert.Equal(0.5, service.Radius);
    }

    [Fact]
    public async Task PresetDefaultsAndRestoredDesignAreAppliedTogether()
    {
        var module = new RecordingModule
        {
            AppliedThemeJson = """{"isDarkMode":true,"baseColor":"Slate","primaryColor":"Blue","radius":0.75,"design":{"density":"dense","font":"mono","menuColor":"inverse"}}"""
        };
        var service = new ThemeService(new StubJsRuntime(module), new ThemeOptions { DefaultPreset = ThemePresets.Glass });
        await service.InitializeAsync();
        Assert.Equal("glass", module.Config.GetProperty("defaults").GetProperty("design").GetProperty("surface").GetString());
        Assert.Equal(ThemeDensity.Dense, service.Preset.Design.Density);
        Assert.Equal(ThemeFont.Mono, service.Preset.Design.Font);
        Assert.Equal(ThemeSurface.Glass, service.Preset.Design.Surface);
        await service.SetDesignAsync(service.Preset.Design with { MenuAccent = ThemeMenuAccent.Primary });
        Assert.True(service.IsDarkMode);
        Assert.Equal(PrimaryColor.Blue, service.PrimaryColor);
        Assert.Contains("theme.saveTheme", module.Calls);
    }

    [Fact]
    public async Task InvalidPresetDoesNotPartiallyMutateTheActiveTheme()
    {
        var module = new RecordingModule();
        var service = new ThemeService(new StubJsRuntime(module), new ThemeOptions());
        var original = service.Preset;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetPresetAsync(ThemePresets.Glass with { Radius = double.NaN }));
        Assert.Equal(original, service.Preset);
        Assert.Empty(module.Calls);
    }

    /// <summary>只交出那一个模块；其余任何调用都是服务不该发起的。</summary>
    private sealed class StubJsRuntime(RecordingModule module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != "import")
            {
                throw new InvalidOperationException(
                    $"ThemeService should only call 'import' on IJSRuntime, not '{identifier}'.");
            }

            return ValueTask.FromResult((TValue)(object)module);
        }
    }

    /// <summary>
    /// 捕获交给 <c>theme.initialize</c> 的配置，并像浏览器那样答回一个状态。
    /// 配置经 JSON 往返一遍，好让测试看到真实互操作所序列化的形状，
    /// 而不是那个匿名类型。
    /// </summary>
    private sealed class RecordingModule : IJSObjectReference
    {
        private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

        public List<string> Calls { get; } = [];

        /// <summary>传给 <c>theme.initialize</c> 的配置。</summary>
        public JsonElement Config { get; private set; }

        /// <summary>浏览器自报已应用的主题；<c>null</c> 表示它答不出来。</summary>
        public string? AppliedThemeJson { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);

            if (identifier == "theme.initialize")
            {
                Config = JsonSerializer.SerializeToElement(args?[0], jsonOptions);

                if (AppliedThemeJson is not null)
                {
                    return ValueTask.FromResult(
                        JsonSerializer.Deserialize<TValue>(AppliedThemeJson, jsonOptions)!);
                }
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
