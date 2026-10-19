using AtomBlazor.Primitives;
namespace AtomBlazor;

/// <summary>
/// 传递给 DataGrid 行提交回调的上下文，携带被编辑的项，并允许处理程序让该行保持编辑状态。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class DataGridRowCommitContext<TData> where TData : class
{
    /// <summary>
    /// 获取被编辑的模型：Row 模式下为原始记录，Cell 模式下为隔离的草稿。
    /// </summary>
    public required TData Item { get; init; }

    /// <summary>
    /// 获取缓冲单元格模式下未经修改的源记录；传统行模式下为 null。
    /// 在单元格模式下，Item 是草稿，只有在本回调接受后才会应用。
    /// </summary>
    public TData? OriginalItem { get; init; }

    /// <summary>
    /// 获取或设置是否让该行保持编辑状态而不关闭。
    /// </summary>
    /// <remarks>
    /// 当保存失败时设置此项——服务器拒绝、冲突、表格无法校验的规则——这样用户已输入的内容得以保留并能继续修正。行模式下把值保留在原始项上；单元格模式下保留隔离的草稿。
    /// </remarks>
    public bool Cancel { get; set; }
}
