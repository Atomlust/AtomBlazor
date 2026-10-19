namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// Code 39：数字、大写字母和少量符号，首尾以星号包裹。
/// </summary>
/// <remarks>
/// 每个字符由九个单元组成——五根条和四个空——其中恰好三个是宽的。
/// 这种自校验形态是它能挺过劣质印刷的原因，也是同样数据下它
/// 比 <see cref="Code128Encoder"/> 宽约三倍的原因。其校验字符可选，
/// 默认仍为关闭，因为多数扫码器并未被配置为期望校验字符。
/// </remarks>
internal static class Code39Encoder
{
    /// <summary>字符集，按校验字符计数的顺序排列。</summary>
    private const string charset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%";

    /// <summary>
    /// 每个单元一个标志，条先行、深浅交替：<c>1</c> 为宽，<c>0</c> 为窄。
    /// </summary>
    private static readonly string[] patterns =
    [
        "000110100", "100100001", "001100001", "101100000", "000110001",
        "100110000", "001110000", "000100101", "100100100", "001100100",
        "100001001", "001001001", "101001000", "000011001", "100011000",
        "001011000", "000001101", "100001100", "001001100", "000011100",
        "100000011", "001000011", "101000010", "000010011", "100010010",
        "001010010", "000000111", "100000110", "001000110", "000010110",
        "110000001", "011000001", "111000000", "010010001", "110010000",
        "011010000", "010000101", "110000100", "011000100", "010101000",
        "010100010", "010001010", "000101010",
    ];

    /// <summary>每个 Code 39 符号的起止星号。</summary>
    private const string delimiter = "010010100";

    // 标准允许 2:1 到 3:1 之间的任意比例。2 是常见默认值，在标签上窄四分之一，
    // 3 则能容忍更差的印刷。
    private const int narrow = 1;
    private const int wide = 2;

    internal static BarcodeSymbol Encode(string value, bool addChecksum)
    {
        var upper = value.ToUpperInvariant();
        var indexes = new int[upper.Length];

        for (var i = 0; i < upper.Length; i++)
        {
            var index = charset.IndexOf(upper[i], StringComparison.Ordinal);
            if (index < 0)
            {
                throw new BarcodeFormatException(
                    $"Code 39 holds digits, letters and - . space $ / + %, and this value contains '{value[i]}'. Use Code 128 for anything else.",
                    nameof(value));
            }

            indexes[i] = index;
        }

        var encoded = upper;
        if (addChecksum)
        {
            var check = charset[indexes.Sum() % 43];
            encoded += check;
            indexes = [.. indexes, charset.IndexOf(check, StringComparison.Ordinal)];
        }

        var builder = new BarcodeBuilder();
        builder.NarrowWide(delimiter, narrow, wide);

        foreach (var index in indexes)
        {
            // 字符之间由一个窄空分隔，它不属于前后任何一个字符。
            builder.Space(narrow);
            builder.NarrowWide(patterns[index], narrow, wide);
        }

        builder.Space(narrow);
        builder.NarrowWide(delimiter, narrow, wide);

        return new BarcodeSymbol(
            BarcodeType.Code39,
            encoded,
            builder.Position,
            10,
            builder.ToBars(),
            [new BarcodeTextGroup($"*{encoded}*", 0, builder.Position)]);
    }
}
