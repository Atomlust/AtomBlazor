namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 条形码的一根深色条，位置与宽度以模块计。
/// </summary>
/// <param name="Start">条的左端，以距符号起点的模块数计。</param>
/// <param name="Width">条的宽度（以模块计）。</param>
/// <param name="Top">
/// 条在垂直方向的起点，按条区比例表示：0 为顶部，0.5 为
/// 中部。
/// </param>
/// <param name="Bottom">
/// 条在垂直方向的终点，按条区比例表示：1 为底部。
/// </param>
/// <remarks>
/// 多数码制以宽度承载数据、所有条保持全高，因此
/// <paramref name="Top"/> 为 0、<paramref name="Bottom"/> 为 1。邮政码制则以
/// 高度承载数据——见 <see cref="BarcodeType.Postnet"/> 与
/// <see cref="BarcodeType.Rm4scc"/>——零售码制的护栏条略带下伸，
/// 让数字恰好落在条之间。
/// </remarks>
public readonly record struct BarcodeBar(int Start, int Width, double Top, double Bottom)
{
    /// <summary>
    /// 创建全高条。
    /// </summary>
    /// <param name="start">条的左端（以模块计）。</param>
    /// <param name="width">条的宽度（以模块计）。</param>
    public BarcodeBar(int start, int width)
        : this(start, width, 0, 1)
    {
    }
}
