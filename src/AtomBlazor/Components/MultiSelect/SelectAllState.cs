namespace AtomBlazor;

/// <summary>
/// 表示 Select All 复选框的状态。
/// </summary>
public enum SelectAllState
{
    /// <summary>没有任何条目被选中。</summary>
    None,
    /// <summary>部分条目被选中。</summary>
    Indeterminate,
    /// <summary>全部条目被选中。</summary>
    All
}
