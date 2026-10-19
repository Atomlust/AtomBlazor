namespace AtomBlazor;

/// <summary>
/// 基础（灰阶）调色板，用于背景、边框与中性 UI 元素。
/// 每个取值对应一组独立的 OKLCH 灰阶 CSS 自定义属性。
/// </summary>
public enum BaseColor
{
    /// <summary>冷灰，带微妙蓝底。shadcn/ui 的默认值。</summary>
    Zinc,

    /// <summary>冷蓝灰，比 Zinc 略暖。</summary>
    Slate,

    /// <summary>纯净均衡的灰，不携带色相。</summary>
    Gray,

    /// <summary>真中性灰——明度步进完全一致、彩度为零。</summary>
    Neutral,

    /// <summary>暖灰，带轻微黄/棕底。</summary>
    Stone
}
