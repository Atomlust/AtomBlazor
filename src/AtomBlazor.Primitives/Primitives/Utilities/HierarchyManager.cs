using Microsoft.Extensions.Logging;

namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 用于管理层级（树）数据结构的通用工具类。
/// 提供索引、遍历与扁平化操作，并支持排序、筛选与子项级分页。
/// </summary>
/// <typeparam name="TItem">层级中项目的类型。</typeparam>
public partial class HierarchyManager<TItem>
{
    private const string rootParentKey = "\0__root__";

    private readonly Func<TItem, string> valueSelector;
    private readonly ILogger? logger;
    private readonly Dictionary<string, TItem> itemsByValue = new();
    private readonly Dictionary<string, string?> parentByValue = new();
    private readonly Dictionary<string, List<string>> childrenByParent = new();
    private readonly Dictionary<string, int> depthByValue = new();

    /// <summary>
    /// 使用指定的取值函数创建新的 HierarchyManager。
    /// </summary>
    /// <param name="valueSelector">从每个项目提取唯一字符串标识符的函数。</param>
    /// <param name="logger">用于上报重复键警告的可选日志记录器。</param>
    public HierarchyManager(Func<TItem, string> valueSelector, ILogger? logger = null)
    {
        this.valueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));
        this.logger = logger;
    }

    // --- Indexing ---

    /// <summary>
    /// 从嵌套数据构建层级，其中每个项目可以有子项目。
    /// </summary>
    /// <param name="rootItems">顶层项目。</param>
    /// <param name="childrenSelector">返回某项目子项的函数，无子项时返回 null/空。</param>
    public void BuildFromNested(
        IEnumerable<TItem> rootItems,
        Func<TItem, IEnumerable<TItem>?> childrenSelector)
    {
        Clear();
        foreach (var item in rootItems)
        {
            IndexNested(item, null, 0, childrenSelector);
        }
    }

    private void IndexNested(
        TItem item, string? parentValue, int depth,
        Func<TItem, IEnumerable<TItem>?> childrenSelector)
    {
        var value = valueSelector(item);
        if (!RegisterItem(value, item, parentValue, depth))
        {
            return;
        }

        var children = childrenSelector(item);
        if (children != null)
        {
            foreach (var child in children)
            {
                IndexNested(child, value, depth + 1, childrenSelector);
            }
        }
    }

    /// <summary>
    /// 从扁平列表构建层级，其中每个项目带有父级引用。
    /// </summary>
    /// <param name="allItems">层级中的全部项目。</param>
    /// <param name="parentValueSelector">返回父级值的函数，根项目返回 null。</param>
    public void BuildFromFlat(
        IEnumerable<TItem> allItems,
        Func<TItem, string?> parentValueSelector)
    {
        Clear();

        // First pass: register all items without depth (skip duplicates)
        var items = allItems as IList<TItem> ?? allItems.ToList();
        foreach (var item in items)
        {
            var value = valueSelector(item);
            if (itemsByValue.ContainsKey(value))
            {
                if (logger != null)
            {
                Log.DuplicateKeyDetected(logger, value);
            }
                continue;
            }

            var parentVal = parentValueSelector(item);
            itemsByValue[value] = item;
            parentByValue[value] = parentVal;

            var parentKey = parentVal ?? rootParentKey;
            if (!childrenByParent.TryGetValue(parentKey, out var children))
            {
                children = new List<string>();
                childrenByParent[parentKey] = children;
            }
            children.Add(value);
        }

        // Second pass: compute depths
        foreach (var value in itemsByValue.Keys)
        {
            depthByValue[value] = ComputeDepth(value);
        }
    }

    private int ComputeDepth(string value)
    {
        if (depthByValue.TryGetValue(value, out var cached))
        {
            return cached;
        }

        if (!parentByValue.TryGetValue(value, out var parentVal) || parentVal == null)
        {
            return 0;
        }

        return ComputeDepth(parentVal) + 1;
    }

    private bool RegisterItem(string value, TItem item, string? parentValue, int depth)
    {
        if (itemsByValue.ContainsKey(value))
        {
            if (logger != null)
            {
                Log.DuplicateKeyDetected(logger, value);
            }
            return false;
        }

        itemsByValue[value] = item;
        parentByValue[value] = parentValue;
        depthByValue[value] = depth;

        var parentKey = parentValue ?? rootParentKey;
        if (!childrenByParent.TryGetValue(parentKey, out var children))
        {
            children = new List<string>();
            childrenByParent[parentKey] = children;
        }
        children.Add(value);
        return true;
    }

    /// <summary>
    /// 向已索引的父节点添加子项。用于懒加载。
    /// </summary>
    /// <param name="parentValue">父节点的值。</param>
    /// <param name="children">要添加的子项。</param>
    /// <param name="childrenSelector">用于递归索引的可选嵌套子项选择器。</param>
    public void AddChildren(
        string parentValue,
        IEnumerable<TItem> children,
        Func<TItem, IEnumerable<TItem>?>? childrenSelector = null)
    {
        var parentDepth = GetDepth(parentValue);

        // Remove existing children for this parent
        if (childrenByParent.TryGetValue(parentValue, out var existingChildren))
        {
            foreach (var childVal in existingChildren.ToList())
            {
                RemoveSubtree(childVal);
            }
            existingChildren.Clear();
        }

        foreach (var child in children)
        {
            if (childrenSelector != null)
            {
                IndexNested(child, parentValue, parentDepth + 1, childrenSelector);
            }
            else
            {
                var value = valueSelector(child);
                RegisterItem(value, child, parentValue, parentDepth + 1);
            }
        }
    }

    private void RemoveSubtree(string value)
    {
        // Recursively remove children first
        if (childrenByParent.TryGetValue(value, out var children))
        {
            foreach (var child in children.ToList())
            {
                RemoveSubtree(child);
            }
            childrenByParent.Remove(value);
        }

        itemsByValue.Remove(value);
        parentByValue.Remove(value);
        depthByValue.Remove(value);
    }

    /// <summary>
    /// 清除全部索引数据。
    /// </summary>
    public void Clear()
    {
        itemsByValue.Clear();
        parentByValue.Clear();
        childrenByParent.Clear();
        depthByValue.Clear();
    }

    // --- Lookups ---

    /// <summary>
    /// 按唯一值获取项目。
    /// </summary>
    public TItem? GetItemByValue(string value) =>
        itemsByValue.TryGetValue(value, out var item) ? item : default;

    /// <summary>
    /// 获取某父节点的直接子项。根项目请传 null。
    /// </summary>
    public IReadOnlyList<TItem> GetChildren(string? parentValue)
    {
        var key = parentValue ?? rootParentKey;
        if (!childrenByParent.TryGetValue(key, out var childValues))
        {
            return Array.Empty<TItem>();
        }

        var result = new List<TItem>(childValues.Count);
        foreach (var cv in childValues)
        {
            if (itemsByValue.TryGetValue(cv, out var item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>
    /// 获取根层级项目。
    /// </summary>
    public IReadOnlyList<TItem> GetRootItems() => GetChildren(null);

    /// <summary>
    /// 获取某项目的父级值；若为根项目则返回 null。
    /// </summary>
    public string? GetParentValue(string value) =>
        parentByValue.TryGetValue(value, out var pv) ? pv : null;

    /// <summary>
    /// 获取某项目的深度（0 = 根）。
    /// </summary>
    public int GetDepth(string value) =>
        depthByValue.TryGetValue(value, out var d) ? d : 0;

    /// <summary>
    /// 获取某项目是否有子项。
    /// </summary>
    public bool HasChildren(string value) =>
        childrenByParent.TryGetValue(value, out var children) && children.Count > 0;

    /// <summary>
    /// 获取某父节点直接子项的总数。
    /// </summary>
    public int GetChildCount(string parentValue) =>
        childrenByParent.TryGetValue(parentValue, out var children) ? children.Count : 0;

    /// <summary>
    /// 获取某项目是否已注册到层级中。
    /// </summary>
    public bool IsRegistered(string value) => itemsByValue.ContainsKey(value);

    /// <summary>
    /// 获取层级中的项目总数。
    /// </summary>
    public int Count => itemsByValue.Count;

    /// <summary>
    /// 获取层级中的全部项目（无序）。
    /// </summary>
    public IEnumerable<TItem> GetAllItems() => itemsByValue.Values;

    /// <summary>
    /// 获取某父节点的直接子项值。根项目请传 null。
    /// </summary>
    public IReadOnlyList<string> GetDirectChildValues(string? parentValue)
    {
        var key = parentValue ?? rootParentKey;
        return childrenByParent.TryGetValue(key, out var children)
            ? children
            : Array.Empty<string>();
    }

    // --- Traversal ---

    /// <summary>
    /// 递归获取某节点的全部后代值。
    /// </summary>
    public IReadOnlyList<string> GetAllDescendantValues(string value)
    {
        var result = new List<string>();
        CollectDescendants(value, result);
        return result;
    }

    private void CollectDescendants(string value, List<string> result)
    {
        if (!childrenByParent.TryGetValue(value, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            result.Add(child);
            CollectDescendants(child, result);
        }
    }

    /// <summary>
    /// 获取某节点的祖先链（父节点、祖父节点……），自下而上。
    /// </summary>
    public IReadOnlyList<string> GetAncestorValues(string value)
    {
        var result = new List<string>();
        if (!parentByValue.TryGetValue(value, out var current))
        {
            return result;
        }

        while (current != null)
        {
            result.Add(current);
            if (!parentByValue.TryGetValue(current, out current))
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// 获取某节点的同级节点值（父节点相同的节点），不含其自身。
    /// </summary>
    public IReadOnlyList<string> GetSiblingValues(string value)
    {
        if (!parentByValue.TryGetValue(value, out var parentVal))
        {
            return Array.Empty<string>();
        }

        var key = parentVal ?? rootParentKey;
        if (!childrenByParent.TryGetValue(key, out var siblings))
        {
            return Array.Empty<string>();
        }

        return siblings.Where(s => s != value).ToList();
    }

    /// <summary>
    /// 获取同一层级的同级节点数量（包含该节点自身）。
    /// </summary>
    public int GetSetSize(string value)
    {
        if (!parentByValue.TryGetValue(value, out var parentVal))
        {
            return 0;
        }

        var key = parentVal ?? rootParentKey;
        return childrenByParent.TryGetValue(key, out var siblings) ? siblings.Count : 0;
    }

    /// <summary>
    /// 获取某节点在其同级节点中从 1 起算的位置。
    /// </summary>
    public int GetPosInSet(string value)
    {
        if (!parentByValue.TryGetValue(value, out var parentVal))
        {
            return 0;
        }

        var key = parentVal ?? rootParentKey;
        if (!childrenByParent.TryGetValue(key, out var siblings))
        {
            return 0;
        }

        for (var i = 0; i < siblings.Count; i++)
        {
            if (siblings[i] == value)
            {
                return i + 1;
            }
        }

        return 0;
    }

    // --- Flattening ---

    /// <summary>
    /// 将层级扁平化为按显示顺序排列的列表，同时顾及展开状态、
    /// 排序、筛选与子项分页。
    /// </summary>
    /// <param name="expandedValues">已展开节点值的集合。</param>
    /// <param name="sortComparison">可选比较器，用于对每一层的同级节点排序。</param>
    /// <param name="filter">可选筛选谓词。设置后，未匹配但拥有匹配后代的条目将作为上下文纳入。</param>
    /// <param name="childPageSize">子项级分页的可选页大小。null 表示显示全部子项。</param>
    /// <param name="childPageIndexes">按节点记录的子项页索引（节点值 → 从 0 起算的页索引）。</param>
    /// <param name="hasChildrenPredicate">可选谓词，用于判断某项目是否有子项（供懒加载使用，此时子项可能尚未索引）。</param>
    /// <param name="filterMode">控制如何处理匹配条目的后代。默认为 <see cref="HierarchyFilterMode.ShowMatchedSubtree"/>。</param>
    /// <returns>携带层级元数据的扁平化项目列表。</returns>
    public IReadOnlyList<HierarchyFlattenResult<TItem>> Flatten(
        HashSet<string> expandedValues,
        Comparison<TItem>? sortComparison = null,
        Func<TItem, bool>? filter = null,
        int? childPageSize = null,
        IReadOnlyDictionary<string, int>? childPageIndexes = null,
        Func<TItem, bool>? hasChildrenPredicate = null,
        HierarchyFilterMode filterMode = HierarchyFilterMode.ShowMatchedSubtree)
    {
        var result = new List<HierarchyFlattenResult<TItem>>();

        // Pre-compute filter matches if a filter is active
        HashSet<string>? matchingValues = null;
        HashSet<string>? valuesWithMatchingDescendants = null;
        if (filter != null)
        {
            matchingValues = new HashSet<string>();
            valuesWithMatchingDescendants = new HashSet<string>();

            foreach (var kvp in itemsByValue)
            {
                if (filter(kvp.Value))
                {
                    matchingValues.Add(kvp.Key);
                }
            }

            // Mark ancestors of matching items
            foreach (var matchValue in matchingValues)
            {
                var ancestors = GetAncestorValues(matchValue);
                foreach (var ancestor in ancestors)
                {
                    valuesWithMatchingDescendants.Add(ancestor);
                }
            }

            // In ShowMatchedSubtree mode, when a parent matches the filter,
            // mark all its descendants as "having matching descendants" so they
            // are visible as context within the subtree. They still won't appear
            // in matchingValues unless they independently match the filter.
            if (filterMode == HierarchyFilterMode.ShowMatchedSubtree)
            {
                foreach (var matchValue in matchingValues)
                {
                    if (HasChildren(matchValue))
                    {
                        var descendants = GetAllDescendantValues(matchValue);
                        foreach (var desc in descendants)
                        {
                            valuesWithMatchingDescendants.Add(desc);
                        }
                    }
                }
            }
        }

        var rootChildren = GetChildValues(rootParentKey);
        var rootItems = ResolveItems(rootChildren);

        if (sortComparison != null)
        {
            rootItems.Sort(sortComparison);
        }

        FlattenRecursive(
            result, rootItems, 0, expandedValues,
            sortComparison, filter, matchingValues, valuesWithMatchingDescendants,
            childPageSize, childPageIndexes, hasChildrenPredicate, filterMode);

        return result;
    }

    private List<string> GetChildValues(string parentKey)
    {
        return childrenByParent.TryGetValue(parentKey, out var children)
            ? children
            : new List<string>();
    }

    private List<TItem> ResolveItems(List<string> values)
    {
        var items = new List<TItem>(values.Count);
        foreach (var v in values)
        {
            if (itemsByValue.TryGetValue(v, out var item))
            {
                items.Add(item);
            }
        }
        return items;
    }

    private void FlattenRecursive(
        List<HierarchyFlattenResult<TItem>> result,
        List<TItem> items,
        int depth,
        HashSet<string> expandedValues,
        Comparison<TItem>? sortComparison,
        Func<TItem, bool>? filter,
        HashSet<string>? matchingValues,
        HashSet<string>? valuesWithMatchingDescendants,
        int? childPageSize,
        IReadOnlyDictionary<string, int>? childPageIndexes,
        Func<TItem, bool>? hasChildrenPredicate,
        HierarchyFilterMode filterMode)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var value = valueSelector(item);
            var itemHasChildren = HasChildren(value) ||
                (hasChildrenPredicate != null && hasChildrenPredicate(item));
            var isExpanded = expandedValues.Contains(value);
            var isLastChild = i == items.Count - 1;

            // Filter logic: skip items that don't match and have no matching descendants
            var isFilterActive = filter != null && matchingValues != null && valuesWithMatchingDescendants != null;
            var directlyMatchesFilter = isFilterActive && matchingValues!.Contains(value);

            if (isFilterActive)
            {
                var hasMatchingDesc = valuesWithMatchingDescendants!.Contains(value);

                if (!directlyMatchesFilter && !hasMatchingDesc)
                {
                    continue;
                }

                result.Add(new HierarchyFlattenResult<TItem>
                {
                    Item = item,
                    Depth = depth,
                    HasChildren = itemHasChildren,
                    IsExpanded = isExpanded && itemHasChildren,
                    IsLastChild = isLastChild,
                    MatchesFilter = directlyMatchesFilter
                });
            }
            else
            {
                result.Add(new HierarchyFlattenResult<TItem>
                {
                    Item = item,
                    Depth = depth,
                    HasChildren = itemHasChildren,
                    IsExpanded = isExpanded && itemHasChildren,
                    IsLastChild = isLastChild,
                    MatchesFilter = true
                });
            }

            // Recurse into children if expanded
            if (isExpanded && itemHasChildren)
            {
                var childValues = GetChildValues(value);
                var childItems = ResolveItems(childValues);

                if (sortComparison != null)
                {
                    childItems.Sort(sortComparison);
                }

                // When filtering is active, disable child pagination so all
                // matching/context children are visible without paging confusion.
                var effectivePageSize = isFilterActive ? null : childPageSize;

                // Apply child pagination (only when no filter is active)
                var totalChildren = childItems.Count;
                if (effectivePageSize.HasValue && totalChildren > effectivePageSize.Value)
                {
                    var pageIndex = 0;
                    if (childPageIndexes != null && childPageIndexes.TryGetValue(value, out var pi))
                    {
                        pageIndex = pi;
                    }

                    var skip = pageIndex * effectivePageSize.Value;
                    var take = Math.Min(effectivePageSize.Value, totalChildren - skip);

                    // Only emit the current page of children
                    var pagedChildren = childItems.GetRange(skip, take);

                    FlattenRecursive(
                        result, pagedChildren, depth + 1, expandedValues,
                        sortComparison, filter, matchingValues, valuesWithMatchingDescendants,
                        effectivePageSize, childPageIndexes, hasChildrenPredicate, filterMode);

                    // Append child pager row
                    result.Add(new HierarchyFlattenResult<TItem>
                    {
                        Depth = depth + 1,
                        IsChildPagerRow = true,
                        ParentValue = value,
                        ChildPageIndex = pageIndex,
                        TotalChildren = totalChildren,
                        MatchesFilter = true
                    });
                }
                else
                {
                    FlattenRecursive(
                        result, childItems, depth + 1, expandedValues,
                        sortComparison, filter, matchingValues, valuesWithMatchingDescendants,
                        effectivePageSize, childPageIndexes, hasChildrenPredicate, filterMode);
                }
            }
        }
    }

    // --- Expansion helpers ---

    /// <summary>
    /// 获取所有拥有子项的节点值集合（用于全部展开）。
    /// </summary>
    public HashSet<string> GetExpandAllValues()
    {
        var result = new HashSet<string>();
        foreach (var kvp in childrenByParent)
        {
            if (kvp.Key != rootParentKey && kvp.Value.Count > 0)
            {
                result.Add(kvp.Key);
            }
        }
        return result;
    }

    /// <summary>
    /// 获取深度小于 maxDepth 且拥有子项的节点值集合。
    /// </summary>
    /// <param name="maxDepth">展开到的最大深度（不含）。深度 0 = 仅展开根节点。</param>
    public HashSet<string> GetExpandToDepthValues(int maxDepth)
    {
        var result = new HashSet<string>();
        foreach (var kvp in childrenByParent)
        {
            if (kvp.Key == rootParentKey || kvp.Value.Count == 0)
            {
                continue;
            }

            if (depthByValue.TryGetValue(kvp.Key, out var depth) && depth < maxDepth)
            {
                result.Add(kvp.Key);
            }
        }
        return result;
    }

    /// <summary>
    /// 在给定的展开集合中，展开指定节点的全部祖先。
    /// </summary>
    public void ExpandAncestorsOf(string value, HashSet<string> expandedValues)
    {
        var ancestors = GetAncestorValues(value);
        foreach (var ancestor in ancestors)
        {
            expandedValues.Add(ancestor);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "HierarchyManager: Duplicate key '{Key}' detected. The item will be skipped. Ensure ItemValueSelector returns unique values for each item.")]
        public static partial void DuplicateKeyDetected(ILogger logger, string key);
    }
}
