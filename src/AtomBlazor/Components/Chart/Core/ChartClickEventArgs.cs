using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 描述用户在图表中点击的数据点。
/// </summary>
/// <remarks>
/// ECharts 报告一个点的值时，放的是该系列所置入之物：柱或线为数字、
/// 散点或 K 线为数组、偶尔是对象。与其把这一形态原样浮出，
/// <see cref="Value"/> 承载标量情形、<see cref="Values"/> 承载数组情形，两种
/// 常见形态遂以强类型抵达。两者皆不适用时二者均为 <c>null</c>，而
/// <see cref="Name"/> 与 <see cref="DataIndex"/> 仍可定位该点。
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "EventArgs suffix is intentional for this DTO, matching SelectionChangeEventArgs and PanelResizeEventArgs")]
public sealed record ChartClickEventArgs
{
    /// <summary>该点所属系列（series）的名称，若该系列设有名称。</summary>
    public string? SeriesName { get; init; }

    /// <summary>系列的零基序号；图表未报告时取 <c>-1</c>。</summary>
    public int SeriesIndex { get; init; } = -1;

    /// <summary>
    /// 该点在其系列内的零基序号；图表未报告时取 <c>-1</c>。
    /// 下钻应当以此字段为键：
    /// 它直接映射回你所绑定集合中的位置。
    /// 就地图而言，源数据集中缺席的国家会报 <c>-1</c>。
    /// </summary>
    public int DataIndex { get; init; } = -1;

    /// <summary>该点的类别或标签——柱状图取 x 轴名称，饼图取扇区名称。</summary>
    public string? Name { get; init; }

    /// <summary>
    /// 点击落在图表的哪一部分，按 ECharts 的命名——数据点为 <c>"series"</c>，
    /// 装饰物等则取 <c>"markPoint"</c> 之类其他值。
    /// </summary>
    public string? ComponentType { get; init; }

    /// <summary>该点的值为单个数字时取此字段，涵盖柱状、折线、面积与饼图。</summary>
    public double? Value { get; init; }

    /// <summary>
    /// 该点的值为数组时取此字段，涵盖散点与 K 线。
    /// </summary>
    /// <remarks>
    /// 散点以 <c>[x, y]</c> 抵达。K 线以
    /// <c>[index, open, close, low, high]</c> 抵达——是五项而非四项：ECharts 会把
    /// 点的序号前置到该系列所给的四个值之前，故 <c>Values[0]</c> 是序号、
    /// 开盘价在 <c>Values[1]</c>。旧文档写作 <c>[open, close, low, high]</c>，
    /// 那会把每个价格都读早一位。
    /// </remarks>
    public IReadOnlyList<double>? Values { get; init; }
}
