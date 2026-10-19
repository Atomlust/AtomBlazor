using System.Globalization;

namespace AtomBlazor;

/// <summary>一条被接纳的链接，其值已换算为数字。</summary>
internal readonly record struct SankeyLink(string Source, string Target, double Value);

/// <summary>sankey 系列据以绘制的不含环的节点-链接图。</summary>
internal sealed class SankeyGraph
{
    /// <summary>节点名按其首次出现的顺序排列，此顺序亦是其调色顺序。</summary>
    internal List<string> Nodes { get; } = [];

    /// <summary>留存下来的链接，按数据顺序。</summary>
    internal List<SankeyLink> Links { get; } = [];

    /// <summary>至少有一条出链的节点。</summary>
    internal HashSet<string> WithOutgoing { get; } = new(StringComparer.Ordinal);

    /// <summary>至少有一条入链的节点。</summary>
    internal HashSet<string> WithIncoming { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// 把三个平行列——每行一个源名、一个目标名、一个值——转换成
/// ECharts sankey 系列所需的有向无环图。
/// </summary>
/// <remarks>
/// <para>
/// <strong>sankey 是一张 DAG。</strong>链接成环时 ECharts 直接抛错而不绘制，
/// 抛错的布局会让整张图空白，而不是照常画出没问题的部分。
/// 凡会闭合出环的链接——包括节点指向自身的链接——
/// 都被略去，图的其余部分照常绘制。
/// </para>
/// <para>
/// 缺任一名称的行被丢弃，因为链接须有两端。值不是数字的行被丢弃而非
/// 强归为零，因为零宽度的缎带会被读成一条恰好很小的真实流量，
/// 而不是被读成读不懂的数据。
/// </para>
/// </remarks>
internal static class SankeyGraphBuilder
{
    internal static SankeyGraph Build(
        IReadOnlyList<string> sources,
        IReadOnlyList<string> targets,
        IReadOnlyList<object?> values)
    {
        var graph = new SankeyGraph();
        var seenNodes = new HashSet<string>(StringComparer.Ordinal);
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var rowCount = Math.Min(sources.Count, Math.Min(targets.Count, values.Count));

        for (var i = 0; i < rowCount; i++)
        {
            var source = sources[i];
            var target = targets[i];

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            {
                continue;
            }

            if (!TryConvertValue(values[i], out var value))
            {
                continue;
            }

            // 自环链接是一节点的环；目标已能到达其源的任一条链接
            // 则闭合出更长的环。
            if (string.Equals(source, target, StringComparison.Ordinal) || CanReach(edges, target, source))
            {
                continue;
            }

            if (seenNodes.Add(source))
            {
                graph.Nodes.Add(source);
            }

            if (seenNodes.Add(target))
            {
                graph.Nodes.Add(target);
            }

            if (!edges.TryGetValue(source, out var outgoing))
            {
                outgoing = [];
                edges[source] = outgoing;
            }

            outgoing.Add(target);
            graph.WithOutgoing.Add(source);
            graph.WithIncoming.Add(target);
            graph.Links.Add(new SankeyLink(source, target, value));
        }

        return graph;
    }

    /// <summary>
    /// 遍历迄今已接纳的链接，判断 <paramref name="to"/> 是否已经可以从
    /// <paramref name="from"/> 到达。
    /// </summary>
    private static bool CanReach(Dictionary<string, List<string>> edges, string from, string to)
    {
        if (edges.Count == 0)
        {
            return false;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal) { from };
        var pending = new Stack<string>();
        pending.Push(from);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (string.Equals(current, to, StringComparison.Ordinal))
            {
                return true;
            }

            if (!edges.TryGetValue(current, out var outgoing))
            {
                continue;
            }

            foreach (var next in outgoing)
            {
                if (visited.Add(next))
                {
                    pending.Push(next);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 把提取到的值换算为数字，凡非数字者一律拒绝。
    /// </summary>
    private static bool TryConvertValue(object? raw, out double value)
    {
        value = 0;

        switch (raw)
        {
            case null:
                return false;
            case double d:
                value = d;
                break;
            case IConvertible convertible:
                try
                {
                    value = convertible.ToDouble(CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                {
                    return false;
                }

                break;
            default:
                return false;
        }

        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
