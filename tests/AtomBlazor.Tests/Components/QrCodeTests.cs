using AtomBlazor.Primitives;
using System.Reflection;
using System.Net;
using AtomBlazor;
using AtomBlazor.Primitives.QrCode;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：QrCode 组件的纯逻辑面——不启宿主、不经渲染器，直接实例化组件、反射注入
/// Localizer 并手动驱动 OnParametersSetAsync，断言参数到 SVG 属性的映射：静区与中心图
/// 尺寸的钳制、中心图抬升纠错等级、模块形状的三种落笔、无障碍名称回落、超长值的本地化
/// 消息而非异常、OnEncoded 仅在网格真变化时触发，以及样式串的 HTML 转义。
/// </summary>
/// <remarks>
/// 对本组件零测试；编码器金样与结构面已由 QrCode/QrEncoderGoldenTests 与
/// QrEncoderTests 件覆盖，这里只补组件侧的参数透写。方法名照 – 自写先例用中文。
/// </remarks>
public class QrCodeTests
{
    private static readonly BindingFlags access = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>造一个已注入默认本地化器的组件：@inject 生成的 Localizer 属性按非公共面反射赋值。</summary>
    private static AtomBlazor.QrCode NewQrCode()
    {
        var qr = new AtomBlazor.QrCode();
        var property = typeof(AtomBlazor.QrCode).GetProperty("Localizer", access);
        Assert.NotNull(property);
        property!.SetValue(qr, new DefaultLocalizer());
        return qr;
    }

    /// <summary>手动跑一次参数期编码：OnParametersSetAsync 为 protected，反射驱动。</summary>
    private static async Task EncodeAsync(AtomBlazor.QrCode qr)
    {
        var method = typeof(AtomBlazor.QrCode).GetMethod("OnParametersSetAsync", access);
        Assert.NotNull(method);
        await (Task)method!.Invoke(qr, null)!;
    }

    private static object? PrivateValue(AtomBlazor.QrCode qr, string name)
    {
        var property = typeof(AtomBlazor.QrCode).GetProperty(name, access);
        if (property is not null)
        {
            return property.GetValue(qr);
        }

        var field = typeof(AtomBlazor.QrCode).GetField(name, access);
        Assert.NotNull(field);
        return field!.GetValue(qr);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task 值缺失或为空时什么都不渲染(string? value)
    {
        var qr = NewQrCode();
        qr.Value = value;

        await EncodeAsync(qr);

        Assert.Null(qr.GetSvg());
        Assert.Null(qr.Matrix);
        Assert.Null(PrivateValue(qr, "encodeError"));
    }

    [Fact]
    public async Task 默认参数把几何与配色透写进svg()
    {
        var qr = NewQrCode();
        qr.Value = "HELLO WORLD";

        await EncodeAsync(qr);

        var svg = qr.GetSvg()!;
        var size = qr.Matrix!.Size;
        Assert.Equal(21, size);
        Assert.Equal(QrErrorCorrection.Medium, qr.Matrix.ErrorCorrection);
        Assert.Equal(QrEncodingMode.Alphanumeric, qr.Matrix.Mode);

        // 静区 4 模块四周；独立文件每模块 8 像素。
        Assert.Contains($"viewBox=\"0 0 {size + 8} {size + 8}\"", svg, StringComparison.Ordinal);
        Assert.Contains($"width=\"{(size + 8) * 8}\" height=\"{(size + 8) * 8}\"", svg, StringComparison.Ordinal);
        Assert.Contains("style=\"width:12rem;height:auto;max-width:100%\"", svg, StringComparison.Ordinal);
        Assert.Contains("role=\"img\"", svg, StringComparison.Ordinal);
        Assert.Contains($"<rect width=\"{size + 8}\" height=\"{size + 8}\" fill=\"#ffffff\"/>", svg, StringComparison.Ordinal);
        Assert.Contains("<path fill=\"#18181b\" shape-rendering=\"crispEdges\"", svg, StringComparison.Ordinal);

        // 左上定位图案首行是横连 7 模块段，静区 4 平移其起点。
        Assert.Contains("M4 4h7v1h-7z", svg, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(20, 16)]
    public async Task 静区钳制在零到十六并反映在视口(int quiet, int expected)
    {
        var qr = NewQrCode();
        qr.Value = "x";
        qr.QuietZone = quiet;

        await EncodeAsync(qr);

        // 单模块 "x" 走字节模式，落在版本 1（21×21）。
        Assert.Contains($"viewBox=\"0 0 {21 + (expected * 2)} {21 + (expected * 2)}\"", qr.GetSvg()!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(QrErrorCorrection.Low, true, QrErrorCorrection.Quartile)]
    [InlineData(QrErrorCorrection.High, true, QrErrorCorrection.High)]
    [InlineData(QrErrorCorrection.Low, false, QrErrorCorrection.Low)]
    public async Task 中心图把纠错等级至少抬到quartile无图则原样透用(
        QrErrorCorrection given, bool withImage, QrErrorCorrection expected)
    {
        var qr = NewQrCode();
        qr.Value = "HELLO";
        qr.ErrorCorrection = given;
        if (withImage)
        {
            qr.Image = "/logo.png";
        }

        await EncodeAsync(qr);

        Assert.Equal(expected, qr.Matrix!.ErrorCorrection);
    }

    [Theory]
    [InlineData(QrCodeModuleShape.Square, false, false)]
    [InlineData(QrCodeModuleShape.Dot, true, false)]
    [InlineData(QrCodeModuleShape.Rounded, false, true)]
    public async Task 模块形状决定塑形通道是否落笔(QrCodeModuleShape shape, bool hasCircles, bool hasRoundedRects)
    {
        var qr = NewQrCode();
        qr.Value = "HELLO WORLD";
        qr.ModuleShape = shape;

        await EncodeAsync(qr);

        var svg = qr.GetSvg()!;
        Assert.Equal(hasCircles, svg.Contains("<circle ", StringComparison.Ordinal));
        Assert.Equal(hasRoundedRects, svg.Contains("rx=\"0.3\"/>", StringComparison.Ordinal));
        Assert.Equal(hasCircles || hasRoundedRects, svg.Contains("<g fill=\"#18181b\">", StringComparison.Ordinal));

        // 无论形状如何，定位图案恒为实心段。
        Assert.Contains("M4 4h7v1h-7z", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 无障碍名称按值长回落且显式优先()
    {
        var shortQr = NewQrCode();
        shortQr.Value = "HELLO WORLD";
        await EncodeAsync(shortQr);
        Assert.Contains("aria-label=\"QR code for HELLO WORLD\"", shortQr.GetSvg()!, StringComparison.Ordinal);

        var longQr = NewQrCode();
        longQr.Value = new string('7', 61);
        await EncodeAsync(longQr);
        Assert.Contains("aria-label=\"QR code\"", longQr.GetSvg()!, StringComparison.Ordinal);

        var explicitQr = NewQrCode();
        explicitQr.Value = "HELLO WORLD";
        explicitQr.AriaLabel = "访客网络二维码";
        await EncodeAsync(explicitQr);
        Assert.Contains($"aria-label=\"{WebUtility.HtmlEncode("访客网络二维码")}\"", explicitQr.GetSvg()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 超出版本四十容量的值落本地化消息而不抛异常()
    {
        var qr = NewQrCode();
        qr.Value = new string('7', 8000);

        await EncodeAsync(qr);

        Assert.Null(qr.GetSvg());
        Assert.Null(qr.Matrix);
        Assert.Equal("This value is too long to fit in a QR code.", PrivateValue(qr, "encodeError"));
    }

    [Fact]
    public async Task 最小版本钳制在一到四十并抬高尺寸()
    {
        var floor = NewQrCode();
        floor.Value = "x";
        floor.MinVersion = 5;
        await EncodeAsync(floor);
        Assert.Equal(5, floor.Matrix!.Version);

        var clamped = NewQrCode();
        clamped.Value = "x";
        clamped.MinVersion = 41;
        await EncodeAsync(clamped);
        Assert.Equal(40, clamped.Matrix!.Version);
        Assert.Contains("viewBox=\"0 0 185 185\"", clamped.GetSvg()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 中心图画衬块与图片且尺寸钳制生效()
    {
        var qr = NewQrCode();
        qr.Value = "HELLO";
        qr.Image = "/logo.png";
        qr.Background = "#eeeeee";
        qr.ImageSize = 0.9;
        await EncodeAsync(qr);

        var svg = qr.GetSvg()!;
        Assert.Equal(0.3, (double)PrivateValue(qr, "EffectiveImageSize")!);
        Assert.Contains("<image ", svg, StringComparison.Ordinal);
        Assert.Contains("href=\"/logo.png\" preserveAspectRatio=\"xMidYMid meet\"", svg, StringComparison.Ordinal);

        // 衬块默认底色取 Background；ImageBackground 显式时改取它。
        Assert.Contains("fill=\"#eeeeee\"/><image", svg, StringComparison.Ordinal);

        var patched = NewQrCode();
        patched.Value = "HELLO";
        patched.Image = "/logo.png";
        patched.Background = "#eeeeee";
        patched.ImageBackground = "#ff0000";
        await EncodeAsync(patched);
        Assert.Contains("fill=\"#ff0000\"", patched.GetSvg()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 样式与颜色一律经html编码()
    {
        var qr = NewQrCode();
        qr.Value = "x";
        qr.Size = "12rem\" onclick=\"alert(1)";
        qr.Foreground = "<i>";
        await EncodeAsync(qr);

        var svg = qr.GetSvg()!;
        Assert.Contains($"width:{WebUtility.HtmlEncode("12rem\" onclick=\"alert(1)")}", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=\"alert(1)\"", svg, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode("<i>"), svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnEncoded仅在网格真变化时触发()
    {
        var calls = new List<QrMatrix?>();
        var qr = NewQrCode();
        qr.Value = "HELLO";
        qr.OnEncoded = EventCallback.Factory.Create<QrMatrix?>(this, calls.Add);

        await EncodeAsync(qr);
        Assert.Single(calls);
        Assert.NotNull(calls[0]);

        // 同参数重跑：SVG 未变，不再触发，避免父组件弹回。
        await EncodeAsync(qr);
        Assert.Single(calls);

        qr.Value = "HELLO WORLD";
        await EncodeAsync(qr);
        Assert.Equal(2, calls.Count);

        // 清空值：网格从有到无，以 null 触发一次。
        qr.Value = null;
        await EncodeAsync(qr);
        Assert.Equal(3, calls.Count);
        Assert.Null(calls[2]);
    }
}
