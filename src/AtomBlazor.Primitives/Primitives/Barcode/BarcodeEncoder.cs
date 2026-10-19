namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 把值编码为任一受支持码制的 <see cref="BarcodeSymbol"/>。
/// </summary>
/// <remarks>
/// <para>
/// 每种码制都有自己的字符集、长度规则和校验位，编码器
/// 对三者都严格执行。所选码制无法承载的值会抛异常，而不是
/// 产出一个任何扫码器都不接受的符号。
/// </para>
/// <para>
/// 凡校验位属于标准的一部分——零售码制、POSTNET、皇家邮政码——
/// 总是自动算出并追加，<see cref="BarcodeSymbol.Value"/> 报告结果。
/// 标准将其列为可选时，由 <c>addChecksum</c> 决定，默认关闭，
/// 因为没被配置期望校验位的扫码器会把它当作数据的一部分报告出来。
/// </para>
/// </remarks>
public static class BarcodeEncoder
{
    /// <summary>
    /// 把值编码为条形码。
    /// </summary>
    /// <param name="value">要编码的值。何为合法取决于 <paramref name="type"/>。</param>
    /// <param name="type">要采用的码制。</param>
    /// <param name="addChecksum">
    /// 对具有可选校验字符的码制，是否追加校验字符：
    /// <see cref="BarcodeType.Code39"/>、<see cref="BarcodeType.Itf"/>、
    /// <see cref="BarcodeType.Codabar"/> 与 <see cref="BarcodeType.Msi"/>。其余码制忽略此参数，
    /// 那些码制的校验位要么强制、要么内建于码制本身。
    /// </param>
    /// <returns>成品符号。</returns>
    /// <exception cref="BarcodeFormatException">
    /// 当 <paramref name="value"/> 为空、含码制无法承载的字符或长度，
    /// 或末尾校验位不匹配时抛出。
    /// </exception>
    public static BarcodeSymbol Encode(string value, BarcodeType type, bool addChecksum = false)
    {
        // null 是调用方代码的错误，永远不会到达扫码器，
        // 因此框架自身的异常正合适。
        ArgumentNullException.ThrowIfNull(value);

        // 空值则不然：它是绑定输入在用户键入前的常态内容，消息会上页面——
        // 所以用我们自己的异常。ArgumentException.ThrowIfNullOrEmpty 会
        // 用框架资源措辞，而在 WebAssembly 上那些资源常被裁剪得只剩
        // 资源键。
        if (value.Length == 0)
        {
            throw new BarcodeFormatException("There is nothing to encode.", nameof(value));
        }

        return type switch
        {
            BarcodeType.Code128 => Code128Encoder.Encode(value),
            BarcodeType.Code39 => Code39Encoder.Encode(value, addChecksum),
            BarcodeType.Ean13 or BarcodeType.Ean8 or BarcodeType.UpcA or BarcodeType.Isbn or BarcodeType.Issn
                => EanUpcEncoder.Encode(value, type),
            BarcodeType.Itf => ItfCodabarMsiEncoder.EncodeItf(value, type, addChecksum),
            BarcodeType.Codabar => ItfCodabarMsiEncoder.EncodeCodabar(value, addChecksum),
            BarcodeType.Msi => ItfCodabarMsiEncoder.EncodeMsi(value, addChecksum),
            BarcodeType.Postnet => PostalEncoder.EncodePostnet(value),
            BarcodeType.Rm4scc => PostalEncoder.EncodeRm4scc(value),
            BarcodeType.Pharmacode => PostalEncoder.EncodePharmacode(value),
            BarcodeType.Telepen => TelepenEncoder.Encode(value),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown barcode symbology."),
        };
    }

    /// <summary>
    /// 获取某种码制接受内容的简短描述，供错误消息或表单提示使用。
    /// </summary>
    /// <param name="type">码制。</param>
    /// <returns>描述合法值的一句话。</returns>
    public static string DescribeInput(BarcodeType type) => type switch
    {
        BarcodeType.Code128 => "Any ASCII text.",
        BarcodeType.Code39 => "Digits, letters and - . space $ / + %.",
        BarcodeType.Ean13 => "12 digits, or 13 with the check digit.",
        BarcodeType.Ean8 => "7 digits, or 8 with the check digit.",
        BarcodeType.UpcA => "11 digits, or 12 with the check digit.",
        BarcodeType.Itf => "Digits, an even number of them.",
        BarcodeType.Codabar => "Digits and - $ : / . +, optionally wrapped in a start and stop letter A to D.",
        BarcodeType.Postnet => "A 5, 9 or 11 digit ZIP code.",
        BarcodeType.Pharmacode => "A whole number from 3 to 131070.",
        BarcodeType.Rm4scc => "Digits and upper-case letters.",
        BarcodeType.Isbn => "A 10 or 13 digit ISBN, with or without hyphens.",
        BarcodeType.Issn => "An 8 character ISSN, with or without a hyphen.",
        BarcodeType.Msi => "Digits.",
        BarcodeType.Telepen => "Any ASCII text.",
        _ => string.Empty,
    };

    /// <summary>
    /// 剔除空格与连字符，并检查余下内容是否全为数字。
    /// </summary>
    /// <param name="value">按原样给入的值。</param>
    /// <param name="type">码制，用于错误消息。</param>
    /// <returns>数字串。</returns>
    /// <exception cref="BarcodeFormatException">值中含有非数字内容。</exception>
    internal static string DigitsOnly(string value, BarcodeType type)
    {
        var digits = new char[value.Length];
        var count = 0;

        foreach (var c in value)
        {
            if (c is '-' or ' ')
            {
                continue;
            }

            if (!char.IsAsciiDigit(c))
            {
                throw new BarcodeFormatException(
                    $"{type} holds digits only, and this value contains '{c}'. {DescribeInput(type)}",
                    nameof(value));
            }

            digits[count] = c;
            count++;
        }

        return new string(digits, 0, count);
    }
}
