using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Barcode;
using Xunit;

namespace AtomBlazor.Tests.Barcode;

/// <summary>
/// 自 Barcode/BarcodeEncoderTests.cs（七规则机械改名 →AtomBlazor、注释汉化；金样与断言字符串字面量逐字节保原样，方法名保英）。
/// <para>
/// 校验、校验位与结构。精确的条杠在 <see cref="BarcodeEncoderGoldenTests"/> 中与 zint 交叉校验。
/// </para>
/// <remarks>
/// 每一次拒绝都是 <see cref="BarcodeFormatException"/>，它是一种
/// <see cref="ArgumentException"/>，其消息就是编码器自己的那句话，别无其他。
/// 这些消息的措辞在 <see cref="BarcodeMessageTests"/> 中覆盖。
/// </remarks>
public class BarcodeEncoderTests
{
    public static TheoryData<BarcodeType> AllTypes() =>
        [.. Enum.GetValues<BarcodeType>()];

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void RejectsAnEmptyValue(BarcodeType type)
    {
        Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode(string.Empty, type));
        Assert.Throws<ArgumentNullException>(() => BarcodeEncoder.Encode(null!, type));
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void DescribesWhatEveryTypeAccepts(BarcodeType type) =>
        Assert.False(string.IsNullOrWhiteSpace(BarcodeEncoder.DescribeInput(type)));

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void ProducesBarsThatSitInsideTheReportedWidth(BarcodeType type)
    {
        var symbol = BarcodeEncoder.Encode(SampleFor(type), type);

        Assert.NotEmpty(symbol.Bars);
        Assert.True(symbol.QuietZoneModules > 0);
        Assert.Equal(type, symbol.Type);

        foreach (var bar in symbol.Bars)
        {
            Assert.True(bar.Width > 0);
            Assert.True(bar.Start >= 0);
            Assert.True(bar.Start + bar.Width <= symbol.ModuleCount);
            Assert.True(bar.Top >= 0 && bar.Top < bar.Bottom && bar.Bottom <= 1);
        }

        // 条杠从不重叠，且自左向右排列。
        for (var i = 1; i < symbol.Bars.Count; i++)
        {
            Assert.True(symbol.Bars[i].Start >= symbol.Bars[i - 1].Start + symbol.Bars[i - 1].Width);
        }
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void SameInputAlwaysProducesTheSameBars(BarcodeType type)
    {
        var first = BarcodeEncoder.Encode(SampleFor(type), type);
        var second = BarcodeEncoder.Encode(SampleFor(type), type);

        Assert.Equal(first.Value, second.Value);
        Assert.Equal(first.ModuleCount, second.ModuleCount);
        Assert.Equal(first.Bars, second.Bars);
    }

    [Theory]
    [InlineData(BarcodeType.Postnet)]
    [InlineData(BarcodeType.Rm4scc)]
    public void PostalCodesCarryTheirDataInTheHeights(BarcodeType type)
    {
        var symbol = BarcodeEncoder.Encode(SampleFor(type), type);

        Assert.True(symbol.IsHeightModulated);
        Assert.True(symbol.Bars.Select(b => (b.Top, b.Bottom)).Distinct().Count() > 1);
        Assert.Single(symbol.Bars.Select(b => b.Width).Distinct());
    }

    [Theory]
    [InlineData(BarcodeType.Code128)]
    [InlineData(BarcodeType.Code39)]
    [InlineData(BarcodeType.Itf)]
    [InlineData(BarcodeType.Codabar)]
    [InlineData(BarcodeType.Msi)]
    [InlineData(BarcodeType.Telepen)]
    [InlineData(BarcodeType.Pharmacode)]
    public void EverythingElseCarriesItInTheWidths(BarcodeType type)
    {
        var symbol = BarcodeEncoder.Encode(SampleFor(type), type);

        Assert.False(symbol.IsHeightModulated);
        Assert.All(symbol.Bars, b => Assert.Equal((0d, 1d), (b.Top, b.Bottom)));
    }

    [Theory]
    [InlineData("590123412345", "5901234123457")]
    [InlineData("400638133393", "4006381333931")]
    [InlineData("978030640615", "9780306406157")]
    public void Ean13WorksOutTheCheckDigit(string input, string expected) =>
        Assert.Equal(expected, BarcodeEncoder.Encode(input, BarcodeType.Ean13).Value);

    [Fact]
    public void Ean13AcceptsAValueThatAlreadyHasItsCheckDigit() =>
        Assert.Equal("5901234123457", BarcodeEncoder.Encode("5901234123457", BarcodeType.Ean13).Value);

    [Fact]
    public void Ean13RejectsAWrongCheckDigit()
    {
        var error = Assert.Throws<BarcodeFormatException>(
            () => BarcodeEncoder.Encode("5901234123450", BarcodeType.Ean13));

        Assert.Contains("check digit", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("9638507", "96385074")]
    [InlineData("96385074", "96385074")]
    public void Ean8WorksOutTheCheckDigit(string input, string expected) =>
        Assert.Equal(expected, BarcodeEncoder.Encode(input, BarcodeType.Ean8).Value);

    [Theory]
    [InlineData("03600029145", "036000291452")]
    [InlineData("036000291452", "036000291452")]
    public void UpcAWorksOutTheCheckDigit(string input, string expected) =>
        Assert.Equal(expected, BarcodeEncoder.Encode(input, BarcodeType.UpcA).Value);

    [Theory]
    [InlineData("11")]
    [InlineData("1234567890")]
    [InlineData("59012341234567")]
    public void Ean13RejectsTheWrongLength(string value) =>
        Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode(value, BarcodeType.Ean13));

    [Theory]
    [InlineData("0306406152")]
    [InlineData("978-0-306-40615-7")]
    [InlineData("9780306406157")]
    public void IsbnAcceptsEitherLengthWithOrWithoutHyphens(string value)
    {
        var symbol = BarcodeEncoder.Encode(value, BarcodeType.Isbn);

        Assert.Equal("9780306406157", symbol.Value);

        // 可引用的编号在条杠上方；产品码在其下方。
        Assert.Contains(symbol.TextGroups, g => g.Text.StartsWith("ISBN ", StringComparison.Ordinal));
    }

    [Fact]
    public void IssnBecomesAnEan13UnderThe977Prefix()
    {
        var symbol = BarcodeEncoder.Encode("0378-5955", BarcodeType.Issn);

        Assert.Equal("9770378595002", symbol.Value);
        Assert.Contains(symbol.TextGroups, g => g.Text == "ISSN 0378-5955");
    }

    [Fact]
    public void ItfPadsAnOddNumberOfDigits() =>
        Assert.Equal("0123", BarcodeEncoder.Encode("123", BarcodeType.Itf).Value);

    [Fact]
    public void Code39AddsItsCheckCharacterOnlyWhenAsked()
    {
        Assert.Equal("ABC-123", BarcodeEncoder.Encode("ABC-123", BarcodeType.Code39).Value);
        Assert.Equal("ABC-123W", BarcodeEncoder.Encode("ABC-123", BarcodeType.Code39, addChecksum: true).Value);
    }

    [Fact]
    public void Code39UppercasesItsInput() =>
        Assert.Equal("ABC", BarcodeEncoder.Encode("abc", BarcodeType.Code39).Value);

    [Theory]
    [InlineData("ABC!")]
    [InlineData("A_B")]
    [InlineData("caf\u00e9")]
    public void Code39RejectsCharactersItCannotCarry(string value) =>
        Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode(value, BarcodeType.Code39));

    [Fact]
    public void Code128RejectsAnythingBeyondAscii()
    {
        var error = Assert.Throws<BarcodeFormatException>(
            () => BarcodeEncoder.Encode("café", BarcodeType.Code128));

        Assert.Contains("ASCII", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Code128PacksDigitsMoreTightlyThanLetters()
    {
        var digits = BarcodeEncoder.Encode("12345678", BarcodeType.Code128).ModuleCount;
        var letters = BarcodeEncoder.Encode("ABCDEFGH", BarcodeType.Code128).ModuleCount;

        Assert.True(digits < letters);
    }

    [Fact]
    public void CodabarSuppliesStartAndStopLettersWhenTheyAreMissing()
    {
        Assert.Equal("A123456A", BarcodeEncoder.Encode("123456", BarcodeType.Codabar).Value);
        Assert.Equal("B123456C", BarcodeEncoder.Encode("B123456C", BarcodeType.Codabar).Value);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("1234567890")]
    public void PostnetRejectsAnythingButFiveNineOrElevenDigits(string value) =>
        Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode(value, BarcodeType.Postnet));

    [Theory]
    [InlineData("12345", "123455")]
    [InlineData("555551237", "5555512372")]
    public void PostnetAddsTheDigitThatRoundsTheSumToTen(string value, string expected)
    {
        var symbol = BarcodeEncoder.Encode(value, BarcodeType.Postnet);

        Assert.Equal(expected, symbol.Value);
        Assert.Equal(0, symbol.Value.Sum(d => d - '0') % 10);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("131071")]
    [InlineData("0")]
    [InlineData("not a number")]
    public void PharmacodeRejectsAnythingOutsideThreeToOneThreeOneZeroSeventy(string value) =>
        Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode(value, BarcodeType.Pharmacode));

    [Theory]
    [InlineData(3)]
    [InlineData(1234)]
    [InlineData(131070)]
    public void PharmacodeReadsBackAsTheNumberItEncoded(int number)
    {
        var symbol = BarcodeEncoder.Encode(
            number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BarcodeType.Pharmacode);

        // 解码即倒着跑编码：细杠计一、粗杠计二
        // 再按自右向左的位置加权。
        var total = 0;
        for (var i = 0; i < symbol.Bars.Count; i++)
        {
            var thick = symbol.Bars[i].Width > 1;
            total += (thick ? 2 : 1) << (symbol.Bars.Count - 1 - i);
        }

        Assert.Equal(number, total);
    }

    [Fact]
    public void Rm4sccAddsItsCheckCharacter() =>
        Assert.Equal("SN34RD1AK", BarcodeEncoder.Encode("SN34RD1A", BarcodeType.Rm4scc).Value);

    [Fact]
    public void Rm4sccUsesAllFourBarShapes()
    {
        var symbol = BarcodeEncoder.Encode("SN34RD1A", BarcodeType.Rm4scc);

        Assert.Equal(4, symbol.Bars.Select(b => (b.Top, b.Bottom)).Distinct().Count());
    }

    [Fact]
    public void MsiAddsItsCheckDigitOnlyWhenAsked()
    {
        Assert.Equal("1234567", BarcodeEncoder.Encode("1234567", BarcodeType.Msi).Value);
        Assert.Equal("12345674", BarcodeEncoder.Encode("1234567", BarcodeType.Msi, addChecksum: true).Value);
    }

    [Fact]
    public void TelepenGivesEveryCharacterTheSameWidth()
    {
        var one = BarcodeEncoder.Encode("A", BarcodeType.Telepen).ModuleCount;
        var two = BarcodeEncoder.Encode("AB", BarcodeType.Telepen).ModuleCount;

        Assert.Equal(16, two - one);
    }

    [Fact]
    public void TelepenRejectsAnythingBeyondAscii() =>
        Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode("café", BarcodeType.Telepen));

    /// <summary>
    /// 每种码制都能接受的一个值，供跨全部码制运行的测试使用。
    /// </summary>
    private static string SampleFor(BarcodeType type) => type switch
    {
        BarcodeType.Ean13 => "590123412345",
        BarcodeType.Ean8 => "9638507",
        BarcodeType.UpcA => "03600029145",
        BarcodeType.Isbn => "9780306406157",
        BarcodeType.Issn => "0378-5955",
        BarcodeType.Postnet => "555551237",
        BarcodeType.Pharmacode => "1234",
        BarcodeType.Rm4scc => "SN34RD1A",
        BarcodeType.Itf => "1234567890",
        BarcodeType.Codabar => "A123456A",
        BarcodeType.Msi => "1234567",
        _ => "ABC123",
    };
}
