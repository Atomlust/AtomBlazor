using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

public partial class TreeItem : ComponentBase
{
    private AtomBlazor.Primitives.TreeItem? primitiveRef;

    /// <summary>
    /// 本节点的唯一值/标识。
    /// </summary>
    [Parameter, EditorRequired]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 节点的显示文本。
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// 节点的 Lucide 图标名。
    /// </summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>
    /// 显示在标签之后的徽章文本。
    /// </summary>
    [Parameter]
    public string? Badge { get; set; }

    /// <summary>
    /// 本节点是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 针对本节点覆写父树的 checkable 设置。
    /// </summary>
    [Parameter]
    public bool? Checkable { get; set; }

    /// <summary>
    /// 显式标记为叶子节点（即使子级尚未加载也不显示展开箭头）。
    /// </summary>
    [Parameter]
    public bool? IsLeaf { get; set; }

    /// <summary>
    /// 替换默认文本的自定义标签内容。
    /// </summary>
    [Parameter]
    public RenderFragment? LabelTemplate { get; set; }

    /// <summary>
    /// 悬停时显示的操作区（编辑、删除按钮）。
    /// </summary>
    [Parameter]
    public RenderFragment? ActionsTemplate { get; set; }

    /// <summary>
    /// 子级 TreeItem 节点。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 节点行的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 树启用 Draggable 时本节点是否可拖拽。
    /// </summary>
    [Parameter]
    public bool? IsDraggable { get; set; }

    /// <summary>
    /// 级联参数：接收树上下文。
    /// </summary>
    [CascadingParameter]
    public TreeViewContext? TreeContext { get; set; }

    /// <summary>
    /// 级联参数：接收父级基元树节点。
    /// </summary>
    [CascadingParameter]
    public AtomBlazor.Primitives.TreeItem? ParentPrimitiveItem { get; set; }

    private int Depth => (ParentPrimitiveItem?.Depth + 1) ?? 0;

    private bool IsExpanded => TreeContext?.IsExpanded(Value) ?? false;

    private bool IsSelected => TreeContext?.IsSelected(Value) ?? false;

    private bool IsChecked => TreeContext?.IsChecked(Value) ?? false;

    private bool IsIndeterminate => TreeContext?.IsIndeterminate(Value) ?? false;

    private bool ShowCheckbox =>
        ((Checkable ?? TreeContext?.Checkable ?? false) && !Disabled) || (Checkable == true);

    private bool HasExpandableChildren =>
        IsLeaf != true && (ChildContent != null || (TreeContext?.HasChildren(Value) ?? false));

    private string NodeCssClass => ClassNames.cn(
        "atom:group/treeitem atom:flex atom:items-center atom:gap-1.5 atom:py-1 atom:px-2 atom:rounded-md atom:cursor-pointer atom:select-none",
        "atom:hover:bg-accent/50 atom:transition-colors",
        IsSelected ? "atom:bg-accent atom:text-accent-foreground" : "atom:text-foreground",
        Disabled ? "atom:opacity-50 atom:cursor-not-allowed" : null,
        Class
    );

    private string NodeStyle => $"padding-left: {(Depth * 1.25) + 0.5}rem;";

    private string ChevronCssClass => ClassNames.cn(
        "atom:h-4 atom:w-4 atom:shrink-0 atom:text-muted-foreground atom:transition-transform atom:duration-200",
        IsExpanded ? "atom:rotate-90" : null
    );
}
