namespace AtomBlazor.Primitives;

/// <summary>
/// 允许浏览器对起始于 <c>SwipeArea</c> 内部的触摸做什么。
/// </summary>
/// <remarks>
/// <para>
/// 每个值都映射到同名的 CSS <c>touch-action</c> 属性。这是让滑动不与页面打架的方式：
/// 在手势开始之前就告诉浏览器哪些方向可以滚动，而不是要求它撤销一个已经开始的滚动。
/// </para>
/// <para>
/// 在指针事件里调用 <c>preventDefault</c> 无法可靠做到这一点。一旦浏览器把触摸交给了
/// 合成器去滚动，本可取消它的那个事件到达时已经太晚；而在 passive 监听器上它会被直接忽略。
/// </para>
/// </remarks>
public enum SwipeTouchAction
{
    /// <summary>
    /// 浏览器保持正常行为，页面照常滚动。横向滑动用鼠标仍然有效；触屏上则在
    /// 浏览器把手势收编为滚动之前有效。默认值。
    /// </summary>
    Auto,

    /// <summary>
    /// 浏览器保留横向平移；浮区接手垂直手势。适用于在横向滚动的页面里做上下滑动。
    /// </summary>
    PanX,

    /// <summary>
    /// 浏览器保留垂直平移；浮区接手横向手势。这是在纵向滚动页面里做左右滑动的常规选择。
    /// </summary>
    PanY,

    /// <summary>
    /// 浏览器在该区域内任何方向都不滚动、不缩放。仅当浮区确实独占两个轴时使用，
    /// 因为它同时也剥夺了页面自身在该区域的滚动。
    /// </summary>
    None
}
