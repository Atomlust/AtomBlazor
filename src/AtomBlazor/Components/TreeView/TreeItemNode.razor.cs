using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 递归渲染一个数据驱动的树节点。通常由 TreeView 创建；公开是为高级模式下的自由组合。
/// </summary>
public partial class TreeItemNode<TItem> : ComponentBase
{
    [Parameter, EditorRequired]
    public TItem Item { get; set; } = default!;

    [Parameter, EditorRequired]
    public Func<TItem, string> TextField { get; set; } = null!;

    [Parameter, EditorRequired]
    public Func<TItem, string> ValueField { get; set; } = null!;

    [Parameter]
    public Func<TItem, string?>? IconField { get; set; }

    [Parameter, EditorRequired]
    public Func<TItem, IEnumerable<TItem>> ChildrenAccessor { get; set; } = null!;

    [Parameter, EditorRequired]
    public Func<TItem, bool> HasChildrenAccessor { get; set; } = null!;

    [Parameter, EditorRequired]
    public Func<TItem, bool> IsFilteredOut { get; set; } = null!;

    [Parameter]
    public bool IsMatchHighlighted { get; set; }

    [Parameter]
    public string? SearchText { get; set; }

    [Parameter]
    public bool ShowIcons { get; set; } = true;

    [Parameter]
    public bool Checkable { get; set; }

    [Parameter]
    public bool Draggable { get; set; }

    [Parameter]
    public Func<TItem, bool>? AllowDrag { get; set; }

    [Parameter]
    public HashSet<string> LoadingNodes { get; set; } = new();

    [Parameter]
    public HashSet<string> ErrorNodes { get; set; } = new();

    [Parameter]
    public EventCallback<string> OnRetryLoad { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [CascadingParameter]
    public TreeViewContext? TreeContext { get; set; }

    [CascadingParameter]
    public AtomBlazor.Primitives.TreeItemPrimitive? ParentPrimitiveItem { get; set; }

    private string NodeValue => ValueField(Item);

    private string LabelValue => TextField(Item);

    private string? IconValue => IconField?.Invoke(Item);

    private bool HasChildren => HasChildrenAccessor(Item);

    private bool IsLoading => LoadingNodes.Contains(NodeValue);

    private bool HasError => ErrorNodes.Contains(NodeValue);

    private bool IsExpanded => TreeContext?.IsExpanded(NodeValue) ?? false;

    private bool IsSelected => TreeContext?.IsSelected(NodeValue) ?? false;

    private bool IsChecked => TreeContext?.IsChecked(NodeValue) ?? false;

    private bool IsIndeterminate => TreeContext?.IsIndeterminate(NodeValue) ?? false;

    private bool IsDraggable => Draggable && (AllowDrag == null || AllowDrag(Item));

    private int Depth => (ParentPrimitiveItem?.Depth + 1) ?? 0;

    private IEnumerable<TItem> Children => ChildrenAccessor(Item);

    private string NodeCssClass => ClassNames.cn(
        "atom:group/treeitem atom:flex atom:items-center atom:gap-1.5 atom:py-1 atom:px-2 atom:rounded-md atom:cursor-pointer atom:select-none",
        "atom:hover:bg-accent/50 atom:transition-colors",
        IsSelected ? "atom:bg-accent atom:text-accent-foreground" : "atom:text-foreground"
    );

    private string NodeStyle => $"padding-left: {(Depth * 1.25) + 0.5}rem;";

    private string ChildNodeStyle => $"padding-left: {((Depth + 1) * 1.25) + 0.5}rem;";

    private string ChevronCssClass => ClassNames.cn(
        "atom:h-4 atom:w-4 atom:shrink-0 atom:text-muted-foreground atom:transition-transform atom:duration-200",
        IsExpanded ? "atom:rotate-90" : null
    );

    private RenderFragment HighlightedLabel => builder =>
    {
        var text = LabelValue;
        if (string.IsNullOrEmpty(SearchText))
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "atom:truncate");
            builder.AddContent(2, text);
            builder.CloseElement();
            return;
        }

        var index = text.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "atom:truncate");
            builder.AddContent(2, text);
            builder.CloseElement();
            return;
        }

        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "atom:truncate");

        if (index > 0)
        {
            builder.AddContent(2, text[..index]);
        }

        builder.OpenElement(3, "mark");
        builder.AddAttribute(4, "class", "atom:bg-yellow-200 atom:dark:bg-yellow-900/50 atom:rounded-sm");
        builder.AddContent(5, text.Substring(index, SearchText.Length));
        builder.CloseElement();

        if (index + SearchText.Length < text.Length)
        {
            builder.AddContent(6, text[(index + SearchText.Length)..]);
        }

        builder.CloseElement();
    };

    private async Task HandleRetry()
    {
        if (OnRetryLoad.HasDelegate)
        {
            await OnRetryLoad.InvokeAsync(NodeValue);
        }
    }
}
