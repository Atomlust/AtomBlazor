using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 可组合图表的柱状系列组件。
/// </summary>
/// <remarks>
/// <para>
/// 在父图表内渲染一个柱状系列。支持可配置的圆角半径、柱宽、堆叠，
/// 以及填充色——含经子 <see cref="Fill"/> 组件实现的渐变。
/// </para>
/// <para>
/// 必须置于提供 <see cref="ChartBase"/> 级联值的图表组件内部。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BarChart Data="@data"&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
///     &lt;Bar DataKey="desktop" Name="Desktop" BorderRadius="6" /&gt;
///     &lt;Bar DataKey="mobile" Name="Mobile" /&gt;
/// &lt;/BarChart&gt;
/// </code>
/// </example>
public partial class Bar : StackableSeriesBase
{
    /// <summary>
    /// 获取或设置柱条圆角的半径（像素）。
    /// </summary>
    /// <remarks>
    /// 施加于柱条的朝向端（垂直柱为上端，水平柱为右端）。
    /// 为 null 时，非堆叠柱默认为 4，堆叠柱默认为 0。
    /// 值为负时，半径会自动施加到另一端。
    /// </remarks>
    [Parameter]
    public int? BorderRadius { get; set; }

    /// <summary>
    /// 获取或设置每根柱条的宽度。
    /// </summary>
    /// <remarks>
    /// 接受百分比字符串（如 "60%"）或像素值（如 "20"）。
    /// 为 null 时由 ECharts 自动适配柱宽。
    /// </remarks>
    [Parameter]
    public string? BarWidth { get; set; }

    /// <summary>
    /// 获取或设置是否在每根柱条上显示数据标签（label）。
    /// </summary>
    [Parameter]
    public bool ShowLabel { get; set; }

    /// <summary>
    /// 获取或设置数据标签相对柱条的位置。
    /// </summary>
    /// <remarks>
    /// 垂直柱条的默认值为 <see cref="LabelPosition.Top"/>。
    /// </remarks>
    [Parameter]
    public LabelPosition LabelPosition { get; set; } = LabelPosition.Top;

    /// <summary>
    /// 获取或设置 label 格式化字符串。
    /// </summary>
    /// <remarks>
    /// ECharts formatter 字符串。用 <c>{b}</c> 表示类目名，<c>{c}</c> 表示数值。
    /// 为 null 时显示默认的数值标签。
    /// </remarks>
    [Parameter]
    public string? LabelFormatter { get; set; }

    /// <summary>
    /// 获取或设置 label 文本颜色。
    /// </summary>
    [Parameter]
    public string? LabelColor { get; set; }

    /// <summary>
    /// 获取或设置 label 字号。
    /// </summary>
    [Parameter]
    public int? LabelFontSize { get; set; }

    /// <summary>
    /// 获取或设置用于从图表数据逐项提取填充色的属性名。
    /// </summary>
    /// <remarks>
    /// 设置后，每个数据项的填充色从该属性读取，从而支持
    /// 逐柱着色。该属性应包含 CSS 颜色值或
    /// CSS 变量引用（如 "var(--chart-1)"）。
    /// </remarks>
    [Parameter]
    public string? FillKey { get; set; }

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var resolvedColor = GetResolvedColor();
        var effectiveColor = GetResolvedFillColor() ?? resolvedColor;
        var isHorizontal = ParentChart?.SwapAxes == true;
        var radius = BorderRadius ?? (Stacked ? 0 : 4);
        var rawData = GetSeriesData();

        // 判断是否需要逐项数据对象
        var perItemColors = !string.IsNullOrEmpty(FillKey)
            ? DataExtractor.ExtractValues(ParentChart?.Data, FillKey)
            : null;
        var hasNegative = HasNegativeValues(rawData);
        var needsPerItemData = hasNegative || perItemColors != null;

        var series = new EChartsSeriesOption
        {
            Type = "bar",
            Name = GetResolvedName(),
            BarWidth = BarWidth,
            Emphasis = new EChartsEmphasisOption { Disabled = true }
        };

        if (needsPerItemData)
        {
            series.Data = BuildPerItemData(rawData, perItemColors, effectiveColor, radius, isHorizontal);
            series.ItemStyle = new EChartsItemStyleOption
            {
                Color = effectiveColor
            };
        }
        else
        {
            series.Data = rawData;
            series.ItemStyle = new EChartsItemStyleOption
            {
                BorderRadius = GetBorderRadiusArray(radius, isHorizontal, false),
                Color = effectiveColor
            };
        }

        if (ShowLabel)
        {
            series.Label = new EChartsLabelOption
            {
                Show = true,
                Position = ToEChartsPosition(LabelPosition),
                Formatter = LabelFormatter,
                Color = LabelColor ?? "var(--foreground)",
                FontSize = LabelFontSize,
                TextBorderWidth = 0
            };
        }

        if (Stacked)
        {
            series.Stack = StackGroup;
        }

        return series;
    }

    private static List<object?> BuildPerItemData(
        List<object?> rawData,
        List<object?>? perItemColors,
        object? defaultColor,
        int radius,
        bool isHorizontal)
    {
        var itemData = new List<object?>(rawData.Count);

        for (var i = 0; i < rawData.Count; i++)
        {
            var value = rawData[i];
            var isNeg = IsNegative(value);
            var itemRadius = GetBorderRadiusArray(radius, isHorizontal, isNeg);
            var itemColor = (perItemColors != null && i < perItemColors.Count)
                ? perItemColors[i]
                : defaultColor;

            itemData.Add(new Dictionary<string, object?>
            {
                ["value"] = value,
                ["itemStyle"] = new Dictionary<string, object?>
                {
                    ["borderRadius"] = itemRadius,
                    ["color"] = itemColor
                }
            });
        }

        return itemData;
    }

    private static int[] GetBorderRadiusArray(int radius, bool isHorizontal, bool isNegative)
    {
        if (radius == 0)
        {
            return [0, 0, 0, 0];
        }

        if (isHorizontal)
        {
            // 水平柱：正值圆化右端，负值圆化左端
            return isNegative
                ? [radius, 0, 0, radius]
                : [0, radius, radius, 0];
        }

        // 垂直柱：正值圆化上端，负值圆化下端
        return isNegative
            ? [0, 0, radius, radius]
            : [radius, radius, 0, 0];
    }

    private static bool HasNegativeValues(List<object?> data)
    {
        foreach (var value in data)
        {
            if (IsNegative(value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNegative(object? value)
    {
        return value switch
        {
            int i => i < 0,
            long l => l < 0,
            double d => d < 0,
            float f => f < 0,
            decimal m => m < 0,
            _ => false
        };
    }
}
