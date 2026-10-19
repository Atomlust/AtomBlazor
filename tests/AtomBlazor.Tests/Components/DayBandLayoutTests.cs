using AtomBlazor.Primitives;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// DayBandLayout 纯函数面测试（自写补足）：跨家族共享的日带布局助手（EventCalendar 与
/// Scheduler 皆消费）——泳道（lane）认领的重叠/复用/增长与参数守卫、横条 calc() 字符串的
/// 缝隙/内缩/接缝/top 计算、泳道垫块高度、接缝圆角表。全为不渲染的纯逻辑，直调内部类型
/// （AtomBlazor.Components 已对本测试程序集开 InternalsVisibleTo）。
/// </summary>
public class DayBandLayoutTests
{
    private const int lanePx = DayBandLayout.LaneHeightPx; // 22
    private const int barHeight = DayBandLayout.BarHeightPx; // 20
    private const int insetPx = DayBandLayout.BarInsetPx; // 3

    // ---- Claim：泳道认领 --------------------------------------------------------------

    [Fact]
    public void 首段应落入零号泳道()
    {
        var occupied = new List<bool[]>();
        var lane = DayBandLayout.Claim(occupied, 7, 0, 3);

        Assert.Equal(0, lane);
        Assert.Single(occupied);
        Assert.True(occupied[0][0] && occupied[0][1] && occupied[0][2]);
    }

    [Fact]
    public void 重叠段应另起新泳道()
    {
        var occupied = new List<bool[]>();
        Assert.Equal(0, DayBandLayout.Claim(occupied, 7, 0, 5));
        // 覆盖列 2..4，与首段相交。
        Assert.Equal(1, DayBandLayout.Claim(occupied, 7, 2, 3));
        Assert.Equal(2, occupied.Count);
    }

    [Fact]
    public void 不相交段应复用同一泳道()
    {
        var occupied = new List<bool[]>();
        Assert.Equal(0, DayBandLayout.Claim(occupied, 7, 0, 2));
        // 列 3..4 与列 0..1 无交集。
        Assert.Equal(0, DayBandLayout.Claim(occupied, 7, 3, 2));
        Assert.Single(occupied);
    }

    [Fact]
    public void 边界相接不算重叠应同泳道()
    {
        var occupied = new List<bool[]>();
        // 列 0..1（半开区间不含列 2）与列 2..3 相接但不交。
        Assert.Equal(0, DayBandLayout.Claim(occupied, 7, 0, 2));
        Assert.Equal(0, DayBandLayout.Claim(occupied, 7, 2, 2));
    }

    [Theory]
    [InlineData(0, 1)] // columns 须为正
    [InlineData(-1, 1)]
    public void 非正列数应抛越界(int columns, int span)
    {
        var occupied = new List<bool[]>();
        Assert.Throws<ArgumentOutOfRangeException>(() => DayBandLayout.Claim(occupied, columns, 0, span));
    }

    [Fact]
    public void 空占用表应抛参数空() =>
        Assert.Throws<ArgumentNullException>(() => DayBandLayout.Claim(null!, 7, 0, 1));

    [Theory]
    [InlineData(-1, 1)] // 负起始列
    [InlineData(0, 0)] // 非正跨度
    [InlineData(5, 3)] // 起始列 + 跨度越出列数
    public void 越界段参数应抛(int startColumn, int span)
    {
        var occupied = new List<bool[]>();
        Assert.Throws<ArgumentOutOfRangeException>(() => DayBandLayout.Claim(occupied, 7, startColumn, span));
    }

    // ---- BarStyle：calc() 串 ---------------------------------------------------------

    [Fact]
    public void 单日首末段应两端内缩()
    {
        var style = DayBandLayout.BarStyle(0, 1, 7, 0, 34, continuesBefore: false, continuesAfter: false);
        // left = 0 + 3，width = 1 - 1 - 3 - 3 = -6。
        Assert.Equal(
            $"inset-inline-start:calc((100% - 6px) * 0 / 7 + {insetPx}px);width:calc((100% - 6px) * 1 / 7 - 6px);top:34px;height:{barHeight}px;",
            style);
    }

    [Fact]
    public void 接前段应取消首端内缩()
    {
        var style = DayBandLayout.BarStyle(0, 2, 7, 0, 34, continuesBefore: true, continuesAfter: false);
        // continuesBefore → startInset 0：left = + 0px，width = 2 - 1 - 0 - 3 = -2。
        Assert.Contains("inset-inline-start:calc((100% - 6px) * 0 / 7 + 0px)", style, StringComparison.Ordinal);
        Assert.Contains("width:calc((100% - 6px) * 2 / 7 - 2px)", style, StringComparison.Ordinal);
    }

    [Fact]
    public void 接后段应取消末端内缩()
    {
        var style = DayBandLayout.BarStyle(5, 2, 7, 0, 6, continuesBefore: true, continuesAfter: true);
        // 中段两端皆不内缩：left = + 5px，width = 2 - 1 - 0 - 0 = + 1px。
        Assert.Contains("inset-inline-start:calc((100% - 6px) * 5 / 7 + 5px)", style, StringComparison.Ordinal);
        Assert.Contains("width:calc((100% - 6px) * 2 / 7 + 1px)", style, StringComparison.Ordinal);
    }

    [Fact]
    public void 泳道应按间距抬高顶边()
    {
        var lane1 = DayBandLayout.BarStyle(0, 1, 7, 1, 34, continuesBefore: false, continuesAfter: false);
        var lane2 = DayBandLayout.BarStyle(0, 1, 7, 2, 34, continuesBefore: false, continuesAfter: false);
        Assert.Contains($";top:{34 + lanePx}px;", lane1, StringComparison.Ordinal);
        Assert.Contains($";top:{34 + (2 * lanePx)}px;", lane2, StringComparison.Ordinal);
    }

    [Fact]
    public void 非七列应按列数与缝隙改写()
    {
        // 五列日行（Scheduler 侧）：缝隙 = 4，除数 = 5。
        var style = DayBandLayout.BarStyle(1, 2, 5, 0, 0, continuesBefore: false, continuesAfter: false);
        Assert.Contains("(100% - 4px) * 1 / 5", style, StringComparison.Ordinal);
        Assert.Contains("(100% - 4px) * 2 / 5", style, StringComparison.Ordinal);
    }

    // ---- LaneSpacerStyle / JoinRounding ----------------------------------------------

    [Theory]
    [InlineData(0, "height:0px;")]
    [InlineData(2, "height:44px;")]
    [InlineData(3, "height:66px;")]
    public void 垫块高度应为泳道数乘间距(int lanes, string expected) =>
        Assert.Equal(expected, DayBandLayout.LaneSpacerStyle(lanes));

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(true, true, "atom:rounded-none")]
    [InlineData(true, false, "atom:rounded-s-none")]
    [InlineData(false, true, "atom:rounded-e-none")]
    public void 圆角表应按接缝端给出圆角类(bool before, bool after, string? expected) =>
        Assert.Equal(expected, DayBandLayout.JoinRounding(before, after));
}
