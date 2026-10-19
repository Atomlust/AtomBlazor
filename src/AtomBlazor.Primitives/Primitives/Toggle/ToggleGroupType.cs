using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 定义切换按钮组（ToggleGroupPrimitive）的选择模式。
/// </summary>
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
