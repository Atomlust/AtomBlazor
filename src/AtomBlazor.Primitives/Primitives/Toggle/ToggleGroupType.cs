using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 定义切换按钮组（ToggleGroupPrimitive）的选择模式。
/// </summary>
/// <remarks>
/// 本枚举与 <c>AtomBlazor.ToggleGroupType</c> 成员集一一对应。
/// 双层架构刻意在组件层与原语层各暴露一份公开枚举以隔离公开 API；
/// 若在此增删成员，必须同步另一侧及 <c>ToggleGroup.razor</c> 中的
/// <c>MappedType</c> 映射。
/// </remarks>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Single 是选择模式的领域术语")]
public enum ToggleGroupType
{
    /// <summary>
    /// 同一时间只能有一个切换按钮处于激活状态。
    /// </summary>
    Single,

    /// <summary>
    /// 可以同时激活多个切换按钮。
    /// </summary>
    Multiple
}
