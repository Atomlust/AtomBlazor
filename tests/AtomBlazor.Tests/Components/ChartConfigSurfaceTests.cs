using AtomBlazor.Primitives;
using System.Collections;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：Chart 公开配置面（ChartConfig / ChartSeriesConfig / ChartColor / ChartClickEventArgs
/// 与 LabelPosition 枚举）的语义与默认值——键的大小写不敏感、索引器写 null 即移除、
/// 标签与颜色的三级回落、调色序号循环、未配置键不再一律返回 1 号色的回归，
/// 以及点击事件参数的哨兵默认值。
/// </summary>
public class ChartConfigSurfaceTests
{
    private static ChartConfig Config() => ChartConfig.Create(
        ("desktop", new ChartSeriesConfig { Label = "桌面端", Color = "var(--chart-1)" }),
        ("mobile", new ChartSeriesConfig { Label = "移动端", Color = "var(--chart-2)" }));

    [Fact]
    public void 默认调色板为五个图表色变量按序排列()
    {
        Assert.Equal(
            ["var(--chart-1)", "var(--chart-2)", "var(--chart-3)", "var(--chart-4)", "var(--chart-5)"],
            ChartColor.DefaultColors);
        Assert.Equal(
            [ChartColor.Chart1, ChartColor.Chart2, ChartColor.Chart3, ChartColor.Chart4, ChartColor.Chart5],
            ChartColor.DefaultColors);
    }

    [Theory]
    [InlineData(0, "var(--chart-1)")]
    [InlineData(1, "var(--chart-2)")]
    [InlineData(4, "var(--chart-5)")]
    [InlineData(5, "var(--chart-1)")]
    [InlineData(12, "var(--chart-3)")]
    [InlineData(-1, "var(--chart-2)")]
    [InlineData(-7, "var(--chart-3)")]
    public void 取默认色按五角循环且负序号取绝对值(int index, string expected) =>
        Assert.Equal(expected, ChartColor.GetDefault(index));

    [Fact]
    public void 批量取色与逐项取默认色同源()
    {
        Assert.Equal(["var(--chart-1)", "var(--chart-2)"], ChartColor.GetColors(2));
        Assert.Empty(ChartColor.GetColors(0));
        Assert.Equal(7, ChartColor.GetColors(7).Length);
        Assert.Equal(ChartColor.GetDefault(6), ChartColor.GetColors(7)[6]);
    }

    [Fact]
    public void 系列配置默认标签与颜色皆空串()
    {
        var config = new ChartSeriesConfig();

        Assert.Equal(string.Empty, config.Label);
        Assert.Equal(string.Empty, config.Color);
    }

    [Fact]
    public void 索引器缺失键返回空而写入空值即移除()
    {
        var config = Config();

        Assert.Null(config["不存在"]);
        Assert.NotNull(config["desktop"]);

        config["desktop"] = null;
        Assert.Null(config["desktop"]);
        Assert.False(config.ContainsKey("desktop"));
        Assert.Equal(1, config.Count);

        // 写 null 于本不存在的键亦为无害移除。
        config["从未存在"] = null;
        Assert.Equal(1, config.Count);
    }

    [Fact]
    public void 键大小写不敏感匹配()
    {
        var config = Config();

        Assert.Same(config["desktop"], config["DESKTOP"]);
        Assert.True(config.ContainsKey("Mobile"));
        Assert.Equal("桌面端", config.GetLabel("Desktop"));
        Assert.Equal("var(--chart-1)", config.GetColor("DeSkToP"));

        // 覆盖写亦按同一不敏感键收敛。
        config.Add("DESKTOP", new ChartSeriesConfig { Label = "桌面端覆盖" });
        Assert.Equal(2, config.Count);
        Assert.Equal("桌面端覆盖", config.GetLabel("desktop"));
    }

    [Fact]
    public void 添加同键即覆盖既有配置()
    {
        var config = Config();
        config.Add("desktop", new ChartSeriesConfig { Label = "桌面", Color = "var(--chart-3)" });

        Assert.Equal(2, config.Count);
        Assert.Equal("桌面", config.GetLabel("desktop"));
        Assert.Equal("var(--chart-3)", config.GetColor("desktop"));
    }

    [Theory]
    [InlineData("desktop", "桌面端")]
    [InlineData("未配置键", "未配置键")]
    public void 标签未配置或为空时回落键本身(string key, string expected)
    {
        var config = Config();
        config["空标签"] = new ChartSeriesConfig { Label = string.Empty, Color = "var(--chart-4)" };

        Assert.Equal(expected, config.GetLabel(key));
    }

    [Fact]
    public void 颜色未配置时默认不兜底以免全部同色()
    {
        var config = Config();

        Assert.Null(config.GetColor("未配置键"));
        Assert.Equal("var(--chart-3)", config.GetColor("未配置键", 2));
        Assert.Equal("var(--chart-1)", config.GetColor("未配置键", 0));

        // 已配置色优先于序号色，无论兜底序号给不给、给什么。
        Assert.Equal("var(--chart-2)", config.GetColor("mobile", 4));
        Assert.Equal("var(--chart-2)", config.GetColor("mobile", -1));
        Assert.Equal("var(--chart-2)", config.GetColor("mobile"));
    }

    [Fact]
    public void 空颜色配置视为未配置()
    {
        var config = new ChartConfig();
        config.Add("blank", new ChartSeriesConfig());

        Assert.Null(config.GetColor("blank"));
        Assert.Equal("blank", config.GetLabel("blank"));
        Assert.Equal("var(--chart-5)", config.GetColor("blank", 4));
    }

    [Fact]
    public void 键集合计数与成员一致()
    {
        var config = Config();

        Assert.Equal(2, config.Count);
        Assert.Equal(["desktop", "mobile"], config.Keys.Order());
        Assert.False(config.ContainsKey("tablet"));

        config["tablet"] = new ChartSeriesConfig { Label = "平板" };
        Assert.Equal(3, config.Count);
        Assert.Contains("tablet", config.Keys);
    }

    [Fact]
    public void 集合初始化器与创建工厂两种写法同形()
    {
        var initializer = new ChartConfig
        {
            { "desktop", new ChartSeriesConfig { Label = "桌面端", Color = "var(--chart-1)" } },
            { "mobile", new ChartSeriesConfig { Label = "移动端", Color = "var(--chart-2)" } }
        };

        var factory = Config();

        Assert.Equal(factory.Count, initializer.Count);
        foreach (var pair in initializer)
        {
            Assert.Equal(factory[pair.Key]!.Label, pair.Value.Label);
        }
    }

    [Fact]
    public void 泛型与非泛型枚举器同源()
    {
        var config = Config();

        var generic = config.Select(pair => pair.Key).ToList();
        var nonGeneric = ((IEnumerable)config).Cast<KeyValuePair<string, ChartSeriesConfig>>().Select(pair => pair.Key).ToList();

        Assert.Equal(generic.Order(), nonGeneric.Order());
        Assert.Equal(2, generic.Count);
    }

    [Fact]
    public void 由标签批量建配置时键取短名并顺序调色()
    {
        var config = ChartConfig.FromLabels("桌面 用户", "Mobile", "平板端");

        Assert.Equal(3, config.Count);
        Assert.True(config.ContainsKey("桌面-用户"));
        Assert.Equal("桌面 用户", config.GetLabel("桌面-用户"));
        Assert.Equal("var(--chart-1)", config.GetColor("桌面-用户"));
        Assert.Equal("var(--chart-2)", config.GetColor("mobile"));
        Assert.Equal("var(--chart-3)", config.GetColor("平板端"));

        Assert.Empty(ChartConfig.Create());
        Assert.Empty(ChartConfig.FromLabels());
    }

    [Fact]
    public void 点击事件参数以负一为缺位哨兵()
    {
        var args = new ChartClickEventArgs();

        Assert.Equal(-1, args.SeriesIndex);
        Assert.Equal(-1, args.DataIndex);
        Assert.Null(args.SeriesName);
        Assert.Null(args.Name);
        Assert.Null(args.ComponentType);
        Assert.Null(args.Value);
        Assert.Null(args.Values);
    }

    [Fact]
    public void 点击事件参数承载标量与数组两种取值形态()
    {
        var scalar = new ChartClickEventArgs
        {
            SeriesName = "营收",
            SeriesIndex = 0,
            DataIndex = 3,
            Name = "四月",
            ComponentType = "series",
            Value = 42.5
        };

        // K 线经数组抵达：ECharts 把点序号前置，故为五项。
        var candle = new ChartClickEventArgs
        {
            SeriesName = "行情",
            ComponentType = "series",
            Values = [4, 100d, 108d, 96d, 110d]
        };

        Assert.Equal(42.5, scalar.Value);
        Assert.Null(scalar.Values);
        Assert.Equal(5, candle.Values!.Count);
        Assert.Equal(100d, candle.Values[1]);
        Assert.Equal(-1, candle.SeriesIndex);
    }

    [Fact]
    public void 标签位置枚举含十二成员且内外侧齐备()
    {
        var names = Enum.GetNames<LabelPosition>();

        Assert.Equal(12, names.Length);
        Assert.Equal(["Top", "Bottom", "Left", "Right", "Inside", "InsideLeft", "InsideRight", "InsideTop", "InsideBottom", "Outside", "Center", "Middle"], names);
    }
}
