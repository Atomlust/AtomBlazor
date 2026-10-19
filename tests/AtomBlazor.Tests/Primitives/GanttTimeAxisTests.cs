using AtomBlazor.Primitives;
using System.Globalization;
using AtomBlazor.Primitives.Gantt;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// 自写补全波：GanttTimeAxis 的刻度与缩放纯逻辑（不渲染、不借件）。
/// 覆盖两层表头的搭建、诸缩放下槽的切分、吸附档位、Position/At 互逆与钳制、
/// 非工作日置灰的适用范围、偏移不变性与自定义措辞。
/// </summary>
public class GanttTimeAxisTests
{
    private static readonly GanttLabels inv = new() { Culture = CultureInfo.InvariantCulture };

    private static DateTimeOffset On(int year, int month, int day, int hour = 0) =>
        new(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);

    [Fact]
    public void DayZoomLaysOneSlotPerDayWithDayNumberLabels()
    {
        var axis = GanttTimeAxis.Build(On(2026, 3, 2), On(2026, 3, 5), GanttZoom.Day, GanttRangeSnap.None, null, inv);

        Assert.Equal(3, axis.SlotCount);
        Assert.Equal(["2", "3", "4"], axis.Minor.Select(s => s.Label));
        Assert.Equal(On(2026, 3, 2), axis.Start);
        Assert.Equal(On(2026, 3, 5), axis.End);

        // 槽与槽无缝相接：前一槽之收束即后一槽之开端。
        for (var i = 1; i < axis.Minor.Count; i++)
        {
            Assert.Equal(axis.Minor[i - 1].End, axis.Minor[i].Start);
        }
    }

    [Fact]
    public void SnapMajorWidensDayZoomToWholeMonths()
    {
        var axis = GanttTimeAxis.Build(On(2026, 3, 2), On(2026, 3, 20), GanttZoom.Day, GanttRangeSnap.Major, null, inv);

        Assert.Equal(On(2026, 3, 1), axis.Start);
        Assert.Equal(On(2026, 4, 1), axis.End);
        Assert.Equal(31, axis.SlotCount);
    }

    [Fact]
    public void SnapMinorKeepsWholeDaysRatherThanWholeMonths()
    {
        var axis = GanttTimeAxis.Build(
            new DateTimeOffset(new DateTime(2026, 3, 2, 10, 0, 0), TimeSpan.Zero),
            new DateTimeOffset(new DateTime(2026, 3, 6, 14, 0, 0), TimeSpan.Zero),
            GanttZoom.Day, GanttRangeSnap.Minor, null, inv);

        Assert.Equal(On(2026, 3, 2), axis.Start);
        Assert.Equal(On(2026, 3, 7), axis.End);
        Assert.Equal(5, axis.SlotCount);
    }

    [Fact]
    public void EvenSnapNoneStillCoversWholeMinorSlots()
    {
        // 裁切末槽会让其间每个位置读数偏短，故 Snap.None 也只是不向外
        // 延展，槽本身仍须完整。
        var axis = GanttTimeAxis.Build(
            new DateTimeOffset(new DateTime(2026, 3, 2, 10, 0, 0), TimeSpan.Zero),
            new DateTimeOffset(new DateTime(2026, 3, 4, 5, 0, 0), TimeSpan.Zero),
            GanttZoom.Day, GanttRangeSnap.None, null, inv);

        Assert.Equal(On(2026, 3, 2), axis.Start);
        Assert.Equal(On(2026, 3, 5), axis.End);
        Assert.Equal(3, axis.SlotCount);
    }

    [Fact]
    public void HourZoomLaysTwentyFourSlotsADayUnderDayHeaders()
    {
        var axis = GanttTimeAxis.Build(On(2026, 3, 2), On(2026, 3, 4), GanttZoom.Hour, GanttRangeSnap.None, null, inv);

        Assert.Equal(48, axis.SlotCount);
        Assert.Equal("00", axis.Minor[0].Label);
        Assert.Equal("23", axis.Minor[23].Label);
        Assert.Equal(["2", "3"], axis.Major.Select(s => s.Label));
        Assert.Equal([24, 24], axis.Major.Select(s => s.Span));
    }

    [Fact]
    public void WeekZoomNumbersEachSlotByTheIsoWeekOfItsMiddleDay()
    {
        // 2026-03-01 是周日，其 ISO 周实为二月之尾；以周三读数便是 W10。
        var axis = GanttTimeAxis.Build(On(2026, 3, 1), On(2026, 3, 29), GanttZoom.Week, GanttRangeSnap.None, null, inv);

        Assert.Equal(4, axis.SlotCount);
        Assert.Equal(["W10", "W11", "W12", "W13"], axis.Minor.Select(s => s.Label));
    }

    [Fact]
    public void MonthZoomAbbreviatesMonthNames()
    {
        var axis = GanttTimeAxis.Build(On(2026, 3, 1), On(2026, 5, 1), GanttZoom.Month, GanttRangeSnap.None, null, inv);

        Assert.Equal(["Mar", "Apr"], axis.Minor.Select(s => s.Label));
        Assert.Equal("2026", axis.Major.Single().Label);
        Assert.Equal(2, axis.Major.Single().Span);
    }

    [Fact]
    public void QuarterZoomGroupsUnderYears()
    {
        var axis = GanttTimeAxis.Build(On(2025, 11, 1), On(2026, 2, 1), GanttZoom.Quarter, GanttRangeSnap.None, null, inv);

        Assert.Equal(["Q4", "Q1"], axis.Minor.Select(s => s.Label));
        Assert.Equal(["2025", "2026"], axis.Major.Select(s => s.Label));
    }

    [Fact]
    public void YearZoomHasNoUpperTier()
    {
        var axis = GanttTimeAxis.Build(On(2024, 1, 1), On(2026, 1, 1), GanttZoom.Year, GanttRangeSnap.None, null, inv);

        Assert.Equal(["2024", "2025"], axis.Minor.Select(s => s.Label));
        Assert.Empty(axis.Major);
    }

    [Fact]
    public void PositionAndAtAreMutualInverses()
    {
        var axis = GanttTimeAxis.Build(On(2026, 3, 1), On(2026, 4, 1), GanttZoom.Day, GanttRangeSnap.None, null, inv);

        for (var slots = 0.0; slots <= axis.SlotCount; slots += 0.25)
        {
            Assert.Equal(slots, axis.Position(axis.At(slots)), 6);
        }
    }

    [Fact]
    public void AtClampsToTheRangeEdges()
    {
        var axis = GanttTimeAxis.Build(On(2026, 3, 1), On(2026, 4, 1), GanttZoom.Day, GanttRangeSnap.None, null, inv);

        Assert.Equal(axis.Start, axis.At(-1));
        Assert.Equal(axis.End, axis.At(axis.SlotCount));
        Assert.Equal(axis.End, axis.At(axis.SlotCount + 3));
    }

    [Fact]
    public void APositionReadsItsInstantOffsetAgnostically()
    {
        // 整轴同处起点偏移；同一瞬间换一种偏移写法，位置读数纹丝不动。
        var eastFive = TimeSpan.FromHours(5);
        var axis = GanttTimeAxis.Build(new DateTimeOffset(new DateTime(2026, 3, 1), eastFive), new DateTimeOffset(new DateTime(2026, 3, 2), eastFive), GanttZoom.Day, GanttRangeSnap.None, null, inv);

        Assert.Equal(eastFive, axis.Minor[0].Start.Offset);
        Assert.Equal(0.25, axis.Position(new DateTimeOffset(new DateTime(2026, 3, 1, 3, 0, 0), TimeSpan.FromHours(2))), 6);
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 3, 1, 6, 0, 0), eastFive), axis.At(0.25));
    }

    [Fact]
    public void CustomNonWorkingDaysReplaceTheDefaultWeekend()
    {
        var axis = GanttTimeAxis.Build(On(2026, 3, 2), On(2026, 3, 9), GanttZoom.Day, GanttRangeSnap.None, [DayOfWeek.Wednesday], inv);

        // 2026-03-04 恰是周三；周六周日此番不灰。
        Assert.Equal([4], axis.Minor.Where(s => s.IsNonWorking).Select(s => s.Start.Day));
    }

    [Fact]
    public void HourZoomShadesTheWeekendHourByHour()
    {
        var axis = GanttTimeAxis.Build(On(2026, 3, 7), On(2026, 3, 9), GanttZoom.Hour, GanttRangeSnap.None, null, inv);

        Assert.Equal(48, axis.SlotCount);
        Assert.All(axis.Minor, s => Assert.True(s.IsNonWorking));
    }

    [Theory]
    [InlineData(GanttZoom.Week)]
    [InlineData(GanttZoom.Month)]
    [InlineData(GanttZoom.Quarter)]
    [InlineData(GanttZoom.Year)]
    public void NoSlotLongerThanADayIsEverShaded(GanttZoom zoom)
    {
        var axis = GanttTimeAxis.Build(On(2026, 2, 1), On(2026, 5, 1), zoom, GanttRangeSnap.None, null, inv);

        Assert.DoesNotContain(axis.Minor, s => s.IsNonWorking);
    }

    [Fact]
    public void AReversedRangeStillYieldsASingleSlot()
    {
        var axis = GanttTimeAxis.Build(On(2026, 3, 5), On(2026, 3, 1), GanttZoom.Day, GanttRangeSnap.None, null, inv);

        Assert.Single(axis.Minor);
        Assert.Equal("5", axis.Minor[0].Label);
    }

    [Fact]
    public void UnequalMonthsStillWeighOneSlotEach()
    {
        // 二月短三日，月缩放下仍与三月等价——位置以槽量度而非以日。
        var axis = GanttTimeAxis.Build(On(2026, 2, 1), On(2026, 4, 1), GanttZoom.Month, GanttRangeSnap.None, null, inv);

        Assert.Equal(2, axis.SlotCount);
        Assert.Equal(1, axis.Position(On(2026, 3, 1)) - axis.Position(On(2026, 2, 1)), 6);
        Assert.Equal(1, axis.Position(On(2026, 4, 1)) - axis.Position(On(2026, 3, 1)), 6);
    }

    [Fact]
    public void CustomWeekAndQuarterFormatsReachTheHeaders()
    {
        var labels = new GanttLabels
        {
            Culture = CultureInfo.InvariantCulture,
            WeekFormat = "第{0}周",
            QuarterFormat = "T{0}",
        };

        var week = GanttTimeAxis.Build(On(2026, 3, 1), On(2026, 3, 8), GanttZoom.Week, GanttRangeSnap.None, null, labels);
        var quarter = GanttTimeAxis.Build(On(2026, 1, 1), On(2026, 7, 1), GanttZoom.Quarter, GanttRangeSnap.None, null, labels);

        Assert.Equal("第10周", week.Minor[0].Label);
        Assert.Equal(["T1", "T2"], quarter.Minor.Select(s => s.Label));
    }
}
