using AtomBlazor.Primitives;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：ECharts* 内部 POCO 图的序列化形状——以 ChartOptionSerializer 的同一实例
/// （即送达 JS 者的配置）驱动，断言 JsonPropertyName 落点、camelCase 命名策略与
/// WhenWritingNull 的省略边界，以及各 POCO 的 C# 侧默认值（animation/category/show/containLabel/scroll 等）。
/// </summary>
public class EChartsSerializationTests
{
    private static readonly JsonSerializerOptions options = ChartOptionSerializer.Options;

    private static string Json(object value) => ChartOptionSerializer.SerializeValue(value);

    private static List<string> Keys(object value)
    {
        using var document = JsonDocument.Parse(Json(value));
        return document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
    }

    [Fact]
    public void 序列化配置与图表基类同源()
    {
        Assert.Same(options, ChartOptionSerializer.Options);
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
    }

    [Fact]
    public void 选项根默认只输出动画与系列两键()
    {
        var option = new EChartsOption();

        Assert.Equal(["animation", "series"], Keys(option));
        Assert.Equal("""{"animation":true,"series":[]}""", Json(option));
    }

    [Fact]
    public void 标题组未添加时不建键添加后成数组()
    {
        var option = new EChartsOption();
        Assert.DoesNotContain("title", Keys(option));

        option.AddTitle(new EChartsTitleOption { Text = "月度营收" });

        using var document = JsonDocument.Parse(Json(option));
        var title = document.RootElement.GetProperty("title");
        Assert.Equal(JsonValueKind.Array, title.ValueKind);
        Assert.Equal("月度营收", title[0].GetProperty("text").GetString());

        // 标题的 left 有 C# 默认值，故始终随形输出。
        Assert.Equal("center", title[0].GetProperty("left").GetString());
    }

    [Fact]
    public void 系列默认类型为折线并省略全部未设属性() =>
        Assert.Equal("""{"type":"line"}""", Json(new EChartsSeriesOption()));

    [Fact]
    public void 系列属性名按标注落点而非命名策略推导()
    {
        var series = new EChartsSeriesOption
        {
            Type = "bar",
            ShowSymbol = false,
            SymbolSize = 9,
            BarWidth = "12",
            BarGap = "10%",
            RoundCap = true,
            CoordinateSystem = "polar",
            RadarIndex = 1,
            ColorBy = "data",
            ShowBackground = true,
            SelectedMode = "single",
            NodeAlign = "justify",
            RoseType = "radius",
            FunnelAlign = "center",
            MinSize = "0%",
            StartAngle = 225,
            SplitNumber = 5,
            LabelLine = new EChartsLabelLineOption { Show = true },
            BackgroundStyle = new EChartsBackgroundStyleOption { Color = "var(--muted)" }
        };

        var keys = Keys(series);

        foreach (var expected in new[]
        {
            "type", "showSymbol", "symbolSize", "barWidth", "barGap", "roundCap", "coordinateSystem",
            "radarIndex", "colorBy", "showBackground", "selectedMode", "nodeAlign", "roseType",
            "funnelAlign", "minSize", "startAngle", "splitNumber", "labelLine", "backgroundStyle"
        })
        {
            Assert.Contains(expected, keys);
        }

        Assert.DoesNotContain("show_symbol", keys);
        Assert.DoesNotContain("SymbolSize", keys);
    }

    [Fact]
    public void 系列数据内的空项不被省略而是保留为空()
    {
        var series = new EChartsSeriesOption { Type = "scatter", Data = [1, null, 3] };

        using var document = JsonDocument.Parse(Json(series));
        var data = document.RootElement.GetProperty("data");

        Assert.Equal(3, data.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, data[1].ValueKind);
    }

    [Fact]
    public void 半径既可为标量亦可为二元数组()
    {
        using var single = JsonDocument.Parse(Json(new EChartsSeriesOption { Type = "pie", Radius = "70%" }));
        Assert.Equal(JsonValueKind.String, single.RootElement.GetProperty("radius").ValueKind);
        Assert.Equal("70%", single.RootElement.GetProperty("radius").GetString());

        var paired = new EChartsSeriesOption { Type = "pie", Radius = new object?[] { "40%", "70%" } };
        using var document = JsonDocument.Parse(Json(paired));

        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("radius").ValueKind);
        Assert.Equal("40%", document.RootElement.GetProperty("radius")[0].GetString());
    }

    [Fact]
    public void 轴默认类别型且默认展示()
    {
        Assert.Equal("""{"type":"category","show":true}""", Json(new EChartsAxisOption()));

        var value = new EChartsAxisOption { Type = "value", BoundaryGap = new object?[] { false, false }, Data = ["一月", "二月"] };
        using var document = JsonDocument.Parse(Json(value));

        Assert.Equal("value", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("boundaryGap").GetArrayLength());
        Assert.Equal("一月", document.RootElement.GetProperty("data")[0].GetString());
    }

    [Fact]
    public void 轴的子件默认展示且样式随名落点()
    {
        var axis = new EChartsAxisOption
        {
            AxisLine = new EChartsAxisLineOption(),
            AxisTick = new EChartsAxisTickOption(),
            SplitLine = new EChartsSplitLineOption { LineStyle = new EChartsLineStyleOption { Curveness = 0.2, Opacity = 0.5 } },
            AxisLabel = new EChartsAxisLabelOption { Rich = new Dictionary<string, EChartsRichStyleOption> { ["标题"] = new() { FontSize = 14 } } }
        };

        using var document = JsonDocument.Parse(Json(axis));
        var root = document.RootElement;

        Assert.True(root.GetProperty("axisLine").GetProperty("show").GetBoolean());
        Assert.True(root.GetProperty("axisTick").GetProperty("show").GetBoolean());
        Assert.Equal(0.2, root.GetProperty("splitLine").GetProperty("lineStyle").GetProperty("curveness").GetDouble());
        Assert.Equal(14, root.GetProperty("axisLabel").GetProperty("rich").GetProperty("标题").GetProperty("fontSize").GetInt32());
    }

    [Fact]
    public void 提示默认展示且指针默认线型()
    {
        Assert.Equal("""{"show":true}""", Json(new EChartsTooltipOption()));
        Assert.Equal("""{"type":"line"}""", Json(new EChartsAxisPointerOption()));

        var tooltip = new EChartsTooltipOption
        {
            Trigger = "axis",
            AppendToBody = true,
            ExtraCssText = "border-radius: 8px;",
            AxisPointer = new EChartsAxisPointerOption { Type = "shadow" },
            TextStyle = new EChartsTextStyleOption { Color = "var(--popover-foreground)", LineHeight = 20 }
        };

        using var document = JsonDocument.Parse(Json(tooltip));
        var root = document.RootElement;

        Assert.Equal("axis", root.GetProperty("trigger").GetString());
        Assert.True(root.GetProperty("appendToBody").GetBoolean());
        Assert.Equal("border-radius: 8px;", root.GetProperty("extraCssText").GetString());
        Assert.Equal("shadow", root.GetProperty("axisPointer").GetProperty("type").GetString());
        Assert.Equal(20, root.GetProperty("textStyle").GetProperty("lineHeight").GetInt32());
    }

    [Fact]
    public void 网格默认容纳轴标签()
    {
        Assert.Equal("""{"containLabel":true}""", Json(new EChartsGridOption()));

        var grid = new EChartsGridOption { Left = "5%", Right = "5%", Top = "15", Bottom = "0", Show = false, BorderColor = "var(--border)" };
        Assert.Equal(["left", "right", "top", "bottom", "containLabel", "show", "borderColor"], Keys(grid));
    }

    [Fact]
    public void 图例默认展示其余键随设置出现()
    {
        Assert.Equal("""{"show":true}""", Json(new EChartsLegendOption()));

        var legend = new EChartsLegendOption { Type = "scroll", ItemGap = 24, Orient = "horizontal", Left = "center", Bottom = "0", Icon = "roundRect", Data = ["营收"] };
        Assert.Equal(["show", "orient", "left", "bottom", "data", "icon", "itemGap", "type"], Keys(legend));
    }

    [Fact]
    public void 视觉映射全空时输出空对象()
    {
        Assert.Equal("{}", Json(new EChartsVisualMapOption()));

        var map = new EChartsVisualMapOption
        {
            Min = 0,
            Max = 100,
            Calculable = true,
            Orient = "horizontal",
            Type = "continuous",
            InRange = new EChartsVisualMapInRangeOption { Color = ["var(--chart-5)", "var(--chart-1)"] }
        };

        Assert.Equal(["min", "max", "calculable", "orient", "type", "inRange"], Keys(map));
    }

    [Fact]
    public void 渐变不停用省略条件故全键随形()
    {
        var gradient = new EChartsLinearGradient { X = 0, Y = 0, X2 = 0, Y2 = 1 };

        Assert.Equal(["type", "x", "y", "x2", "y2", "colorStops", "global"], Keys(gradient));
        using (var document = JsonDocument.Parse(Json(gradient)))
        {
            Assert.Equal("linear", document.RootElement.GetProperty("type").GetString());
        }

        var stop = new EChartsColorStop { Offset = 0.5, Color = "var(--chart-1)" };
        Assert.Equal("""{"offset":0.5,"color":"var(--chart-1)"}""", Json(stop));
    }

    [Fact]
    public void 雷达指标空值省略空标签保留()
    {
        Assert.Equal("""{"name":""}""", Json(new EChartsRadarIndicator()));

        // 非 ASCII 名称经默认转义写出，故按键取值而非按字面串比较。
        using (var named = JsonDocument.Parse(Json(new EChartsRadarIndicator { Name = "稳定性", Max = 95 })))
        {
            Assert.Equal("稳定性", named.RootElement.GetProperty("name").GetString());
            Assert.Equal(95, named.RootElement.GetProperty("max").GetDouble());
        }

        var radar = new EChartsRadarOption { Indicator = [new EChartsRadarIndicator { Name = "稳定性" }], Shape = "polygon", SplitNumber = 4 };
        using var document = JsonDocument.Parse(Json(radar));

        Assert.Equal(1, document.RootElement.GetProperty("indicator").GetArrayLength());
        Assert.Equal("polygon", document.RootElement.GetProperty("shape").GetString());
    }

    [Fact]
    public void 极坐标轴默认展示()
    {
        Assert.Equal("""{"show":true}""", Json(new EChartsRadiusAxisOption()));
        Assert.Equal(["show"], Keys(new EChartsAngleAxisOption()));
    }

    [Fact]
    public void 仪表盘子件无默认值故未设即省()
    {
        Assert.Equal("{}", Json(new EChartsGaugeProgressOption()));
        Assert.Equal("{}", Json(new EChartsGaugePointerOption()));
        Assert.Equal("{}", Json(new EChartsGaugeDetailOption()));
        Assert.Equal("{}", Json(new EChartsSeriesTitleOption()));

        var progress = new EChartsGaugeProgressOption { Show = true, Width = 16, RoundCap = true, Color = "var(--chart-1)" };
        Assert.Equal(["show", "width", "roundCap", "color"], Keys(progress));
    }

    [Fact]
    public void 强调与文本样式按标注落点()
    {
        var emphasis = new EChartsEmphasisOption
        {
            Disabled = false,
            Focus = "series",
            Scale = true,
            ScaleSize = 6,
            ItemStyle = new EChartsItemStyleOption { BorderRadius = new object?[] { 4, 4, 0, 0 }, Color0 = "var(--muted)", BorderColor0 = "var(--border)" },
            Label = new EChartsLabelOption { Position = "inside", Formatter = "{b}: {d}%", TextBorderColor = "var(--background)", TextBorderWidth = 2, Rotate = 45 }
        };

        using var document = JsonDocument.Parse(Json(emphasis));
        var root = document.RootElement;

        Assert.Equal("series", root.GetProperty("focus").GetString());
        Assert.Equal(6, root.GetProperty("scaleSize").GetInt32());
        Assert.Equal("var(--muted)", root.GetProperty("itemStyle").GetProperty("color0").GetString());
        Assert.Equal("var(--border)", root.GetProperty("itemStyle").GetProperty("borderColor0").GetString());
        Assert.Equal(4, root.GetProperty("itemStyle").GetProperty("borderRadius").GetArrayLength());
        Assert.Equal("{b}: {d}%", root.GetProperty("label").GetProperty("formatter").GetString());
        Assert.Equal(2, root.GetProperty("label").GetProperty("textBorderWidth").GetInt32());
    }

    [Fact]
    public void 颜色表为对象表时原样输出混合值()
    {
        var option = new EChartsOption { Color = [new EChartsLinearGradient { X2 = 1 }, 1, "var(--chart-1)"] };

        using var document = JsonDocument.Parse(Json(option));
        var color = document.RootElement.GetProperty("color");

        Assert.Equal(3, color.GetArrayLength());
        Assert.Equal("linear", color[0].GetProperty("type").GetString());
        Assert.Equal(1, color[1].GetInt32());
        Assert.Equal("var(--chart-1)", color[2].GetString());
    }
}
