using AtomBlazor.Primitives;
namespace AtomBlazor;

/// <summary>
/// 向承载该层级列的表格暴露层级列的缩进量。
/// <para>
/// 表格将其层级列保存为 <c>IDataGridColumn&lt;TData&gt;</c>，而列自身的类型
/// 为绑定属性携带了第二个类型参数，因此表格无法命名该类型。这个窄接口正是
/// 表格读取它所需唯一值的方式——这也是为什么 <c>IndentSize</c> 此前无人读取，
/// 每一级层级都按硬编码的 24 像素缩进。
/// </para>
/// </summary>
internal interface IHierarchyIndentColumn
{
    /// <summary>每个深度层级的缩进量，以像素为单位。</summary>
    public int IndentSize { get; }
}
