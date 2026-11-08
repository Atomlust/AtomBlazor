using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

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
