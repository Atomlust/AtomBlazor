using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="Chip"/> 的视觉风格变体。
/// </summary>
/// <remarks>
/// 每个变体都有两种形态：未选中样式与选中样式。选中态只为参与选择的芯片渲染——
/// 即声明了 <see cref="Chip.Selectable"/> 或处于 <see cref="ChipSet{TValue}"/> 中的芯片。
/// </remarks>
public enum ChipVariant
{
    /// <summary>中性填充芯片。选中时呈现 primary 色。</summary>
    Default,

    /// <summary>低强调的 muted 填充芯片。选中时呈现柔和的 primary 着色。</summary>
    Secondary,

    /// <summary>透明带边框的芯片。选中时呈现 accent 色。</summary>
    Outline,

    /// <summary>错误或移除配色。选中时呈现实心 destructive 填充。</summary>
    Destructive,

    /// <summary>柔和的 primary 着色。选中时呈现实心 primary 填充。</summary>
    Soft
}

/// <summary>
/// 定义 <see cref="Chip"/> 的尺寸。
/// </summary>
public enum ChipSize
{
    /// <summary>紧凑型芯片，高 24px。</summary>
    Small,

    /// <summary>标准芯片，高 32px。</summary>
    Default,

    /// <summary>宽松芯片，高 40px。</summary>
    Large
}

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
