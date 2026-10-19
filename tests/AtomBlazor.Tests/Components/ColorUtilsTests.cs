using AtomBlazor.Primitives;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ColorUtils 纯函数单测：hex/rgb/hsl/hsv 解析与格式化往返、
/// 短写展开、越界钳制由调用方负责（此处守护原值）、非法输入的 FormatException
/// 与 IsValidHex 白名单边界，以及不变文化（InvariantCulture）下的输出形状。
/// </summary>
public class ColorUtilsTests
{
    // ------------------------------------------------------------------
    // RgbToHsl / HslToRgb
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(255, 0, 0, 0.0, 1.0, 0.5)]     // 纯红
    [InlineData(0, 255, 0, 120.0, 1.0, 0.5)]   // 纯绿
    [InlineData(0, 0, 255, 240.0, 1.0, 0.5)]   // 纯蓝
    [InlineData(255, 255, 255, 0.0, 0.0, 1.0)] // 白：max==min 走灰阶早退
    [InlineData(0, 0, 0, 0.0, 0.0, 0.0)]       // 黑：同上
    public void RgbToHsl应给出标准三元组(int r, int g, int b, double h, double s, double l)
    {
        var (hh, ss, ll) = ColorUtils.RgbToHsl(r, g, b);
        Assert.Equal(h, hh, 6);
        Assert.Equal(s, ss, 6);
        Assert.Equal(l, ll, 6);
    }

    [Fact]
    public void RgbToHsl灰阶色相饱和度归零且明度为缩放值()
    {
        var (h, s, l) = ColorUtils.RgbToHsl(128, 128, 128);
        Assert.Equal(0, h);
        Assert.Equal(0, s);
        Assert.Equal(128 / 255.0, l, 6);
    }

    [Fact]
    public void RgbToHsl蓝色主导色相走加六回绕分支()
    {
        // 品红：gf(0) < bf(1) → ((gf-bf)/d + 6)/6 → 300°
        var (h, s, l) = ColorUtils.RgbToHsl(255, 0, 255);
        Assert.Equal(300.0, h, 6);
        Assert.Equal(1.0, s, 6);
        Assert.Equal(0.5, l, 6);
    }

    [Theory]
    [InlineData(0, 1.0, 0.5, 255, 0, 0)]
    [InlineData(120, 1.0, 0.5, 0, 255, 0)]
    [InlineData(240, 1.0, 0.5, 0, 0, 255)]
    [InlineData(0, 0.0, 0.5, 128, 128, 128)] // s==0 灰阶早退：127.5 取中偶为 128
    public void HslToRgb应还原标准色与灰阶(double h, double s, double l, int r, int g, int b) =>
        Assert.Equal((r, g, b), ColorUtils.HslToRgb(h, s, l));

    [Fact]
    public void Hsl与Rgb往返应保持原色()
    {
        foreach (var color in new[] { (12, 34, 56), (128, 64, 64), (255, 102, 0), (0, 255, 255) })
        {
            var (h, s, l) = ColorUtils.RgbToHsl(color.Item1, color.Item2, color.Item3);
            var back = ColorUtils.HslToRgb(h, s, l);
            Assert.Equal(color, back);
        }
    }

    // ------------------------------------------------------------------
    // RgbToHsv / HsvToRgb
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(255, 0, 0, 0.0, 1.0, 1.0)]
    [InlineData(0, 255, 0, 120.0, 1.0, 1.0)]
    [InlineData(0, 0, 255, 240.0, 1.0, 1.0)]
    [InlineData(0, 0, 0, 0.0, 0.0, 0.0)] // max==0：饱和度除零护栏
    public void RgbToHsv应给出标准三元组(int r, int g, int b, double h, double s, double v)
    {
        var (hh, ss, vv) = ColorUtils.RgbToHsv(r, g, b);
        Assert.Equal(h, hh, 6);
        Assert.Equal(s, ss, 6);
        Assert.Equal(v, vv, 6);
    }

    [Theory]
    [InlineData(0.0, 0.5, 0.5, 128, 64, 64)]   // ColorPicker 区域拖动基准色
    [InlineData(240.0, 1.0, 1.0, 0, 0, 255)]
    [InlineData(360.0, 1.0, 1.0, 255, 0, 0)]   // i%6 回绕到扇区 0
    [InlineData(60.0, 1.0, 1.0, 255, 255, 0)]
    public void HsvToRgb应覆盖六扇区与回绕(double h, double s, double v, int r, int g, int b) =>
        Assert.Equal((r, g, b), ColorUtils.HsvToRgb(h, s, v));

    [Fact]
    public void Hsv与Rgb往返应保持原色()
    {
        foreach (var color in new[] { (12, 34, 56), (128, 64, 64), (255, 102, 0), (192, 192, 192) })
        {
            var (h, s, v) = ColorUtils.RgbToHsv(color.Item1, color.Item2, color.Item3);
            var back = ColorUtils.HsvToRgb(h, s, v);
            Assert.Equal(color, back);
        }
    }

    // ------------------------------------------------------------------
    // ToHex / ParseHex / IsValidHex
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(255, 0, 0, null, "#FF0000")]
    [InlineData(255, 0, 0, 255, "#FF0000")]   // 全不透明省略 alpha 尾
    [InlineData(0, 0, 0, 128, "#00000080")]
    [InlineData(0, 0, 0, 0, "#00000000")]
    [InlineData(12, 34, 56, null, "#0C2238")] // 半字节补零
    public void ToHex应大写补零并按需带透明度尾(int r, int g, int b, int? a, string expected) =>
        Assert.Equal(expected, ColorUtils.ToHex(r, g, b, a));

    [Theory]
    [InlineData("#ff0000", 255, 0, 0, null)]
    [InlineData("f00", 255, 0, 0, null)]           // 短写 #RGB 展开
    [InlineData("#f008", 255, 0, 0, 136)]          // 短写 #RGBA 展开
    [InlineData("ff000080", 255, 0, 0, 128)]
    [InlineData("#ff0000ff8080", 255, 0, 0, 255)]  // 超 8 位只取前 8
    [InlineData("", 0, 0, 0, null)]                // 短于 6 位整体回黑
    [InlineData("#", 0, 0, 0, null)]
    public void ParseHex应展开短写并容忍尾巴(string hex, int r, int g, int b, int? a) =>
        Assert.Equal((r, g, b, a), ColorUtils.ParseHex(hex));

    [Fact]
    public void ParseHex非法字符应抛FormatException()
    {
        // int.Parse 走 NumberStyles.HexNumber——非十六进制字符原样抛出
        Assert.Throws<FormatException>(() => ColorUtils.ParseHex("gggggg"));
        Assert.Throws<FormatException>(() => ColorUtils.ParseHex("#12zz34"));
    }

    [Theory]
    [InlineData("#abc", true)]
    [InlineData("ABCD", true)]
    [InlineData("123456", true)]
    [InlineData("#12345678", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("#", false)]      // 剥 # 后为空
    [InlineData("#ab", false)]    // 2 位不在白名单
    [InlineData("#abcde", false)] // 5 位不在白名单
    [InlineData("#1234567", false)]
    [InlineData("gggggg", false)] // 非十六进制字符
    public void IsValidHex应精确执行长度与字符白名单(string? hex, bool expected) =>
        Assert.Equal(expected, ColorUtils.IsValidHex(hex));

    [Fact]
    public void Hex格式化与解析应往返一致()
    {
        foreach (var color in new[] { (12, 34, 56), (255, 102, 0), (0, 0, 0), (192, 192, 192) })
        {
            var hex = ColorUtils.ToHex(color.Item1, color.Item2, color.Item3);
            Assert.True(ColorUtils.IsValidHex(hex));
            var (r, g, b, a) = ColorUtils.ParseHex(hex);
            Assert.Equal(color.Item1, r);
            Assert.Equal(color.Item2, g);
            Assert.Equal(color.Item3, b);
            Assert.Null(a);
        }
    }

    // ------------------------------------------------------------------
    // ToRgbString / ToHslString
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(1, 2, 3, null, "rgb(1, 2, 3)")]
    [InlineData(1, 2, 3, 255, "rgb(1, 2, 3)")] // 全不透明省略 alpha
    [InlineData(1, 2, 3, 128, "rgba(1, 2, 3, 0.50)")]
    [InlineData(0, 0, 0, 0, "rgba(0, 0, 0, 0.00)")]
    public void ToRgbString应按不变文化格式化(int r, int g, int b, int? a, string expected) =>
        Assert.Equal(expected, ColorUtils.ToRgbString(r, g, b, a));

    [Theory]
    [InlineData(0.0, 1.0, 0.5, null, "hsl(0, 100%, 50%)")]
    [InlineData(59.99, 0.5, 0.25, null, "hsl(60, 50%, 25%)")] // 色相 F0 取整
    [InlineData(0.0, 1.0, 0.5, 255, "hsl(0, 100%, 50%)")]
    [InlineData(0.0, 1.0, 0.5, 128, "hsla(0, 100%, 50%, 0.50)")]
    public void ToHslString应取整饱和明度并格式化透明度(double h, double s, double l, int? a, string expected) =>
        Assert.Equal(expected, ColorUtils.ToHslString(h, s, l, a));

    [Fact]
    public void Hsl格式化与解析往返应与ColorPicker输出同形()
    {
        // 组件 HSL 分支的复合明度公式：l = b * (1 - s/2)
        var (h, s, l) = ColorUtils.RgbToHsl(255, 0, 0);
        Assert.Equal("hsl(0, 100%, 50%)", ColorUtils.ToHslString(h, s, l));
    }
}
