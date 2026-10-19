namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 把一张平铺的任务表化为图表：一棵树、上卷的汇总项、一条时间轴，
/// 以及横条之间的诸箭头。
/// </summary>
/// <remarks>
/// 纯引擎，不含标记，故计划可绘于屏幕之外的地方。
/// </remarks>
public static class GanttBuilder
{
    /// <summary>
    /// 构建图表。
    /// </summary>
    /// <typeparam name="TItem">源项的类型。</typeparam>
    /// <param name="source">任务之所来，及时间轴之切割方式。</param>
    /// <returns>图表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 null。</exception>
    /// <exception cref="ArgumentException">
    /// 两任务共用一个标识符，或诸父标识符成环。
    /// </exception>
    public static GanttChart<TItem> Build<TItem>(GanttSource<TItem> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var nodes = Collect(source);
        var roots = Link(nodes, source);

        if (source.Order is { } order)
        {
            Sort(roots, nodes, order);
        }

        foreach (var root in roots)
        {
            RollUp(root, source.RollUpSummaries);
        }

        var axis = BuildAxis(nodes, source);
        var rows = Flatten(roots, source.Collapsed, axis);
        var links = Resolve(nodes, rows, source.Dependencies);

        return new GanttChart<TItem>(rows, axis, links, nodes.Count);
    }

    private static Dictionary<string, Node<TItem>> Collect<TItem>(GanttSource<TItem> source)
    {
        var nodes = new Dictionary<string, Node<TItem>>(StringComparer.Ordinal);

        foreach (var item in source.Items)
        {
            var id = source.Id(item);
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Every task needs an identifier.", nameof(source));
            }

            var start = source.Start(item);
            var end = source.End(item);

            var node = new Node<TItem>
            {
                Item = item,
                Id = id,
                ParentId = source.ParentId?.Invoke(item),
                Text = source.Text(item) ?? string.Empty,
                Start = start,
                // 任务不可先结束再开始；如此便作零时长处理，画出的是
                // 里程碑而非一根逆行横条。
                End = end < start ? start : end,
                Progress = Math.Clamp(source.Progress?.Invoke(item) ?? 0, 0, 1),
            };

            if (!nodes.TryAdd(id, node))
            {
                throw new ArgumentException($"Two tasks share the identifier '{id}'.", nameof(source));
            }
        }

        return nodes;
    }

    private static List<Node<TItem>> Link<TItem>(Dictionary<string, Node<TItem>> nodes, GanttSource<TItem> source)
    {
        var roots = new List<Node<TItem>>();

        foreach (var node in nodes.Values)
        {
            // 不在集合中的父即无父：计划经筛取的切片照样可画。
            if (node.ParentId is { Length: > 0 } parentId && nodes.TryGetValue(parentId, out var parent) && parent != node)
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        // 凡自根不可达者皆在环中，那会令下面的遍历挂死。
        var reachable = 0;
        var stack = new Stack<Node<TItem>>(roots);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            reachable++;
            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }

        if (reachable != nodes.Count)
        {
            throw new ArgumentException("The parent identifiers form a loop.", nameof(source));
        }

        return roots;
    }

    private static void Sort<TItem>(List<Node<TItem>> roots, Dictionary<string, Node<TItem>> nodes, IComparer<TItem> order)
    {
        // 用 OrderBy 而非 List.Sort——后者不稳定：并列的同辈须保持
        // 到达时的次序，否则在一列满是平值的列上重排会把计划打乱。
        Reorder(roots);
        foreach (var node in nodes.Values)
        {
            Reorder(node.Children);
        }

        void Reorder(List<Node<TItem>> siblings)
        {
            if (siblings.Count < 2)
            {
                return;
            }

            var sorted = siblings.OrderBy(n => n.Item, order).ToList();
            siblings.Clear();
            siblings.AddRange(sorted);
        }
    }

    private static void RollUp<TItem>(Node<TItem> node, bool rollUp)
    {
        foreach (var child in node.Children)
        {
            RollUp(child, rollUp);
        }

        if (node.Children.Count == 0 || !rollUp)
        {
            return;
        }

        node.Start = node.Children.Min(c => c.Start);
        node.End = node.Children.Max(c => c.End);

        // 按时长加权，故半成的两周重过完工的半日。其下只剩
        // 里程碑时无长度可权，便一律等量计入。
        double total = 0;
        double done = 0;
        foreach (var child in node.Children)
        {
            var length = (child.End - child.Start).TotalMinutes;
            total += length;
            done += child.Progress * length;
        }

        node.Progress = total > 0 ? done / total : node.Children.Average(c => c.Progress);
    }

    private static GanttTimeAxis BuildAxis<TItem>(Dictionary<string, Node<TItem>> nodes, GanttSource<TItem> source)
    {
        DateTimeOffset start;
        DateTimeOffset end;

        if (nodes.Count == 0)
        {
            start = source.RangeStart ?? DateTimeOffset.Now;
            end = source.RangeEnd ?? start;
        }
        else
        {
            start = source.RangeStart ?? nodes.Values.Min(n => n.Start);
            end = source.RangeEnd ?? nodes.Values.Max(n => n.End);
        }

        return GanttTimeAxis.Build(start, end, source.Zoom, source.Snap, source.NonWorkingDays, source.Labels);
    }

    private static List<GanttRow<TItem>> Flatten<TItem>(
        List<Node<TItem>> roots,
        IReadOnlySet<string>? collapsed,
        GanttTimeAxis axis)
    {
        var rows = new List<GanttRow<TItem>>();

        foreach (var root in roots)
        {
            Walk(root, 0);
        }

        return rows;

        void Walk(Node<TItem> node, int depth)
        {
            var expanded = collapsed is null || !collapsed.Contains(node.Id);
            var offset = axis.Position(node.Start);

            var row = new GanttRow<TItem>(node.Item, node.Id, node.ParentId, node.Text, depth)
            {
                Index = rows.Count,
                Start = node.Start,
                End = node.End,
                Progress = node.Progress,
                IsSummary = node.Children.Count > 0,
                IsMilestone = node.Children.Count == 0 && node.End == node.Start,
                IsExpanded = expanded,
                ChildCount = node.Children.Count,
                OffsetSlots = offset,
                LengthSlots = axis.Position(node.End) - offset,
            };

            node.Row = row;
            rows.Add(row);

            if (!expanded)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                Walk(child, depth + 1);
            }
        }
    }

    private static List<GanttLink> Resolve<TItem>(
        Dictionary<string, Node<TItem>> nodes,
        List<GanttRow<TItem>> rows,
        IEnumerable<GanttDependency>? dependencies)
    {
        var links = new List<GanttLink>();
        if (dependencies is null)
        {
            return links;
        }

        var drawn = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            drawn.Add(row.Id);
        }

        foreach (var dependency in dependencies)
        {
            var from = Visible(dependency.FromId);
            var to = Visible(dependency.ToId);

            // 两端同折入一个汇总项，便成了一支从横条射向自己的箭头。
            if (from is null || to is null || ReferenceEquals(from, to))
            {
                continue;
            }

            var (fromAnchor, toAnchor) = dependency.Type switch
            {
                GanttDependencyType.StartToStart => (GanttAnchor.Start, GanttAnchor.Start),
                GanttDependencyType.FinishToFinish => (GanttAnchor.End, GanttAnchor.End),
                GanttDependencyType.StartToFinish => (GanttAnchor.Start, GanttAnchor.End),
                _ => (GanttAnchor.End, GanttAnchor.Start),
            };

            links.Add(new GanttLink(
                dependency,
                from.Index,
                to.Index,
                fromAnchor == GanttAnchor.Start ? from.OffsetSlots : from.OffsetSlots + from.LengthSlots,
                toAnchor == GanttAnchor.Start ? to.OffsetSlots : to.OffsetSlots + to.LengthSlots,
                fromAnchor,
                toAnchor));
        }

        return links;

        GanttRow<TItem>? Visible(string id)
        {
            // 向上走到仍在屏上的那一层，故收拢分支时保留
            // 越过它的箭头，而非将其悄然丢弃。
            var node = nodes.GetValueOrDefault(id);
            while (node is not null && !drawn.Contains(node.Id))
            {
                node = node.Parent;
            }

            return node?.Row;
        }
    }

    private sealed class Node<TItem>
    {
        public required TItem Item { get; init; }

        public required string Id { get; init; }

        public required string? ParentId { get; init; }

        public required string Text { get; init; }

        public DateTimeOffset Start { get; set; }

        public DateTimeOffset End { get; set; }

        public double Progress { get; set; }

        public Node<TItem>? Parent { get; set; }

        public List<Node<TItem>> Children { get; } = [];

        public GanttRow<TItem>? Row { get; set; }
    }
}
