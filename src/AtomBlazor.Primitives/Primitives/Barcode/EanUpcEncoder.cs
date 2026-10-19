using System.Globalization;

namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 零售码族：EAN-13、EAN-8、UPC-A，以及底层同为 EAN-13 的 ISBN 与 ISSN。
/// </summary>
/// <remarks>
/// 它们都是同一套机构：首尾和中央有护栏条，每位数字编码为 7 个模块，
/// 校验位以 1 与 3 交替加权算出。EAN-13 多一个小机关——
/// 它有 13 位数字却只有 12 位的位置，因此首位数字
/// 由左侧六位所用的奇偶校验图案来承载。
/// </remarks>
internal static class EanUpcEncoder
{
    // 每位数字 7 个模块。L 为奇校验，G 是 L 的镜像取反，R 是 L 取反。
    private static readonly string[] leftOdd =
    [
        "0001101", "0011001", "0010011", "0111101", "0100011",
        "0110001", "0101111", "0111011", "0110111", "0001011",
    ];

    private static readonly string[] leftEven =
    [
        "0100111", "0110011", "0011011", "0100001", "0011101",
        "0111001", "0000101", "0010001", "0001001", "0010111",
    ];

    private static readonly string[] right =
    [
        "1110010", "1100110", "1101100", "1000010", "1011100",
        "1001110", "1010000", "1000100", "1001000", "1110100",
    ];

    // 左侧六位数字中哪些用偶校验，由 EAN-13 的首位数字决定。
    private static readonly string[] parityPatterns =
    [
        "000000", "001011", "001101", "001110", "010011",
        "011001", "011100", "010101", "010110", "011010",
    ];

    private const string endGuard = "101";
    private const string centreGuard = "01010";

    /// <summary>护栏条比数字更长，因此印刷的数字恰好坐在它们之间。</summary>
    private const double dataBarBottom = 100.0 / 110.0;

    internal static BarcodeSymbol Encode(string value, BarcodeType type)
    {
        var digits = type switch
        {
            BarcodeType.Isbn => IsbnDigits(value),
            BarcodeType.Issn => IssnDigits(value),
            _ => BarcodeEncoder.DigitsOnly(value, type),
        };

        var length = type switch
        {
            BarcodeType.Ean8 => 8,
            BarcodeType.UpcA => 12,
            _ => 13,
        };

        digits = WithCheckDigit(digits, length, type);

        return type == BarcodeType.Ean8
            ? EncodeEan8(digits)
            : EncodeThirteen(digits, type, value);
    }

    /// <summary>
    /// 接受带或不带末尾校验位的值，带时予以校验。
    /// </summary>
    private static string WithCheckDigit(string digits, int length, BarcodeType type)
    {
        if (digits.Length == length - 1)
        {
            return digits + CheckDigit(digits);
        }

        if (digits.Length != length)
        {
            throw new BarcodeFormatException(
                $"{type} needs {length - 1} digits, or {length} with the check digit already on the end. This value has {digits.Length}.",
                nameof(digits));
        }

        var expected = CheckDigit(digits[..^1]);
        if (digits[^1] != expected)
        {
            throw new BarcodeFormatException(
                $"The check digit is wrong: {type} of {digits[..^1]} ends in {expected}, not {digits[^1]}. Leave the check digit off and it is worked out.",
                nameof(digits));
        }

        return digits;
    }

    /// <summary>
    /// 计算校验位，从右端起每隔一位加权三。
    /// </summary>
    /// <param name="withoutCheck">不含校验位的值。</param>
    /// <returns>以字符表示的校验位。</returns>
    internal static char CheckDigit(string withoutCheck)
    {
        var sum = 0;
        for (var i = 0; i < withoutCheck.Length; i++)
        {
            // 从右端起算，使权重无论长度如何都落在此起彼伏的同一侧。
            var weight = (withoutCheck.Length - i) % 2 == 1 ? 3 : 1;
            sum += (withoutCheck[i] - '0') * weight;
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }

    private static BarcodeSymbol EncodeThirteen(string digits, BarcodeType type, string original)
    {
        // UPC-A 就是首位为 0 的 EAN-13；如此编码并只显示十二位
        // 正是标准规定的做法。
        var thirteen = type == BarcodeType.UpcA ? "0" + digits : digits;
        var parity = parityPatterns[thirteen[0] - '0'];

        var builder = new BarcodeBuilder();
        builder.Modules(endGuard);
        var leftStart = builder.Position;

        // 在 UPC-A 上首位与末位数字印在护栏条外侧，因此这两处数据条
        // 也保持全高，整个号码读起来是一行。
        var outerDescend = type == BarcodeType.UpcA;

        for (var i = 0; i < 6; i++)
        {
            var digit = thirteen[i + 1] - '0';
            var bottom = outerDescend && i == 0 ? 1 : dataBarBottom;
            builder.Modules(parity[i] == '1' ? leftEven[digit] : leftOdd[digit], bottom);
        }

        var leftEnd = builder.Position;
        builder.Modules(centreGuard);
        var rightStart = builder.Position;

        for (var i = 0; i < 6; i++)
        {
            var bottom = outerDescend && i == 5 ? 1 : dataBarBottom;
            builder.Modules(right[thirteen[i + 7] - '0'], bottom);
        }

        var rightEnd = builder.Position;
        builder.Modules(endGuard);

        var text = type switch
        {
            BarcodeType.UpcA =>
            [
                new BarcodeTextGroup(digits[..1], -9, -1),
                new BarcodeTextGroup(digits[1..6], leftStart + 7, leftEnd),
                new BarcodeTextGroup(digits[6..11], rightStart, rightEnd - 7),
                new BarcodeTextGroup(digits[11..], builder.Position + 1, builder.Position + 9),
            ],
            _ => new List<BarcodeTextGroup>
            {
                new(thirteen[..1], -9, -1),
                new(thirteen[1..7], leftStart, leftEnd),
                new(thirteen[7..], rightStart, rightEnd),
            },
        };

        if (type is BarcodeType.Isbn or BarcodeType.Issn)
        {
            // 书号或刊号的文本形态印在条上方，因为
            // 其下方的 13 位数字是商品代码，而非人们引用的那个号。
            text.Insert(0, new BarcodeTextGroup(
                type == BarcodeType.Isbn ? FormatIsbn(thirteen) : FormatIssn(original, thirteen),
                0,
                builder.Position));
        }

        return new BarcodeSymbol(type, digits, builder.Position, 11, builder.ToBars(), text);
    }

    private static BarcodeSymbol EncodeEan8(string digits)
    {
        var builder = new BarcodeBuilder();
        builder.Modules(endGuard);
        var leftStart = builder.Position;

        for (var i = 0; i < 4; i++)
        {
            builder.Modules(leftOdd[digits[i] - '0'], dataBarBottom);
        }

        var leftEnd = builder.Position;
        builder.Modules(centreGuard);
        var rightStart = builder.Position;

        for (var i = 4; i < 8; i++)
        {
            builder.Modules(right[digits[i] - '0'], dataBarBottom);
        }

        var rightEnd = builder.Position;
        builder.Modules(endGuard);

        List<BarcodeTextGroup> text =
        [
            new(digits[..4], leftStart, leftEnd),
            new(digits[4..], rightStart, rightEnd),
        ];

        return new BarcodeSymbol(BarcodeType.Ean8, digits, builder.Position, 7, builder.ToBars(), text);
    }

    /// <summary>
    /// 把 ISBN（10 位或 13 位，连字符可选）转换为
    /// EAN-13 所需的 12 位数字。
    /// </summary>
    private static string IsbnDigits(string value)
    {
        var cleaned = Strip(value);

        // 10 位 ISBN 的末位可以是 X，且它的校验位算法与
        // EAN 的不同，因此直接丢弃而不带过去。
        if (cleaned.Length == 10 && cleaned[..9].All(char.IsAsciiDigit))
        {
            return "978" + cleaned[..9];
        }

        if (cleaned.Length is 12 or 13
            && cleaned.All(char.IsAsciiDigit)
            && (cleaned.StartsWith("978", StringComparison.Ordinal) || cleaned.StartsWith("979", StringComparison.Ordinal)))
        {
            return cleaned;
        }

        throw new BarcodeFormatException(
            $"An ISBN is 10 digits, or 13 beginning 978 or 979, with or without hyphens. This value is \"{value}\".",
            nameof(value));
    }

    /// <summary>
    /// 把 8 位 ISSN 加上 977 前缀转换为 EAN-13 所需的 12 位数字。
    /// </summary>
    private static string IssnDigits(string value)
    {
        var cleaned = Strip(value);

        // ISSN 自身的校验位被丢弃：EAN 码有自己的校验位，中间两个零
        // 是变体码，除非出版商另有指定，否则为零。
        if (cleaned.Length == 8 && cleaned[..7].All(char.IsAsciiDigit))
        {
            return "977" + cleaned[..7] + "00";
        }

        if (cleaned.Length is 12 or 13 && cleaned.All(char.IsAsciiDigit) && cleaned.StartsWith("977", StringComparison.Ordinal))
        {
            return cleaned;
        }

        throw new BarcodeFormatException(
            $"An ISSN is 8 characters, with or without a hyphen. This value is \"{value}\".",
            nameof(value));
    }

    private static string Strip(string value) =>
        new([.. value.Where(c => c is not ('-' or ' '))]);

    /// <summary>
    /// 在 13 位 ISBN 仅有的两个恒定边界处插入连字符。
    /// </summary>
    /// <remarks>
    /// 注册组及其内部的出版者由一张多达数千区间的表来划分，
    /// 且新区间发行时表会随之变化，因此前缀与校验位之间的
    /// 各段保留为连续串，不作猜测。
    /// </remarks>
    private static string FormatIsbn(string thirteen) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"ISBN {thirteen[..3]}-{thirteen[3..^1]}-{thirteen[^1]}");

    private static string FormatIssn(string original, string thirteen)
    {
        var cleaned = Strip(original);
        var body = cleaned.Length == 8 ? cleaned : thirteen[3..10] + IssnCheckCharacter(thirteen[3..10]);

        return string.Create(CultureInfo.InvariantCulture, $"ISSN {body[..4]}-{body[4..]}");
    }

    /// <summary>
    /// 计算 ISSN 自身的校验字符，采用模 11 算法，因此可能是 X。
    /// </summary>
    private static char IssnCheckCharacter(string seven)
    {
        var sum = 0;
        for (var i = 0; i < 7; i++)
        {
            sum += (seven[i] - '0') * (8 - i);
        }

        var remainder = (11 - (sum % 11)) % 11;
        return remainder == 10 ? 'X' : (char)('0' + remainder);
    }
}
