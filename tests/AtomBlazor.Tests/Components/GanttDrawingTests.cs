using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using AtomBlazor.Primitives.Gantt;
using AtomBlazor.Tests.Performance;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：Gantt.Drawing.cs 的落屏几何——横条/里程碑/汇总支架的放置样式、
/// 依赖箭头走线与箭头多边形、槽界细线背景、置灰色带的合并、上层分界线、
/// 缩进与列宽样式。皆经反射直取组件私有绘制件，不经渲染。
/// 行对象取自 GanttBuilder，故几何输入与屏上真正所用者同源。
/// 参数以属性赋值经反射写入（直接 SetParametersAsync 会走渲染句柄生命周期而抛出）。
/// </summary>
public class GanttDrawingTests
{
    private sealed record Job(string Id, string? Parent, string Text, DateTimeOffset Start, DateTimeOffset End, double Progress = 0);

    private static DateTimeOffset On(int year, int month, int day) =>
        new(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);

    private static GanttChart<Job> Chart(params Job[] jobs) => GanttBuilder.Build(new GanttSource<Job>
    {
        Items = jobs,
        Id = j => j.Id,
        ParentId = j => j.Parent,
        Text = j => j.Text,
        Start = j => j.Start,
        End = j => j.End,
        Progress = j => j.Progress,
    });

    private static T SetProps<T>(T component, params (string Name, object? Value)[] values)
    {
        foreach (var (name, value) in values)
        {
            typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!.SetValue(component, value);
        }

        return component;
    }

    // 行高 36、槽宽 40：一切像素期望皆以此双数为基准。
    private static Gantt<Job> NewGantt(params (string Name, object? Value)[] extra) =>
        SetProps(new Gantt<Job>(),
            [(nameof(Gantt<Job>.RowHeight), 36), (nameof(Gantt<Job>.SlotWidth), 40), .. extra]);

    private static object? CallStatic(string name, params object?[] args) =>
        typeof(Gantt<Job>).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);

    [Fact]
    public void BarStylePlacesAndSizesTheBarInPixelsFromSlotGeometry()
    {
        var gantt = NewGantt();
        var row = Chart(new Job("a", null, "A", On(2026, 3, 2), On(2026, 3, 5))).Rows[0];

        // 一轴自 3 月 1 日开端（缺省 Major 吸附展至整月）：起点一格、长三格。
        Assert.Equal("inset-inline-start:40px;width:120px;top:8px;height:20px", ComponentProbe.Call(gantt, "BarStyle", row));
    }

    [Fact]
    public void BarStyleNeverDrawsABarThinnerThanTwoPixels()
    {
        // 月缩放下一小时的任务也须是读者看得见、抓得住的东西。
        var gantt = NewGantt();
        var start = On(2026, 3, 2);
        var row = Chart(new Job("a", null, "A", start, start.AddHours(1))).Rows[0];

        Assert.Contains("width:2px", (string)ComponentProbe.Call(gantt, "BarStyle", row)!);
    }

    [Fact]
    public void BarStyleFlattensASummaryToItsBracketGlyph()
    {
        var gantt = NewGantt();
        var chart = Chart(
            new Job("top", null, "Top", On(2026, 3, 1), On(2026, 3, 2)),
            new Job("kid", "top", "Kid", On(2026, 3, 2), On(2026, 3, 4)));
        var summary = chart.Rows[0];

        Assert.True(summary.IsSummary);

        // 横梁高 6、双腿共 6，glyph 共 12 → 行内居中 top:12px。
        Assert.Equal("inset-inline-start:40px;width:80px;top:12px;height:6px", ComponentProbe.Call(gantt, "BarStyle", summary));
    }

    [Fact]
    public void MilestoneStyleCentersTheMarkerOnTheStartEdge()
    {
        var gantt = NewGantt();
        var row = Chart(new Job("ship", null, "Ship", On(2026, 3, 2), On(2026, 3, 2))).Rows[0];

        // 记号 16 见方，中心落在起点格 40px 处 → 左缘 32px；行内居中 top:10px。
        Assert.Equal("inset-inline-start:32px;top:10px;width:16px;height:16px", ComponentProbe.Call(gantt, "MilestoneStyle", row));
    }

    [Fact]
    public void LinkPathTakesTheDirectElbowWhenTheTargetSitsRight()
    {
        var gantt = NewGantt();
        var link = new GanttLink(new GanttDependency("a", "b"), 0, 1, 3, 5, GanttAnchor.End, GanttAnchor.Start);

        // M(120,18) → 折线 183 → 垂到下一行 → 进箭头前留 6px：L 194 54。
        Assert.Equal("M 120 18 L 183 18 L 183 54 L 194 54", ComponentProbe.Call(gantt, "LinkPath", link));
    }

    [Fact]
    public void LinkPathLoopsOverTheRowEdgeWhenTheTargetSitsLeft()
    {
        var gantt = NewGantt();
        var link = new GanttLink(new GanttDependency("a", "b"), 0, 1, 5, 3, GanttAnchor.End, GanttAnchor.Start);

        // 目标在源之左：让开横条、越过行边线之前一段（between=32）再折回。
        Assert.Equal("M 200 18 L 211 18 L 211 32 L 103 32 L 103 54 L 114 54", ComponentProbe.Call(gantt, "LinkPath", link));
    }

    [Theory]
    [InlineData(GanttAnchor.Start, "120,18 114,14 114,22")]
    [InlineData(GanttAnchor.End, "120,18 126,14 126,22")]
    public void LinkArrowNosesInFromTheEndItEnters(GanttAnchor anchor, string expected)
    {
        var gantt = NewGantt();
        var link = new GanttLink(new GanttDependency("a", "b"), 0, 0, 3, 3, GanttAnchor.End, anchor);

        Assert.Equal(expected, ComponentProbe.Call(gantt, "LinkArrow", link));
    }

    [Fact]
    public void GridBackgroundRepeatsOneHairlinePerSlot()
    {
        var gantt = NewGantt();

        Assert.Equal(
            "background-image:repeating-linear-gradient(to var(--atom-gantt-flow)," +
            "color-mix(in oklab,var(--border) 70%,transparent) 0 1px,transparent 1px 40px)",
            ComponentProbe.Call(gantt, "GridBackground"));
    }

    [Fact]
    public void SummaryLegStyleStacksBracketAndLeg()
    {
        var gantt = NewGantt();

        // 梁 6 + 腿 6 = 12px，皆导自单枚 --atom-gantt-bar。
        Assert.Equal("height:12px;background-color:var(--atom-gantt-bar)", ComponentProbe.Call(gantt, "SummaryLegStyle"));
    }

    [Fact]
    public void ProgressStyleAndHandleTrackTheDoneShare()
    {
        var row = Chart(new Job("a", null, "A", On(2026, 3, 2), On(2026, 3, 6), 0.25)).Rows[0];

        Assert.Equal("width:25%;background-color:var(--atom-gantt-bar)", CallStatic("ProgressStyle", row));
        Assert.Equal("inset-inline-start:calc(25% - 5px)", CallStatic("ProgressHandleStyle", row));
    }

    [Theory]
    [InlineData(0.25, "25")]
    [InlineData(4.0, "100")]
    [InlineData(-1.0, "0")]
    public void PercentClampsThenFormatsWithoutABlankOrCulture(double value, string expected) =>
        Assert.Equal(expected, CallStatic("Percent", value));

    [Fact]
    public void IndentStyleStepsSixteenPixelsPerDepth()
    {
        var chart = Chart(
            new Job("a", null, "A", On(2026, 3, 2), On(2026, 3, 6)),
            new Job("b", "a", "B", On(2026, 3, 2), On(2026, 3, 3)),
            new Job("c", "b", "C", On(2026, 3, 2), On(2026, 3, 3)));

        Assert.Equal("padding-inline-start:0px", CallStatic("IndentStyle", chart.Rows[0]));
        Assert.Equal("padding-inline-start:16px", CallStatic("IndentStyle", chart.Rows[1]));
        Assert.Equal("padding-inline-start:32px", CallStatic("IndentStyle", chart.Rows[2]));
    }

    [Fact]
    public void ConnectorStyleHangsTheHandlesOutsideTheBar()
    {
        Assert.Equal("inset-inline-start:-14px", CallStatic("ConnectorStyle", true));
        Assert.Equal("inset-inline-end:-14px", CallStatic("ConnectorStyle", false));
    }

    [Fact]
    public void ColumnWidthStyleFixesTheColumnToItsWidth()
    {
        var column = SetProps(new GanttColumn<Job>(), (nameof(GanttColumn<Job>.Title), "Task"), (nameof(GanttColumn<Job>.Width), 180));

        // 无父图表接管，有效宽度即声明宽度。
        Assert.Equal("width:180px", CallStatic("ColumnWidthStyle", column));
    }

    [Theory]
    [InlineData(GanttColumnAlign.Start, "atom:text-start")]
    [InlineData(GanttColumnAlign.Center, "atom:text-center")]
    [InlineData(GanttColumnAlign.End, "atom:text-end")]
    public void AlignClassMirrorsTheColumnAlignment(GanttColumnAlign align, string expected)
    {
        var column = SetProps(new GanttColumn<Job>(), (nameof(GanttColumn<Job>.Align), align));

        Assert.Equal(expected, CallStatic("AlignClass", column));
    }

    [Fact]
    public void BarPaintDerivesEveryPartFromOneColor()
    {
        var gantt = NewGantt();
        var leaf = Chart(new Job("a", null, "A", On(2026, 3, 2), On(2026, 3, 6))).Rows[0];
        var milestone = Chart(new Job("s", null, "S", On(2026, 3, 2), On(2026, 3, 2))).Rows[0];

        Assert.Contains("--atom-gantt-bar:var(--primary)", (string)ComponentProbe.Call(gantt, "BarPaint", leaf)!);
        Assert.Contains("--atom-gantt-bar:var(--foreground)", (string)ComponentProbe.Call(gantt, "BarPaint", milestone)!);

        var custom = NewGantt((nameof(Gantt<Job>.BarColorSelector), (Func<Job, string?>)(_ => "#ff0000")));

        Assert.Contains("--atom-gantt-bar:#ff0000", (string)ComponentProbe.Call(custom, "BarPaint", leaf)!);
    }

    [Fact]
    public void NonWorkingBandsMergeAWeekendIntoASingleRibbon()
    {
        var gantt = NewGantt();
        ComponentProbe.SetField(gantt, "chart", Chart(new Job("a", null, "A", On(2026, 3, 2), On(2026, 3, 20))));

        // 2026 年 3 月周六日界：1 / 7-8 / 14-15 / 21-22 / 28-29，整周末只出一条色带。
        var bands = ((IEnumerable<(double X, double Width)>)ComponentProbe.Call(gantt, "NonWorkingBands")!).ToList();

        Assert.Equal([(0.0, 40.0), (240.0, 80.0), (520.0, 80.0), (800.0, 80.0), (1080.0, 80.0)], bands);
    }

    [Fact]
    public void NonWorkingBandsYieldNothingWhenShadingIsOff()
    {
        var gantt = NewGantt((nameof(Gantt<Job>.ShowNonWorkingDays), false));
        ComponentProbe.SetField(gantt, "chart", Chart(new Job("a", null, "A", On(2026, 3, 2), On(2026, 3, 20))));

        Assert.Empty((IEnumerable<(double X, double Width)>)ComponentProbe.Call(gantt, "NonWorkingBands")!);
    }

    [Fact]
    public void MajorLinesFallBetweenUpperSlotsOnly()
    {
        var gantt = NewGantt();

        // 跨年的月缩放：2025 与 2026 两个年层各占一格，分界只在两格之间；
        // 最右一格之右不再有线。
        var chart = GanttBuilder.Build(new GanttSource<Job>
        {
            Items = [new Job("a", null, "A", On(2025, 12, 1), On(2026, 2, 1))],
            Id = j => j.Id,
            Text = j => j.Text,
            Start = j => j.Start,
            End = j => j.End,
            Zoom = GanttZoom.Month,
            Snap = GanttRangeSnap.None,
        });
        ComponentProbe.SetField(gantt, "chart", chart);

        var lines = ((IEnumerable<double>)ComponentProbe.Call(gantt, "MajorLines")!).ToList();

        Assert.Equal([40.0], lines);
    }
}
