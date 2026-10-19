namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 一段人读文本及其在条形码下方所属的区间。
/// </summary>
/// <param name="Text">要打印的字符。</param>
/// <param name="Start">区间左端，以距符号起点的模块数计。</param>
/// <param name="End">区间右端（以模块计）。</param>
/// <remarks>
/// 多数码制把值作为一段文本居中印在整个符号下方。零售码制则不然：
/// EAN-13 把首位数字放在左侧静区，其余数字分列中央护栏
/// 两侧，而这些文本的落位是标准规定而非装饰。
/// </remarks>
public readonly record struct BarcodeTextGroup(string Text, double Start, double End);
