using AtomBlazor.Primitives;
using System.Reflection;
using System.Net;
using AtomBlazor;
using AtomBlazor.Primitives.Barcode;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：Barcode 组件的纯逻辑面——不启宿主、不经渲染器，直接实例化组件、反射注入
/// Localizer 并手动驱动 OnParametersSetAsync，断言参数到 SVG 属性的映射：页内条图按符号
/// 几何透写、静区默认取码制要求且可覆盖钳制、邮政码的高调制与非邮政恒满高、非法值显示
/// 编码器原句而非抛异常、可选校验字符默认不进打印行、零售码制恒打印完整编号、ISBN 把
/// 可引用编号移到条上方、独立文档的真实比例，以及 OnEncoded 仅变化触发与颜色 HTML 转义。
/// </summary>
/// <remarks>
/// 对本组件零测试；编码器面已由 Barcode/BarcodeEncoderTests 等件覆盖，这里只补
/// 组件侧的参数透写。方法名照 – 自写先例用中文。
/// </remarks>
public class BarcodeTests
{
    private static readonly BindingFlags access = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>造一个已注入默认本地化器的组件：@inject 生成的 Localizer 属性按非公共面反射赋值。</summary>
    private static AtomBlazor.Barcode NewBarcode()
    {
        var code = new AtomBlazor.Barcode();
        var property = typeof(AtomBlazor.Barcode).GetProperty("Localizer", access);
        Assert.NotNull(property);
        property!.SetValue(code, new DefaultLocalizer());
        return code;
    }

    /// <summary>手动跑一次参数期编码：OnParametersSetAsync 为 protected，反射驱动。</summary>
    private static async Task EncodeAsync(AtomBlazor.Barcode code)
    {
        var method = typeof(AtomBlazor.Barcode).GetMethod("OnParametersSetAsync", access);
        Assert.NotNull(method);
        await (Task)method!.Invoke(code, null)!;
    }

    private static object? PrivateValue(AtomBlazor.Barcode code, string name)
    {
        var property = typeof(AtomBlazor.Barcode).GetProperty(name, access);
        if (property is not null)
        {
            return property.GetValue(code);
        }

        var field = typeof(AtomBlazor.Barcode).GetField(name, access);
        Assert.NotNull(field);
        return field!.GetValue(code);
    }

    /// <summary>页内标记存于私有字段 svg（GetSvg() 返回的是独立文档）。</summary>
    private static string? PageSvg(AtomBlazor.Barcode code) => (string?)PrivateValue(code, "svg");

    private static int CountOf(string haystack, string needle)
    {
        var count = 0;
        for (var at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = haystack.IndexOf(needle, at + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task 值缺失或为空时什么都不渲染(string? value)
    {
        var code = NewBarcode();
        code.Value = value;

        await EncodeAsync(code);

        Assert.Null(PageSvg(code));
        Assert.Null(code.Symbol);
        Assert.Null(code.GetSvg());
        Assert.Null(PrivateValue(code, "encodeError"));
    }

    [Fact]
    public async Task 页内svg按符号几何透写且样式串跟随模块宽度()
    {
        var code = NewBarcode();
        code.Value = "ABC123";
        await EncodeAsync(code);

        var symbol = code.Symbol!;
        var quiet = (int)PrivateValue(code, "EffectiveQuietZone")!;
        var width = symbol.ModuleCount + (quiet * 2);
        var svg = PageSvg(code)!;

        // 条区高恒为 100 SVG 单位，preserveAspectRatio 为 none 由 CSS 拉高。
        Assert.Contains($"viewBox=\"0 0 {width} 100\"", svg, StringComparison.Ordinal);
        Assert.Contains("preserveAspectRatio=\"none\" role=\"img\"", svg, StringComparison.Ordinal);
        Assert.Contains("style=\"display:block;width:100%;height:4rem\"", svg, StringComparison.Ordinal);
        Assert.Contains($"<rect width=\"{width}\" height=\"100\" fill=\"#ffffff\"/>", svg, StringComparison.Ordinal);
        Assert.Contains("<g fill=\"#18181b\" shape-rendering=\"crispEdges\">", svg, StringComparison.Ordinal);

        // 一条杠一个 rect，外加底色矩形。
        Assert.Equal(symbol.Bars.Count + 1, CountOf(svg, "<rect "));
        Assert.Contains($"<rect x=\"{symbol.Bars[0].Start + quiet}\" y=\"0\" width=\"{symbol.Bars[0].Width}\" height=\"100\"/>", svg, StringComparison.Ordinal);

        // 符号宽度与字号样式跟随 ModuleWidth。
        code.ModuleWidth = "3px";
        Assert.Equal($"calc(3px * {width})", PrivateValue(code, "SymbolWidthStyle"));
        Assert.Equal("clamp(0.625rem, calc(3px * 5), 1rem)", PrivateValue(code, "TextSizeStyle"));

        // 无障碍名称默认含码制与值，显式设置则优先。
        Assert.Contains("aria-label=\"Code128 barcode for ABC123\"", svg, StringComparison.Ordinal);
        code.AriaLabel = "订单 4051";
        await EncodeAsync(code);
        Assert.Contains($"aria-label=\"{WebUtility.HtmlEncode("订单 4051")}\"", PageSvg(code)!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 静区默认取码制要求且可覆盖并钳制在零到四十()
    {
        var code = NewBarcode();
        code.Value = "ABC123";
        await EncodeAsync(code);
        Assert.Equal(code.Symbol!.QuietZoneModules, PrivateValue(code, "EffectiveQuietZone"));

        code.QuietZone = 0;
        await EncodeAsync(code);
        Assert.Equal(0, PrivateValue(code, "EffectiveQuietZone"));
        Assert.Contains($"viewBox=\"0 0 {code.Symbol!.ModuleCount} 100\"", PageSvg(code)!, StringComparison.Ordinal);

        code.QuietZone = 999;
        await EncodeAsync(code);
        Assert.Equal(40, PrivateValue(code, "EffectiveQuietZone"));
    }

    [Fact]
    public async Task 邮政码用高度承载数据其余码制恒满高()
    {
        var postnet = NewBarcode();
        postnet.Value = "12345";
        postnet.Type = BarcodeType.Postnet;
        await EncodeAsync(postnet);

        var svg = PageSvg(postnet)!;
        // 短条半高（y=50、h=50），长条满高（y=0、h=100）。
        Assert.Contains("y=\"50\"", svg, StringComparison.Ordinal);
        Assert.Contains("height=\"50\"", svg, StringComparison.Ordinal);
        Assert.Contains("height=\"100\"", svg, StringComparison.Ordinal);

        var plain = NewBarcode();
        plain.Value = "ABC123";
        await EncodeAsync(plain);
        Assert.DoesNotContain("height=\"50\"", PageSvg(plain)!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 码制无法承载的值显示编码器原句而不抛异常()
    {
        var code = NewBarcode();
        code.Value = "5901234123450";
        code.Type = BarcodeType.Ean13;

        await EncodeAsync(code);

        Assert.Null(PageSvg(code));
        Assert.Null(code.Symbol);
        Assert.Null(code.GetSvg());
        var error = (string?)PrivateValue(code, "encodeError");
        Assert.NotNull(error);
        Assert.Contains("check digit", error!, StringComparison.OrdinalIgnoreCase);

        // 原样取用编码器自己的句子，剥掉框架后缀的事不该由这里做。
        var encoderError = Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode("5901234123450", BarcodeType.Ean13));
        Assert.Equal(encoderError.Message, error);
    }

    [Theory]
    [InlineData(BarcodeType.Msi, "1234567", "12345674", "1234567", "12345674")]
    [InlineData(BarcodeType.Code39, "ABC-123", "ABC-123W", "*ABC-123*", "*ABC-123W*")]
    public async Task 可选校验字符默认不进打印行除非显式要求(BarcodeType type, string value, string encodedValue, string hidden, string shown)
    {
        var code = NewBarcode();
        code.Value = value;
        code.Type = type;
        code.AddChecksum = true;
        await EncodeAsync(code);

        Assert.Equal(encodedValue, code.Symbol!.Value);
        Assert.Contains($">{hidden}</text>", code.GetSvg()!, StringComparison.Ordinal);

        code.ShowChecksum = true;
        await EncodeAsync(code);
        Assert.Contains($">{shown}</text>", code.GetSvg()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 打印行随showvalue开关且独立文档按真实比例()
    {
        var code = NewBarcode();
        code.Value = "ABC123";
        await EncodeAsync(code);

        var symbol = code.Symbol!;
        var width = symbol.ModuleCount + (symbol.QuietZoneModules * 2);

        // ShowValue 默认开：打印行在内，条上留 8+1.5 文本带，高度 59.5，像素每模块 3。
        var svg = code.GetSvg()!;
        Assert.Contains($">ABC123</text>", svg, StringComparison.Ordinal);
        Assert.Contains($"viewBox=\"0 0 {width} 59.5\"", svg, StringComparison.Ordinal);
        Assert.Contains($"width=\"{width * 3}\" height=\"178.5\"", svg, StringComparison.Ordinal);

        code.ShowValue = false;
        await EncodeAsync(code);
        var bare = code.GetSvg()!;
        Assert.DoesNotContain("<text", bare, StringComparison.Ordinal);
        Assert.Contains($"viewBox=\"0 0 {width} 50\"", bare, StringComparison.Ordinal);
        Assert.Contains($"width=\"{width * 3}\" height=\"150\"", bare, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 零售码制无论开关都打印完整编号()
    {
        var code = NewBarcode();
        code.Value = "590123412345";
        code.Type = BarcodeType.Ean13;
        code.ShowChecksum = false;
        await EncodeAsync(code);

        // EAN 的四段文本落位是标准规定：编号含校验位逐段落纸。
        Assert.Equal("5901234123457", code.Symbol!.Value);
        var svg = code.GetSvg()!;
        foreach (var group in code.Symbol.TextGroups)
        {
            Assert.Contains($">{group.Text}</text>", svg, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task isbn把可引用编号移到条上方()
    {
        var code = NewBarcode();
        code.Value = "0306406152";
        code.Type = BarcodeType.Isbn;
        await EncodeAsync(code);

        var symbol = code.Symbol!;
        Assert.Equal("9780306406157", symbol.Value);
        var above = symbol.TextGroups
            .Where(g => g.Start <= 0 && g.End >= symbol.ModuleCount && g.Text.StartsWith("ISBN ", StringComparison.Ordinal))
            .Cast<BarcodeTextGroup?>()
            .FirstOrDefault();
        Assert.NotNull(above);

        // 上方带与下方带各占 9.5 单位：50+9.5+9.5=69，上方基线为 y=8。
        var svg = code.GetSvg()!;
        Assert.Contains($"viewBox=\"0 0 {symbol.ModuleCount + (symbol.QuietZoneModules * 2)} 69\"", svg, StringComparison.Ordinal);
        Assert.Contains($"y=\"8\">{WebUtility.HtmlEncode(above!.Value.Text)}</text>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 颜色一律经html编码()
    {
        var code = NewBarcode();
        code.Value = "ABC123";
        code.Foreground = "<b>";
        code.Background = "a\"b";
        await EncodeAsync(code);

        var svg = PageSvg(code)!;
        Assert.Contains($"fill=\"{WebUtility.HtmlEncode("<b>")}\" shape-rendering", svg, StringComparison.Ordinal);
        Assert.Contains($"height=\"100\" fill=\"{WebUtility.HtmlEncode("a\"b")}\"/>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnEncoded仅在符号真变化时触发()
    {
        var calls = new List<BarcodeSymbol?>();
        var code = NewBarcode();
        code.Value = "ABC123";
        code.OnEncoded = EventCallback.Factory.Create<BarcodeSymbol?>(this, calls.Add);

        await EncodeAsync(code);
        Assert.Single(calls);
        Assert.NotNull(calls[0]);

        // 同参数重跑：SVG 未变，不再触发，避免父组件弹回。
        await EncodeAsync(code);
        Assert.Single(calls);

        // 换成非法值：符号从有到无，以 null 触发一次。
        code.Value = "5901234123450";
        code.Type = BarcodeType.Ean13;
        await EncodeAsync(code);
        Assert.Equal(2, calls.Count);
        Assert.Null(calls[1]);

        await EncodeAsync(code);
        Assert.Equal(2, calls.Count);
    }
}
