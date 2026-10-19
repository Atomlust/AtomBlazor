namespace AtomBlazor.Primitives;

/// <summary>
/// TreeView 基元上下文的状态。
/// </summary>
public class TreeViewState
{
    /// <summary>
    /// 获取或设置处于展开状态的节点值集合。
    /// </summary>
    public HashSet<string> ExpandedValues { get; set; } = new();

    /// <summary>
    /// 获取或设置处于选中状态的节点值集合。
    /// </summary>
    public HashSet<string> SelectedValues { get; set; } = new();

    /// <summary>
    /// 获取或设置处于勾选状态的节点值集合。
    /// </summary>
    public HashSet<string> CheckedValues { get; set; } = new();

    /// <summary>
    /// 获取或设置选择模式。
    /// </summary>
    public TreeSelectionMode SelectionMode { get; set; } = TreeSelectionMode.None;

    /// <summary>
    /// 获取或设置节点是否显示复选框。
    /// </summary>
    public bool Checkable { get; set; }

    /// <summary>
    /// 获取或设置是否禁用级联勾选（各复选框相互独立）。
    /// </summary>
    public bool CheckStrictly { get; set; }

    /// <summary>
    /// 获取或设置是否显示连接线。
    /// </summary>
    public bool ShowLines { get; set; }

    /// <summary>
    /// 获取或设置是否显示图标（默认：true）。
    /// </summary>
    public bool ShowIcons { get; set; } = true;
}
