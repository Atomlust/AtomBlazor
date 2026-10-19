using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 一次进行中的滑动，在指针仍按下期间上报。
/// </summary>
/// <param name="DeltaX">
/// 自手势起点起的横向位移，单位 CSS 像素。负值表示向左。
/// </param>
/// <param name="DeltaY">
/// 自手势起点起的垂直位移，单位 CSS 像素。负值表示向上。
/// </param>
/// <remarks>
/// 这些更新被节流为每 50ms 至多一条、且同时在途的调用只有一个，因此慢速电路会丢弃
/// 中间位置而不是排队累积。把每一条都当作当前偏移量，而非待累加的增量。
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "该类型就是事件参数")]
public readonly record struct SwipeMoveEventArgs(double DeltaX, double DeltaY);
