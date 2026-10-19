using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的径向柱系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内借极坐标系（polar）渲染径向柱（环形进度）系列。支持圆头端点、
/// 逐项配色、标签、背景轨道，以及经子 <see cref="CenterLabel"/> 组件
/// 可选添加的中心标签。
/// </para>
/// <para>
/// 必须置于 <see cref="RadialBarChart"/> 组件内部。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;RadialBarChart Data="@data"&gt;
///     &lt;RadialBar DataKey="visitors" NameKey="browser"&gt;
///         &lt;CenterLabel Value="925" Title="Visitors" /&gt;
///     &lt;/RadialBar&gt;
/// &lt;/RadialBarChart&gt;
/// </code>
/// </example>
public partial class RadialBar : StackableSeriesBase
{
    /// <summary>
    /// 获取或设置用于从图表数据提取类目名称的属性名。
    /// </summary>
    [Parameter]
    public string? NameKey { get; set; }

    /// <summary>
    /// 获取或设置柱条端点是否为圆头。
    /// </summary>
    [Parameter]
    public bool RoundCap { get; set; } = true;

    /// <summary>
    /// 获取或设置是否在每根柱条上显示数值标签。
    /// </summary>
    [Parameter]
    public bool ShowLabels { get; set; }

    /// <summary>
    /// 获取或设置是否在数据柱条后方显示背景轨道。
    /// </summary>
    [Parameter]
    public bool ShowBackground { get; set; }

    private CenterLabel? centerLabel;

    /// <summary>
    /// 由子 <see cref="CenterLabel"/> 组件调用，向本系列注册自身。
    /// </summary>
    internal void SetCenterLabel(CenterLabel label) =>
        centerLabel = label;

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var series = new EChartsSeriesOption
        {
            Type = "bar",
            Name = GetResolvedName(),
            CoordinateSystem = "polar",
            RoundCap = RoundCap,
            ColorBy = "data",
            Emphasis = new EChartsEmphasisOption { Disabled = true },
            Label = new EChartsLabelOption
            {
                Show = false
            }
        };

        if (Stacked)
        {
            series.Stack = StackGroup;
        }

        if (ShowBackground)
        {
            series.ShowBackground = true;
            series.BackgroundStyle = new EChartsBackgroundStyleOption
            {
                Color = "var(--foreground)",
                Opacity = 0.15
            };
        }

        if (ShowLabels)
        {
            // "middle" 会把各标签放在其柱条自身的角度中点。数值相近的柱条，其中点
            // 角度也相近，于是标签挤进同一扇区并相互覆盖，
            // 无论图表画多大。"insideStart"
            // 则把各标签锚定在其所在环的起点——环与环之间恒有一根柱宽的距离，
            // 故它们叠排而非相撞。
            series.Label = new EChartsLabelOption
            {
                Show = true,
                Position = "insideStart",
                Rotate = "tangential",
                Formatter = "{b}",
                Color = "#fff",
                FontSize = 11
            };
        }

        // 构建数据项
        if (!string.IsNullOrEmpty(DataKey) && !string.IsNullOrEmpty(NameKey))
        {
            var pairs = DataExtractor.ExtractNameValuePairs(ParentChart?.Data, NameKey, DataKey);
            series.Data = new List<object?>(pairs.Select(p => (object?)p));
        }
        else if (!string.IsNullOrEmpty(DataKey))
        {
            series.Data = GetSeriesData();
        }

        // 为单色系列施加显式颜色
        var resolvedColor = GetResolvedColor();
        if (resolvedColor != null)
        {
            series.ItemStyle = new EChartsItemStyleOption { Color = resolvedColor };
            series.ColorBy = null;
        }

        return series;
    }

    /// <inheritdoc />
    private protected override void ApplyToOption(EChartsOption option)
    {
        base.ApplyToOption(option);

        // 经 NameKey 设置 radius 轴的类目名
        if (!string.IsNullOrEmpty(NameKey) && option.RadiusAxis != null)
        {
            var names = DataExtractor.ExtractStringValues(ParentChart?.Data, NameKey);
            if (option.RadiusAxis.Data == null || option.RadiusAxis.Data.Count == 0)
            {
                option.RadiusAxis.Data = names;
            }
        }

        // 依数据自动计算 angle 轴最大值
        if (!string.IsNullOrEmpty(DataKey) && option.AngleAxis != null && option.AngleAxis.Max == null)
        {
            var values = DataExtractor.ExtractValues(ParentChart?.Data, DataKey);
            double maxValue = 0;
            foreach (var v in values)
            {
                if (v != null)
                {
                    var d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                    if (d > maxValue)
                    {
                        maxValue = d;
                    }
                }
            }

            if (maxValue > 0)
            {
                // 堆叠系列时，对所有 polar 系列求和以得到真实最大值
                if (Stacked && option.Series.Count > 1)
                {
                    double stackedMax = 0;
                    foreach (var existingSeries in option.Series)
                    {
                        if (existingSeries.CoordinateSystem != "polar" || existingSeries.Data == null)
                        {
                            continue;
                        }

                        foreach (var item in existingSeries.Data)
                        {
                            if (item is Dictionary<string, object?> dict && dict.TryGetValue("value", out var val) && val != null)
                            {
                                stackedMax += Convert.ToDouble(val, CultureInfo.InvariantCulture);
                            }
                            else if (item != null)
                            {
                                stackedMax += Convert.ToDouble(item, CultureInfo.InvariantCulture);
                            }
                        }
                    }

                    option.AngleAxis.Max = stackedMax * 1.2;
                }
                else
                {
                    option.AngleAxis.Max = maxValue * 1.2;
                }
            }
        }

        // 将中心标签作为图表标题施加（定位于极坐标中心）
        if (centerLabel != null)
        {
            ApplyCenterTitle(option);
        }
    }

    private void ApplyCenterTitle(EChartsOption option)
    {
        if (!string.IsNullOrEmpty(centerLabel!.Text))
        {
            option.AddTitle(new EChartsTitleOption
            {
                Text = centerLabel.Text,

                // left/top 已把整个标题块居中于极坐标中心。
                // 再加 textAlign/textVerticalAlign 会让 ECharts 把 left/top 当作
                // 文字锚点、在其上把文字再居中一次，
                // 从而把整块向左上偏移半个自身尺寸。
                // 块内的行对齐应放在 textStyle.align 上。
                Left = "center",
                Top = "middle",
                TextStyle = new EChartsTextStyleOption
                {
                    Align = "center",
                    FontSize = centerLabel.FontSize,
                    FontWeight = centerLabel.FontWeight,
                    Color = "var(--foreground)"
                }
            });
        }
        else
        {
            // 用单一 text 属性内的富文本，让 textVerticalAlign 居中
            // 整个块（数值与标题两行），而非只居中首行。
            var valueText = centerLabel.Value ?? "";
            var titleText = centerLabel.Title ?? "";
            var text = $"{{value|{valueText}}}\n{{title|{titleText}}}";

            option.AddTitle(new EChartsTitleOption
            {
                Text = text,

                // 见上方说明：left/top 定位整块，textStyle.align 在块内居中
                // 两行。textAlign/textVerticalAlign 会居中两次。
                Left = "center",
                Top = "middle",
                TextStyle = new EChartsTextStyleOption
                {
                    Align = "center",
                    Rich = new Dictionary<string, EChartsRichStyleOption>
                    {
                        ["value"] = new EChartsRichStyleOption
                        {
                            FontSize = centerLabel.FontSize,
                            FontWeight = centerLabel.FontWeight,
                            Color = "var(--foreground)",
                            LineHeight = (int)(centerLabel.FontSize * 1.4)
                        },
                        ["title"] = new EChartsRichStyleOption
                        {
                            FontSize = 12,
                            Color = "var(--muted-foreground)",
                            LineHeight = 20
                        }
                    }
                }
            });
        }
    }
}
