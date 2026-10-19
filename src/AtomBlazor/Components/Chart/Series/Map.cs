using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>国家填充色表示数值的世界地图（world map）系列组件。</summary>
/// <remarks>
/// 每张图表只请使用一个 map 系列。请按国家逐行绑定，国家可用 ISO alpha-2 或
/// alpha-3 代码，也可用随库打包的 Natural Earth 数据集中的英文名。匹配忽略
/// 大小写与首尾空白。无法识别的国家会被忽略；多行指向同一国家时以首行为准，
/// 请先自行聚合源数据。
/// </remarks>
public partial class Map : SeriesBase
{
    /// <summary>其值为国家代码或英文国家名称的属性。</summary>
    [Parameter]
    public string? CountryKey { get; set; }

    /// <summary>用户可否平移与缩放地图。默认为 false。</summary>
    [Parameter]
    public bool Roam { get; set; }

    /// <summary>无数值国家的填充色。</summary>
    [Parameter]
    public string NoDataColor { get; set; } = "var(--muted)";

    /// <summary>国界线的颜色。</summary>
    [Parameter]
    public string BorderColor { get; set; } = "var(--background)";

    /// <summary>是否在地图上显示国家名称。</summary>
    [Parameter]
    public bool ShowLabel { get; set; }

    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var series = new EChartsSeriesOption
        {
            Type = "map",
            Map = "atom-world",
            Name = GetResolvedName(),
            Roam = Roam,
            SelectedMode = false,
            Left = "0",
            Right = "0",
            Top = "30",
            Bottom = "55",
            ItemStyle = new EChartsItemStyleOption
            {
                AreaColor = GetResolvedFillColor() ?? GetResolvedColor() ?? NoDataColor,
                BorderColor = BorderColor,
                BorderWidth = 1
            },
            Label = new EChartsLabelOption { Show = ShowLabel, Color = "var(--foreground)" },
            Emphasis = new EChartsEmphasisOption
            {
                Label = new EChartsLabelOption { Show = true, Color = "var(--foreground)" },
                ItemStyle = new EChartsItemStyleOption { BorderColor = "var(--foreground)" }
            },
            Data = []
        };

        if (string.IsNullOrWhiteSpace(CountryKey) || string.IsNullOrWhiteSpace(DataKey))
        {
            return series;
        }

        var countries = DataExtractor.ExtractStringValues(ParentChart?.Data, CountryKey);
        var values = GetSeriesData();
        for (var i = 0; i < Math.Min(countries.Count, values.Count); i++)
        {
            if (string.IsNullOrWhiteSpace(countries[i]))
            {
                continue;
            }

            // 缺失与非有限值不得变成零，否则 JSON 序列化会被破坏。
            var numeric = double.TryParse(Convert.ToString(values[i], CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value);
            series.Data.Add(new Dictionary<string, object?>
            {
                ["name"] = countries[i].Trim(),
                ["value"] = numeric ? value : null,
                ["atomSourceIndex"] = i
            });
        }

        return series;
    }
}
