namespace AtomBlazor.Primitives;

/// <summary>
/// 定义滑块的方向。
/// </summary>
/// <remarks>
/// 本枚举与 <c>AtomBlazor.SliderOrientation</c> 成员集一一对应。
/// 双层架构刻意在组件层与原语层各暴露一份公开枚举以隔离公开 API；
/// 若在此增删成员，必须同步另一侧及 <c>Slider.razor</c> 中的
/// <c>MappedOrientation</c> 映射。
/// </remarks>
public enum SliderOrientation
{
    /// <summary>
    /// 水平滑块（从左到右）。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 垂直滑块（从下到上）。
    /// </summary>
    Vertical
}
