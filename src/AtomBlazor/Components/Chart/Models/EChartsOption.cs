using System.Text.Json.Serialization;

namespace AtomBlazor;

internal sealed class EChartsOption
{
    [JsonPropertyName("animation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Animation { get; set; } = true;

    [JsonPropertyName("xAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsAxisOption? XAxis { get; set; }

    [JsonPropertyName("yAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsAxisOption? YAxis { get; set; }

    [JsonPropertyName("grid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsGridOption? Grid { get; set; }

    [JsonPropertyName("tooltip")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsTooltipOption? Tooltip { get; set; }

    [JsonPropertyName("legend")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsLegendOption? Legend { get; set; }

    [JsonPropertyName("series")]
    public List<EChartsSeriesOption> Series { get; set; } = [];

    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? Color { get; set; }

    [JsonPropertyName("radar")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsRadarOption? Radar { get; set; }

    /// <summary>
    /// 图表的标题组。ECharts 在此接受数组，图表因此既能保留自己的
    /// 标题，又可让某个系列借走第二个标题，为该系列类型本身没有
    /// 标签可挂的东西加注——例如径向柱图中央的文字。
    /// </summary>
    /// <remarks>
    /// 什么都没添加时保持 null 而非空表，于是无标题的图表
    /// 压根不会输出 <c>title</c> 键。
    /// </remarks>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<EChartsTitleOption>? Titles { get; set; }

    [JsonPropertyName("polar")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsPolarOption? Polar { get; set; }

    [JsonPropertyName("angleAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsAngleAxisOption? AngleAxis { get; set; }

    [JsonPropertyName("radiusAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsRadiusAxisOption? RadiusAxis { get; set; }

    [JsonPropertyName("visualMap")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsVisualMapOption? VisualMap { get; set; }

    /// <summary>添加一个标题，首次使用时创建列表。</summary>
    internal void AddTitle(EChartsTitleOption title) =>
        (Titles ??= []).Add(title);
}
