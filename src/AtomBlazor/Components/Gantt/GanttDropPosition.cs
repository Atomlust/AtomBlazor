namespace AtomBlazor;

/// <summary>
/// 被拖的行所请求的落点。
/// </summary>
public enum GanttDropPosition
{
    /// <summary>紧在目标之上，与其同辈。</summary>
    Before,

    /// <summary>紧在目标之下，与其同辈。</summary>
    After,

    /// <summary>在目标之下，为其子项。</summary>
    Inside,
}
