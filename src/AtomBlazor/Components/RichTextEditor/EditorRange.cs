namespace AtomBlazor;

/// <summary>
/// 表示富文本编辑器中的选区范围。
/// </summary>
public class EditorRange
{
    /// <summary>
    /// 获取或设置选区的起始索引。
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 获取或设置选区的长度。
    /// </summary>
    public int Length { get; set; }
}
