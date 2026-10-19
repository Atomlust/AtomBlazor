namespace AtomBlazor;

/// <summary>
/// 定义富文本编辑器的工具栏预设配置。
/// </summary>
public enum ToolbarPreset
{
    /// <summary>
    /// 不显示工具栏。
    /// </summary>
    None,

    /// <summary>
    /// 加粗、斜体、下划线、无序列表与有序列表。
    /// </summary>
    Simple,

    /// <summary>
    /// 在 <see cref="Simple"/> 之外增加撤销/重做、标题、删除线、检查清单与链接。
    /// </summary>
    Standard,

    /// <summary>
    /// 在 <see cref="Standard"/> 之外增加行内代码、文本对齐、文字颜色与高亮、
    /// 图片、引用块、代码块与表格。
    /// </summary>
    Full,

    /// <summary>
    /// 自定义工具栏——通过 ToolbarContent 提供自定义工具栏标记。
    /// </summary>
    Custom
}
