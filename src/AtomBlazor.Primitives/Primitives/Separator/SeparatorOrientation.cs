namespace AtomBlazor.Primitives;

/// <summary>
/// 定义分隔符的方向。
/// </summary>
/// <remarks>
/// 本枚举与 <c>AtomBlazor.SeparatorOrientation</c> 成员集一一对应。
/// 双层架构刻意在组件层与原语层各暴露一份公开枚举以隔离公开 API；
/// 若在此增删成员，必须同步另一侧及 <c>Separator.razor.cs</c> 中的
/// <c>MappedOrientation</c> 映射。
/// </remarks>
public enum SeparatorOrientation
{
    /// <summary>
    /// 横跨全宽的水平分隔符。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 纵贯全高的垂直分隔符。
    /// </summary>
    Vertical
}
