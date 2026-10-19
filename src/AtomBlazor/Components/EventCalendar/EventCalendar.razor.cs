using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 事件日历，具月、周与议程（agenda）三种视图，对使用者的事件类型泛型化。
/// </summary>
/// <typeparam name="TEvent">使用者的事件类型。经访问器委托将其映射为起止日期与标题。</typeparam>
public partial class EventCalendar<TEvent> : ComponentBase
{
    private CultureInfo culture = CultureInfo.CurrentCulture;
    private Dictionary<DateTime, List<TEvent>> eventsByDay = new();
    private EventCalendarRange? lastNotifiedRange;
    private string[]? cachedDayNames;
    private DayOfWeek cachedFirstDayOfWeek;

    /// <summary>
    /// 要显示的事件。
    /// </summary>
    [Parameter]
    public IEnumerable<TEvent>? Items { get; set; }

    /// <summary>
    /// 返回事件的起始日期/时间。
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TEvent, DateTime> EventStart { get; set; } = default!;

    /// <summary>
    /// 返回事件的结束日期/时间；瞬时事件返回 null。
    /// 若委托本身为 null，则所有事件一律按瞬时事件处理。
    /// 结束日晚于开始日的事件渲染为一条横条、贯穿其所覆盖的各日，而非
    /// 在每日各落一枚独立小条（chip）。跨周边界的事件在每个周行各渲染
    /// 一条横条，接缝处收为直角。
    /// </summary>
    [Parameter]
    public Func<TEvent, DateTime?>? EventEnd { get; set; }

    /// <summary>
    /// 返回事件的显示标题。
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TEvent, string> EventTitle { get; set; } = default!;

    /// <summary>
    /// 为特定事件的小条（chip）返回附加 CSS 类，与内置小条类合并应用。
    /// 可用于逐事件着色（例如按类别）。
    /// </summary>
    [Parameter]
    public Func<TEvent, string?>? EventClass { get; set; }

    /// <summary>
    /// 可选模板，用于完全取代内置事件小条（chip）内容。
    /// 设定后，小条按钮仅施加精简的布局/焦点类；小条层级的
    /// 样式请配合 <see cref="EventClass"/> 使用。
    /// </summary>
    [Parameter]
    public RenderFragment<TEvent>? EventTemplate { get; set; }

    /// <summary>
    /// 当前视图。双向绑定请使用 @bind-View。
    /// </summary>
    [Parameter]
    public EventCalendarView View { get; set; } = EventCalendarView.Month;

    /// <summary>
    /// 当前视图变更时的回调。
    /// </summary>
    [Parameter]
    public EventCallback<EventCalendarView> ViewChanged { get; set; }

    /// <summary>
    /// 用于控制可见期间的日期（即其所落入的月份或周）。
    /// 双向绑定请使用 @bind-CurrentDate。缺省为今天。
    /// </summary>
    [Parameter]
    public DateTime CurrentDate { get; set; } = DateTime.Today;

    /// <summary>
    /// 经导航改变当前日期时的回调。
    /// </summary>
    [Parameter]
    public EventCallback<DateTime> CurrentDateChanged { get; set; }

    /// <summary>
    /// 每周首日。缺省取当前区域性的每周首日。
    /// </summary>
    [Parameter]
    public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>
    /// 月视图中每日最多显示的事件小条（chip）数，超出即出现溢出按钮
    /// "+x more"。缺省为 3。
    /// </summary>
    [Parameter]
    public int MaxEventsPerDay { get; set; } = 3;

    /// <summary>
    /// 事件小条（chip）被点击时的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TEvent> OnEventClick { get; set; }

    /// <summary>
    /// 日号（月视图）或日头（周视图）被点击时的回调。
    /// </summary>
    [Parameter]
    public EventCallback<DateTime> OnDateClick { get; set; }

    /// <summary>
    /// 可见日期区间变更（含初始区间）时的回调。
    /// 适用于按需加载事件。
    /// </summary>
    [Parameter]
    public EventCallback<EventCalendarRange> OnViewRangeChanged { get; set; }

    /// <summary>
    /// 应用到根元素的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 应用到视图容器——月网格、周网格或议程（agenda）列表——而非根元素的
    /// 附加 CSS 类。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 根元素同时包裹工具栏与视图，故设定其尺寸并不能带动视图尺寸
    /// （#544）。本参数直接针对视图本身，这正是必须铺满页面的日历所需。
    /// 无论当前显示哪个 <see cref="View"/>，应用的类都相同，故在此设定的
    /// 高度可在 Month、Week 与 Agenda 之间切换后仍然生效。
    /// </para>
    /// <para>
    /// 若要让日历填满工具栏之外的剩余空间，可把根元素设为纵向 flex 容器，
    /// 并让视图自行伸展填满：
    /// <code>
    /// &lt;EventCalendar Class="atom:flex atom:h-full atom:flex-col" ContainerClass="grow overflow-auto" ... /&gt;
    /// </code>
    /// </para>
    /// </remarks>
    [Parameter]
    public string? ContainerClass { get; set; }

    /// <summary>
    /// 应用到根元素的附加属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private DayOfWeek EffectiveFirstDayOfWeek => FirstDayOfWeek ?? culture.DateTimeFormat.FirstDayOfWeek;

    private string CssClass => ClassNames.cn("atom:w-full", Class);

    /// <summary>月网格与周网格共用同一外壳，故容器类只需合并一次。</summary>
    private string GridCssClass => ClassNames.cn(
        "atom:grid atom:gap-px atom:overflow-hidden atom:rounded-lg atom:border atom:border-border atom:bg-border",
        ContainerClass);

    /// <summary>议程（agenda）列表，系分隔卡片而非网格。</summary>
    private string AgendaCssClass => ClassNames.cn(
        "atom:divide-y atom:divide-border atom:overflow-hidden atom:rounded-lg atom:border atom:border-border atom:bg-card atom:text-card-foreground",
        ContainerClass);

    protected override async Task OnParametersSetAsync()
    {
        var currentCulture = CultureInfo.CurrentCulture;
        if (!ReferenceEquals(culture, currentCulture) && culture.Name != currentCulture.Name)
        {
            culture = currentCulture;
            cachedDayNames = null;
        }

        if (cachedFirstDayOfWeek != EffectiveFirstDayOfWeek)
        {
            cachedFirstDayOfWeek = EffectiveFirstDayOfWeek;
            cachedDayNames = null;
        }

        await RefreshAsync();
    }

    #region Range & lookup

    private EventCalendarRange GetVisibleRange()
    {
        switch (View)
        {
            case EventCalendarView.Week:
                var weekStart = StartOfWeek(CurrentDate);
                return new EventCalendarRange(weekStart, weekStart.AddDays(6));
            case EventCalendarView.Agenda:
                var agendaStart = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
                return new EventCalendarRange(agendaStart, agendaStart.AddMonths(1).AddDays(-1));
            default:
                var firstOfMonth = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
                var gridStart = StartOfWeek(firstOfMonth);
                return new EventCalendarRange(gridStart, gridStart.AddDays(41));
        }
    }

    private DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)EffectiveFirstDayOfWeek + 7) % 7;
        return date.Date.AddDays(-diff);
    }

    /// <summary>
    /// 重建可见区间的逐日事件查找表，并在区间发生变化时触发
    /// <see cref="OnViewRangeChanged"/>。
    /// </summary>
    private async Task RefreshAsync()
    {
        RebuildEventLookup();

        var range = GetVisibleRange();
        if (lastNotifiedRange == range)
        {
            return;
        }

        lastNotifiedRange = range;
        if (OnViewRangeChanged.HasDelegate)
        {
            await OnViewRangeChanged.InvokeAsync(range);
        }
    }

    private void RebuildEventLookup()
    {
        eventsByDay = new Dictionary<DateTime, List<TEvent>>();
        if (Items is null || EventStart is null)
        {
            return;
        }

        var range = GetVisibleRange();
        foreach (var item in Items)
        {
            var startDay = EventStart(item).Date;
            var endDay = (EventEnd?.Invoke(item) ?? EventStart(item)).Date;
            if (endDay < startDay)
            {
                endDay = startDay;
            }

            var from = startDay < range.Start ? range.Start : startDay;
            var to = endDay > range.End ? range.End : endDay;
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (!eventsByDay.TryGetValue(day, out var list))
                {
                    list = new List<TEvent>();
                    eventsByDay[day] = list;
                }
                list.Add(item);
            }
        }

        foreach (var list in eventsByDay.Values)
        {
            list.Sort(CompareEvents);
        }
    }

    private int CompareEvents(TEvent a, TEvent b)
    {
        // 全天事件优先，其次按开始时间，再按标题以求稳定次序。
        var allDayCompare = IsAllDay(b).CompareTo(IsAllDay(a));
        if (allDayCompare != 0)
        {
            return allDayCompare;
        }

        var startCompare = EventStart(a).CompareTo(EventStart(b));
        if (startCompare != 0)
        {
            return startCompare;
        }

        return string.Compare(EventTitle(a), EventTitle(b), StringComparison.Ordinal);
    }

    private IReadOnlyList<TEvent> GetEventsForDay(DateTime day) =>
        eventsByDay.TryGetValue(day.Date, out var list) ? list : Array.Empty<TEvent>();

    /// <summary>事件是否跨越多于一个日历日。</summary>
    private bool IsMultiDay(TEvent item)
    {
        var start = EventStart(item).Date;
        var end = (EventEnd?.Invoke(item) ?? EventStart(item)).Date;
        return end > start;
    }

    /// <summary>
    /// 归入某月单元的单日事件。跨日事件改以横条铺于周行之上，
    /// 故不得在此重复出现，否则将被渲染两次。
    /// </summary>
    private List<TEvent> GetSingleDayEvents(DateTime day)
    {
        var chips = new List<TEvent>();
        foreach (var item in GetEventsForDay(day))
        {
            if (!IsMultiDay(item))
            {
                chips.Add(item);
            }
        }

        return chips;
    }

    #endregion

    #region Week-row event bars

    /// <summary>
    /// 跨日事件在单个周行内的一段水平延伸。
    /// </summary>
    /// <param name="Event">使用者的事件。</param>
    /// <param name="StartColumn">该段起始列（零基）。</param>
    /// <param name="Span">该段覆盖的列数。</param>
    /// <param name="Lane">周内堆叠行（零基），分配时保证各段互不重叠。</param>
    /// <param name="ContinuesBefore">事件始于更早的周行。</param>
    /// <param name="ContinuesAfter">事件延续至更晚的周行。</param>
    private sealed record EventBar(
        TEvent Event,
        int StartColumn,
        int Span,
        int Lane,
        bool ContinuesBefore,
        bool ContinuesAfter);

    /// <summary>单个周行的横条，以及它们占用其下各日单元的额度。</summary>
    private sealed class WeekRowLayout
    {
        /// <summary>待绘制的各段，已装入泳道（lane）。</summary>
        public List<EventBar> Bars { get; } = new();

        /// <summary>实际绘制的泳道数，令行内每个单元预留同样高度。</summary>
        public int VisibleLaneCount { get; set; }

        /// <summary>覆盖各列的已绘横条数——它们消耗当日的显示额度。</summary>
        public int[] BarsPerColumn { get; } = new int[7];

        /// <summary>因超额而被舍弃的横条数，计入当日的 "+x more"。</summary>
        public int[] HiddenPerColumn { get; } = new int[7];
    }

    /// <summary>
    /// 将与某周行重叠的跨日事件装填为互不重叠的泳道（lane）。
    /// </summary>
    /// <remarks>
    /// 跨周边界的事件在每个行各产生一段；该段带有
    /// <see cref="EventBar.ContinuesBefore"/> / <see cref="EventBar.ContinuesAfter"/>，
    /// 接缝因此画成直角而非圆角。最长的片段优先放置，令长横条
    /// 居于堆叠顶部，看上去如贯穿整月的连续线条。
    /// </remarks>
    /// <param name="week">该行的七日，按显示顺序。</param>
    /// <param name="maxLanes">
    /// 单日可为横条让出的泳道数。月视图消耗该日的
    /// <see cref="MaxEventsPerDay"/> 额度；周视图无此限制，传入
    /// <see cref="int.MaxValue"/>。
    /// </param>
    private WeekRowLayout BuildWeekLayout(DateTime[] week, int maxLanes)
    {
        var layout = new WeekRowLayout();
        if (Items is null || EventStart is null || maxLanes <= 0)
        {
            return layout;
        }

        var weekStart = week[0];
        var weekEnd = week[6];

        var spanning = new List<(TEvent Item, DateTime Start, DateTime End)>();
        foreach (var item in Items)
        {
            var start = EventStart(item).Date;
            var end = (EventEnd?.Invoke(item) ?? EventStart(item)).Date;
            if (end < start)
            {
                end = start;
            }

            // 单日事件保留为单元内的小条（chip）。
            if (end == start || end < weekStart || start > weekEnd)
            {
                continue;
            }

            spanning.Add((item, start, end));
        }

        spanning.Sort(CompareSpanning);

        // occupied[lane][column]：每个泳道（lane）已占用该行中的哪些单元。
        var occupied = new List<bool[]>();

        foreach (var (item, start, end) in spanning)
        {
            var from = start < weekStart ? weekStart : start;
            var to = end > weekEnd ? weekEnd : end;
            var startColumn = (from - weekStart).Days;
            var span = (to - from).Days + 1;
            var lane = DayBandLayout.Claim(occupied, 7, startColumn, span);

            if (lane >= maxLanes)
            {
                // 超出当日额度：舍弃该横条而非任行无限增高，
                // 并循小条（chip）同款 "+x more" 上报其存在。
                for (var column = startColumn; column < startColumn + span; column++)
                {
                    layout.HiddenPerColumn[column]++;
                }

                continue;
            }

            layout.Bars.Add(new EventBar(item, startColumn, span, lane, start < from, end > to));
            for (var column = startColumn; column < startColumn + span; column++)
            {
                layout.BarsPerColumn[column]++;
            }

            layout.VisibleLaneCount = Math.Max(layout.VisibleLaneCount, lane + 1);
        }

        return layout;
    }

    /// <summary>先按最长，再按最早，其后循视图常规的事件次序。</summary>
    private int CompareSpanning(
        (TEvent Item, DateTime Start, DateTime End) a,
        (TEvent Item, DateTime Start, DateTime End) b)
    {
        var lengthCompare = (b.End - b.Start).CompareTo(a.End - a.Start);
        if (lengthCompare != 0)
        {
            return lengthCompare;
        }

        var startCompare = a.Start.CompareTo(b.Start);
        return startCompare != 0 ? startCompare : CompareEvents(a.Item, b.Item);
    }

    #endregion

    #region View data

    private string[] DayNames => cachedDayNames ??= BuildDayNames();

    private string[] BuildDayNames()
    {
        var cultureNames = culture.DateTimeFormat.AbbreviatedDayNames;
        var start = (int)EffectiveFirstDayOfWeek;
        var names = new string[7];
        for (var i = 0; i < 7; i++)
        {
            names[i] = cultureNames[(start + i) % 7];
        }
        return names;
    }

    private IEnumerable<DateTime[]> MonthWeeks
    {
        get
        {
            var start = GetVisibleRange().Start;
            for (var week = 0; week < 6; week++)
            {
                var days = new DateTime[7];
                for (var i = 0; i < 7; i++)
                {
                    days[i] = start.AddDays((week * 7) + i);
                }
                yield return days;
            }
        }
    }

    private DateTime[] WeekDays
    {
        get
        {
            var start = StartOfWeek(CurrentDate);
            var days = new DateTime[7];
            for (var i = 0; i < 7; i++)
            {
                days[i] = start.AddDays(i);
            }
            return days;
        }
    }

    private IEnumerable<DateTime> AgendaDays => eventsByDay.Keys.OrderBy(d => d);

    private string HeaderTitle
    {
        get
        {
            if (View == EventCalendarView.Week)
            {
                var range = GetVisibleRange();
                return string.Format(
                    culture,
                    "{0} – {1}, {2}",
                    range.Start.ToString("M", culture),
                    range.End.ToString("M", culture),
                    range.End.Year);
            }

            return CurrentDate.ToString("Y", culture);
        }
    }

    private bool IsAllDay(TEvent item)
    {
        var start = EventStart(item);
        if (start.TimeOfDay != TimeSpan.Zero)
        {
            return false;
        }

        var end = EventEnd?.Invoke(item);
        if (end is null)
        {
            return true;
        }

        return end.Value.TimeOfDay == TimeSpan.Zero;
    }

    private string GetEventTimeLabel(TEvent item)
    {
        if (IsAllDay(item))
        {
            return Localizer["EventCalendar.AllDay"];
        }

        var start = EventStart(item);
        var end = EventEnd?.Invoke(item);
        if (end is null || end.Value == start)
        {
            return start.ToString("t", culture);
        }

        return string.Format(
            culture,
            "{0} – {1}",
            start.ToString("t", culture),
            end.Value.ToString("t", culture));
    }

    #endregion

    #region Navigation & interaction

    private async Task SetViewAsync(EventCalendarView view)
    {
        if (View == view)
        {
            return;
        }

        View = view;
        await ViewChanged.InvokeAsync(view);
        await RefreshAsync();
    }

    private async Task SetCurrentDateAsync(DateTime date)
    {
        CurrentDate = date.Date;
        await CurrentDateChanged.InvokeAsync(CurrentDate);
        await RefreshAsync();
    }

    private Task NavigatePreviousAsync() =>
        SetCurrentDateAsync(View == EventCalendarView.Week ? CurrentDate.AddDays(-7) : CurrentDate.AddMonths(-1));

    private Task NavigateNextAsync() =>
        SetCurrentDateAsync(View == EventCalendarView.Week ? CurrentDate.AddDays(7) : CurrentDate.AddMonths(1));

    private Task NavigateTodayAsync() => SetCurrentDateAsync(DateTime.Today);

    private Task HandleEventClick(TEvent item) => OnEventClick.InvokeAsync(item);

    private Task HandleDateClick(DateTime day) => OnDateClick.InvokeAsync(day.Date);

    /// <summary>
    /// "+x more" 溢出入口：跳转至议程（agenda）视图中的被点击日。
    /// </summary>
    private async Task ShowMoreAsync(DateTime day)
    {
        await SetViewAsync(EventCalendarView.Agenda);
        await SetCurrentDateAsync(day);
    }

    #endregion

    #region Styling

    private const string chipBaseClasses = "atom:block atom:w-full atom:rounded atom:px-1.5 atom:py-0.5 atom:text-start atom:text-xs atom:font-medium atom:bg-primary/10 atom:text-primary atom:hover:bg-primary/20 atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring";
    private const string chipTemplateClasses = "atom:block atom:w-full atom:rounded atom:text-start atom:text-xs atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring";
    private const string dayNumberBaseClasses = "atom:flex atom:h-6 atom:w-6 atom:items-center atom:justify-center atom:rounded-full atom:text-xs atom:font-medium atom:hover:bg-accent atom:hover:text-accent-foreground atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring";
    private const string dayNumberTodayClasses = "atom:flex atom:h-6 atom:w-6 atom:items-center atom:justify-center atom:rounded-full atom:text-xs atom:font-medium atom:bg-primary atom:text-primary-foreground atom:hover:bg-primary atom:hover:text-primary-foreground atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring";

    private string GetEventChipClasses(TEvent item)
    {
        var baseClasses = EventTemplate is not null ? chipTemplateClasses : chipBaseClasses;
        var customClass = EventClass?.Invoke(item);
        return string.IsNullOrEmpty(customClass) ? baseClasses : ClassNames.cn(baseClasses, customClass);
    }

    // 月视图横条几何。横条以周行为定位基准而非其单元，故此处须复现
    // 单元自身盒模型：`p-1.5` 上内边距、`h-6` 日号按钮，以及事件堆叠上方的
    // `mt-1`。一个泳道（lane）即一枚小条（chip，text-xs、py-0.5）加上其间
    // 起分隔作用的 `space-y-0.5`，令横条与小条落在同一节奏上。
    private const int monthCellPaddingPx = 6;
    private const int monthDayNumberHeightPx = 24;
    private const int monthDayNumberGapPx = 4;
    private const int monthBarsTopPx = monthCellPaddingPx + monthDayNumberHeightPx + monthDayNumberGapPx;

    // 周视图将日号置于独立表头行，故其横条自单元 padding 盒顶部起算，
    // 而非落在日号按钮之下。
    private const int weekBarsTopPx = monthCellPaddingPx;

    private const string eventBarBaseClasses = "atom:absolute atom:z-10 atom:flex atom:items-center atom:overflow-hidden atom:rounded atom:px-1.5 atom:text-start atom:text-xs atom:font-medium atom:bg-primary/10 atom:text-primary atom:hover:bg-primary/20 atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring";
    private const string eventBarTemplateClasses = "atom:absolute atom:z-10 atom:flex atom:items-center atom:overflow-hidden atom:rounded atom:text-start atom:text-xs atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring";

    private string GetEventBarClasses(EventBar bar)
    {
        var baseClasses = EventTemplate is not null ? eventBarTemplateClasses : eventBarBaseClasses;

        var rounding = DayBandLayout.JoinRounding(bar.ContinuesBefore, bar.ContinuesAfter);

        return ClassNames.cn(baseClasses, rounding, EventClass?.Invoke(bar.Event));
    }

    /// <summary>
    /// 将横条铺设于其所从属的周行之上。
    /// </summary>
    /// <remarks>
    /// 行为七列网格、列间 1px 缝隙，故一列宽为 <c>(100% - 6px) / 7</c>，
    /// 第 <c>s</c> 列自第 <c>s</c> 个缝隙后起算。以 <c>calc()</c> 表达此式，可在任意宽度
    /// 下令横条严丝合缝地贴住列边界，无需测量亦无需 JavaScript。
    /// 横条居于其起始日的单元内，使其留在 <c>gridcell</c> 之中，对辅助
    /// 技术（assistive technology）保持可达，并落于 Tab 键序的恰当位置；单元为
    /// static 而行是 relative，故偏移最终相对行求解。
    /// </remarks>
    private static string GetEventBarStyle(EventBar bar, int topBasePx) =>
        DayBandLayout.BarStyle(bar.StartColumn, bar.Span, 7, bar.Lane, topBasePx, bar.ContinuesBefore, bar.ContinuesAfter);

    /// <summary>
    /// 预留横条悬浮所占的高度，令其下的小条（chip）不被遮盖。
    /// 行内每个单元预留同样高度，使各小条对齐于同一基线。
    /// </summary>
    private static string GetLaneSpacerStyle(int laneCount) => DayBandLayout.LaneSpacerStyle(laneCount);

    /// <summary>
    /// 播报整个事件及其所覆盖的日期，而非仅本行所见部分——跨周边界的
    /// 视觉切分只是布局细节，不应泄漏进 ARIA 标签。
    /// </summary>
    private string GetEventBarAriaLabel(EventBar bar)
    {
        var start = EventStart(bar.Event).Date;
        var end = (EventEnd?.Invoke(bar.Event) ?? EventStart(bar.Event)).Date;

        return string.Format(
            culture,
            "{0}, {1} – {2}",
            EventTitle(bar.Event),
            start.ToString("D", culture),
            end.ToString("D", culture));
    }

    private string GetMonthCellClasses(DateTime day)
    {
        var isOutside = day.Month != CurrentDate.Month || day.Year != CurrentDate.Year;
        return isOutside
            ? "atom:min-h-28 atom:bg-card atom:p-1.5 atom:text-muted-foreground"
            : "atom:min-h-28 atom:bg-card atom:p-1.5";
    }

    private static string GetDayNumberClasses(DateTime day) =>
        day.Date == DateTime.Today ? dayNumberTodayClasses : dayNumberBaseClasses;

    private string GetViewSwitchClasses(EventCalendarView view) =>
        View == view
            ? "atom:rounded-sm atom:px-3 atom:py-1 atom:text-sm atom:font-medium atom:bg-primary atom:text-primary-foreground"
            : "atom:rounded-sm atom:px-3 atom:py-1 atom:text-sm atom:font-medium atom:text-muted-foreground atom:hover:bg-accent atom:hover:text-accent-foreground atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring";

    #endregion
}
