using AtomBlazor.Primitives;
namespace AtomBlazor;

/// <summary>Select 选项列表的呈现形态。</summary>
public enum SelectPresentation
{
    /// <summary>锚定在触发器上的浮动列表。</summary>
    Popover,
    /// <summary>带可滚动列表的模态底部抽屉。</summary>
    BottomSheet
}
