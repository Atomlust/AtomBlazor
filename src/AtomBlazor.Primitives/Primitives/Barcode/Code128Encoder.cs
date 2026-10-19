namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// Code 128：任意 ASCII 字符，线性码中密度最高。
/// </summary>
/// <remarks>
/// <para>
/// 每个符号字符由三根条与三个空共 11 个模块组成。它之所以致密，
/// 在于码制 C 把一对数字压进一个字符，于是一段数字只花
/// 其他码制一半的宽度。编码器随进度在各码制间切换，并在
/// 数字段结束时切换回来。
/// </para>
/// <para>
/// 码制 A 覆盖控制字符，码制 B 覆盖小写字母。编码器从数据需要的
/// 码制起步，只有必要时才花一个字符去切换。
/// </para>
/// </remarks>
internal static class Code128Encoder
{
    /// <summary>符号值 0 到 106 的单元宽度，每个均为三条三空。</summary>
    private static readonly string[] patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112",
    ];

    private const int codeC = 99;
    private const int codeB = 100;
    private const int codeA = 101;
    private const int startA = 103;
    private const int startB = 104;
    private const int startC = 105;
    private const int stop = 106;

    internal static BarcodeSymbol Encode(string value)
    {
        foreach (var c in value)
        {
            if (c > 127)
            {
                throw new BarcodeFormatException(
                    $"Code 128 holds ASCII only, and this value contains '{c}'. Use a QR code for anything beyond that.",
                    nameof(value));
            }
        }

        var symbols = BuildSymbols(value);

        // 校验字符按位置为每个符号加权，起始字符权重为一。
        var sum = symbols[0];
        for (var i = 1; i < symbols.Count; i++)
        {
            sum += symbols[i] * i;
        }

        symbols.Add(sum % 103);
        symbols.Add(stop);

        var builder = new BarcodeBuilder();
        foreach (var symbol in symbols)
        {
            builder.Widths(patterns[symbol]);
        }

        return new BarcodeSymbol(
            BarcodeType.Code128,
            value,
            builder.Position,
            10,
            builder.ToBars(),
            [new BarcodeTextGroup(value, 0, builder.Position)]);
    }

    private static List<int> BuildSymbols(string value)
    {
        var symbols = new List<int>();
        var position = 0;

        // 四个及以上的数字立刻值得切入码制 C；恰好两个且
        // 整个值正好装进一个字符时亦然。
        var digitRun = DigitRun(value, 0);
        var mode = digitRun >= 4 || (digitRun == value.Length && digitRun == 2) ? codeC
            : NeedsSetA(value, 0) ? codeA
            : codeB;

        symbols.Add(mode switch { codeC => startC, codeA => startA, _ => startB });

        while (position < value.Length)
        {
            if (mode == codeC)
            {
                if (position + 1 < value.Length && char.IsAsciiDigit(value[position]) && char.IsAsciiDigit(value[position + 1]))
                {
                    symbols.Add(int.Parse(value.AsSpan(position, 2), System.Globalization.CultureInfo.InvariantCulture));
                    position += 2;
                    continue;
                }

                mode = NeedsSetA(value, position) ? codeA : codeB;
                symbols.Add(mode);
                continue;
            }

            // 只有数字段长到足以抵回切换字符的成本时才回到码制 C。
            var run = DigitRun(value, position);
            if (run >= 6 || (run >= 2 && position + run == value.Length && run % 2 == 0))
            {
                // 奇数长度段晚一个字符切入，好让两位一组对齐。
                if (run % 2 == 1)
                {
                    symbols.Add(SymbolFor(value[position], mode));
                    position++;
                }

                mode = codeC;
                symbols.Add(codeC);
                continue;
            }

            if (mode == codeB && value[position] < ' ')
            {
                mode = codeA;
                symbols.Add(codeA);
                continue;
            }

            if (mode == codeA && value[position] > '_')
            {
                mode = codeB;
                symbols.Add(codeB);
                continue;
            }

            symbols.Add(SymbolFor(value[position], mode));
            position++;
        }

        return symbols;
    }

    /// <summary>
    /// 获取某位置的字符是否只能用码制 A 书写。
    /// </summary>
    private static bool NeedsSetA(string value, int position) =>
        position < value.Length && value[position] < ' ';

    private static int DigitRun(string value, int start)
    {
        var run = 0;
        while (start + run < value.Length && char.IsAsciiDigit(value[start + run]))
        {
            run++;
        }

        return run;
    }

    /// <summary>
    /// 把 ASCII 字符映射到码制 A 或码制 B 中的符号值。
    /// </summary>
    private static int SymbolFor(char c, int mode)
    {
        if (mode == codeA)
        {
            // 码制 A 从空格排到下划线，其后是 64 起的控制字符。
            return c < ' ' ? c + 64 : c - ' ';
        }

        return c - ' ';
    }
}
