using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 已注册命令项的元数据。
/// </summary>
public class CommandItemMetadata
{
    /// <summary>
    /// 获取或设置命令项的值。
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置用于搜索过滤的文本。
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// 获取或设置一个值，指示该项是否禁用。
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置该项被选中时调用的回调。
    /// </summary>
    public EventCallback OnSelect { get; set; }

    /// <summary>
    /// 获取或设置该项所属分组的 ID。
    /// </summary>
    public string? GroupId { get; set; }
}

/// <summary>
/// 供虚拟化分组参与导航的接口。
/// </summary>
public interface IVirtualizedGroupHandler
{
    /// <summary>
    /// 获取该虚拟化分组中可见项的数量。
    /// </summary>
    public int VisibleItemCount { get; }

    /// <summary>
    /// 获取或设置该分组内的焦点索引（未聚焦时为 -1）。
    /// </summary>
    public int FocusedIndex { get; set; }

    /// <summary>
    /// 若该元素 id 属于本分组，则聚焦对应项。
    /// </summary>
    /// <param name="elementId">悬停元素的 <c>id</c>。</param>
    /// <returns>若该 id 属于本分组的项则返回 <c>true</c>。</returns>
    public bool TryHoverItem(string elementId);

    /// <summary>
    /// 选中当前聚焦的项。
    /// </summary>
    public Task SelectFocusedItemAsync();

    /// <summary>
    /// 通知该分组重新渲染。
    /// </summary>
    public void NotifyStateChanged();

    /// <summary>
    /// 滚动到指定索引并设置焦点。用于懒加载模式下目标项可能尚未加载时的
    /// 环绕导航。
    /// </summary>
    /// <param name="index">要滚动到并聚焦的索引。</param>
    public Task ScrollToIndexAsync(int index);
}

/// <summary>
/// Command 组件及其子组件的上下文。
/// 管理搜索查询、项注册、过滤与键盘导航。
/// </summary>
public class CommandContext
{
    private readonly List<CommandItemMetadata> _items = new();
    private readonly List<IVirtualizedGroupHandler> _virtualizedGroups = new();
    private string _searchQuery = string.Empty;
    private int _focusedIndex = -1;
    private int _focusedVirtualizedGroupIndex = -1; // 哪个虚拟化分组持有焦点（-1 = 常规项）
    private Func<CommandItemMetadata, string, bool>? _filterFunction;
    private bool _disabled;
    private bool _hasRegisteredItems;
    private bool _isKeyboardNavigating; // 键盘导航期间抑制悬停的标志
    private List<CommandItemMetadata>? filteredItems;
    private readonly Dictionary<CommandItemMetadata, int> filteredIndices = new();

    /// <summary>
    /// 状态变化时引发的事件。
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// 焦点变化时引发的事件。参数为（旧索引，新索引）。
    /// </summary>
    public event Action<int, int>? OnFocusChanged;

    /// <summary>
    /// 搜索查询变化时引发的事件。
    /// </summary>
    public event Action? OnSearchChanged;

    /// <summary>
    /// 键盘导航状态变化时引发的事件。
    /// </summary>
    public event Action<bool>? OnKeyboardNavigationChanged;

    /// <summary>
    /// 获取或设置项被选中时调用的回调。
    /// </summary>
    public EventCallback<string> OnValueChange { get; set; }

    /// <summary>
    /// 获取该命令上下文的唯一 ID。
    /// </summary>
    public string Id { get; } = $"command-{Guid.NewGuid():N}";

    /// <summary>
    /// 获取命令输入框的 ID。
    /// </summary>
    public string InputId => $"{Id}-input";

    /// <summary>
    /// 获取命令列表的 ID。
    /// </summary>
    public string ListId => $"{Id}-list";

    /// <summary>
    /// 获取当前搜索查询。
    /// </summary>
    public string SearchQuery => _searchQuery;

    /// <summary>
    /// 获取或设置自定义过滤函数。
    /// </summary>
    public Func<CommandItemMetadata, string, bool>? FilterFunction
    {
        get => _filterFunction;
        set
        {
            // 稳定的委托可能闭合了可变状态。父组件每次渲染都会重新应用该
            // 参数，这也正是那份状态的刷新边界。
            _filterFunction = value;
            InvalidateFilter();
        }
    }

    /// <summary>
    /// 获取或设置命令是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用期间列表既不移动焦点也不选中任何东西，因此该值曾被存储却从未被读取：
    /// 一个禁用的命令仍会响应方向键，仍会在回车或点击时抛出 <c>OnValueChange</c>。
    /// </remarks>
    public bool Disabled
    {
        get => _disabled;
        set => _disabled = value;
    }

    /// <summary>
    /// 获取过滤项中当前聚焦项的索引。
    /// </summary>
    public int FocusedIndex => _focusedIndex;

    /// <summary>
    /// 获取当前聚焦的虚拟化分组索引（焦点在常规项上时为 -1）。
    /// </summary>
    public int FocusedVirtualizedGroupIndex => _focusedVirtualizedGroupIndex;

    /// <summary>
    /// 获取当前是否处于键盘导航状态（抑制鼠标悬停）。
    /// </summary>
    public bool IsKeyboardNavigating => _isKeyboardNavigating;

    /// <summary>
    /// 鼠标实际移动时调用，用于重新启用悬停聚焦。
    /// </summary>
    public void OnMouseMove()
    {
        if (_isKeyboardNavigating)
        {
            _isKeyboardNavigating = false;
            OnKeyboardNavigationChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// 注册一个参与导航的虚拟化分组处理器。
    /// </summary>
    public void RegisterVirtualizedGroup(IVirtualizedGroupHandler handler)
    {
        if (!_virtualizedGroups.Contains(handler))
        {
            _virtualizedGroups.Add(handler);
        }
    }

    /// <summary>
    /// 注销一个虚拟化分组处理器。
    /// </summary>
    public void UnregisterVirtualizedGroup(IVirtualizedGroupHandler handler) =>
        _virtualizedGroups.Remove(handler);

    /// <summary>
    /// 获取已注册虚拟化分组的列表。
    /// </summary>
    public IReadOnlyList<IVirtualizedGroupHandler> GetVirtualizedGroups() =>
        _virtualizedGroups.AsReadOnly();

    /// <summary>
    /// 将焦点设置到指定虚拟化分组，并清除其他所有分组与常规项的焦点。
    /// 这确保整个命令内同时只有一个项处于聚焦状态。
    /// </summary>
    public void SetVirtualizedGroupFocus(IVirtualizedGroupHandler group, int indexInGroup)
    {
        // 清除常规项的焦点
        if (_focusedIndex >= 0)
        {
            var previousIndex = _focusedIndex;
            _focusedIndex = -1;
            OnFocusChanged?.Invoke(previousIndex, -1);
        }

        // 找到分组索引并设置
        var groupIndex = _virtualizedGroups.IndexOf(group);
        if (groupIndex >= 0)
        {
            _focusedVirtualizedGroupIndex = groupIndex;
        }

        // 清除其他所有虚拟化分组的焦点
        foreach (var otherGroup in _virtualizedGroups)
        {
            if (otherGroup != group && otherGroup.FocusedIndex >= 0)
            {
                otherGroup.FocusedIndex = -1;
                otherGroup.NotifyStateChanged();
            }
        }
    }

    /// <summary>
    /// 更新搜索查询并通知订阅者。
    /// </summary>
    /// <param name="query">新的搜索查询。</param>
    public void SetSearchQuery(string query)
    {
        if (_searchQuery != query)
        {
            _searchQuery = query;
            InvalidateFilter();
            _focusedIndex = -1; // 搜索变化时重置焦点
            _focusedVirtualizedGroupIndex = -1; // 重置虚拟化分组焦点

            // 重置所有虚拟化分组的焦点
            foreach (var group in _virtualizedGroups)
            {
                group.FocusedIndex = -1;
            }

            OnSearchChanged?.Invoke();
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// 向命令上下文注册一个项。
    /// </summary>
    /// <returns>已注册项的索引。</returns>
    public int RegisterItem(string? value, string? searchText, bool disabled, EventCallback onSelect, string? groupId = null)
    {
        var metadata = new CommandItemMetadata
        {
            Value = value,
            SearchText = searchText ?? value,
            Disabled = disabled,
            OnSelect = onSelect,
            GroupId = groupId
        };
        _items.Add(metadata);
        InvalidateFilter();
        _hasRegisteredItems = true;
        NotifyStateChanged(); // 通知以便 CommandEmpty 更新
        return _items.Count - 1;
    }

    /// <summary>
    /// 更新既有项的元数据。
    /// </summary>
    public void UpdateItem(int index, string? value, string? searchText, bool disabled, EventCallback onSelect, string? groupId = null)
    {
        if (index >= 0 && index < _items.Count)
        {
            var item = _items[index];
            if (item == null)
            {
                return;
            }

            if (item.Value != value || item.SearchText != (searchText ?? value)
                || item.Disabled != disabled || item.GroupId != groupId)
            {
                InvalidateFilter();
            }
            item.Value = value;
            item.SearchText = searchText ?? value;
            item.Disabled = disabled;
            item.OnSelect = onSelect;
            item.GroupId = groupId;
        }
    }

    /// <summary>
    /// 从命令上下文注销一个项。
    /// </summary>
    public void UnregisterItem(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            _items[index] = null!; // 标记为 null，不移除以保留索引
            InvalidateFilter();
            NotifyStateChanged(); // 通知以便 CommandEmpty 更新
        }
    }

    /// <summary>
    /// 获取按当前搜索查询过滤后的项列表。
    /// </summary>
    public List<CommandItemMetadata> GetFilteredItems()
    {
        // 元数据是公开可变的。显式索取快照的调用方仍能观察到通过 GetItemByIndex
        // 做出的修改，而组件渲染路径使用的是缓存查找。
        InvalidateFilter();
        return new(GetFilteredItemsCore());
    }

    // 让公开列表保持为调用方自有的快照。内部消费者共享结果与索引映射，
    // 因此刷新 N 个项组件不会每项跑 N 遍谓词。
    private List<CommandItemMetadata> GetFilteredItemsCore()
    {
        if (filteredItems != null)
        {
            return filteredItems;
        }

        filteredItems = new List<CommandItemMetadata>();
        filteredIndices.Clear();
        var showAll = string.IsNullOrWhiteSpace(_searchQuery);
        foreach (var item in _items)
        {
            if (item != null && (showAll || (_filterFunction != null
                ? _filterFunction(item, _searchQuery)
                : item.SearchText?.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) == true)))
            {
                filteredIndices[item] = filteredItems.Count;
                filteredItems.Add(item);
            }
        }

        return filteredItems;
    }

    private void InvalidateFilter() => filteredItems = null;

    internal int GetFilteredIndex(int registrationIndex)
    {
        GetFilteredItemsCore();
        var item = GetItemByIndex(registrationIndex);
        return item != null && filteredIndices.TryGetValue(item, out var index) ? index : -1;
    }

    /// <summary>
    /// 按注册索引获取项。
    /// </summary>
    public CommandItemMetadata? GetItemByIndex(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            return _items[index];
        }
        return null;
    }

    /// <summary>
    /// 获取项 ID。
    /// </summary>
    public string GetItemId(int index) => $"{Id}-item-{index}";

    /// <summary>
    /// 获取是否存在任何可见项。
    /// 若尚未注册任何项则返回 true（避免初始加载时闪现"无结果"），
    /// 除非外部过滤已启用（设置了 FilterFunction）——此时项列表由消费者
    /// 控制，空列表就是真正的"无结果"。
    /// </summary>
    public bool HasVisibleItems()
    {
        // 使用外部过滤时，信任项的状态——空列表就是无结果。
        // 未使用外部过滤时，等项有机会注册完毕再判断，
        // 以免初始渲染期间闪现"无结果"。
        if (!_hasRegisteredItems && FilterFunction == null)
        {
            return true;
        }

        return GetFilteredItemsCore().Any(i => !i.Disabled);
    }

    /// <summary>
    /// 获取指定分组是否存在任何可见项。
    /// </summary>
    /// <param name="groupId">要检查的分组 ID。</param>
    /// <returns>分组有可见项时为 true，否则为 false。</returns>
    public bool GroupHasVisibleItems(string groupId)
    {
        var filteredItems = GetFilteredItemsCore();
        return filteredItems.Any(i => i.GroupId == groupId);
    }

    /// <summary>
    /// 根据悬停元素的 id，把高亮移动到指针下方的项。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 由 JavaScript 调用，指针每真正移动到一个新项上才回调一次。项过去自行绑定
    /// <c>@onmouseenter</c> 与 <c>@onmousemove</c>，在 Blazor Server 上这意味着指针
    /// 每移动一像素都是一次电路消息。绑定 mousemove 只是为了区分真实悬停与键盘导航
    /// 时列表在静止指针下方滚过的假悬停——如今浏览器自己能回答这个问题
    /// （<c>elementUtils.observeHover</c>），所以到达这里的必然是真实悬停，
    /// 而真实悬停也一如往昔地终结键盘模式。
    /// </para>
    /// </remarks>
    /// <param name="elementId">悬停的 <c>role="option"</c> 元素的 <c>id</c>。</param>
    public void HoverItemById(string elementId)
    {
        if (_disabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(elementId))
        {
            return;
        }

        var prefix = $"{Id}-item-";
        if (elementId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(elementId.AsSpan(prefix.Length), out var rawIndex))
        {
            if (rawIndex < 0 || rawIndex >= _items.Count)
            {
                return;
            }

            var item = _items[rawIndex];
            if (item == null || item.Disabled)
            {
                return;
            }

            var filteredIndex = GetFilteredIndex(rawIndex);
            if (filteredIndex < 0)
            {
                return;
            }

            OnMouseMove();
            SetFocusedIndex(filteredIndex);
            return;
        }

        foreach (var group in _virtualizedGroups)
        {
            if (group.TryHoverItem(elementId))
            {
                OnMouseMove();
                return;
            }
        }
    }

    /// <summary>
    /// 设置过滤项中的焦点索引，并清除虚拟化分组的焦点。
    /// </summary>
    public void SetFocusedIndex(int index)
    {
        if (_focusedIndex != index)
        {
            var previousIndex = _focusedIndex;
            _focusedIndex = index;

            // 聚焦常规项时清除所有虚拟化分组的焦点
            if (index >= 0)
            {
                _focusedVirtualizedGroupIndex = -1;
                foreach (var group in _virtualizedGroups)
                {
                    if (group.FocusedIndex >= 0)
                    {
                        group.FocusedIndex = -1;
                        group.NotifyStateChanged();
                    }
                }
            }

            // 使用定向焦点通知代替全局状态变更
            OnFocusChanged?.Invoke(previousIndex, index);
        }
    }

    /// <summary>
    /// 将焦点移动到下一项/上一项，涵盖虚拟化分组。
    /// </summary>
    /// <param name="direction">1 为向后，-1 为向前。</param>
    public async Task MoveFocusAsync(int direction)
    {
        if (_disabled)
        {
            return;
        }

        // 键盘导航期间抑制鼠标悬停
        if (!_isKeyboardNavigating)
        {
            _isKeyboardNavigating = true;
            OnKeyboardNavigationChanged?.Invoke(true);
        }

        var allFiltered = GetFilteredItemsCore();
        var enabledFilteredItems = allFiltered.Where(i => !i.Disabled).ToList();

        // 取得有可见项的虚拟化分组
        var activeVirtualizedGroups = _virtualizedGroups.Where(g => g.VisibleItemCount > 0).ToList();

        // 计算可导航项总数
        var totalRegularItems = allFiltered.Count;
        var totalVirtualizedItems = activeVirtualizedGroups.Sum(g => g.VisibleItemCount);

        if (enabledFilteredItems.Count == 0 && totalVirtualizedItems == 0)
        {
            return;
        }

        // 当前在虚拟化分组中？
        if (_focusedVirtualizedGroupIndex >= 0)
        {
            // 校验索引依然有效（分组可能因过滤而变化）
            if (_focusedVirtualizedGroupIndex >= activeVirtualizedGroups.Count)
            {
                _focusedVirtualizedGroupIndex = -1;
                // 落到常规项处理
            }
            else
            {
                var currentGroup = activeVirtualizedGroups[_focusedVirtualizedGroupIndex];
                var currentIndexInGroup = currentGroup.FocusedIndex;
                var newIndexInGroup = currentIndexInGroup + direction;

                if (newIndexInGroup >= 0 && newIndexInGroup < currentGroup.VisibleItemCount)
                {
                    // 留在当前分组内
                    currentGroup.FocusedIndex = newIndexInGroup;
                    currentGroup.NotifyStateChanged();
                    return;
                }
                else if (direction > 0)
                {
                    // 向后移动——尝试下一个虚拟化分组或环绕回常规项
                    currentGroup.FocusedIndex = -1;
                    currentGroup.NotifyStateChanged();

                    if (_focusedVirtualizedGroupIndex + 1 < activeVirtualizedGroups.Count)
                    {
                        // 移动到下一个虚拟化分组
                        _focusedVirtualizedGroupIndex++;
                        var nextGroup = activeVirtualizedGroups[_focusedVirtualizedGroupIndex];
                        // 为保持一致性使用 ScrollToIndexAsync
                        await nextGroup.ScrollToIndexAsync(0);
                    }
                    else
                    {
                        // 环绕到第一个常规项（无常规项时则到第一个虚拟化分组）
                        _focusedVirtualizedGroupIndex = -1;
                        if (enabledFilteredItems.Count > 0)
                        {
                            FocusFirstRegularItem();
                        }
                        else if (activeVirtualizedGroups.Count > 0)
                        {
                            // 无常规项，环绕到第一个虚拟化分组
                            _focusedVirtualizedGroupIndex = 0;
                            var firstGroup = activeVirtualizedGroups[0];
                            // 使用 ScrollToIndexAsync 确保滚动位置同步更新
                            await firstGroup.ScrollToIndexAsync(0);
                        }
                    }
                    return;
                }
                else
                {
                    // 向前移动——尝试上一个虚拟化分组或进入常规项
                    currentGroup.FocusedIndex = -1;
                    currentGroup.NotifyStateChanged();

                    if (_focusedVirtualizedGroupIndex > 0)
                    {
                        // 移动到上一个虚拟化分组（末项）
                        _focusedVirtualizedGroupIndex--;
                        var prevGroup = activeVirtualizedGroups[_focusedVirtualizedGroupIndex];
                        // 面向可能未加载的项，使用 ScrollToIndexAsync
                        await prevGroup.ScrollToIndexAsync(prevGroup.VisibleItemCount - 1);
                    }
                    else
                    {
                        // 移动到最后一个常规项（无常规项时则到最后一个虚拟化分组）
                        _focusedVirtualizedGroupIndex = -1;
                        if (enabledFilteredItems.Count > 0)
                        {
                            FocusLastRegularItem();
                        }
                        else if (activeVirtualizedGroups.Count > 0)
                        {
                            // 无常规项，环绕到最后一个虚拟化分组
                            _focusedVirtualizedGroupIndex = activeVirtualizedGroups.Count - 1;
                            var lastGroup = activeVirtualizedGroups[_focusedVirtualizedGroupIndex];
                            // 面向可能未加载的项，使用 ScrollToIndexAsync
                            await lastGroup.ScrollToIndexAsync(lastGroup.VisibleItemCount - 1);
                        }
                    }
                    return;
                }
            }
        }

        // 当前在常规项中
        var currentIndex = _focusedIndex;
        var newIndex = currentIndex + direction;

        if (direction > 0)
        {
            // 向后移动
            if (newIndex >= totalRegularItems)
            {
                // 有虚拟化分组则移动到第一个分组
                if (activeVirtualizedGroups.Count > 0)
                {
                    var previousIndex = _focusedIndex;
                    _focusedIndex = -1;
                    OnFocusChanged?.Invoke(previousIndex, -1);

                    _focusedVirtualizedGroupIndex = 0;
                    var firstGroup = activeVirtualizedGroups[0];
                    // 使用 ScrollToIndexAsync 确保滚动位置同步更新
                    await firstGroup.ScrollToIndexAsync(0);
                    return;
                }
                else
                {
                    // 环绕到第一个常规项
                    newIndex = 0;
                }
            }

            // 跳过禁用项
            while (newIndex < totalRegularItems && allFiltered[newIndex].Disabled)
            {
                newIndex++;
            }

            if (newIndex >= totalRegularItems)
            {
                // 其余项全部禁用，转入虚拟化分组
                if (activeVirtualizedGroups.Count > 0)
                {
                    var previousIndex = _focusedIndex;
                    _focusedIndex = -1;
                    OnFocusChanged?.Invoke(previousIndex, -1);

                    _focusedVirtualizedGroupIndex = 0;
                    var firstGroup = activeVirtualizedGroups[0];
                    // 使用 ScrollToIndexAsync 确保滚动位置同步更新
                    await firstGroup.ScrollToIndexAsync(0);
                    return;
                }
                newIndex = 0;
                while (newIndex < totalRegularItems && allFiltered[newIndex].Disabled)
                {
                    newIndex++;
                }
            }
        }
        else
        {
            // 向前移动
            if (newIndex < 0)
            {
                // 有虚拟化分组则移动到最后一个分组
                if (activeVirtualizedGroups.Count > 0)
                {
                    var previousIndex = _focusedIndex;
                    _focusedIndex = -1;
                    OnFocusChanged?.Invoke(previousIndex, -1);

                    _focusedVirtualizedGroupIndex = activeVirtualizedGroups.Count - 1;
                    var lastGroup = activeVirtualizedGroups[_focusedVirtualizedGroupIndex];
                    // 面向可能未加载的项，使用 ScrollToIndexAsync
                    await lastGroup.ScrollToIndexAsync(lastGroup.VisibleItemCount - 1);
                    return;
                }
                else
                {
                    // 环绕到最后一个常规项
                    newIndex = totalRegularItems - 1;
                }
            }

            // 跳过禁用项
            while (newIndex >= 0 && allFiltered[newIndex].Disabled)
            {
                newIndex--;
            }

            if (newIndex < 0)
            {
                // 前面各项全部禁用，转入虚拟化分组
                if (activeVirtualizedGroups.Count > 0)
                {
                    var previousIndex = _focusedIndex;
                    _focusedIndex = -1;
                    OnFocusChanged?.Invoke(previousIndex, -1);

                    _focusedVirtualizedGroupIndex = activeVirtualizedGroups.Count - 1;
                    var lastGroup = activeVirtualizedGroups[_focusedVirtualizedGroupIndex];
                    // 面向可能未加载的项，使用 ScrollToIndexAsync
                    await lastGroup.ScrollToIndexAsync(lastGroup.VisibleItemCount - 1);
                    return;
                }
                newIndex = totalRegularItems - 1;
                while (newIndex >= 0 && allFiltered[newIndex].Disabled)
                {
                    newIndex--;
                }
            }
        }

        if (newIndex >= 0 && newIndex < totalRegularItems)
        {
            SetFocusedIndex(newIndex);
        }
    }

    private void FocusFirstRegularItem()
    {
        var allFiltered = GetFilteredItemsCore();
        for (var i = 0; i < allFiltered.Count; i++)
        {
            if (!allFiltered[i].Disabled)
            {
                SetFocusedIndex(i);
                return;
            }
        }
    }

    private void FocusLastRegularItem()
    {
        var allFiltered = GetFilteredItemsCore();
        for (var i = allFiltered.Count - 1; i >= 0; i--)
        {
            if (!allFiltered[i].Disabled)
            {
                SetFocusedIndex(i);
                return;
            }
        }
    }

    /// <summary>
    /// 将焦点移动到第一个启用项（先常规项，再虚拟化分组）。
    /// </summary>
    public async Task FocusFirstAsync()
    {
        if (_disabled)
        {
            return;
        }

        // 键盘导航期间抑制鼠标悬停
        if (!_isKeyboardNavigating)
        {
            _isKeyboardNavigating = true;
            OnKeyboardNavigationChanged?.Invoke(true);
        }

        // 先尝试常规项
        var filteredItems = GetFilteredItemsCore();
        for (var i = 0; i < filteredItems.Count; i++)
        {
            if (!filteredItems[i].Disabled)
            {
                // 清除虚拟化分组焦点
                ClearVirtualizedGroupFocus();
                SetFocusedIndex(i);
                return;
            }
        }

        // 再尝试第一个虚拟化分组
        var activeGroups = _virtualizedGroups.Where(g => g.VisibleItemCount > 0).ToList();
        if (activeGroups.Count > 0)
        {
            ClearVirtualizedGroupFocus();
            _focusedVirtualizedGroupIndex = 0;
            await activeGroups[0].ScrollToIndexAsync(0);
        }
    }

    /// <summary>
    /// 将焦点移动到最后一个启用项（先最后一个虚拟化分组，再常规项）。
    /// </summary>
    public async Task FocusLastAsync()
    {
        if (_disabled)
        {
            return;
        }

        // 键盘导航期间抑制鼠标悬停
        if (!_isKeyboardNavigating)
        {
            _isKeyboardNavigating = true;
            OnKeyboardNavigationChanged?.Invoke(true);
        }

        // 先尝试最后一个虚拟化分组
        var activeGroups = _virtualizedGroups.Where(g => g.VisibleItemCount > 0).ToList();
        if (activeGroups.Count > 0)
        {
            ClearVirtualizedGroupFocus();
            _focusedVirtualizedGroupIndex = activeGroups.Count - 1;
            var lastGroup = activeGroups[_focusedVirtualizedGroupIndex];
            await lastGroup.ScrollToIndexAsync(lastGroup.VisibleItemCount - 1);
            return;
        }

        // 再尝试常规项
        var filteredItems = GetFilteredItemsCore();
        for (var i = filteredItems.Count - 1; i >= 0; i--)
        {
            if (!filteredItems[i].Disabled)
            {
                SetFocusedIndex(i);
                return;
            }
        }
    }

    /// <summary>
    /// 清除所有虚拟化分组的焦点。
    /// </summary>
    private void ClearVirtualizedGroupFocus()
    {
        foreach (var group in _virtualizedGroups)
        {
            if (group.FocusedIndex >= 0)
            {
                group.FocusedIndex = -1;
                group.NotifyStateChanged();
            }
        }
    }

    /// <summary>
    /// 选中当前聚焦的项（常规项或虚拟化项）。
    /// </summary>
    public async Task SelectFocusedItemAsync()
    {
        if (_disabled)
        {
            return;
        }

        // 检查焦点是否在虚拟化分组中
        if (_focusedVirtualizedGroupIndex >= 0)
        {
            var activeGroups = _virtualizedGroups.Where(g => g.VisibleItemCount > 0).ToList();
            if (_focusedVirtualizedGroupIndex < activeGroups.Count)
            {
                await activeGroups[_focusedVirtualizedGroupIndex].SelectFocusedItemAsync();
                return;
            }
        }

        // 常规项选择
        var filteredItems = GetFilteredItemsCore();
        if (_focusedIndex >= 0 && _focusedIndex < filteredItems.Count)
        {
            var item = filteredItems[_focusedIndex];
            if (!item.Disabled)
            {
                await SelectItemAsync(item);
            }
        }
    }

    /// <summary>
    /// 按值选中一个项。
    /// </summary>
    public async Task SelectItemByValueAsync(string value)
    {
        if (_disabled)
        {
            return;
        }

        var item = _items.FirstOrDefault(i => i != null && i.Value == value);
        if (item != null && !item.Disabled)
        {
            await SelectItemAsync(item);
        }
    }

    /// <summary>
    /// 选中一个项并调用各回调。
    /// </summary>
    private async Task SelectItemAsync(CommandItemMetadata item)
    {
        if (_disabled)
        {
            return;
        }

        if (item.OnSelect.HasDelegate)
        {
            await item.OnSelect.InvokeAsync();
        }

        if (OnValueChange.HasDelegate)
        {
            await OnValueChange.InvokeAsync(item.Value);
        }
    }

    /// <summary>
    /// 通知订阅者状态已变化。
    /// </summary>
    private void NotifyStateChanged() =>
        OnStateChanged?.Invoke();
}
