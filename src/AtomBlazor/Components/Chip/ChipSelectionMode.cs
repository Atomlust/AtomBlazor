using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="ChipSet{TValue}"/> 一次可选中多少芯片。
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Single 是选择模式的领域术语")]
public enum ChipSelectionMode
{
    /// <summary>芯片不可选中。集合仅负责分组与间距。</summary>
    None,

    /// <summary>一次一个，通过 <c>Value</c> 绑定。</summary>
    Single,

    /// <summary>任意多个，通过 <c>Values</c> 绑定。</summary>
    Multiple
}
