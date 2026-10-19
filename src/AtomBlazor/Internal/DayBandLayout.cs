using System.Globalization;

namespace AtomBlazor;

/// <summary>
/// 绘制跨日事件横条中与类型无关的一半：把各段装入互不重叠的泳道（lane），
/// 以及将单段铺于 N 列日网格之上。
/// </summary>
/// <remarks>
/// <para>
/// 由 <see cref="EventCalendar{TEvent}"/> 与 <c>Scheduler</c> 共用。此处只收录不
/// 依赖事件类型的部分。各组件保留自身的分段排序规则与自身的横条记录，
/// 因为它们需要事件本身：<c>EventCalendar</c> 读取使用者提供的
/// <c>EventStart</c> / <c>EventEnd</c> 委托，调度器则基于已实体化的
/// <c>SchedulerOccurrence</c> 工作。对此强行泛化只会把一方的访问器
/// 拖进另一方，得不偿失。
/// </para>
/// </remarks>
internal static class DayBandLayout
{
    /// <summary>泳道（lane）间距，单位像素——一条横条加其下间隙。</summary>
    internal const int LaneHeightPx = 22;

    /// <summary>横条高度，单位像素。</summary>
    internal const int BarHeightPx = 20;

    /// <summary>
    /// 横条哪一端是真正的起始或终结、而非接入相邻行的接缝，
    /// 即自该端裁进的像素数。
    /// </summary>
    internal const int BarInsetPx = 3;

    /// <summary>
    /// 取最上方的空闲泳道（lane）——皆不空闲则新增一条——并标记
    /// 所认领的列。
    /// </summary>
    /// <param name="occupied">
    /// 泳道占用表，每道一枚 <c>bool[columns]</c>，随认领而增长。同一行的
    /// 各段须传同一列表，每行则须另起新列表。
    /// </param>
    /// <param name="columns">该行的列数。一周为七列；他处为日数。</param>
    /// <param name="startColumn">该段起始列（零基）。</param>
    /// <param name="span">该段覆盖的列数。</param>
    /// <returns>该段被置于其中的泳道序号（零基）。</returns>
    internal static int Claim(List<bool[]> occupied, int columns, int startColumn, int span)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        ArgumentOutOfRangeException.ThrowIfNegative(startColumn);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(span);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startColumn + span, columns);

        for (var lane = 0; ; lane++)
        {
            if (lane == occupied.Count)
            {
                occupied.Add(new bool[columns]);
            }

            var taken = occupied[lane];
            var free = true;
            for (var column = startColumn; column < startColumn + span; column++)
            {
                if (taken[column])
                {
                    free = false;
                    break;
                }
            }

            if (!free)
            {
                continue;
            }

            for (var column = startColumn; column < startColumn + span; column++)
            {
                taken[column] = true;
            }

            return lane;
        }
    }

    /// <summary>
    /// 将一条横条铺满其所从属的行。
    /// </summary>
    /// <remarks>
    /// 行为 N 列网格、列间 1px 缝隙，故一列宽为 <c>(100% - (N-1)px) / N</c>，
    /// 第 <c>s</c> 列自第 <c>s</c> 个缝隙后起算。以 <c>calc()</c> 表达此式，可在任意
    /// 宽度下令横条严丝合缝地贴住列边界，无需测量亦无需 JavaScript。
    /// </remarks>
    /// <param name="startColumn">该段起始列（零基）。</param>
    /// <param name="span">该段覆盖的列数。</param>
    /// <param name="columns">该行的列数。</param>
    /// <param name="lane">泳道（lane）序号（零基），取自 <see cref="Claim"/>。</param>
    /// <param name="topBasePx">首道相对行顶的偏移。</param>
    /// <param name="continuesBefore">该段始于更早的行；其首端不作内缩。</param>
    /// <param name="continuesAfter">该段延入更晚的行；其末端不作内缩。</param>
    internal static string BarStyle(
        int startColumn,
        int span,
        int columns,
        int lane,
        int topBasePx,
        bool continuesBefore,
        bool continuesAfter)
    {
        var startInset = continuesBefore ? 0 : BarInsetPx;
        var endInset = continuesAfter ? 0 : BarInsetPx;
        var top = topBasePx + (lane * LaneHeightPx);
        var gaps = columns - 1;

        // 缝隙项：自第 s 列起的横条需让开 s 个缝隙，横跨 n 列者吞下 n - 1 个。
        // 内缩量则只裁向真正的首末端，接缝一端不加裁。
        var left = SignedPixels(startColumn + startInset);
        var width = SignedPixels(span - 1 - startInset - endInset);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"inset-inline-start:calc((100% - {gaps}px) * {startColumn} / {columns} {left});width:calc((100% - {gaps}px) * {span} / {columns} {width});top:{top}px;height:{BarHeightPx}px;");
    }

    /// <summary>
    /// 预留横条悬浮所占的高度，令其下内容不被遮盖。行内每个单元
    /// 预留同样高度，使该内容对齐于同一基线。
    /// </summary>
    internal static string LaneSpacerStyle(int laneCount) =>
        string.Create(CultureInfo.InvariantCulture, $"height:{laneCount * LaneHeightPx}px;");

    /// <summary>
    /// 横条延入相邻行的那一端收成直角，使两段看上去是一整条
    /// 而非两条。
    /// </summary>
    internal static string? JoinRounding(bool continuesBefore, bool continuesAfter) =>
        (continuesBefore, continuesAfter) switch
        {
            (true, true) => "atom:rounded-none",
            (true, false) => "atom:rounded-s-none",
            (false, true) => "atom:rounded-e-none",
            _ => null,
        };

    /// <summary>
    /// 供 <c>calc()</c> 使用的带符号像素项。写作 <c>- 4px</c> 而非 <c>+ -4px</c>，
    /// 后者虽合法，但在 devtools 中看来像是笔误。
    /// </summary>
    private static string SignedPixels(int value) =>
        string.Create(CultureInfo.InvariantCulture, $"{(value < 0 ? '-' : '+')} {Math.Abs(value)}px");
}
