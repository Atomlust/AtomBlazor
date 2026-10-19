using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的饼图/环形图系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个饼图或环形图系列。数据以名称-值对形式提取：
/// 值用 <see cref="SeriesBase.DataKey"/>，标签用 <see cref="NameKey"/>。
/// 将 <see cref="InnerRadius"/> 设为大于 0 即可创建环形图。
/// </para>
/// <para>
/// 支持经子 <see cref="CenterLabel"/> 组件可选地添加中心标签
/// （仅当 <see cref="InnerRadius"/> 大于 0 时可见）。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PieChart Data="@data"&gt;
///     &lt;Pie DataKey="visitors" NameKey="browser" InnerRadius="60" ShowLabels="true" /&gt;
/// &lt;/PieChart&gt;
/// </code>
/// </example>
public partial class Pie : SeriesBase
{
    /// <summary>
    /// 获取或设置用于从图表数据提取类目名称的属性名。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="SeriesBase.DataKey"/> 配合使用，为饼图扇区提取名称-值对。
    /// 例如 NameKey="browser" 且 DataKey="visitors"。
    /// </remarks>
    [Parameter]
    public string? NameKey { get; set; }

    /// <summary>
    /// 获取或设置外半径（相对图表容器的百分比）。
    /// </summary>
    [Parameter]
    public int OuterRadius { get; set; } = 80;

    /// <summary>
    /// 获取或设置内半径（相对图表容器的百分比）。
    /// </summary>
    /// <remarks>
    /// 设为 0 得完整饼图，大于 0 得环形图。
    /// </remarks>
    [Parameter]
    public int InnerRadius { get; set; }

    /// <summary>
    /// 获取或设置扇区间的间隔角度（度）。
    /// </summary>
    /// <remarks>
    /// 通过绘制与图表背景同色的边框在饼图扇区间制造视觉分隔。
    /// 默认为 2 像素。
    /// </remarks>
    [Parameter]
    public int PaddingAngle { get; set; } = 2;

    /// <summary>
    /// 获取或设置是否在每个扇区上显示标签。
    /// </summary>
    [Parameter]
    public bool ShowLabels { get; set; }

    /// <summary>
    /// 获取或设置 label 相对饼图扇区的位置。
    /// </summary>
    /// <remarks>
    /// 默认为 <see cref="LabelPosition.Outside"/>。要在扇区内部渲染标签，
    /// 请用 <see cref="LabelPosition.Inside"/>。
    /// </remarks>
    [Parameter]
    public LabelPosition LabelPosition { get; set; } = LabelPosition.Outside;

    /// <summary>
    /// 获取或设置 ECharts label 格式化字符串。
    /// </summary>
    /// <remarks>
    /// 使用 ECharts 模板变量：{b} 表示名称，{c} 表示数值，{d} 表示百分比。
    /// 设置本值会自动启用标签。
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

    /// <summary>
    /// 获取或设置是否显示把标签连到扇区的引导线。
    /// </summary>
    /// <remarks>
    /// 标签在外侧时默认为 true，在内侧时为 false。
    /// </remarks>
    [Parameter]
    public bool? ShowLabelLine { get; set; }

    /// <summary>
    /// 获取或设置预选（激活）扇区的索引。
    /// </summary>
    /// <remarks>
    /// 被选中的扇区会自圆心视觉偏移以示激活态。
    /// 使用 ECharts selectedMode="single"，并预选指定的数据项。
    /// </remarks>
    [Parameter]
    public int? ActiveIndex { get; set; }

    private CenterLabel? centerLabel;

    /// <summary>
    /// 由子 <see cref="CenterLabel"/> 组件调用，向本系列注册自身。
    /// </summary>
    internal void SetCenterLabel(CenterLabel label) =>
        centerLabel = label;

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var hasCenterLabel = centerLabel != null && InnerRadius > 0;
        var showLabel = !hasCenterLabel && (ShowLabels || !string.IsNullOrEmpty(LabelFormatter));

        var series = new EChartsSeriesOption
        {
            Type = "pie",
            Name = GetResolvedName(),
            Radius = InnerRadius > 0
                ? new[] { $"{InnerRadius}%", $"{OuterRadius}%" }
                : (object)$"{OuterRadius}%",
            ItemStyle = new EChartsItemStyleOption
            {
                // 饼图或漏斗图会自图表调色板为每个扇区着色；系列上显式的 Color 则
                // 把所有扇区涂成同色。此前该参数不生效——它被继承了、被展示了，
                // 然后被忽略了。
                Color = GetResolvedColor(),
                BorderColor = "var(--background)",
                BorderWidth = PaddingAngle
            },
            Emphasis = new EChartsEmphasisOption
            {
                Disabled = true,
                Scale = false
            }
        };

        if (ActiveIndex.HasValue)
        {
            series.SelectedMode = "single";
        }

        // 配置标签
        if (hasCenterLabel)
        {
            ApplyCenterLabel(series);
        }
        else
        {
            series.Label = new EChartsLabelOption
            {
                Show = showLabel,
                Position = ToEChartsPosition(LabelPosition),
                Formatter = LabelFormatter,
                Color = LabelColor ?? "var(--foreground)",
                FontSize = LabelFontSize,
                TextBorderWidth = 0
            };
            series.LabelLine = showLabel
                ? new EChartsLabelLineOption
                {
                    Show = ShowLabelLine ?? (LabelPosition != LabelPosition.Inside)
                }
                : null;
        }

        // 提取数据
        if (!string.IsNullOrEmpty(DataKey) && !string.IsNullOrEmpty(NameKey))
        {
            var pairs = DataExtractor.ExtractNameValuePairs(ParentChart?.Data, NameKey, DataKey);

            if (ActiveIndex.HasValue && ActiveIndex.Value >= 0 && ActiveIndex.Value < pairs.Count)
            {
                pairs[ActiveIndex.Value]["selected"] = true;
            }

            series.Data = new List<object?>(pairs.Select(p => (object?)p));
        }

        return series;
    }

    private void ApplyCenterLabel(EChartsSeriesOption series)
    {
        if (!string.IsNullOrEmpty(centerLabel!.Text))
        {
            series.Label = new EChartsLabelOption
            {
                Show = true,
                Position = "center",
                FontSize = centerLabel.FontSize,
                FontWeight = centerLabel.FontWeight,
                Color = "var(--foreground)",
                Formatter = centerLabel.Text
            };
        }
        else
        {
            var formatterParts = new List<string>();
            if (!string.IsNullOrEmpty(centerLabel.Value))
            {
                formatterParts.Add($"{{value|{centerLabel.Value}}}");
            }

            if (!string.IsNullOrEmpty(centerLabel.Title))
            {
                formatterParts.Add($"{{title|{centerLabel.Title}}}");
            }

            series.Label = new EChartsLabelOption
            {
                Show = true,
                Position = "center",
                Formatter = string.Join("\n", formatterParts),
                Rich = new Dictionary<string, object>
                {
                    ["value"] = new Dictionary<string, object>
                    {
                        ["fontSize"] = centerLabel.FontSize,
                        ["fontWeight"] = centerLabel.FontWeight,
                        ["color"] = "var(--foreground)",
                        ["lineHeight"] = centerLabel.FontSize + 8
                    },
                    ["title"] = new Dictionary<string, object>
                    {
                        ["fontSize"] = 12,
                        ["color"] = "var(--muted-foreground)",
                        ["lineHeight"] = 20
                    }
                }
            };
        }

        series.LabelLine = new EChartsLabelLineOption { Show = false };
    }
}
