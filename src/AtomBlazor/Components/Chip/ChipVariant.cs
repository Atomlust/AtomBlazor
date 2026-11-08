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
