namespace AtomBlazor;

/// <summary>
/// 定义动态表单的布局模式。
/// </summary>
public enum FormLayout
{
    /// <summary>字段纵向堆叠，标签位于上方（默认）。</summary>
    Vertical,

    /// <summary>标签置于字段左侧。</summary>
    Horizontal,

    /// <summary>字段单行内联排列。</summary>
    Inline
}
