using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 使用 Floating UI 把浮动元素相对参照元素定位的服务。
/// </summary>
public interface IPositioningService
{
    /// <summary>
    /// 计算浮动元素相对参照元素的最优位置。
    /// </summary>
    /// <param name="reference">要相对定位的参照元素。</param>
    /// <param name="floating">待定位的浮动元素。</param>
    /// <param name="options">定位选项。</param>
    /// <returns>含 x、y 坐标等数据的定位结果。</returns>
    public Task<PositionResult> ComputePositionAsync(
        ElementReference reference,
        ElementReference floating,
        PositioningOptions? options = null);

    /// <summary>
    /// 把算好的位置应用到浮动元素上。
    /// </summary>
    /// <param name="floating">要定位的元素。</param>
    /// <param name="position">要应用的位置数据。</param>
    /// <param name="makeVisible">定位完成后是否把元素设为可见。</param>
    public Task ApplyPositionAsync(ElementReference floating, PositionResult position, bool makeVisible = false);

    /// <summary>
    /// 让浮动元素回到隐藏的离屏状态。
    /// </summary>
    /// <remarks>
    /// 与 <c>makeVisible: true</c> 的 <see cref="ApplyPositionAsync"/> 互为镜像：后者会用
    /// <c>!important</c> 把可见性、透明度与指针事件写到元素上，从而把 style 属性的所有权
    /// 从 Blazor 手里拿走。Blazor 的 diff 对照的是它上次<em>渲染</em>出的内容而非 DOM 里的
    /// 实际内容，所以关闭时若组件的标记与打开前一致，就不会发出任何 style 更新——那些可见值
    /// 否则会一直留在元素上。
    /// </remarks>
    /// <param name="floating">要隐藏的元素。</param>
    public Task HidePositionAsync(ElementReference floating);

    /// <summary>
    /// 为动态定位建立自动更新（例如随滚动/缩放刷新位置）。
    /// </summary>
    /// <param name="reference">参照元素。</param>
    /// <param name="floating">浮动元素。</param>
    /// <param name="options">定位选项。</param>
    /// <returns>用于停止自动更新的可释放句柄。</returns>
    public Task<IAsyncDisposable> AutoUpdateAsync(
        ElementReference reference,
        ElementReference floating,
        PositioningOptions? options = null);
}

/// <summary>
/// 浮动元素定位选项。
/// </summary>
public class PositioningOptions
{
    /// <summary>
    /// 浮动元素的首选摆放方位。
    /// 取值："top"、"top-start"、"top-end"、"bottom"、"bottom-start"、"bottom-end"、
    /// "left"、"left-start"、"left-end"、"right"、"right-start"、"right-end"。
    /// </summary>
    public string Placement { get; set; } = "bottom";

    /// <summary>
    /// 相对参照元素的偏移像素数。
    /// </summary>
    public int Offset { get; set; } = 8;

    /// <summary>
    /// 空间不足时是否翻转到对面方位。
    /// </summary>
    public bool Flip { get; set; } = true;

    /// <summary>
    /// 是否沿交叉轴平移以保持元素在视口内。
    /// </summary>
    public bool Shift { get; set; } = true;

    /// <summary>
    /// 距视口边缘的内边距像素数。
    /// </summary>
    public int Padding { get; set; } = 8;

    /// <summary>
    /// 浮动元素的 CSS 定位策略。
    /// <see cref="PositioningStrategy.Absolute"/> 相对 offset parent 定位，
    /// <see cref="PositioningStrategy.Fixed"/> 相对视口定位。
    /// </summary>
    public PositioningStrategy Strategy { get; set; } = PositioningStrategy.Absolute;

    /// <summary>
    /// 是否让浮动元素宽度与参照元素保持一致。
    /// </summary>
    public bool MatchReferenceWidth { get; set; }
}

/// <summary>
/// 位置计算的结果。
/// </summary>
public class PositionResult
{
    /// <summary>
    /// 浮动元素的 X 坐标。
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// 浮动元素的 Y 坐标。
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// 各种调整后的最终方位。
    /// </summary>
    public string Placement { get; set; } = "bottom";

    /// <summary>
    /// 供动画使用的 CSS transform-origin。
    /// </summary>
    public string? TransformOrigin { get; set; }

    /// <summary>
    /// 实际使用的定位策略："absolute" 或 "fixed"。
    /// </summary>
    public string Strategy { get; set; } = "absolute";
}
