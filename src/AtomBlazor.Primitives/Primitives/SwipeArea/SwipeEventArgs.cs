using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 一次已完成的滑动。
/// </summary>
/// <param name="Direction">
/// 指针在浮区所上报的轴上的移动方向。
/// </param>
/// <param name="DeltaX">
/// 自手势起点起的横向位移，单位 CSS 像素。负值表示向左。
/// </param>
/// <param name="DeltaY">
/// 自手势起点起的垂直位移，单位 CSS 像素。负值表示向上。
/// </param>
/// <param name="Distance">
/// 沿被上报轴的移动距离，单位 CSS 像素。恒为正。
/// </param>
/// <param name="Velocity">
/// 手势结束时的速度，单位 CSS 像素/毫秒，按最近 100ms 的移动测量而非整个手势。
/// 因此一次抬指前停顿的长距离滑动会上报很低的速度。
/// </param>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "该类型就是事件参数")]
public readonly record struct SwipeEventArgs(
    SwipeDirection Direction,
    double DeltaX,
    double DeltaY,
    double Distance,
    double Velocity);
