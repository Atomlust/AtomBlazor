using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 一次完成的面板组尺寸调整的事件参数。
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Type represents event arguments")]
public class PanelResizeEventArgs
{
    /// <summary>
    /// 各面板的尺寸（百分比），按面板声明顺序排列。
    /// </summary>
    /// <remarks>
    /// 这些是组提交的尺寸，经过其自身最小与最大值钳制之后的值，因此是
    /// 面板实际渲染所用的值，而非原始拖拽结果。
    /// </remarks>
    public IReadOnlyList<double> Sizes { get; init; } = Array.Empty<double>();
}
