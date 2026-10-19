namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 以条高而非条宽承载数据的码制：POSTNET、皇家邮政
/// 四状态码，以及同列于此的 Laetus Pharmacode。
/// </summary>
/// <remarks>
/// 邮政分拣机读的是运动中的信封，因此这些码制让所有条保持同一间距、
/// 改以形状变化承载信息。这比改变条宽更能扛住传送带造成的拖影。
/// 对渲染器的实际后果是：压扁条高就会毁坏数据。
/// </remarks>
internal static class PostalEncoder
{
    /// <summary>每位数字五根条，其中两根为长条。</summary>
    private static readonly string[] postnetPatterns =
    [
        "11000", "00011", "00101", "00110", "01001",
        "01010", "01100", "10001", "10010", "10100",
    ];

    /// <summary>POSTNET 符号的短条从半高处起画。</summary>
    private const double postnetShortTop = 0.5;

    /// <summary>
    /// 每个字符四根条。每根条是四种形状之一，这里写成它贡献的
    /// 那一对比特：第一位给上半部的上伸，第二位给下半部的下伸。
    /// </summary>
    private static readonly string rm4sccCharset =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// 四状态码的四种条形，按表中索引顺序：导引条、
    /// 上伸条、下伸条、全高条。
    /// </summary>
    private static readonly (double Top, double Bottom)[] rm4sccShapes =
    [
        (0.375, 0.625),
        (0, 0.625),
        (0.375, 1),
        (0, 1),
    ];

    /// <summary>四取二（two-of-four）取值，按标准一至六的编号顺序。</summary>
    private static readonly int[] twoOfFour = [0b0011, 0b0101, 0b0110, 0b1001, 0b1010, 0b1100];

    /// <summary>
    /// 每个字符的四根条，取 <see cref="rm4sccShapes"/> 的形状索引。
    /// </summary>
    /// <remarks>
    /// 这张表是程序构建而非手写的：皇家邮政码把字符编在
    /// 六乘六的网格里，字符的条就是行号与列号以四取二码写出的形状，
    /// 行驱动上伸部分、列驱动下伸部分。之所以声明在
    /// <see cref="twoOfFour"/> 之后，是因为静态字段按源码顺序初始化。
    /// </remarks>
    private static readonly int[][] rm4sccPatterns = BuildRm4sccPatterns();

    internal static BarcodeSymbol EncodePostnet(string value)
    {
        var digits = BarcodeEncoder.DigitsOnly(value, BarcodeType.Postnet);

        if (digits.Length is not (5 or 9 or 11))
        {
            throw new BarcodeFormatException(
                $"POSTNET carries a 5-digit ZIP, a 9-digit ZIP+4 or an 11-digit delivery point code. This value has {digits.Length} digits.",
                nameof(value));
        }

        // 校验位把各位数字之和补足到下一个十的倍数。
        var sum = digits.Sum(d => d - '0');
        digits += (char)('0' + ((10 - (sum % 10)) % 10));

        var builder = new BarcodeBuilder();
        AppendPostnetBar(builder, tall: true);

        foreach (var digit in digits)
        {
            foreach (var bar in postnetPatterns[digit - '0'])
            {
                AppendPostnetBar(builder, bar == '1');
            }
        }

        AppendPostnetBar(builder, tall: true);

        return new BarcodeSymbol(
            BarcodeType.Postnet,
            digits,
            builder.Position - 1,
            9,
            builder.ToBars(),
            [new BarcodeTextGroup(digits, 0, builder.Position - 1)]);
    }

    private static void AppendPostnetBar(BarcodeBuilder builder, bool tall)
    {
        builder.Bar(1, tall ? 0 : postnetShortTop, 1);

        // 条以两个模块为间距排布：一条一隙。
        builder.Space(1);
    }

    internal static BarcodeSymbol EncodeRm4scc(string value)
    {
        var upper = value.ToUpperInvariant();

        foreach (var c in upper)
        {
            if (!rm4sccCharset.Contains(c, StringComparison.Ordinal))
            {
                throw new BarcodeFormatException(
                    $"The Royal Mail 4-state code holds digits and upper-case letters, and this value contains '{c}'.",
                    nameof(value));
            }
        }

        if (upper.Length == 0)
        {
            throw new BarcodeFormatException("The Royal Mail 4-state code needs at least one character.", nameof(value));
        }

        var builder = new BarcodeBuilder();

        // 起始条是上伸条、终止条是全高条，分拣机正是据此判断
        // 信封是以哪个方向通过传送的。
        AppendRm4sccBar(builder, 1);

        foreach (var c in upper)
        {
            AppendRm4sccCharacter(builder, rm4sccPatterns[rm4sccCharset.IndexOf(c, StringComparison.Ordinal)]);
        }

        var check = Rm4sccCheckCharacter(upper);
        AppendRm4sccCharacter(builder, rm4sccPatterns[rm4sccCharset.IndexOf(check, StringComparison.Ordinal)]);

        AppendRm4sccBar(builder, 3);

        return new BarcodeSymbol(
            BarcodeType.Rm4scc,
            upper + check,
            builder.Position - 1,
            6,
            builder.ToBars(),
            [new BarcodeTextGroup(upper + check, 0, builder.Position - 1)]);
    }

    private static void AppendRm4sccCharacter(BarcodeBuilder builder, int[] shapes)
    {
        foreach (var shape in shapes)
        {
            AppendRm4sccBar(builder, shape);
        }
    }

    private static void AppendRm4sccBar(BarcodeBuilder builder, int shape)
    {
        var (top, bottom) = rm4sccShapes[shape];
        builder.Bar(1, top, bottom);
        builder.Space(1);
    }

    /// <summary>
    /// 把每个字符的行号与列号各自求和，据此算出校验字符。
    /// </summary>
    private static char Rm4sccCheckCharacter(string value)
    {
        var rowSum = 0;
        var columnSum = 0;

        foreach (var c in value)
        {
            var index = rm4sccCharset.IndexOf(c, StringComparison.Ordinal);
            rowSum += (index / 6) + 1;
            columnSum += (index % 6) + 1;
        }

        var row = ((rowSum - 1) % 6) + 1;
        var column = ((columnSum - 1) % 6) + 1;

        return rm4sccCharset[((row - 1) * 6) + (column - 1)];
    }

    private static int[][] BuildRm4sccPatterns()
    {
        var patterns = new int[36][];

        for (var index = 0; index < 36; index++)
        {
            var ascenders = twoOfFour[index / 6];
            var descenders = twoOfFour[index % 6];
            var bars = new int[4];

            for (var bar = 0; bar < 4; bar++)
            {
                // 高位先行，因此 0 号条在最左侧。
                var shift = 3 - bar;
                var hasAscender = ((ascenders >> shift) & 1) == 1;
                var hasDescender = ((descenders >> shift) & 1) == 1;

                bars[bar] = (hasAscender, hasDescender) switch
                {
                    (false, false) => 0,
                    (true, false) => 1,
                    (false, true) => 2,
                    _ => 3,
                };
            }

            patterns[index] = bars;
        }

        return patterns;
    }

    internal static BarcodeSymbol EncodePharmacode(string value)
    {
        if (!int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
            || number is < 3 or > 131070)
        {
            throw new BarcodeFormatException(
                $"Pharmacode carries a whole number from 3 to 131070, and this value is \"{value}\".",
                nameof(value));
        }

        // 该数字以二进制自低位向上读取，因此条序是倒着产出的。
        var widths = new List<int>();
        var remaining = number;
        while (remaining > 0)
        {
            if (remaining % 2 == 0)
            {
                widths.Add(3);
                remaining = (remaining - 2) / 2;
            }
            else
            {
                widths.Add(1);
                remaining = (remaining - 1) / 2;
            }
        }

        widths.Reverse();

        var builder = new BarcodeBuilder();
        for (var i = 0; i < widths.Count; i++)
        {
            if (i > 0)
            {
                builder.Space(2);
            }

            builder.Bar(widths[i]);
        }

        return new BarcodeSymbol(
            BarcodeType.Pharmacode,
            number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            builder.Position,
            6,
            builder.ToBars(),
            [new BarcodeTextGroup(number.ToString(System.Globalization.CultureInfo.InvariantCulture), 0, builder.Position)]);
    }
}
