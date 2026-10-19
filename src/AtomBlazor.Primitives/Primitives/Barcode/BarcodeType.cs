namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 编码器能够生成的条形码码制。
/// </summary>
/// <remarks>
/// 码制是字符集与规则的总称，不只是外观。每种码制接受的
/// 字符集与长度都不同，因此类型必须与另一端扫码器期望的一致。
/// 在没有强制规定时，<see cref="Code128"/> 是通用选择。
/// </remarks>
public enum BarcodeType
{
    /// <summary>
    /// 通用选择。支持任意 ASCII 字符，是线性码中密度最高的，
    /// 因为它把一对数字压进一个符号字符。
    /// </summary>
    Code128,

    /// <summary>
    /// 数字、大写字母、<c>-.$/+%</c> 及空格。比
    /// <see cref="Code128"/> 更老、宽得多，但某些物流与国防标准仍要求使用。
    /// </summary>
    Code39,

    /// <summary>
    /// 北美以外普遍使用的 13 位零售商品代码。给 12 位则
    /// 自动算出校验位，给足 13 位则予以校验。
    /// </summary>
    Ean13,

    /// <summary>
    /// 用于小包装的短零售码。给 7 位则自动算出校验位。
    /// </summary>
    Ean8,

    /// <summary>
    /// 北美 12 位零售码。给 11 位则自动算出校验位。
    /// </summary>
    UpcA,

    /// <summary>
    /// Interleaved 2 of 5（交错五中取二）。仅数字且必须为偶数位，
    /// 这样才能让一个数字由条承载、下一个由空承载。
    /// </summary>
    Itf,

    /// <summary>
    /// 数字与 <c>-$:/.+</c>，首尾由 A 到 D 起止符包裹。在血库、
    /// 图书馆与照相馆中仍是标准。
    /// </summary>
    Codabar,

    /// <summary>
    /// 美国邮政 ZIP 分拣码。采用条高调制而非
    /// 条宽调制：所有条同宽，由长条承载数据。
    /// </summary>
    Postnet,

    /// <summary>
    /// Laetus Pharmacode。3 到 131070 的单个数字，以粗细条序列表示，
    /// 专为即使印刷不佳也能在高速包装线上读识而设计。
    /// </summary>
    Pharmacode,

    /// <summary>
    /// 英国皇家邮政四状态客户码。用四种条形而非两种条宽，
    /// 在同样长度的标签上容纳更多信息。
    /// </summary>
    Rm4scc,

    /// <summary>
    /// 以 <see cref="Ean13"/> 表示的图书号。接受 10 或 13 位 ISBN，
    /// 连字符可选，并把带连字符的 ISBN 印在条上方。
    /// </summary>
    Isbn,

    /// <summary>
    /// 以 <see cref="Ean13"/> 表示的连续出版物号。接受 8 位 ISSN 并
    /// 将其印在条上方。
    /// </summary>
    Issn,

    /// <summary>
    /// Modified Plessey（修正普莱西）。仅数字，校验位可选。
    /// 常见于仓库货架边缘标签。
    /// </summary>
    Msi,

    /// <summary>
    /// 任意 ASCII 字符，每个 16 个模块，只用两种单元宽度。
    /// 是英国图书馆与某些实验室系统的标准码。
    /// </summary>
    Telepen,
}
