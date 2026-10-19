using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的桑基图系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 请将父图表绑定到<em>链路（links）</em>集合而非节点集合：每行经
/// <see cref="SourceKey"/> 提供源节点名、经 <see cref="TargetKey"/> 提供目标节点名、经
/// <see cref="SeriesBase.DataKey"/> 提供数值。节点列表按名称首次出现的顺序自链路
/// 推导，该顺序也是各节点从图表调色板取色的顺序。
/// </para>
/// <para>
/// <strong>桑基图是有向无环图。</strong>ECharts 在链路成环时会直接抛错而非绘制，
/// 整张图因此空白。因此会造成闭环的链路——包括节点自环的链路——会被丢弃，
/// 图的其余部分照常绘制。缺一边名称、或数值非数字的行
/// 也因同样的原因被丢弃。
/// </para>
/// <para>
/// 最外一列的节点会把 label 指向画布边缘，ECharts 在那里绘制
/// 并任其溢出。这些节点会被赋予相反的 <see cref="LabelPosition"/>，使文字转向内侧，
/// 无论名称多长都保持可读。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;SankeyChart Data="@links"&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Sankey SourceKey="from" TargetKey="to" DataKey="value" /&gt;
/// &lt;/SankeyChart&gt;
/// </code>
/// </example>
public partial class Sankey : SeriesBase
{
    /// <summary>
    /// 获取或设置存放各链路出发节点名的属性名。
    /// </summary>
    [Parameter]
    public string? SourceKey { get; set; }

    /// <summary>
    /// 获取或设置存放各链路进入节点名的属性名。
    /// </summary>
    [Parameter]
    public string? TargetKey { get; set; }

    /// <summary>
    /// 获取或设置整张图的流向。
    /// </summary>
    [Parameter]
    public SankeyOrientation Orientation { get; set; } = SankeyOrientation.Horizontal;

    /// <summary>
    /// 获取或设置无出链节点的摆放位置。
    /// </summary>
    [Parameter]
    public SankeyNodeAlign NodeAlign { get; set; } = SankeyNodeAlign.Justify;

    /// <summary>
    /// 获取或设置节点的厚度（像素）。
    /// </summary>
    [Parameter]
    public int NodeWidth { get; set; } = 20;

    /// <summary>
    /// 获取或设置同一列中相邻节点间的间隙（像素）。
    /// </summary>
    [Parameter]
    public int NodeGap { get; set; } = 8;

    /// <summary>
    /// 获取或设置访问者能否把节点拖到新位置。
    /// </summary>
    /// <remarks>
    /// 默认关闭——与 ECharts 本身不同：被拖动的节点会停在落点，除重新加载外无路回退，
    /// 因此一次误拖会永久变形整张图。
    /// </remarks>
    [Parameter]
    public bool Draggable { get; set; }

    /// <summary>
    /// 获取或设置两节点间缎带的着色方式。
    /// </summary>
    [Parameter]
    public SankeyLinkColor LinkColor { get; set; } = SankeyLinkColor.Gradient;

    /// <summary>
    /// 获取或设置缎带的不透明度，取值 0 到 1。
    /// </summary>
    [Parameter]
    public double LinkOpacity { get; set; } = 0.4;

    /// <summary>
    /// 获取或设置缎带在两节点间的弯曲程度，从 0（直线）到 1。
    /// </summary>
    [Parameter]
    public double Curveness { get; set; } = 0.5;

    /// <summary>
    /// 获取或设置悬停节点时是否调暗与其不相连的一切。
    /// </summary>
    /// <remarks>
    /// 在繁忙的图中追踪一条路径正是绘制桑基图而非条形图的理由，
    /// 因此本项默认开启。
    /// </remarks>
    [Parameter]
    public bool FocusAdjacent { get; set; } = true;

    /// <summary>
    /// 获取或设置每个节点是否以其名称标注。
    /// </summary>
    [Parameter]
    public bool ShowLabels { get; set; } = true;

    /// <summary>
    /// 获取或设置 label 相对其节点的位置。
    /// </summary>
    [Parameter]
    public LabelPosition LabelPosition { get; set; } = LabelPosition.Right;

    /// <summary>
    /// 获取或设置 ECharts label 格式化字符串。
    /// </summary>
    /// <remarks>
    /// 使用 ECharts 模板变量：<c>{b}</c> 表示节点名，<c>{c}</c> 表示其值。
    /// </remarks>
    [Parameter]
    public string? LabelFormatter { get; set; }

    /// <summary>
    /// 获取或设置 label 文本颜色。
    /// </summary>
    [Parameter]
    public string? LabelColor { get; set; }

    /// <summary>
    /// 获取或设置 label 字号（像素）。
    /// </summary>
    [Parameter]
    public int? LabelFontSize { get; set; }

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var series = new EChartsSeriesOption
        {
            Type = "sankey",
            Name = GetResolvedName(),
            Left = "12",
            Top = "12",
            Right = "12",
            Bottom = "12",
            Orient = Orientation == SankeyOrientation.Vertical ? "vertical" : "horizontal",
            NodeAlign = NodeAlign switch
            {
                SankeyNodeAlign.Left => "left",
                SankeyNodeAlign.Right => "right",
                _ => "justify"
            },
            NodeWidth = NodeWidth,
            NodeGap = NodeGap,
            Draggable = Draggable,
            Label = new EChartsLabelOption
            {
                Show = ShowLabels,
                Position = ToEChartsPosition(LabelPosition),
                Formatter = LabelFormatter,
                Color = LabelColor ?? "var(--foreground)",
                FontSize = LabelFontSize,
                TextBorderWidth = 0
            },
            ItemStyle = new EChartsItemStyleOption
            {
                // 桑基图会自图表调色板为每个节点着色；系列上显式的 Color 则
                // 把所有节点涂成同色。此前该参数不生效——它被继承了、被展示了，
                // 然后被忽略了。
                Color = GetResolvedColor(),
                BorderWidth = 0
            },
            LineStyle = new EChartsLineStyleOption
            {
                Color = LinkColor switch
                {
                    SankeyLinkColor.Source => "source",
                    SankeyLinkColor.Target => "target",
                    _ => "gradient"
                },
                Opacity = LinkOpacity,
                Curveness = Curveness
            },
            Emphasis = FocusAdjacent
                ? new EChartsEmphasisOption { Focus = "adjacency" }
                : new EChartsEmphasisOption { Disabled = true }
        };

        ApplyGraph(series);

        return series;
    }

    /// <summary>
    /// 把绑定的行读成无环的节点-链路图并写入系列。
    /// </summary>
    private void ApplyGraph(EChartsSeriesOption series)
    {
        if (string.IsNullOrEmpty(SourceKey) || string.IsNullOrEmpty(TargetKey) || string.IsNullOrEmpty(DataKey))
        {
            return;
        }

        var graph = SankeyGraphBuilder.Build(
            DataExtractor.ExtractStringValues(ParentChart?.Data, SourceKey),
            DataExtractor.ExtractStringValues(ParentChart?.Data, TargetKey),
            DataExtractor.ExtractValues(ParentChart?.Data, DataKey));

        series.Data = new List<object?>(graph.Nodes.Select(name => (object?)BuildNode(name, graph)));

        series.Links = new List<object?>(graph.Links.Select(link => (object?)new Dictionary<string, object?>
        {
            ["source"] = link.Source,
            ["target"] = link.Target,
            ["value"] = link.Value
        }));
    }

    /// <summary>
    /// 构建单个节点；当 label 原本会跑出图表
    /// 边缘时将其掉头。
    /// </summary>
    /// <remarks>
    /// label 位于节点之外，故最外一列的节点会把 label 指向画布边缘，文字被裁切——
    /// ECharts 照绘并任其溢出。哪一列算最外取决于 label 的指向：
    /// 朝右的 label 在无出链的节点上有风险，朝左的则是在
    /// 无入链的节点上。这些节点会被换成对侧，使文字转向内侧，
    /// 无论名称多长都保持可读。
    /// </remarks>
    private Dictionary<string, object?> BuildNode(string name, SankeyGraph graph)
    {
        var node = new Dictionary<string, object?> { ["name"] = name };

        var flipped = LabelPosition switch
        {
            LabelPosition.Right => "left",
            LabelPosition.Bottom => "top",
            LabelPosition.Left => "right",
            LabelPosition.Top => "bottom",
            _ => null
        };

        if (flipped == null)
        {
            return node;
        }

        // 朝右或朝下的 label 在无后继的节点上会被裁切；朝左或朝上的
        // 则在无前驱的节点上会被裁切。
        var atRisk = LabelPosition is LabelPosition.Right or LabelPosition.Bottom
            ? !graph.WithOutgoing.Contains(name)
            : !graph.WithIncoming.Contains(name);

        if (atRisk)
        {
            node["label"] = new Dictionary<string, object?> { ["position"] = flipped };
        }

        return node;
    }
}
