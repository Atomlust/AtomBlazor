using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

public partial class DataView<TItem> where TItem : class
{
    /// <summary>项目选择是否禁用、单选或多选。</summary>
    [Parameter] public DataTableSelectionMode SelectionMode { get; set; }
    /// <summary>已选中的项目。ItemKey 可在实例重新加载后保留选中状态。</summary>
    [Parameter] public IReadOnlyCollection<TItem> SelectedItems { get; set; } = [];
    /// <summary>项目选择发生变化时调用。</summary>
    [Parameter] public EventCallback<IReadOnlyCollection<TItem>> SelectedItemsChanged { get; set; }
    /// <summary>项目的唯一稳定键。默认使用项目自身的相等性语义。</summary>
    [Parameter] public Func<TItem, object>? ItemKey { get; set; }
    /// <summary>项目选择控件的可访问标签。</summary>
    [Parameter] public Func<TItem, string>? ItemLabel { get; set; }
    /// <summary>项目的选择控件是否被禁用。</summary>
    [Parameter] public Func<TItem, bool>? IsItemDisabled { get; set; }
    /// <summary>对当前已加载页面/批次的可选分组，按首次出现顺序排列。</summary>
    [Parameter] public Func<TItem, string?>? GroupBy { get; set; }
    /// <summary>自定义分组标题。接收分组键与当前已加载的组内项目。</summary>
    [Parameter] public RenderFragment<DataViewGroupContext<TItem>>? GroupHeaderTemplate { get; set; }
    /// <summary>对列表行启用虚拟化。网格布局仍正常渲染；数据提供程序加载仍按页面/批次进行。</summary>
    [Parameter] public bool EnableVirtualization { get; set; }
    /// <summary>列表行高估算值（像素），含行间距。</summary>
    [Parameter] public float VirtualItemSize { get; set; } = 56;
    /// <summary>可见窗口前后额外渲染的行数。</summary>
    [Parameter] public int OverscanCount { get; set; } = 3;

    private HashSet<object> selectedKeys = [];
    private bool VirtualizeList => EnableVirtualization && _effectiveLayout == DataViewLayout.List;
    private string ScrollContainerStyle => $"height: {ScrollHeight ?? "400px"}";
    private object GetItemKey(TItem item) => ItemKey?.Invoke(item) ?? item;
    private bool IsSelected(TItem item) => selectedKeys.Contains(GetItemKey(item));
    private bool ItemDisabled(TItem item) => IsItemDisabled?.Invoke(item) == true;
    private string SelectionLabel(TItem item) => ItemLabel?.Invoke(item) ?? Localizer["DataView.SelectItem"];

    private void SyncSelection()
    {
        if (!Enum.IsDefined(SelectionMode)) { throw new ArgumentOutOfRangeException(nameof(SelectionMode)); }
        if (!float.IsFinite(VirtualItemSize) || VirtualItemSize <= 0) { throw new ArgumentOutOfRangeException(nameof(VirtualItemSize)); }
        if (OverscanCount < 0) { throw new ArgumentOutOfRangeException(nameof(OverscanCount)); }
        selectedKeys = SelectedItems.Select(GetItemKey).ToHashSet();
    }

    private async Task ToggleSelectionAsync(TItem item, bool selected)
    {
        if (SelectionMode == DataTableSelectionMode.None || ItemDisabled(item)) { return; }
        var key = GetItemKey(item);
        if (selected == selectedKeys.Contains(key)) { return; }
        var next = SelectionMode == DataTableSelectionMode.Single
            ? new List<TItem>()
            : SelectedItems.Where(candidate => !Equals(GetItemKey(candidate), key)).ToList();
        if (selected) { next.Add(item); }
        SelectedItems = next;
        selectedKeys = next.Select(GetItemKey).ToHashSet();
        _parametersChanged = true;
        await SelectedItemsChanged.InvokeAsync(next);
        StateHasChanged();
    }

    private IEnumerable<DataViewGroupContext<TItem>> VisibleGroups =>
        _visibleData.GroupBy(item => GroupBy!(item))
            .Select(group => new DataViewGroupContext<TItem>(group.Key, group.ToList()));
}
