using System.Collections.Generic;

namespace AtomBlazor;

/// <summary>
/// dock 分屏节点的朝向。
/// </summary>
internal enum DockOrientation
{
    /// <summary>子节点自左向右排列。</summary>
    Horizontal,

    /// <summary>子节点自上而下排列。</summary>
    Vertical
}

/// <summary>
/// dock 布局树中节点的基类型。节点要么是 <see cref="DockSplitNode"/>
/// （一个水平/垂直分屏），要么是 <see cref="DockTabGroupNode"/>
/// （一组带标签页的面板）。
/// </summary>
internal abstract class DockLayoutNode
{
    /// <summary>节点稳定标识，用于差分与 keying。</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// 沿某条轴排布两个及以上子节点的分屏容器。
/// </summary>
internal sealed class DockSplitNode : DockLayoutNode
{
    /// <summary>子节点排布所沿的轴。</summary>
    public DockOrientation Orientation { get; set; }

    /// <summary>有序的子节点集合。</summary>
    public List<DockLayoutNode> Children { get; set; } = new();

    /// <summary>每个子节点占分屏的百分比尺寸（与 <see cref="Children"/> 平行）。</summary>
    public List<double> Sizes { get; set; } = new();

    /// <summary>返回一个结构签名，分屏形状一变它随之变化。</summary>
    public string Signature => $"{Orientation}:{string.Join(',', Children.Select(c => c.Id))}";

    /// <summary>确保 <see cref="Sizes"/> 列表与子节点数一致，均匀分布。</summary>
    public void NormalizeSizes()
    {
        if (Children.Count == 0)
        {
            Sizes.Clear();
            return;
        }

        if (Sizes.Count != Children.Count)
        {
            var even = 100.0 / Children.Count;
            Sizes = Enumerable.Repeat(even, Children.Count).ToList();
            return;
        }

        var total = Sizes.Sum();
        if (total <= 0)
        {
            var even = 100.0 / Children.Count;
            Sizes = Enumerable.Repeat(even, Children.Count).ToList();
            return;
        }

        // 重缩放到总计 100，让 flex 百分比始终合理。
        for (var i = 0; i < Sizes.Count; i++)
        {
            Sizes[i] = Sizes[i] / total * 100.0;
        }
    }
}

/// <summary>
/// 以标签页形式渲染的一组面板。同一时刻恰有一个面板处于活动态。
/// </summary>
internal sealed class DockTabGroupNode : DockLayoutNode
{
    /// <summary>以标签页展示的有序面板标识。</summary>
    public List<string> PanelIds { get; set; } = new();

    /// <summary>当前活动面板的标识，若有的话。</summary>
    public string? ActivePanelId { get; set; }

    /// <summary>确保 <see cref="ActivePanelId"/> 指向仍然存在的面板。</summary>
    public void EnsureActive()
    {
        if (PanelIds.Count == 0)
        {
            ActivePanelId = null;
            return;
        }

        if (ActivePanelId is null || !PanelIds.Contains(ActivePanelId))
        {
            ActivePanelId = PanelIds[0];
        }
    }
}

/// <summary>
/// 一个自由浮动窗口，承载从停靠布局中摘出的标签组。
/// </summary>
internal sealed class DockFloatingWindow
{
    /// <summary>创建浮动窗口时的默认宽度（像素），未计入约束。</summary>
    public const double DefaultWidth = 360;

    /// <summary>创建浮动窗口时的默认高度（像素），未计入约束。</summary>
    public const double DefaultHeight = 260;

    /// <summary>浮动窗口可用的最小宽度（像素），不受面板约束影响。</summary>
    public const double MinFloatingWidth = 180;

    /// <summary>浮动窗口可用的最小高度（像素），不受面板约束影响。</summary>
    public const double MinFloatingHeight = 120;

    /// <summary>窗口的稳定标识。</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>本窗口承载的标签组。</summary>
    public DockTabGroupNode Group { get; set; } = new();

    /// <summary>在 dock 表面内的水平偏移（像素）。</summary>
    public double X { get; set; }

    /// <summary>在 dock 表面内的垂直偏移（像素）。</summary>
    public double Y { get; set; }

    /// <summary>窗口宽度（像素）。</summary>
    public double Width { get; set; } = DefaultWidth;

    /// <summary>窗口高度（像素）。</summary>
    public double Height { get; set; } = DefaultHeight;
}
