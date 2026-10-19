namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 三种以数字为主的码制，除了年头久以外毫无共同点：Interleaved 2 of 5、
/// Codabar 与 MSI。
/// </summary>
internal static class ItfCodabarMsiEncoder
{
    /// <summary>
    /// 每位数字五个单元，其中两个宽。交错编码让一个数字由条承载、
    /// 下一个由空承载——致密度与偶数长度规则都由它而来。
    /// </summary>
    private static readonly string[] itfPatterns =
    [
        "00110", "10001", "01001", "11000", "00101",
        "10100", "01100", "00011", "10010", "01010",
    ];

    private const string codabarCharset = "0123456789-$:/.+";
    private const string codabarDelimiters = "ABCD";

    /// <summary>每个字符七个单元，条先行、深浅交替；<c>1</c> 为宽。</summary>
    private static readonly string[] codabarPatterns =
    [
        "0000011", "0000110", "0001001", "1100000", "0010010",
        "1000010", "0100001", "0100100", "0110000", "1001000",
        "0001100", "0011000", "1000101", "1010001", "1010100", "0010101",
    ];

    private static readonly string[] codabarDelimiterPatterns =
    [
        "0011010", "0101001", "0001011", "0001110",
    ];

    internal static BarcodeSymbol EncodeItf(string value, BarcodeType type, bool addChecksum)
    {
        var digits = BarcodeEncoder.DigitsOnly(value, type);

        if (addChecksum)
        {
            digits += EanUpcEncoder.CheckDigit(digits);
        }

        // 数字按两位一组消费，因此奇数个时前面补零而非报错。
        if (digits.Length % 2 == 1)
        {
            digits = "0" + digits;
        }

        if (digits.Length == 0)
        {
            throw new BarcodeFormatException("Interleaved 2 of 5 needs at least two digits.", nameof(value));
        }

        var builder = new BarcodeBuilder();

        // 起始：四个窄单元。终止：一根宽条、一个窄空和一根窄条。
        builder.NarrowWide("0000", 1, 3);

        for (var i = 0; i < digits.Length; i += 2)
        {
            var bars = itfPatterns[digits[i] - '0'];
            var spaces = itfPatterns[digits[i + 1] - '0'];

            for (var element = 0; element < 5; element++)
            {
                builder.Bar(bars[element] == '1' ? 3 : 1);
                builder.Space(spaces[element] == '1' ? 3 : 1);
            }
        }

        builder.Bar(3);
        builder.Space(1);
        builder.Bar(1);

        return new BarcodeSymbol(
            type,
            digits,
            builder.Position,
            10,
            builder.ToBars(),
            [new BarcodeTextGroup(digits, 0, builder.Position)]);
    }

    internal static BarcodeSymbol EncodeCodabar(string value, bool addChecksum)
    {
        var upper = value.ToUpperInvariant();

        // 起止字母是值的一部分。不给的话会自动补上。
        var start = 'A';
        var stop = 'A';
        var body = upper;

        if (upper.Length >= 2
            && codabarDelimiters.Contains(upper[0], StringComparison.Ordinal)
            && codabarDelimiters.Contains(upper[^1], StringComparison.Ordinal))
        {
            start = upper[0];
            stop = upper[^1];
            body = upper[1..^1];
        }

        foreach (var c in body)
        {
            if (!codabarCharset.Contains(c, StringComparison.Ordinal))
            {
                throw new BarcodeFormatException(
                    $"Codabar holds digits and - $ : / . +, wrapped in a start and stop letter from A to D. This value contains '{c}'.",
                    nameof(value));
            }
        }

        if (addChecksum)
        {
            var sum = body.Sum(c => codabarCharset.IndexOf(c, StringComparison.Ordinal));
            body += codabarCharset[(16 - (sum % 16)) % 16];
        }

        var builder = new BarcodeBuilder();
        builder.NarrowWide(codabarDelimiterPatterns[codabarDelimiters.IndexOf(start, StringComparison.Ordinal)], 1, 2);

        foreach (var c in body)
        {
            builder.Space(1);
            builder.NarrowWide(codabarPatterns[codabarCharset.IndexOf(c, StringComparison.Ordinal)], 1, 2);
        }

        builder.Space(1);
        builder.NarrowWide(codabarDelimiterPatterns[codabarDelimiters.IndexOf(stop, StringComparison.Ordinal)], 1, 2);

        var encoded = $"{start}{body}{stop}";

        return new BarcodeSymbol(
            BarcodeType.Codabar,
            encoded,
            builder.Position,
            10,
            builder.ToBars(),
            [new BarcodeTextGroup(encoded, 0, builder.Position)]);
    }

    internal static BarcodeSymbol EncodeMsi(string value, bool addChecksum)
    {
        var digits = BarcodeEncoder.DigitsOnly(value, BarcodeType.Msi);

        if (digits.Length == 0)
        {
            throw new BarcodeFormatException("MSI needs at least one digit.", nameof(value));
        }

        if (addChecksum)
        {
            digits += MsiCheckDigit(digits);
        }

        var builder = new BarcodeBuilder();

        // 起始：一根宽条加一个窄空，与置位的比特形状相同。
        builder.Bar(2);
        builder.Space(1);

        foreach (var digit in digits)
        {
            var bits = digit - '0';

            // 四个比特，高位先行。1 是一根宽条加一个窄空；0 相反，
            // 于是无论比特值如何，每位都占三个模块。
            for (var bit = 3; bit >= 0; bit--)
            {
                if (((bits >> bit) & 1) == 1)
                {
                    builder.Bar(2);
                    builder.Space(1);
                }
                else
                {
                    builder.Bar(1);
                    builder.Space(2);
                }
            }
        }

        // 终止：一根窄条、一个宽空和一根窄条。
        builder.Bar(1);
        builder.Space(2);
        builder.Bar(1);

        return new BarcodeSymbol(
            BarcodeType.Msi,
            digits,
            builder.Position,
            12,
            builder.ToBars(),
            [new BarcodeTextGroup(digits, 0, builder.Position)]);
    }

    /// <summary>
    /// 计算 MSI 模 10 校验位：从右端起每隔一位加倍、
    /// 再把结果的各位数字相加。
    /// </summary>
    private static char MsiCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var value = digits[^(i + 1)] - '0';
            if (i % 2 == 0)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }

            sum += value;
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }
}
