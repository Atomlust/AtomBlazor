using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 树节点放置操作的事件参数。
/// </summary>
/// <typeparam name="TItem">树数据项的类型。</typeparam>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Type represents event arguments")]
public class TreeDropEventArgs<TItem>
{
    /// <summary>
    /// 获取被拖拽的节点。
    /// </summary>
    public TItem SourceNode { get; }

    /// <summary>
    /// 获取放置目标节点。
    /// </summary>
    public TItem TargetNode { get; }

    /// <summary>
    /// 获取相对目标节点的放置位置。
    /// </summary>
    public TreeDropPosition Position { get; }

    /// <summary>
    /// 初始化 <see cref="TreeDropEventArgs{TItem}"/> 的新实例。
    /// </summary>
    public TreeDropEventArgs(TItem sourceNode, TItem targetNode, TreeDropPosition position)
    {
        SourceNode = sourceNode;
        TargetNode = targetNode;
        Position = position;
    }
}
