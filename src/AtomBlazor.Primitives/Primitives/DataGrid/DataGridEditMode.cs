// In the AtomBlazor.Primitives namespace rather than AtomBlazor.Primitives.DataGrid,
// alongside SortDirection and DataGridSelectionBehavior: an app that imports
// AtomBlazor.Components cannot also import AtomBlazor.Primitives.DataGrid without making
// the name DataGrid ambiguous, so an enum a consumer has to name in markup goes here.
namespace AtomBlazor.Primitives;

/// <summary>
/// 定义 DataGrid 允许用户就地编辑行的方式。
/// </summary>
public enum DataGridEditMode
{
    /// <summary>
    /// 行不可编辑。这是默认值。
    /// </summary>
    None,

    /// <summary>
    /// 整行同时进入编辑态：每个可编辑单元格都变为输入框，
    /// 所有修改一起提交或一起丢弃。
    /// </summary>
    Row,

    /// <summary>在隔离草稿中编辑单个单元格，接受时提交该单元格。</summary>
    Cell,

    /// <summary>跨行暂存相互独立的单元格编辑，直到整批保存或丢弃。</summary>
    Batch
}
