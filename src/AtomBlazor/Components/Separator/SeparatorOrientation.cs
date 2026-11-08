namespace AtomBlazor;

/// <summary>
/// 定义 Separator 组件的方向。
/// </summary>
/// <remarks>
/// 决定分隔符显示为横跨宽度的水平分割线，还是纵贯高度的垂直分割线。
/// 它同时影响视觉外观与面向屏幕阅读器的 aria-orientation 属性。
/// </remarks>
/// <remarks>
/// 本枚举与 <c>AtomBlazor.Primitives.SeparatorOrientation</c> 成员集一一对应。
/// 双层架构刻意在组件层与原语层各暴露一份公开枚举以隔离公开 API；
/// 若在此增删成员，必须同步另一侧及 <c>Separator.razor.cs</c> 中的
/// <c>MappedOrientation</c> 映射。
/// </remarks>
public enum SeparatorOrientation
{
    /// <summary>
    /// 横跨全宽的水平分隔符。
    /// 在纵向堆叠的内容之间创建水平分割线。
    /// CSS：h-[1px] w-full
    /// </summary>
    Horizontal,

    /// <summary>
    /// 纵贯全高的垂直分隔符。
    /// 在横向排列的内容之间创建垂直分割线。
    /// CSS：h-full w-[1px]
    /// </summary>
    Vertical
}
