using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// 已注册树节点的信息。
/// </summary>
internal sealed class TreeNodeInfo
{
    public string Value { get; set; } = string.Empty;
    public string? ParentValue { get; set; }
    public int Depth { get; set; }
    public bool Disabled { get; set; }
    public int Order { get; set; }
}

/// <summary>
/// TreeView 基元组件及其子组件的上下文。
/// 管理树的展开/折叠、选中与复选框状态。
/// </summary>
public class TreeViewContext : PrimitiveContextWithEvents<TreeViewState>
{
    private const string rootParentKey = "\0__root__";
    private readonly Dictionary<string, TreeNodeInfo> nodeRegistry = new();
    private readonly Dictionary<string, List<TreeNodeInfo>> childrenByParent = new();
    private int nextOrder;

    /// <summary>
    /// 获取或设置当前获得焦点的节点值（roving tabindex 用）。
    /// 由 JS 可调方法设置，使 Blazor 重渲染时保持正确的 tabindex。
    /// </summary>
    public string? FocusedNodeValue { get; set; }

    /// <summary>
    /// 为 true 时，节点在注册时自动展开，使树逐层渐进地完整渲染，
    /// 而不必一次性实例化整棵组件树（对 WASM 性能至关重要）。
    /// </summary>
    internal bool AutoExpandAll { get; set; }

    /// <summary>
    /// 设置后，深度小于该值的节点在注册时自动展开。
    /// </summary>
    internal int? AutoExpandDepth { get; set; }

    private static string ParentKey(string? parentValue) => parentValue ?? rootParentKey;

    /// <summary>
    /// 初始化 TreeViewContext 的新实例。
    /// </summary>
    public TreeViewContext() : base(new TreeViewState(), "tree")
    {
    }

    /// <summary>
    /// 获取指定树节点的 ARIA ID。
    /// </summary>
    public string GetNodeId(string value) => GetScopedId($"node-{value}");

    // --- 节点注册表 ---

    /// <summary>
    /// 向树上下文注册一个节点。
    /// </summary>
    public void RegisterNode(string value, string? parentValue, int depth, bool disabled)
    {
        // 重新注册时先从旧的父级索引中移除
        if (nodeRegistry.TryGetValue(value, out var existing))
        {
            RemoveFromChildrenIndex(existing);
        }

        var info = new TreeNodeInfo
        {
            Value = value,
            ParentValue = parentValue,
            Depth = depth,
            Disabled = disabled,
            Order = nextOrder++
        };
        nodeRegistry[value] = info;

        // 加入父级到子级的索引
        var key = ParentKey(parentValue);
        if (!childrenByParent.TryGetValue(key, out var siblings))
        {
            siblings = new List<TreeNodeInfo>();
            childrenByParent[key] = siblings;
        }
        siblings.Add(info);

        // 初始渲染期间自动展开本节点，让 ChildContent 逐层渐进渲染，
        // 避免一次性实例化整棵组件树的开销（对 WASM 性能至关重要）。
        // 直接修改 State.ExpandedValues 而不调用 UpdateState，
        // 以免在渲染中途触发 OnStateChanged。
        if (AutoExpandAll || (AutoExpandDepth.HasValue && depth < AutoExpandDepth.Value))
        {
            State.ExpandedValues.Add(value);
        }
    }

    /// <summary>
    /// 从树上下文注销一个节点。
    /// </summary>
    public void UnregisterNode(string value)
    {
        if (nodeRegistry.TryGetValue(value, out var info))
        {
            RemoveFromChildrenIndex(info);
            nodeRegistry.Remove(value);
        }
    }

    private void RemoveFromChildrenIndex(TreeNodeInfo info)
    {
        var key = ParentKey(info.ParentValue);
        if (childrenByParent.TryGetValue(key, out var siblings))
        {
            siblings.Remove(info);
            if (siblings.Count == 0)
            {
                childrenByParent.Remove(key);
            }
        }
    }

    /// <summary>
    /// 更新已注册节点的禁用状态。
    /// </summary>
    public void UpdateNodeDisabled(string value, bool disabled)
    {
        if (nodeRegistry.TryGetValue(value, out var info))
        {
            info.Disabled = disabled;
        }
    }

    /// <summary>
    /// 获取指定父节点下的全部子节点值。
    /// </summary>
    public List<string> GetChildValues(string? parentValue)
    {
        if (!childrenByParent.TryGetValue(ParentKey(parentValue), out var children))
        {
            return new List<string>();
        }

        return children
            .OrderBy(n => n.Order)
            .Select(n => n.Value)
            .ToList();
    }

    /// <summary>
    /// 递归获取指定节点的全部后代节点值。
    /// </summary>
    public List<string> GetAllDescendants(string value)
    {
        var result = new List<string>();
        CollectDescendants(value, result);
        return result;
    }

    private void CollectDescendants(string value, List<string> result)
    {
        var children = GetChildValues(value);
        foreach (var child in children)
        {
            result.Add(child);
            CollectDescendants(child, result);
        }
    }

    /// <summary>
    /// 获取节点的祖先链（父级、祖父级等），自底向上。
    /// </summary>
    public List<string> GetAncestors(string value)
    {
        var result = new List<string>();
        if (!nodeRegistry.TryGetValue(value, out var info))
        {
            return result;
        }

        var current = info.ParentValue;
        while (current != null)
        {
            result.Add(current);
            if (!nodeRegistry.TryGetValue(current, out var parentInfo))
            {
                break;
            }
            current = parentInfo.ParentValue;
        }

        return result;
    }

    /// <summary>
    /// 获取节点的兄弟节点值（同一父级下的节点），不含自身。
    /// </summary>
    public List<string> GetSiblingValues(string value)
    {
        if (!nodeRegistry.TryGetValue(value, out var info))
        {
            return new List<string>();
        }

        if (!childrenByParent.TryGetValue(ParentKey(info.ParentValue), out var siblings))
        {
            return new List<string>();
        }

        return siblings
            .Where(n => n.Value != value)
            .OrderBy(n => n.Order)
            .Select(n => n.Value)
            .ToList();
    }

    /// <summary>
    /// 获取节点的深度。
    /// </summary>
    public int GetNodeDepth(string value) =>
        nodeRegistry.TryGetValue(value, out var info) ? info.Depth : 0;

    /// <summary>
    /// 获取同一层级的兄弟节点数量（含节点自身）。
    /// </summary>
    public int GetSetSize(string value)
    {
        if (!nodeRegistry.TryGetValue(value, out var info))
        {
            return 0;
        }

        return childrenByParent.TryGetValue(ParentKey(info.ParentValue), out var siblings) ? siblings.Count : 0;
    }

    /// <summary>
    /// 获取节点在兄弟节点中的 1 起算序位。
    /// </summary>
    public int GetPosInSet(string value)
    {
        if (!nodeRegistry.TryGetValue(value, out var info))
        {
            return 0;
        }

        if (!childrenByParent.TryGetValue(ParentKey(info.ParentValue), out var siblings))
        {
            return 0;
        }

        var ordered = siblings.OrderBy(n => n.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Value == value)
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// 检查节点是否有已注册的子节点。
    /// </summary>
    public bool HasChildren(string value) =>
        childrenByParent.TryGetValue(value, out var children) && children.Count > 0;

    /// <summary>
    /// 判断给定值是否是第一个可用的根节点（roving tabindex 用）。
    /// </summary>
    public bool IsFirstEnabledRoot(string value)
    {
        if (!childrenByParent.TryGetValue(rootParentKey, out var roots))
        {
            return false;
        }

        var firstEnabled = roots
            .Where(n => !n.Disabled)
            .OrderBy(n => n.Order)
            .FirstOrDefault();

        return firstEnabled?.Value == value;
    }

    /// <summary>
    /// 检查节点是否已注册。
    /// </summary>
    public bool IsNodeRegistered(string value) =>
        nodeRegistry.ContainsKey(value);

    /// <summary>
    /// 获取节点的父节点值，根节点为 null。
    /// </summary>
    public string? GetParentValue(string value) =>
        nodeRegistry.TryGetValue(value, out var info) ? info.ParentValue : null;

    // --- 展开/折叠 ---

    /// <summary>
    /// 检查指定节点当前是否处于展开状态。
    /// </summary>
    public bool IsExpanded(string value) => State.ExpandedValues.Contains(value);

    /// <summary>
    /// 切换节点的展开状态。
    /// </summary>
    public void ToggleExpanded(string value)
    {
        UpdateState(state =>
        {
            if (!state.ExpandedValues.Remove(value))
            {
                state.ExpandedValues.Add(value);
            }
        });
    }

    /// <summary>
    /// 展开节点。
    /// </summary>
    public void ExpandNode(string value)
    {
        if (!State.ExpandedValues.Contains(value))
        {
            UpdateState(state => state.ExpandedValues.Add(value));
        }
    }

    /// <summary>
    /// 折叠节点。
    /// </summary>
    public void CollapseNode(string value)
    {
        if (State.ExpandedValues.Contains(value))
        {
            UpdateState(state => state.ExpandedValues.Remove(value));
        }
    }

    /// <summary>
    /// 展开指定节点的全部兄弟节点。
    /// </summary>
    public void ExpandSiblings(string value)
    {
        var siblings = GetSiblingValues(value);
        UpdateState(state =>
        {
            foreach (var sibling in siblings)
            {
                if (HasChildren(sibling))
                {
                    state.ExpandedValues.Add(sibling);
                }
            }
            if (HasChildren(value))
            {
                state.ExpandedValues.Add(value);
            }
        });
    }

    /// <summary>
    /// 设置展开值集合。用于受控状态。
    /// </summary>
    public void SetExpandedValues(HashSet<string> values) =>
        UpdateState(state => state.ExpandedValues = new HashSet<string>(values));

    /// <summary>
    /// 展开所有有子节点的已注册节点。
    /// </summary>
    public void ExpandAllWithChildren()
    {
        // 直接从索引键构建全部可展开节点的集合
        var allExpandable = new HashSet<string>();
        foreach (var kvp in childrenByParent)
        {
            if (kvp.Key != rootParentKey && kvp.Value.Count > 0)
            {
                allExpandable.Add(kvp.Key);
            }
        }

        if (allExpandable.Count > 0)
        {
            UpdateState(state => state.ExpandedValues = allExpandable);
        }
    }

    // --- 选中 ---

    /// <summary>
    /// 获取选择模式。
    /// </summary>
    public TreeSelectionMode SelectionMode => State.SelectionMode;

    /// <summary>
    /// 检查指定节点是否被选中。
    /// </summary>
    public bool IsSelected(string value) => State.SelectedValues.Contains(value);

    /// <summary>
    /// 按当前选择模式选中节点。
    /// </summary>
    public void SelectNode(string value)
    {
        if (State.SelectionMode == TreeSelectionMode.None)
        {
            return;
        }

        if (nodeRegistry.TryGetValue(value, out var info) && info.Disabled)
        {
            return;
        }

        UpdateState(state =>
        {
            if (state.SelectionMode == TreeSelectionMode.Single)
            {
                state.SelectedValues.Clear();
                state.SelectedValues.Add(value);
            }
            else if (state.SelectionMode == TreeSelectionMode.Multiple)
            {
                if (!state.SelectedValues.Remove(value))
                {
                    state.SelectedValues.Add(value);
                }
            }
        });
    }

    /// <summary>
    /// 设置选中值集合。用于受控状态。
    /// </summary>
    public void SetSelectedValues(HashSet<string> values) =>
        UpdateState(state => state.SelectedValues = new HashSet<string>(values));

    // --- 可勾选 ---

    /// <summary>
    /// 获取是否启用复选框。
    /// </summary>
    public bool Checkable => State.Checkable;

    /// <summary>
    /// 获取是否禁用级联。
    /// </summary>
    public bool CheckStrictly => State.CheckStrictly;

    /// <summary>
    /// 检查指定节点是否被勾选。
    /// </summary>
    public bool IsChecked(string value) => State.CheckedValues.Contains(value);

    /// <summary>
    /// 检查指定节点是否处于不确定（半选）状态
    /// （部分但非全部后代被勾选）。
    /// </summary>
    public bool IsIndeterminate(string value)
    {
        if (State.CheckStrictly || !HasChildren(value))
        {
            return false;
        }

        var descendants = GetAllDescendants(value);
        if (descendants.Count == 0)
        {
            return false;
        }

        var hasChecked = false;
        var hasUnchecked = false;

        foreach (var d in descendants)
        {
            if (nodeRegistry.TryGetValue(d, out var info) && info.Disabled)
            {
                continue;
            }

            if (State.CheckedValues.Contains(d))
            {
                hasChecked = true;
            }
            else
            {
                hasUnchecked = true;
            }

            if (hasChecked && hasUnchecked)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 按级联逻辑切换节点的勾选状态。
    /// </summary>
    public void ToggleChecked(string value)
    {
        if (!State.Checkable)
        {
            return;
        }

        if (nodeRegistry.TryGetValue(value, out var info) && info.Disabled)
        {
            return;
        }

        UpdateState(state =>
        {
            var newChecked = !state.CheckedValues.Contains(value);

            if (state.CheckStrictly)
            {
                if (newChecked)
                {
                    state.CheckedValues.Add(value);
                }
                else
                {
                    state.CheckedValues.Remove(value);
                }
                return;
            }

            // 向下级联：勾选/取消全部后代
            var descendants = GetAllDescendants(value);
            if (newChecked)
            {
                state.CheckedValues.Add(value);
                foreach (var d in descendants)
                {
                    if (!nodeRegistry.TryGetValue(d, out var dInfo) || !dInfo.Disabled)
                    {
                        state.CheckedValues.Add(d);
                    }
                }
            }
            else
            {
                state.CheckedValues.Remove(value);
                foreach (var d in descendants)
                {
                    if (!nodeRegistry.TryGetValue(d, out var dInfo) || !dInfo.Disabled)
                    {
                        state.CheckedValues.Remove(d);
                    }
                }
            }

            // 向上级联：重算父级状态
            var ancestors = GetAncestors(value);
            foreach (var ancestor in ancestors)
            {
                if (nodeRegistry.TryGetValue(ancestor, out var aInfo) && aInfo.Disabled)
                {
                    continue;
                }

                var children = GetChildValues(ancestor);
                var enabledChildren = children.Where(c =>
                    !nodeRegistry.TryGetValue(c, out var cInfo) || !cInfo.Disabled).ToList();

                if (enabledChildren.Count == 0)
                {
                    continue;
                }

                var allChecked = enabledChildren.All(c => state.CheckedValues.Contains(c));
                if (allChecked)
                {
                    state.CheckedValues.Add(ancestor);
                }
                else
                {
                    state.CheckedValues.Remove(ancestor);
                }
            }
        });
    }

    /// <summary>
    /// 设置勾选值集合。用于受控状态。
    /// </summary>
    public void SetCheckedValues(HashSet<string> values) =>
        UpdateState(state => state.CheckedValues = new HashSet<string>(values));
}
