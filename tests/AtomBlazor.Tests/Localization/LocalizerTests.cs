using AtomBlazor.Primitives;
using System.Globalization;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Localization;

/// <summary>
/// DefaultLocalizer / ILocalizer 的行为测试：键查找、英文默认值（ 约定
/// 字符串值与源项目）、未知键回落、格式化索引器与 Set 覆盖。
/// </summary>
public class LocalizerTests
{
    // 刻意以 ILocalizer 接口形态返回并调用索引器：本组测试的靶心就是接口多态派发
    // CA1859 的"改用具体类型"建议与测试目的相悖，就地抑制。
#pragma warning disable CA1859
    /// <summary>以接口形态取得默认本地化器，保证断言走的是 ILocalizer 虚索引器。</summary>
    private static ILocalizer CreateDefault() => new DefaultLocalizer();
#pragma warning restore CA1859

    [Fact]
    public void 已知键应返回英文默认值()
    {
        var localizer = CreateDefault();

        Assert.Equal("Dismiss", localizer["Alert.Dismiss"]);
        Assert.Equal("Go back", localizer["AppBar.Back"]);
        Assert.Equal("Base color", localizer["Theme.BaseColor"]);
        Assert.Equal("Switch to dark mode", localizer["Theme.SwitchToDark"]);
        Assert.Equal("Time", localizer["TimeInput.Label"]);
        Assert.Equal("Timeline", localizer["Timeline.Timeline"]);
    }

    [Fact]
    public void 未知键应原样返回键本身()
    {
        var localizer = CreateDefault();

        Assert.Equal("NoSuch.Component.Key", localizer["NoSuch.Component.Key"]);
    }

    [Theory]
    [InlineData("FileUpload.MaxFiles", 5, "Max files: 5")]
    [InlineData("MultiSelect.More", 3, "+3 more")]
    [InlineData("FileUpload.Accepted", ".png, .jpg", "Accepted: .png, .jpg")]
    public void 格式化索引器应替换占位符(string key, object argument, string expected)
    {
        var localizer = CreateDefault();

        Assert.Equal(expected, localizer[key, argument]);
    }

    [Fact]
    public void 格式化索引器对未知键应返回键本身而非抛错()
    {
        var localizer = CreateDefault();

        Assert.Equal("Unknown.Format", localizer["Unknown.Format", 1, 2]);
    }

    [Fact]
    public void 多参数格式化键应逐位替换()
    {
        var localizer = CreateDefault();

        Assert.Equal("QR barcode for ticket", localizer["Barcode.AriaLabel", "QR", "ticket"]);
    }

    [Fact]
    public void Set应覆盖默认值并同时作用于两个索引器()
    {
        var localizer = new DefaultLocalizer();

        localizer.Set("Alert.Dismiss", "Verwerfen");
        localizer.Set("FileUpload.MaxFiles", "Maximal {0} Dateien");

        Assert.Equal("Verwerfen", localizer["Alert.Dismiss"]);
        Assert.Equal("Maximal 4 Dateien", localizer["FileUpload.MaxFiles", 4]);
    }

    [Fact]
    public void Set新键后应可直接检索()
    {
        var localizer = new DefaultLocalizer();

        localizer.Set("MyApp.Custom", "hello");

        Assert.Equal("hello", localizer["MyApp.Custom"]);
    }

    [Fact]
    public void 并发Set与读取不应破坏本地化器()
    {
        var localizer = new DefaultLocalizer();

        Parallel.For(0, 2000, i =>
        {
            var key = $"Concurrent.Key{i % 64}";
            localizer.Set(key, $"v{i}");
            _ = localizer[key];
            _ = localizer["Alert.Dismiss"];
        });

        Assert.StartsWith("v", localizer["Concurrent.Key0"]);
        Assert.Equal("Dismiss", localizer["Alert.Dismiss"]);
    }

    [Fact]
    public void 格式化应遵循当前区域文化()
    {
        // string.Format(CurrentCulture) 语义：断言与进程文化一致即可
        // 不同文化下（en-US 小数点 / de-DE 逗号）两侧同步变化。
        var localizer = CreateDefault();

        var formatted = localizer["FileUpload.Accepted", 1.5];
        var expected = string.Format(CultureInfo.CurrentCulture, "Accepted: {0}", 1.5);

        Assert.Equal(expected, formatted);
    }

#pragma warning disable CA1859 // 同上：接口形态是测试目的本身
    /// <summary>以接口形态取得子类实例，验证多态派发。</summary>
    private static ILocalizer CreateFallback() => new FallbackSubclass();
#pragma warning restore CA1859

    [Fact]
    public void 子类可对接外部资源并回落到默认值()
    {
        var localizer = CreateFallback();

        Assert.Equal("override:Theme.Radius", localizer["Theme.Radius"]);
        // 基类默认值在子类未命中时原样保留（"Theme.Mode" 的英文默认是 "Mode"）。
        Assert.Equal("Mode", localizer["Theme.Mode"]);
        // 未覆写格式化索引器时，它仍走 DefaultLocalizer 的虚实现（组合子类常见形态）。
        Assert.Equal("Max files: 2", localizer["FileUpload.MaxFiles", 2]);
    }

    /// <summary>模拟 IStringLocalizer 对接：命中前缀则改写，其余回落基类。</summary>
    private sealed class FallbackSubclass : DefaultLocalizer
    {
        public override string this[string key] =>
            key.StartsWith("Theme.R", StringComparison.Ordinal) ? $"override:{key}" : base[key];
    }
}
