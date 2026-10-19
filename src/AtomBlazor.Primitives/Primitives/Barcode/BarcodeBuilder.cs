namespace AtomBlazor.Primitives.Barcode;

/// <summary>
/// 从左到右排布条与空，并跟踪下一个单元的起点。
/// </summary>
/// <remarks>
/// 这里的每种码制都是深浅色序列，编码器之间只是索取的宽度
/// 与顺序不同。把这些收拢到一处，各编码器便只保留
/// 自己的表和自己的校验和。
/// </remarks>
internal sealed class BarcodeBuilder
{
    private readonly List<BarcodeBar> bars = [];

    /// <summary>
    /// 获取下一个单元的起点（以模块计），编码完成时即符号总宽。
    /// </summary>
    internal int Position { get; private set; }

    /// <summary>
    /// 添加一根深色条并越过它推进。
    /// </summary>
    /// <param name="width">宽度（以模块计）。</param>
    /// <param name="top">上边缘，按条区比例表示。</param>
    /// <param name="bottom">下边缘，按条区比例表示。</param>
    internal void Bar(int width, double top = 0, double bottom = 1)
    {
        if (width > 0)
        {
            bars.Add(new BarcodeBar(Position, width, top, bottom));
            Position += width;
        }
    }

    /// <summary>
    /// 越过一段浅色间隙推进。
    /// </summary>
    /// <param name="width">宽度（以模块计）。</param>
    internal void Space(int width) => Position += width;

    /// <summary>
    /// 按给定的宽度序列排布单元，从条开始深浅交替。
    /// </summary>
    /// <param name="widths">每个单元一个数字，例如 <c>"212222"</c>。</param>
    /// <param name="bottom">每根条的下边缘，按条区比例表示。</param>
    internal void Widths(ReadOnlySpan<char> widths, double bottom = 1)
    {
        for (var i = 0; i < widths.Length; i++)
        {
            var width = widths[i] - '0';
            if (i % 2 == 0)
            {
                Bar(width, 0, bottom);
            }
            else
            {
                Space(width);
            }
        }
    }

    /// <summary>
    /// 按窄宽标志序列排布单元，从条开始深浅交替。
    /// </summary>
    /// <param name="flags">每个单元一个字符：<c>'1'</c> 为宽，其余为窄。</param>
    /// <param name="narrow">窄单元宽度（以模块计）。</param>
    /// <param name="wide">宽单元宽度（以模块计）。</param>
    internal void NarrowWide(ReadOnlySpan<char> flags, int narrow, int wide)
    {
        for (var i = 0; i < flags.Length; i++)
        {
            var width = flags[i] == '1' ? wide : narrow;
            if (i % 2 == 0)
            {
                Bar(width);
            }
            else
            {
                Space(width);
            }
        }
    }

    /// <summary>
    /// 每个字符排布一个模块，字符为 <c>'1'</c> 时置深色。
    /// </summary>
    /// <param name="modules">模块图案，例如 <c>"0001101"</c>。</param>
    /// <param name="bottom">每根条的下边缘，按条区比例表示。</param>
    /// <remarks>
    /// 连续的深色模块会合并为一根条，这既是渲染器想要的，
    /// 也是扫码器实际看到的样子。
    /// </remarks>
    internal void Modules(ReadOnlySpan<char> modules, double bottom = 1)
    {
        var i = 0;
        while (i < modules.Length)
        {
            if (modules[i] != '1')
            {
                Space(1);
                i++;
                continue;
            }

            var run = 0;
            while (i + run < modules.Length && modules[i + run] == '1')
            {
                run++;
            }

            Bar(run, 0, bottom);
            i += run;
        }
    }

    /// <summary>
    /// 返回迄今排布的所有条。
    /// </summary>
    /// <returns>条序列，从左到右。</returns>
    internal IReadOnlyList<BarcodeBar> ToBars() => bars;
}
