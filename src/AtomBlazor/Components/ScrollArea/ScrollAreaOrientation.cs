namespace AtomBlazor;

/// <summary>
/// 定义滚动区域的可滚动朝向。
/// </summary>
/// <remarks>
/// 本枚举与 <c>AtomBlazor.Primitives.ScrollAreaOrientation</c> 成员集一一对应。
/// 双层架构刻意在组件层与原语层各暴露一份公开枚举以隔离公开 API；
/// 若在此增删成员，必须同步另一侧及 <c>ScrollArea.razor</c> 中的
/// <c>MappedOrientation</c> 映射。
/// </remarks>
public enum ScrollAreaOrientation
{
    Vertical,
    Horizontal,
    Both
}
