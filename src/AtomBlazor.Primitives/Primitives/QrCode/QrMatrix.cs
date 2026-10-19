namespace AtomBlazor.Primitives.QrCode;

/// <summary>
/// 以明暗模块方格阵列呈现的成品二维码。
/// </summary>
/// <remarks>
/// <para>
/// 这是无头（headless）产物：只有几何信息，没有颜色、尺寸或静区。
/// 可按任意方式渲染——SVG、画布、位图或文本。带样式的
/// <c>QrCode</c> 组件将其渲染为 SVG。
/// </para>
/// <para>
/// 网格不包含静区，即扫码器定位二维码所需的浅色边距。
/// 规范要求每侧至少四个模块；渲染时请留出该空间。
/// </para>
/// </remarks>
public sealed class QrMatrix
{
    private readonly bool[] modules;

    internal QrMatrix(int size, bool[] modules, int version, QrErrorCorrection errorCorrection, int mask, QrEncodingMode mode)
    {
        Size = size;
        Version = version;
        ErrorCorrection = errorCorrection;
        Mask = mask;
        Mode = mode;
        this.modules = modules;
    }

    /// <summary>
    /// 获取网格的宽高（以模块计），不含静区。
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// 获取二维码版本，1 到 40。每升一版，每边增加四个模块。
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// 获取构建该二维码时使用的纠错等级。
    /// </summary>
    public QrErrorCorrection ErrorCorrection { get; }

    /// <summary>
    /// 获取应用于数据区域的掩码图案，0 到 7。
    /// </summary>
    public int Mask { get; }

    /// <summary>
    /// 获取打包文本时使用的编码模式。
    /// </summary>
    public QrEncodingMode Mode { get; }

    /// <summary>
    /// 获取给定位置的模块是否为深色。
    /// </summary>
    /// <param name="x">列号，最左为 0。越界读取按浅色处理。</param>
    /// <param name="y">行号，最上为 0。越界读取按浅色处理。</param>
    /// <returns>模块为深色时返回 <see langword="true"/>。</returns>
    /// <remarks>
    /// 越界位置按浅色读取而不抛异常，渲染器因此可以遍历包含静区的
    /// 网格，无需自行做边界检查。
    /// </remarks>
    public bool this[int x, int y] =>
        x >= 0 && x < Size && y >= 0 && y < Size && modules[(y * Size) + x];
}
