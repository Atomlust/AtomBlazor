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

/// <summary>
/// 定义 Toggle 组件的尺寸。
/// </summary>
public enum ToggleSize
{
    /// <summary>
    /// 小号按钮，高度 36px。
    /// </summary>
    Small,

    /// <summary>
    /// 默认尺寸按钮，高度 40px。
    /// </summary>
    Default,

    /// <summary>
    /// 大号按钮，高度 44px。
    /// </summary>
    Large
}

/// <summary>
/// 定义切换按钮组的选择模式。
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
