using System.Globalization;
using System.Text;
using AtomBlazor.Primitives.Gantt;

namespace AtomBlazor;

/// <summary>
/// 一切落于屏幕何处。由 C# 依列宽与槽宽算出，故无须在浏览器中
/// 测量任何东西，且这些数字在 Server、WebAssembly
/// 与打印中同然成立。
/// </summary>
public partial class Gantt<TItem>
{
    private const int linkStub = 11;
    private const int arrowLength = 6;
    private const int arrowHalfWidth = 4;

    /// <summary>获取时间轴绘制宽度（像素）。</summary>
    private int TimelineWidth => (chart?.Axis.SlotCount ?? 0) * EffectiveSlotWidth;

    /// <summary>获取各行合计高度（像素）。</summary>
    private int BodyHeight => (chart?.Rows.Count ?? 0) * RowHeight;

    /// <summary>获取表头的层数。</summary>
    private int HeaderTiers => chart is not null && chart.Axis.Major.Count > 0 ? 2 : 1;

    /// <summary>获取表头整体高度（像素）。</summary>
    private int HeaderHeight => HeaderTiers * HeaderRowHeight;

    /// <summary>获取横条绘制高度（像素）。</summary>
    private int BarHeight => Math.Max(10, RowHeight - 16);

    /// <summary>获取汇总项支架横梁的高度（像素）。</summary>
    private int SummaryHeight => Math.Max(4, BarHeight / 3);

    /// <summary>获取汇总项双腿垂于横梁之下的长度（像素）。</summary>
    private int SummaryLeg => Math.Max(4, BarHeight / 3);

    /// <summary>获取汇总项支架两端腿部的样式。</summary>
    /// <remarks>
    /// 正是双腿让汇总项读起来是一段跨度而非一项任务。没了它们，
    /// 稍细一点的横条是无人留意的差别——事实恰恰如此。
    /// </remarks>
    private string SummaryLegStyle() =>
        $"height:{Px(SummaryHeight + SummaryLeg)}px;background-color:var(--atom-gantt-bar)";

    /// <summary>获取里程碑记号绘制宽度（像素）。</summary>
    private int MilestoneSize => Math.Max(10, RowHeight - 20);

    private static string Px(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// 生成重复背景，在每个槽界处画一条细线。
    /// </summary>
    /// <remarks>
    /// 用渐变而非每槽一元素：季度范围内的小时缩放有成千上万个分界，
    /// 而千百个空 div 只会造出一页滚动不畅而毫无收益的
    /// 版面。各槽一律同宽，这正是此法得以成立的全部理由。
    /// </remarks>
    private string GridBackground()
    {
        // 自阅读起始侧量起，故在从右至左的页面中线条同样落在槽界
        // 上。时间轴的宽度反正都是整数个槽。
        var width = EffectiveSlotWidth;
        return "background-image:repeating-linear-gradient(to var(--atom-gantt-flow)," +
               $"color-mix(in oklab,var(--border) 70%,transparent) 0 1px,transparent 1px {width}px)";
    }

    /// <summary>获取为整表定宽的样式。</summary>
    private string TableStyle() =>
        $"width:{Px(TreeWidth + TimelineWidth)}px;table-layout:fixed";

    /// <summary>获取为任务列表一列定宽的样式。</summary>
    /// <param name="column">该列。</param>
    /// <returns>内联样式。</returns>
    private static string ColumnWidthStyle(GanttColumn<TItem> column) =>
        $"width:{Px(column.EffectiveWidth)}px";

    /// <summary>获取为时间轴单槽定宽的样式。</summary>
    private string SlotWidthStyle() => $"width:{Px(EffectiveSlotWidth)}px";

    /// <summary>获取固定任务列表表头并设定其高度的样式。</summary>
    /// <param name="offset">列起点距任务列表边缘的距离（像素）。</param>
    /// <returns>内联样式。</returns>
    private string HeaderCellStyle(int offset) =>
        $"inset-inline-start:{Px(offset)}px;height:{Px(HeaderHeight)}px";

    /// <summary>获取设定时间轴表头单层高度的样式。</summary>
    private string TierStyle() => $"height:{Px(HeaderRowHeight)}px";

    /// <summary>获取时间轴表头兼代两层时所用的样式。</summary>
    private string SingleTierStyle() => $"height:{Px(HeaderHeight)}px";

    /// <summary>获取将下层固定于上层之下的样式。</summary>
    private string LowerTierStyle() =>
        $"top:{Px(HeaderRowHeight)}px;height:{Px(HeaderRowHeight)}px";

    /// <summary>
    /// 获取样式：主层表头所属单元滚过之后仍令其可读。
    /// </summary>
    /// <remarks>
    /// 一个月可以比窗口还宽。没有它，其名称便落在无人可见的
    /// 左缘，上层读来就成了一条空白带。
    /// </remarks>
    private string MajorLabelStyle() => $"inset-inline-start:{Px(TreeWidth + 8)}px";

    /// <summary>获取为整张图表所居画盒定宽的样式。</summary>
    private string ContentStyle() => $"width:{Px(TreeWidth + TimelineWidth)}px";

    /// <summary>
    /// 获取一切依时间轴绘制之物所在的单一图层的样式。
    /// </summary>
    /// <remarks>
    /// 无论页面方向如何一律从左至右：横条依轴起点布放，
    /// 箭头则画在一个不会镜像的 SVG 坐标系中。
    /// </remarks>
    private string DrawingLayerStyle() =>
        $"inset-inline-start:{Px(TreeWidth)}px;top:{Px(HeaderHeight)}px;width:{Px(TimelineWidth)}px;" +
        $"height:{Px(BodyHeight)}px;{GridBackground()}";

    /// <summary>获取放置一条非工作日色带的样式。</summary>
    /// <param name="x">色带左缘（像素）。</param>
    /// <param name="width">色带宽度（像素）。</param>
    /// <returns>内联样式。</returns>
    private static string BandStyle(double x, double width) =>
        $"inset-inline-start:{Px(x)}px;width:{Px(width)}px";

    /// <summary>获取放置一条竖线的样式。</summary>
    /// <param name="x">线条位置（像素）。</param>
    /// <returns>内联样式。</returns>
    private static string LineStyle(double x) => $"inset-inline-start:{Px(x)}px";

    /// <summary>获取设定行高的样式。</summary>
    private string RowStyle() => $"height:{Px(RowHeight)}px";

    /// <summary>获取固定任务列表单元并设定其高度的样式。</summary>
    /// <param name="offset">列起点距任务列表边缘的距离（像素）。</param>
    /// <returns>内联样式。</returns>
    private string BodyCellStyle(int offset) =>
        $"inset-inline-start:{Px(offset)}px;height:{Px(RowHeight)}px";

    /// <summary>获取按任务在树中的深度为其缩进的样式。</summary>
    /// <param name="row">该行。</param>
    /// <returns>内联样式。</returns>
    private static string IndentStyle(GanttRow<TItem> row) =>
        $"padding-inline-start:{Px(row.Depth * 16)}px";

    /// <summary>获取为横条已完成部分定宽上色的样式。</summary>
    /// <param name="row">该行。</param>
    /// <returns>内联样式。</returns>
    private static string ProgressStyle(GanttRow<TItem> row) =>
        $"width:{Percent(row.Progress)}%;background-color:var(--atom-gantt-bar)";

    /// <summary>
    /// 把非工作槽并入尽可能少的几条色带。
    /// </summary>
    /// <returns>各条色带的左缘与宽度（像素）。</returns>
    private IEnumerable<(double X, double Width)> NonWorkingBands()
    {
        if (chart is null || !ShowNonWorkingDays)
        {
            yield break;
        }

        var slots = chart.Axis.Minor;
        var width = EffectiveSlotWidth;
        var index = 0;

        while (index < slots.Count)
        {
            if (!slots[index].IsNonWorking)
            {
                index++;
                continue;
            }

            // 整个周末是一条色带而非两条：元素更少，且周六与周日之间
            // 不会有两片半透明填充交叠出的发丝线。
            var run = 0;
            while (index + run < slots.Count && slots[index + run].IsNonWorking)
            {
                run++;
            }

            yield return (index * width, run * width);
            index += run;
        }
    }

    /// <summary>
    /// 找出上层各槽之间的分界。
    /// </summary>
    /// <returns>除首条外各分界的左缘（像素）。</returns>
    private IEnumerable<double> MajorLines()
    {
        if (chart is null)
        {
            yield break;
        }

        var width = EffectiveSlotWidth;
        var covered = 0;

        foreach (var slot in chart.Axis.Major)
        {
            covered += slot.Span;
            if (covered < chart.Axis.SlotCount)
            {
                yield return covered * width;
            }
        }
    }

    /// <summary>
    /// 找出当前时刻落在何处。
    /// </summary>
    /// <returns>位置（像素）；若此时在图表范围之外则为 null。</returns>
    private double? TodayLine()
    {
        if (chart is null || !ShowToday)
        {
            return null;
        }

        var now = DateTimeOffset.Now;
        if (now < chart.Axis.Start || now > chart.Axis.End)
        {
            return null;
        }

        return chart.Axis.Position(now) * EffectiveSlotWidth;
    }

    /// <summary>
    /// 算出给定高度之物在行中的落点，其度量自绘制层
    /// 顶部起算。
    /// </summary>
    /// <param name="row">该行。</param>
    /// <param name="height">该物的高度（像素）。</param>
    /// <returns>距图层顶部的偏移（像素）。</returns>
    private double Top(GanttRow<TItem> row, int height) =>
        (row.Index * RowHeight) + ((RowHeight - height) / 2.0);

    /// <summary>获取放置并为任务横条定宽的样式。</summary>
    /// <param name="row">该行。</param>
    /// <returns>内联样式。</returns>
    private string BarStyle(GanttRow<TItem> row)
    {
        var width = EffectiveSlotWidth;
        var height = row.IsSummary ? SummaryHeight : BarHeight;
        var glyph = row.IsSummary ? SummaryHeight + SummaryLeg : BarHeight;
        var left = row.OffsetSlots * width;

        // 绝不低于两个像素：月缩放下一小时长的任务，也总得是读者
        // 看得见、抓得住的东西。
        var length = Math.Max(row.LengthSlots * width, 2);

        return $"inset-inline-start:{Px(left)}px;width:{Px(length)}px;top:{Px(Top(row, glyph))}px;height:{height}px";
    }

    /// <summary>获取放置里程碑记号的样式。</summary>
    /// <param name="row">该行。</param>
    /// <returns>内联样式。</returns>
    private string MilestoneStyle(GanttRow<TItem> row)
    {
        var size = MilestoneSize;
        var left = (row.OffsetSlots * EffectiveSlotWidth) - (size / 2.0);

        return $"inset-inline-start:{Px(left)}px;top:{Px(Top(row, size))}px;width:{size}px;height:{size}px";
    }

    /// <summary>获取放置横条之旁所书名称的样式。</summary>
    /// <param name="row">该行。</param>
    /// <returns>内联样式。</returns>
    private string LabelStyle(GanttRow<TItem> row)
    {
        var width = EffectiveSlotWidth;
        var end = row.IsMilestone
            ? (row.OffsetSlots * width) + (MilestoneSize / 2.0)
            : (row.OffsetSlots + row.LengthSlots) * width;

        return $"inset-inline-start:{Px(end + 8)}px;top:{Px(row.Index * RowHeight)}px;height:{Px(RowHeight)}px";
    }

    /// <summary>获取放置任务进度拖拽握柄的样式。</summary>
    /// <param name="row">该行。</param>
    /// <returns>内联样式。</returns>
    /// <remarks>
    /// 以逻辑偏移放置且不加 transform，故在从右至左的页面中它照样
    /// 落在填充边缘，无须另算平移的正负号。
    /// </remarks>
    private static string ProgressHandleStyle(GanttRow<TItem> row) =>
        $"inset-inline-start:calc({Percent(row.Progress)}% - 5px)";

    /// <summary>获取把连接点置于横条一端外侧的样式。</summary>
    /// <param name="start">是否为起始端。</param>
    /// <returns>内联样式。</returns>
    private static string ConnectorStyle(bool start) =>
        start ? "inset-inline-start:-14px" : "inset-inline-end:-14px";

    /// <summary>
    /// 为一条依赖箭头走线。
    /// </summary>
    /// <param name="link">该连接。</param>
    /// <returns>路径的 <c>d</c> 属性。</returns>
    /// <remarks>
    /// 两端彼此相对时为三段，不相对时为五段——先于其前驱结束之前
    /// 就开始的后继，只能绕行其行而及，
    /// 不能径直折回穿过横条。
    /// </remarks>
    private string LinkPath(GanttLink link)
    {
        var width = EffectiveSlotWidth;
        var x1 = link.FromSlots * width;
        var x2 = link.ToSlots * width;
        var y1 = (link.FromRow * RowHeight) + (RowHeight / 2.0);
        var y2 = (link.ToRow * RowHeight) + (RowHeight / 2.0);

        var leaving = link.FromAnchor == GanttAnchor.End ? 1 : -1;
        var arriving = link.ToAnchor == GanttAnchor.Start ? 1 : -1;
        var out1 = x1 + (leaving * linkStub);
        var approach = x2 - (arriving * arrowLength);
        var turn = approach - (arriving * linkStub);

        var path = new StringBuilder();
        path.Append(CultureInfo.InvariantCulture, $"M {Px(x1)} {Px(y1)}");

        var direct = leaving > 0 ? turn >= out1 : turn <= out1;
        if (direct)
        {
            path.Append(CultureInfo.InvariantCulture, $" L {Px(turn)} {Px(y1)}");
            path.Append(CultureInfo.InvariantCulture, $" L {Px(turn)} {Px(y2)}");
        }
        else
        {
            // 让开源横条、又止于本行边线之前：贴着边线走线会把那段
            // 长横线正好画在边线之上，箭头看来便哪也没
            // 去。
            var between = y1 + ((y2 > y1 ? 1 : -1) * ((BarHeight / 2.0) + 4));
            path.Append(CultureInfo.InvariantCulture, $" L {Px(out1)} {Px(y1)}");
            path.Append(CultureInfo.InvariantCulture, $" L {Px(out1)} {Px(between)}");
            path.Append(CultureInfo.InvariantCulture, $" L {Px(turn)} {Px(between)}");
            path.Append(CultureInfo.InvariantCulture, $" L {Px(turn)} {Px(y2)}");
        }

        path.Append(CultureInfo.InvariantCulture, $" L {Px(approach)} {Px(y2)}");
        return path.ToString();
    }

    /// <summary>
    /// 生成一条依赖末端的箭头。
    /// </summary>
    /// <param name="link">该连接。</param>
    /// <returns>多边形的 <c>points</c> 属性。</returns>
    private string LinkArrow(GanttLink link)
    {
        var x2 = link.ToSlots * EffectiveSlotWidth;
        var y2 = (link.ToRow * RowHeight) + (RowHeight / 2.0);
        var arriving = link.ToAnchor == GanttAnchor.Start ? 1 : -1;
        var back = x2 - (arriving * arrowLength);

        return $"{Px(x2)},{Px(y2)} {Px(back)},{Px(y2 - arrowHalfWidth)} {Px(back)},{Px(y2 + arrowHalfWidth)}";
    }

    /// <summary>获取根元素上的类。</summary>
    private string RootClass => ClassNames.cn("atom:flex atom:w-full atom:flex-col atom:gap-3", Class);

    /// <summary>获取把单元内容放到其列右侧的类。</summary>
    /// <param name="column">该列。</param>
    /// <returns>类名。</returns>
    private static string AlignClass(GanttColumn<TItem> column) => column.Align switch
    {
        GanttColumnAlign.Center => "atom:text-center",
        GanttColumnAlign.End => "atom:text-end",
        _ => "atom:text-start",
    };

    /// <summary>
    /// 获取横条的上色：一个其各部分共读的自定义属性。
    /// </summary>
    /// <param name="row">该行。</param>
    /// <returns>样式片段。</returns>
    /// <remarks>
    /// 颜色一经声明，轨道、已完成部分与汇总项的端笔皆由其导出。只给
    /// 轨道上色，则全部完工的任务反而不露分毫本色，因为
    /// 已完成部分覆住了整根横条。
    /// </remarks>
    private string BarPaint(GanttRow<TItem> row)
    {
        var color = BarColorSelector?.Invoke(row.Item) is { Length: > 0 } custom
            ? custom
            : row.IsSummary || row.IsMilestone ? "var(--foreground)" : "var(--primary)";

        return $";--atom-gantt-bar:{color};background-color:color-mix(in oklab,var(--atom-gantt-bar) 28%,transparent)";
    }

    /// <summary>获取里程碑记号的上色：它是实心体而非轨道。</summary>
    /// <param name="row">该行。</param>
    /// <returns>样式片段。</returns>
    private string MilestonePaint(GanttRow<TItem> row)
    {
        var color = BarColorSelector?.Invoke(row.Item) is { Length: > 0 } custom ? custom : "var(--foreground)";

        return $";background-color:{color}";
    }

    /// <summary>获取描边：未开工的横条衬着网格仍可见。</summary>
    private static string BarOutline() =>
        "box-shadow:inset 0 0 0 1px color-mix(in oklab,var(--atom-gantt-bar) 55%,transparent)";

    /// <summary>把比例化为宽度百分比。</summary>
    /// <param name="value">比例，取 0 至 1。</param>
    /// <returns>百分数。</returns>
    private static string Percent(double value) =>
        (Math.Clamp(value, 0, 1) * 100).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// 获取放置横条悬停卡片的样式。
    /// </summary>
    /// <param name="row">该行。</param>
    /// <returns>内联样式。</returns>
    /// <remarks>
    /// 在图表上半部卡片垂于横条之下、下半部则浮于其上，令卡片
    /// 永远朝行内展开，而不会顶穿表头或底边。邻近时间轴
    /// 末端的横条把卡片挂在自己端头，否则卡片会撑宽滚动区。
    /// </remarks>
    private string TooltipStyle(GanttRow<TItem> row)
    {
        var rows = chart?.Rows.Count ?? 1;
        var vertical = row.Index < rows / 2 ? "top:calc(100% + 6px)" : "bottom:calc(100% + 6px)";
        var nearEnd = (row.OffsetSlots * EffectiveSlotWidth) > TimelineWidth - 260;

        return $"{vertical};{(nearEnd ? "inset-inline-end:0" : "inset-inline-start:0")}";
    }

    /// <summary>获取横条悬停卡片的日期行。</summary>
    /// <param name="row">该行。</param>
    /// <returns>该行文字。</returns>
    private string TooltipRange(GanttRow<TItem> row)
    {
        var culture = CultureInfo.CurrentCulture;
        var pattern = Zoom == GanttZoom.Hour ? "g" : "d MMM yyyy";

        return row.IsMilestone
            ? row.Start.ToString(pattern, culture)
            : string.Format(culture, Localizer["Gantt.TooltipRange"], row.Start.ToString(pattern, culture), row.End.ToString(pattern, culture));
    }

    /// <summary>获取横条悬停卡片的时长行。</summary>
    /// <param name="row">该行。</param>
    /// <returns>该行文字；里程碑无时长，故为 null。</returns>
    private string? TooltipLength(GanttRow<TItem> row)
    {
        if (row.IsMilestone)
        {
            return null;
        }

        var culture = CultureInfo.CurrentCulture;
        var span = row.End - row.Start;

        // 不足一天时，按天读数成 "0" 等于什么都没说。
        return span.TotalDays >= 1
            ? string.Format(culture, Localizer["Gantt.TooltipDays"], span.TotalDays.ToString("0.#", culture))
            : string.Format(culture, Localizer["Gantt.TooltipHours"], span.TotalHours.ToString("0.#", culture));
    }

    /// <summary>
    /// 向看不见箭头的读者道明一条依赖为何。
    /// </summary>
    /// <param name="link">该连接。</param>
    /// <returns>那句话。</returns>
    private string LinkLabel(GanttLink link)
    {
        var key = link.Dependency.Type switch
        {
            GanttDependencyType.StartToStart => "Gantt.StartToStart",
            GanttDependencyType.FinishToFinish => "Gantt.FinishToFinish",
            GanttDependencyType.StartToFinish => "Gantt.StartToFinish",
            _ => "Gantt.FinishToStart",
        };

        var from = chart?.Rows.ElementAtOrDefault(link.FromRow)?.Text ?? link.Dependency.FromId;
        var to = chart?.Rows.ElementAtOrDefault(link.ToRow)?.Text ?? link.Dependency.ToId;

        return string.Format(CultureInfo.CurrentCulture, Localizer[key], from, to);
    }

    /// <summary>
    /// 向看不见横条的读者道明它覆盖了什么。
    /// </summary>
    /// <param name="row">该行。</param>
    /// <returns>那句话。</returns>
    private string BarLabel(GanttRow<TItem> row)
    {
        var culture = CultureInfo.CurrentCulture;
        var pattern = Zoom == GanttZoom.Hour ? "g" : "d";

        if (row.IsMilestone)
        {
            return string.Format(culture, Localizer["Gantt.MilestoneLabel"], row.Text, row.Start.ToString(pattern, culture));
        }

        return string.Format(
            culture,
            Localizer["Gantt.BarLabel"],
            row.Text,
            row.Start.ToString(pattern, culture),
            row.End.ToString(pattern, culture),
            (row.Progress * 100).ToString("0", culture));
    }
}
