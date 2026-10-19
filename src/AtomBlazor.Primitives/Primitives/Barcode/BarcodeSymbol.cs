namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 以条列表呈现的成品条形码，位置与宽度均以模块计。
/// </summary>
/// <remarks>
/// 这是无头产物：只有几何信息，没有颜色、尺寸或已绘制的静区。
/// 可按任意方式渲染——SVG、画布、位图或直接送打印机。带样式的
/// <c>Barcode</c> 组件将其渲染为 SVG。
/// </remarks>
public sealed class BarcodeSymbol
{
    internal BarcodeSymbol(
        BarcodeType type,
        string value,
        int moduleCount,
        int quietZoneModules,
        IReadOnlyList<BarcodeBar> bars,
        IReadOnlyList<BarcodeTextGroup> textGroups)
    {
        Type = type;
        Value = value;
        ModuleCount = moduleCount;
        QuietZoneModules = quietZoneModules;
        Bars = bars;
        TextGroups = textGroups;
    }

    /// <summary>
    /// 获取编码该值时所用的码制。
    /// </summary>
    public BarcodeType Type { get; }

    /// <summary>
    /// 获取编码后的值，含编码器算出的任何校验位。
    /// </summary>
    /// <remarks>
    /// 它并不总是传入的原文。把 <c>5901234123</c> 交给 <see cref="BarcodeType.Ean13"/>，
    /// 编码器会补位并追加校验位，于是这里读作 <c>5901234123457</c>。凡数值必须与
    /// 扫码器将报告的结果一致之处，请保存此值而非输入值。
    /// </remarks>
    public string Value { get; }

    /// <summary>
    /// 获取符号的宽度（以模块计），不含静区。
    /// </summary>
    public int ModuleCount { get; }

    /// <summary>
    /// 获取码制要求的两侧浅色边距宽度（以模块计）。
    /// </summary>
    /// <remarks>
    /// 扫码器需要这条边距来定位符号的起止。它随码制而异——
    /// EAN-13 左侧要 11 个模块而 Code 128 要 10 个——此处取
    /// 两侧中较大者，因此两边都用它总是安全的。
    /// </remarks>
    public int QuietZoneModules { get; }

    /// <summary>
    /// 获取深色条，从左到右。
    /// </summary>
    public IReadOnlyList<BarcodeBar> Bars { get; }

    /// <summary>
    /// 获取人读文本及每段文本在符号下方的归属位置。
    /// </summary>
    /// <remarks>
    /// 码制无标准文本行时为空。否则通常是一段横跨整个符号的文本，
    /// 零售码制例外，其数字分布在护栏条周围。
    /// </remarks>
    public IReadOnlyList<BarcodeTextGroup> TextGroups { get; }

    /// <summary>
    /// 获取数据是否由条高而非条宽承载。
    /// </summary>
    /// <remarks>
    /// <see cref="BarcodeType.Postnet"/> 与 <see cref="BarcodeType.Rm4scc"/> 为真。渲染器
    /// 若压扁这两者的条高就会毁坏数据，而在其他所有码制上
    /// 高度只影响扫码器对准的难易而已。
    /// </remarks>
    public bool IsHeightModulated => Type is BarcodeType.Postnet or BarcodeType.Rm4scc;
}
