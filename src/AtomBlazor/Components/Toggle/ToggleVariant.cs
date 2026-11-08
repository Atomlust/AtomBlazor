using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 定义 Toggle 组件的视觉样式变体。
/// </summary>
public enum ToggleVariant
{
    /// <summary>
    /// 默认变体：透明背景，仅以悬停/按下态着色。
    /// </summary>
    Default,

    /// <summary>
    /// 描线变体：带边框，悬停时显示强调色背景。
    /// </summary>
    Outline
}
