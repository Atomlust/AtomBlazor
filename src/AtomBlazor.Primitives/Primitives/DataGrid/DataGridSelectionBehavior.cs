

// Deliberately in the AtomBlazor.Primitives namespace rather than
// AtomBlazor.Primitives.DataGrid, alongside SortDirection. An app that imports
// AtomBlazor.Components cannot also import AtomBlazor.Primitives.DataGrid without
// making the name DataGrid ambiguous, so an enum a consumer has to name in markup goes here.
namespace AtomBlazor.Primitives;



/// <summary>
/// 定义点击行时选择状态如何变化。
/// </summary>
public enum DataGridSelectionBehavior
{
    /// <summary>
    /// 每次点击切换单行的选中/未选中状态，其余行保持不变。
    /// 忽略修饰键。这是默认值。
    /// </summary>
    Toggle,

    /// <summary>
    /// 点击会替换当前选择，与文件管理器或电子表格的行为一致。
    /// 普通点击只选中被点击的行，Shift+Click 选中从锚点到该行的范围，
    /// Ctrl+Click（macOS 上为 Cmd+Click）增删单行而不影响其余选择。
    /// </summary>
    /// <remarks>
    /// 范围选择与叠加选择需要 <see cref="SelectionMode.Multiple"/>。在
    /// <see cref="SelectionMode.Single"/> 下，每次点击都按普通点击处理。
    /// 即使采用此行为，选择列中的复选框点击仍保持 <see cref="TogglePrimitive"/>
    /// 语义，因为会清空其余选择的复选框将不可用。
    /// </remarks>
    Replace
}
