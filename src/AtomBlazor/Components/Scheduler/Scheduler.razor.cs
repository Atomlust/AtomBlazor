#pragma warning disable BL0016 // 与一致保留两处未守卫首渲 interop（initialize/restoreDeleteFocus）：皆在 firstRender/disposed 双闸门内，断线由电路生命周期兜底；采文件级抑制以免逐点 pragma 打散与逐行对齐的行位（照 Dock／ResizablePanelGroup 先例）
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>资源调度器，具时间槽、周期性约定与可选的事件编辑器。</summary>
public partial class Scheduler
{
    [Parameter] public IReadOnlyList<SchedulerEvent> Events { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<SchedulerEvent>> EventsChanged { get; set; }
    [Parameter] public IReadOnlyList<SchedulerResource> Resources { get; set; } = [];
    /// <summary>要显示的资源 ID。null 显示全部资源；空列表不显示任何资源。空字符串选中未分配（Unassigned）泳道。支持 @bind-VisibleResourceIds。</summary>
    [Parameter] public IReadOnlyList<string>? VisibleResourceIds { get; set; }
    /// <summary>可见资源变更时触发。支持 @bind-VisibleResourceIds。</summary>
    [Parameter] public EventCallback<IReadOnlyList<string>?> VisibleResourceIdsChanged { get; set; }
    /// <summary>在工具栏显示内置资源选择器。未提供 Resources 时无效果。</summary>
    [Parameter] public bool ShowResourceFilter { get; set; }
    [Parameter] public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Parameter] public EventCallback<DateOnly> DateChanged { get; set; }
    [Parameter] public SchedulerView View { get; set; } = SchedulerView.Week;
    [Parameter] public EventCallback<SchedulerView> ViewChanged { get; set; }
    /// <summary>周视图（Week）的首日。工具栏提供周一与周日两项；工作周（WorkWeek）恒显周一至周五。</summary>
    [Parameter] public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Monday;
    /// <summary>每周首日选项变更时触发。支持 @bind-FirstDayOfWeek。</summary>
    [Parameter] public EventCallback<DayOfWeek> FirstDayOfWeekChanged { get; set; }
    /// <summary>泳道日期与时刻标签所用的 IANA 时区。各事件自带的时区保持独立。</summary>
    [Parameter] public string TimeZoneId { get; set; } = "UTC";
    /// <summary>
    /// 编辑器提供的时区，名称由使用者自定。留空——缺省——即列出宿主
    /// 支持的全部 IANA 时区，约 600 条，多半不是应用想要的规模。
    /// 已使用未列入时区的事件仍会显示该时区，故打开该事件并
    /// 不会悄然改写其时区。
    /// </summary>
    [Parameter] public IReadOnlyList<SchedulerTimeZone> TimeZones { get; set; } = [];
    /// <summary>是否显示逐事件时区。为 false 时，显示与编辑器改用 TimeZoneId 并隐藏时区控件；已存时刻与重复规则所用时区仍然保留。</summary>
    [Parameter] public bool EnableTimeZones { get; set; } = true;
    /// <summary>允许在时间槽、日与资源泳道之间拖放约定。对重复事件的改动只作用于单次发生（occurrence）。</summary>
    [Parameter] public bool AllowDrag { get; set; } = true;
    /// <summary>允许拖动约定的上下边缘改其尺寸，吸附步进为 SlotMinutes。</summary>
    [Parameter] public bool AllowResize { get; set; } = true;
    /// <summary>时间槽尺寸与拖拽/缩放的吸附间隔，单位分钟。支持 15、30、60 以及 5 至 120 之间 1440 的其他约数。</summary>
    [Parameter] public int SlotMinutes { get; set; } = 30;
    /// <summary>
    /// 调度器加以突出的工作日时段。落于该日所有区间之外的槽会被置灰。
    /// 留空——缺省——不置灰任何槽。整日无区间者全日置灰，故周末
 /// 无需登记；同一日两段区间则置灰其间空档。置灰仅为视觉呈现
    /// 且月视图（Month）没有时间轴可供置灰。
    /// </summary>
    [Parameter] public IReadOnlyList<SchedulerDayHours> ActiveHours { get; set; } = [];
    /// <summary>
    /// 拒绝在 <see cref="ActiveHours"/> 之外的槽上创建、移动或缩放。缺省关闭
    /// 故有效时段仅作置灰。它守卫指针与键盘两条通路；并不校验
    /// 你所供的 <see cref="Events"/>，且 <c>CreateEvent</c> 仍保持开放，作为
    /// 程序化调用的逃生口。
    /// </summary>
    [Parameter] public bool BlockOutsideActiveHours { get; set; }
    [Parameter] public int StartHour { get; set; } = 8;
    [Parameter] public int EndHour { get; set; } = 18;
    /// <summary>首次可交互渲染时滚动至的显示时区小时（0–23）。null 则自 StartHour 起。其后的渲染保留用户的滚动位置。</summary>
    [Parameter] public int? InitialScrollHour { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter] public RenderFragment<SchedulerOccurrence>? EventTemplate { get; set; }
    /// <summary>月视图（Month）单元内折叠为 "+x more" 计数之前所绘制的事件小条（chip）数。跨日横条在其覆盖的每一日都消耗额度。</summary>
    [Parameter] public int MaxEventsPerDay { get; set; } = 3;
    /// <summary>全天带在其折叠为 "+x more" 计数之前所绘制的行数。一条横条在其覆盖的每一日都消耗额度。</summary>
    [Parameter] public int MaxAllDayRows { get; set; } = 2;
    /// <summary>
    /// 在编辑器中渲染你自己的字段。渲染上下文的 DefaultContent 即可保留
    /// 内置字段并在其周围增补。若有自有字段，请从 SchedulerEvent 
    /// 并把上下文的 Draft 转为你的类型以完成绑定。
    /// </summary>
    [Parameter] public RenderFragment<SchedulerEditorContext>? EditorContent { get; set; }
    /// <summary>
    /// 创建编辑器起手所用的空白事件。当你从 SchedulerEvent 时须一并给出
    /// 否则新建事件即为基础类型、你的自有字段将丢失。
    /// </summary>
    [Parameter] public Func<SchedulerEvent>? NewEventFactory { get; set; }
    /// <summary>取代内置工具栏。渲染上下文的 DefaultContent 即可保留它并在其周围增补。</summary>
    [Parameter] public RenderFragment<SchedulerToolbarContext>? ToolbarContent { get; set; }
    /// <summary>取代空时间槽上显示的右键菜单。渲染上下文的 DefaultItems 即可保留内置项并在其周围增补自有项。</summary>
    [Parameter] public RenderFragment<SchedulerSlotMenuContext>? SlotContextMenuContent { get; set; }
    /// <summary>取代事件上显示的右键菜单。渲染上下文的 DefaultItems 即可保留内置项并在其周围增补自有项。</summary>
    [Parameter] public RenderFragment<SchedulerEventMenuContext>? EventContextMenuContent { get; set; }
    /// <summary>在 EventsChanged 之前运行。置 Cancel 或抛出异常，可留住编辑器与原事件。</summary>
    [Parameter] public EventCallback<SchedulerChangeContext> OnEventChange { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly string editorId = $"atom-scheduler-{Guid.NewGuid():N}";
    private IReadOnlyList<SchedulerOccurrence> occurrences = [];
    private List<Lane> lanes = [];
    private (int LaneIndex, DateTimeOffset Start)? selectedSlot;
    private DateOnly? selectedDay;
    private (string Id, DateTimeOffset Start)? selectedOccurrence;
    private List<BandRow> bandRows = [];
    private List<MonthWeek> monthWeeks = [];
    private ContextMenu? contextMenu;
    private SchedulerSlotMenuContext? slotMenuContext;
    private SchedulerEventMenuContext? eventMenuContext;
    private SchedulerEvent? draft;
    private SchedulerOccurrence? editingOccurrence;
    private SchedulerEditScope editScope = SchedulerEditScope.Occurrence;
    private SchedulerAmbiguousTimeResolution startResolution;
    private SchedulerAmbiguousTimeResolution endResolution;
    private DateTime? localStart;
    private DateTime? localEnd;
    /// <summary>
    /// 编辑器所列示的时区。已存储却不在使用者给定列表中的时区会被追加进来
    /// 故打开事件绝不会把它悄悄挪到应用恰好偏好的时区。
    /// </summary>
    private IEnumerable<SchedulerTimeZone> EditorTimeZones
    {
        get
        {
            if (TimeZones.Count == 0)
            {
                return timeZoneIds.Select(id => new SchedulerTimeZone(id, id.Replace('_', ' ')));
            }
            var current = draft?.TimeZoneId;
            return string.IsNullOrEmpty(current) || TimeZones.Any(zone => zone.Id == current)
                ? TimeZones
                : [.. TimeZones, new SchedulerTimeZone(current, current.Replace('_', ' '))];
        }
    }

    /// <summary>时区的标签：你命名过即用你的名称，否则用标识符。</summary>
    private string TimeZoneTitle(string id) =>
        TimeZones.FirstOrDefault(zone => zone.Id == id)?.Title ?? id.Replace('_', ' ');

    private static readonly string[] timeZoneIds = [.. NodaTime.DateTimeZoneProviders.Tzdb.Ids
        .Where(id => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _)).Order(StringComparer.Ordinal)];
    private static readonly string[] recurrenceFrequencies = ["NONE", "DAILY", "WEEKLY", "MONTHLY", "YEARLY"];
    private static readonly string[] recurrenceDayCodes = ["SU", "MO", "TU", "WE", "TH", "FR", "SA"];
    private readonly HashSet<DayOfWeek> recurrenceDays = [];
    private bool limitRecurrence;
    private string recurrenceFrequency = "NONE";
    private int? recurrenceCount;
    private bool editorOpen;
    private bool deleteConfirmationOpen;
    private bool restoreDeleteFocus;
    private bool saving;
    private string? editorError;
    private string? loadError;
    private ElementReference schedulerElement;
    private IJSObjectReference? interactionModule;
    private DotNetObjectReference<Scheduler>? dotNetReference;
    private bool disposed;
    private int revision;

    private DayOfWeek EffectiveWeekStart => View == SchedulerView.WorkWeek ? DayOfWeek.Monday : FirstDayOfWeek;
    private DateOnly RangeDate => View switch
    {
        SchedulerView.Day => Date,
        SchedulerView.Month => WeekStartOn(new DateOnly(Date.Year, Date.Month, 1)),
        _ => WeekStartOn(Date)
    };
    private DateOnly WeekStartOn(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek - (int)EffectiveWeekStart + 7) % 7));
    // 恒为六行，故网格高度不会在月份之间跳动。
    private int DayCount => View switch { SchedulerView.Day => 1, SchedulerView.WorkWeek => 5, SchedulerView.Month => 42, _ => 7 };
    private bool IsMonth => View == SchedulerView.Month;
    private string RangeHeading => IsMonth
        ? Date.ToDateTime(TimeOnly.MinValue).ToString("Y", CultureInfo.CurrentCulture)
        : RangeDate.ToString("D", CultureInfo.CurrentCulture);
    private TimeZoneInfo DisplayZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    private string EditorTimeZoneId => EnableTimeZones ? draft!.TimeZoneId : TimeZoneId;
    private string DeleteDescription => Localizer[editingOccurrence != null && !string.IsNullOrWhiteSpace(editingOccurrence.Event.RecurrenceRule)
        ? editScope == SchedulerEditScope.Series ? "Scheduler.DeleteSeriesDescription" : "Scheduler.DeleteOccurrenceDescription"
        : "Scheduler.DeleteDescription", editingOccurrence?.Event.Title ?? ""];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }
        if (firstRender)
        {
            interactionModule = await JsModules.GetAsync(JSRuntime, JsModules.Versioned("./_content/AtomBlazor/js/scheduler.js?assets=2", typeof(Scheduler).Assembly));
            if (disposed)
            {
                return;
            }
            dotNetReference = DotNetObjectReference.Create(this);
            await interactionModule.InvokeVoidAsync("initialize", schedulerElement, dotNetReference);
        }
        if (restoreDeleteFocus && interactionModule != null)
        {
            restoreDeleteFocus = false;
            await interactionModule.InvokeVoidAsync("restoreDeleteFocus", editorId);
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        disposed = true;
        if (interactionModule != null)
        {
            try { await interactionModule.InvokeVoidAsync("dispose", schedulerElement); }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException) { }
        }
        dotNetReference?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override void OnParametersSet()
    {
        if (SlotMinutes < 5 || SlotMinutes > 120 || 1440 % SlotMinutes != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SlotMinutes), "Use a divisor of 1440 between 5 and 120 minutes.");
        }
        if (StartHour < 0 || EndHour > 24 || EndHour <= StartHour)
        {
            throw new ArgumentOutOfRangeException(nameof(StartHour), "Use 0 <= StartHour < EndHour <= 24.");
        }
        if (InitialScrollHour is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(InitialScrollHour), "Use an hour between 0 and 23, or null.");
        }
        if (Resources.Any(r => string.IsNullOrWhiteSpace(r.Id)) || Resources.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != Resources.Count)
        {
            throw new ArgumentException("Resource IDs must be unique.", nameof(Resources));
        }
        if (TimeZones.Any(zone => string.IsNullOrWhiteSpace(zone.Id) || string.IsNullOrWhiteSpace(zone.Title))
            || TimeZones.Select(zone => zone.Id).Distinct(StringComparer.Ordinal).Count() != TimeZones.Count)
        {
            throw new ArgumentException("Time zone IDs must be unique and every zone needs a title.", nameof(TimeZones));
        }
        foreach (var zone in TimeZones)
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone.Id, out _)
                || NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(zone.Id) == null)
            {
                throw new ArgumentException($"'{zone.Id}' is not an IANA time zone this host supports.", nameof(TimeZones));
            }
        }
        // MinValue 系「日终午夜」的约定写法；TimeOnly 并无 24:00。
        if (ActiveHours.Any(range => range.End != TimeOnly.MinValue && range.End <= range.Start))
        {
            throw new ArgumentException("An active-hours range must end after it starts. Use TimeOnly.MinValue for midnight.", nameof(ActiveHours));
        }
        RefreshSchedule();
    }

    private double? GetInitialScrollTop()
    {
        // 月视图（Month）没有时间轴可滚动。
        if (IsMonth || InitialScrollHour is not { } hour || lanes.Count == 0)
        {
            return null;
        }
        var lane = lanes[0];
        var minutes = (DayBoundary(lane.Date, hour) - lane.Start).TotalMinutes;
        return Math.Clamp(minutes, 0, (lane.End - lane.Start).TotalMinutes) / SlotMinutes * 40;
    }

    private void RefreshSchedule()
    {
        revision++;
        loadError = null;
        // 高亮所凭依的泳道序号即将重建。
        selectedSlot = null;
        selectedDay = null;
        selectedOccurrence = null;
        try
        {
            var start = DayBoundary(RangeDate, 0);
            var end = DayBoundary(RangeDate.AddDays(DayCount), 0);
            occurrences = SchedulerEngine.Expand(Events, start, end);
            lanes = [];
            monthWeeks = [];
            if (IsMonth)
            {
                BuildMonthGrid();
                return;
            }
            for (var day = 0; day < DayCount; day++)
            {
                var date = RangeDate.AddDays(day);
                var laneStart = DayBoundary(date, StartHour);
                var laneEnd = DayBoundary(date, EndHour);
                if (Resources.Count == 0)
                {
                    lanes.Add(CreateLane(date, null, laneStart, laneEnd));
                }
                else
                {
                    foreach (var resource in VisibleResources)
                    {
                        lanes.Add(CreateLane(date, resource, laneStart, laneEnd));
                    }
                    if (ShowUnassignedLane)
                    {
                        lanes.Add(CreateLane(date, new SchedulerResource(string.Empty, Localizer["Scheduler.Unassigned"]), laneStart, laneEnd));
                    }
                }
            }
            BuildAllDayBand();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or TimeZoneNotFoundException or InvalidTimeZoneException or Ical.Net.Evaluation.EvaluationException)
        {
            lanes = [];
            bandRows = [];
            loadError = Localizer["Scheduler.InvalidEvents"];
        }
    }

    private DateTimeOffset DayBoundary(DateOnly date, int hour)
    {
        var local = date.ToDateTime(TimeOnly.MinValue).AddHours(hour);
        // 少数时区在午夜拨快时钟。泳道自当日首个有效墙上分钟起算。
        while (DisplayZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }
        return SchedulerEngine.ToInstant(local, TimeZoneId);
    }

    /// <summary>
    /// 构建六周制月视图（Month）网格：跨日片段化为贯穿周行的横条，单日
    /// 事件仍以单元内的小条（chip）留存。
    /// </summary>
    /// <remarks>
    /// 此处资源是过滤器而非泳道——月单元无法为每类资源各辟一列。
    /// 可见集合收窄到哪些发生（occurrence）被绘制；用户经
    /// 工具栏选择器改变它。
    /// </remarks>
    private void BuildMonthGrid()
    {
        monthWeeks = [];
        var month = Date.Month;
        var budget = Math.Max(1, MaxEventsPerDay);
        var visible = occurrences.Where(IsVisibleInMonth).ToList();

        for (var week = 0; week < 6; week++)
        {
            var weekStart = RangeDate.AddDays(week * 7);
            var weekEnd = weekStart.AddDays(6);
            var runs = new List<(SchedulerOccurrence Occurrence, int StartColumn, int Span, bool Before, bool After, int Length)>();
            var single = new List<SchedulerOccurrence>[7];
            for (var day = 0; day < 7; day++)
            {
                single[day] = [];
            }

            foreach (var occurrence in visible)
            {
                var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrence.Start, DisplayZone).Date);
                // 全天的结束端是排他的；恰于午夜收束的计时事件属于
                // 前一日，故两种情形都以最后一刻定出所覆盖的末日。
                var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrence.End.AddTicks(-1), DisplayZone).Date);
                if (endDate < startDate)
                {
                    endDate = startDate;
                }
                if (endDate < weekStart || startDate > weekEnd)
                {
                    continue;
                }
                if (startDate == endDate)
                {
                    single[startDate.DayNumber - weekStart.DayNumber].Add(occurrence);
                    continue;
                }
                var visibleStart = startDate < weekStart ? weekStart : startDate;
                var visibleEnd = endDate > weekEnd ? weekEnd : endDate;
                runs.Add((occurrence, visibleStart.DayNumber - weekStart.DayNumber,
                    visibleEnd.DayNumber - visibleStart.DayNumber + 1,
                    (startDate < visibleStart), (endDate > visibleEnd), endDate.DayNumber - startDate.DayNumber));
            }

            runs.Sort((a, b) =>
            {
                var length = b.Length.CompareTo(a.Length);
                if (length != 0)
                {
                    return length;
                }
                var start = a.Occurrence.Start.CompareTo(b.Occurrence.Start);
                return start != 0 ? start : string.CompareOrdinal(a.Occurrence.Event.Id, b.Occurrence.Event.Id);
            });

            var bars = new List<BandBar>();
            var barsPerColumn = new int[7];
            var hidden = new int[7];
            var occupied = new List<bool[]>();
            var laneCount = 0;

            foreach (var run in runs)
            {
                var lane = DayBandLayout.Claim(occupied, 7, run.StartColumn, run.Span);
                if (lane >= budget)
                {
                    for (var column = run.StartColumn; column < run.StartColumn + run.Span; column++)
                    {
                        hidden[column]++;
                    }
                    continue;
                }
                bars.Add(new BandBar(run.Occurrence, run.StartColumn, run.Span, lane, run.Before, run.After));
                for (var column = run.StartColumn; column < run.StartColumn + run.Span; column++)
                {
                    barsPerColumn[column]++;
                }
                laneCount = Math.Max(laneCount, lane + 1);
            }

            var cells = new List<MonthCell>(7);
            for (var day = 0; day < 7; day++)
            {
                var date = weekStart.AddDays(day);
                // 横条在其覆盖的每一日都消耗当日额度，故承载横条之日显示的
                // 小条（chip）更少，差额计入该日自身的 "+x more"。
                var chipBudget = Math.Max(0, budget - barsPerColumn[day]);
                var chips = single[day].OrderBy(o => o.Start).ThenBy(o => o.Event.Id, StringComparer.Ordinal).ToList();
                var overflow = hidden[day] + Math.Max(0, chips.Count - chipBudget);
                if (chips.Count > chipBudget)
                {
                    chips = [.. chips.Take(chipBudget)];
                }
                cells.Add(new MonthCell(date, date.Month == month, chips, overflow));
            }

            monthWeeks.Add(new MonthWeek(cells, bars, laneCount));
        }
    }

    private bool IsVisibleInMonth(SchedulerOccurrence occurrence)
    {
        if (Resources.Count == 0 || VisibleResourceIds is null)
        {
            return true;
        }
        return VisibleResources.Any(r => occurrence.Event.ResourceIds.Contains(r.Id, StringComparer.Ordinal))
            || (ShowUnassignedLane && IsUnassigned(occurrence.Event));
    }

    /// <summary>
    /// 横条浮于周行之上，故须让开其下单元内的日号按钮：
    /// 单元 4px 内边距 + 24px 按钮 + 其 2px 外边距。
    /// </summary>
    private const int monthBarsTopPx = 30;

    private static string MonthBarStyle(BandBar bar) =>
        DayBandLayout.BarStyle(bar.StartColumn, bar.Span, 7, bar.Lane, monthBarsTopPx, bar.ContinuesBefore, bar.ContinuesAfter);

    private string MonthChipLabel(SchedulerOccurrence occurrence) => occurrence.Event.IsAllDay
        ? Localizer["Scheduler.AllDay"]
        : TimeLabel(occurrence.Start);

    private static string MonthCellClass(MonthCell cell) => ClassNames.cn(
        "atom:relative atom:flex-1 atom:min-w-0 atom:border-e atom:last:border-e-0 atom:p-1 atom:text-start atom:align-top",
        cell.InMonth ? null : "atom:bg-muted/30 atom:text-muted-foreground");

    /// <summary>
    /// 将可见的全天发生（occurrence）装入时间网格上方的条带。
    /// </summary>
    /// <remarks>
    /// 无资源、或仅一类资源可见时，条带是一行日列，跨日事件即为一条
    /// 连贯横条。两类及以上可见时，条带改为每类资源各一行，而不再
    /// 仿照其下以日为主序的泳道次序：泳道依次为 day0-roomA、day0-roomB、
    /// day1-roomA，于是同一事件所占各列并不相邻，无一条横条能够跨越。
    /// 条带因此不再与网格逐列对齐，这正是把横条画对所要付的代价。
    /// 将资源过滤器收窄至一类即可恢复对齐。
    /// </remarks>
    private void BuildAllDayBand()
    {
        bandRows = [];
        var allDay = occurrences.Where(o => o.Event.IsAllDay).ToList();
        if (allDay.Count == 0 || DayCount <= 0)
        {
            return;
        }

        var visible = VisibleResources;
        var groups = new List<SchedulerResource?>();
        if (Resources.Count > 0)
        {
            groups.AddRange(visible);
            if (ShowUnassignedLane)
            {
                groups.Add(new SchedulerResource(string.Empty, Localizer["Scheduler.Unassigned"]));
            }
        }
        if (groups.Count <= 1)
        {
            // 仅一类资源或全无：单行可令条带与网格对齐。有资源时它仍然
            // 过滤，故条带所示即下方泳道所示。
            var single = BuildBandRow(groups.Count == 1 ? groups[0] : null, allDay, filterByResource: groups.Count == 1);
            if (single.Bars.Count > 0 || single.Hidden.Any(count => count > 0))
            {
                bandRows.Add(single);
            }
            return;
        }
        foreach (var resource in groups)
        {
            var row = BuildBandRow(resource, allDay, filterByResource: true);
            // 没有全天事项的资源本会得着一段仅带标签的空条带。反正条带并
            // 不与下方泳道逐列对齐，保留它也
            // 无从保全什么。
            if (row.Bars.Count > 0 || row.Hidden.Any(count => count > 0))
            {
                bandRows.Add(row);
            }
        }
    }

    private BandRow BuildBandRow(SchedulerResource? resource, List<SchedulerOccurrence> allDay, bool filterByResource)
    {
        var first = RangeDate;
        var last = RangeDate.AddDays(DayCount - 1);
        var runs = new List<(SchedulerOccurrence Occurrence, int StartColumn, int Span, bool Before, bool After, int Length)>();

        foreach (var occurrence in allDay)
        {
            if (filterByResource && !MatchesResource(occurrence.Event, resource))
            {
                continue;
            }
            // 结束端是排他的，故所覆盖的末日即最后一刻落入的那一日。
            var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrence.Start, DisplayZone).Date);
            var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrence.End.AddTicks(-1), DisplayZone).Date);
            if (endDate < first || startDate > last)
            {
                continue;
            }
            var visibleStart = startDate < first ? first : startDate;
            var visibleEnd = endDate > last ? last : endDate;
            // 比较式须自带括号：元组内裸写 "a < b, c > d" 会被解析为
            // 泛型类型 a<b, c> 再跟上 d。
            runs.Add((occurrence, visibleStart.DayNumber - first.DayNumber, visibleEnd.DayNumber - visibleStart.DayNumber + 1,
                (startDate < visibleStart), (endDate > visibleEnd), endDate.DayNumber - startDate.DayNumber));
        }

        // 先取最长、再取最早，令长段居于顶部、看上去是连续的线条。
        runs.Sort((a, b) =>
        {
            var length = b.Length.CompareTo(a.Length);
            if (length != 0)
            {
                return length;
            }
            var start = a.Occurrence.Start.CompareTo(b.Occurrence.Start);
            return start != 0 ? start : string.CompareOrdinal(a.Occurrence.Event.Id, b.Occurrence.Event.Id);
        });

        var bars = new List<BandBar>();
        var hidden = new int[DayCount];
        var occupied = new List<bool[]>();
        var laneCount = 0;
        var budget = Math.Max(1, MaxAllDayRows);

        foreach (var run in runs)
        {
            var lane = DayBandLayout.Claim(occupied, DayCount, run.StartColumn, run.Span);
            if (lane >= budget)
            {
                // 超出额度：计入该行的 "+x more"，而非任条带增高。
                for (var column = run.StartColumn; column < run.StartColumn + run.Span; column++)
                {
                    hidden[column]++;
                }
                continue;
            }
            bars.Add(new BandBar(run.Occurrence, run.StartColumn, run.Span, lane, run.Before, run.After));
            laneCount = Math.Max(laneCount, lane + 1);
        }

        return new BandRow(resource, bars, Math.Max(1, laneCount), hidden);
    }

    private bool MatchesResource(SchedulerEvent item, SchedulerResource? resource) => resource is null
        || (resource.Id.Length == 0 ? IsUnassigned(item) : item.ResourceIds.Contains(resource.Id, StringComparer.Ordinal));

    private string BandBarStyle(BandBar bar) =>
        DayBandLayout.BarStyle(bar.StartColumn, bar.Span, DayCount, bar.Lane, 2, bar.ContinuesBefore, bar.ContinuesAfter);

    private static string BandBarClass(BandBar bar) => ClassNames.cn(
        "atom:absolute atom:z-10 atom:flex atom:items-center atom:overflow-hidden atom:rounded atom:px-1.5 atom:text-start atom:text-xs atom:font-medium atom:bg-primary/10 atom:text-primary atom:hover:bg-primary/20 atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        DayBandLayout.JoinRounding(bar.ContinuesBefore, bar.ContinuesAfter));

    /// <summary>
    /// 播报整个事件及其所覆盖的每一日，而非仅可见区间内的部分——
    /// 裁切只是布局细节，不应泄漏进 ARIA 标签。
    /// </summary>
    private string BandBarLabel(BandBar bar)
    {
        var start = TimeZoneInfo.ConvertTime(bar.Occurrence.Start, DisplayZone).Date;
        var last = TimeZoneInfo.ConvertTime(bar.Occurrence.End.AddTicks(-1), DisplayZone).Date;
        return start == last
            ? Localizer["Scheduler.AllDayLabel", bar.Occurrence.Event.Title, start.ToString("D", CultureInfo.CurrentCulture)]
            : Localizer["Scheduler.AllDayRangeLabel", bar.Occurrence.Event.Title,
                start.ToString("D", CultureInfo.CurrentCulture), last.ToString("D", CultureInfo.CurrentCulture)];
    }

    /// <summary>
    /// 资源过滤器是否已把可显示之物尽数排除。月视图（Month）不建泳道
    /// 故它须直接问询过滤器，而非由泳道数目反推其为空。
    /// </summary>
    private bool NothingSelected => Resources.Count > 0
        && (IsMonth ? VisibleResources.Count == 0 && !ShowUnassignedLane : lanes.Count == 0);

    private bool IsUnassigned(SchedulerEvent item) => !item.ResourceIds.Any(id => Resources.Any(r => r.Id == id));

    /// <summary>通过过滤的资源，按声明次序。过滤器为 null 即指全部资源。</summary>
    private IReadOnlyList<SchedulerResource> VisibleResources => VisibleResourceIds is null
        ? Resources
        : [.. Resources.Where(r => VisibleResourceIds.Contains(r.Id, StringComparer.Ordinal))];

    /// <summary>
    /// 是否绘制未分配（Unassigned）泳道。无过滤器时它仅在确有所需时出现
    /// 此即既有发版行为。有过滤器时则只要用户选过它便出现——
    /// 显式选定的泳道却悄然消失，看来像是缺陷。
    /// </summary>
    private bool ShowUnassignedLane => VisibleResourceIds is null
        ? occurrences.Any(o => IsUnassigned(o.Event))
        : VisibleResourceIds.Contains(string.Empty, StringComparer.Ordinal);

    /// <summary>选择器所提供的全资，含以空字符串为键的未分配（Unassigned）。</summary>
    private IEnumerable<SelectOption<string>> ResourceFilterOptions =>
        Resources.Select(r => new SelectOption<string>(r.Id, r.Title))
            .Append(new SelectOption<string>(string.Empty, Localizer["Scheduler.Unassigned"]));

    /// <summary>null 与「全部选中」所见相同；存储 null，令 @bind 往返的即是更简的值。</summary>
    private async Task SetVisibleResourcesAsync(IEnumerable<string>? values)
    {
        VisibleResourceIds = values is null ? null : [.. values];
        RefreshSchedule();
        await VisibleResourceIdsChanged.InvokeAsync(VisibleResourceIds);
    }

    private Lane CreateLane(DateOnly date, SchedulerResource? resource, DateTimeOffset start, DateTimeOffset end)
    {
        // 全天事件已在上方条带内以横条绘制；此处再放行便会
        // 使每一枚都显示两次，且呈满高块状。
        var items = occurrences.Where(o => !o.Event.IsAllDay && o.Start < end && o.End > start
            && (resource == null || (resource.Id.Length == 0 ? IsUnassigned(o.Event) : o.Event.ResourceIds.Contains(resource.Id))))
            .OrderBy(o => o.Start).ThenByDescending(o => o.End).ToList();
        var placements = new List<Placement>();
        var group = new List<Placement>();
        var columns = new List<DateTimeOffset>();
        var groupEnd = DateTimeOffset.MinValue;
        foreach (var item in items)
        {
            if (item.Start >= groupEnd)
            {
                FinishGroup();
            }
            var column = columns.FindIndex(e => e <= item.Start);
            if (column < 0)
            {
                column = columns.Count;
                columns.Add(item.End);
            }
            else
            {
                columns[column] = item.End;
            }
            group.Add(new Placement(item, column, 1));
            if (item.End > groupEnd)
            {
                groupEnd = item.End;
            }
        }
        FinishGroup();
        return new Lane(date, resource, start, end, placements);

        void FinishGroup()
        {
            placements.AddRange(group.Select(p => p with { Columns = columns.Count }));
            group.Clear();
            columns.Clear();
            groupEnd = DateTimeOffset.MinValue;
        }
    }

    private string PlacementStyle(Lane lane, Placement placement)
    {
        var from = placement.Occurrence.Start > lane.Start ? placement.Occurrence.Start : lane.Start;
        var to = placement.Occurrence.End < lane.End ? placement.Occurrence.End : lane.End;
        // 卡片在其时间与重叠配额之内作内缩，使相邻事件彼此分离。
        var top = ((from - lane.Start).TotalMinutes / SlotMinutes * 40) + 2;
        var height = Math.Max(20, (to - from).TotalMinutes / SlotMinutes * 40) - 4;
        var left = 100.0 * placement.Column / placement.Columns;
        var width = 100.0 / placement.Columns;
        return FormattableString.Invariant($"top:{top}px;height:{height}px;inset-inline-start:calc(4.5rem + (100% - 4.5rem) * {left / 100} + 4px);width:calc((100% - 4.5rem) * {width / 100} - 8px);");
    }

    /// <summary>该槽是否既在有效时段之外、又被封闭了交互。</summary>
    private bool IsSlotBlocked(DateTimeOffset slot) => BlockOutsideActiveHours && IsSlotMuted(slot);

    /// <summary>事件所覆盖的每一分钟是否都在有效时段之内。</summary>
    private bool IsRangeAllowed(DateTimeOffset start, DateTimeOffset end)
    {
        if (!BlockOutsideActiveHours || ActiveHours.Count == 0)
        {
            return true;
        }
        // 遍历事件所覆盖的各槽：无一被封锁时该次移动方为合法。
        for (var slot = start; slot < end; slot = slot.AddMinutes(SlotMinutes))
        {
            if (IsSlotMuted(slot))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 该槽是否落于其所属星期各有效区间之外。区间以显示时区的墙上时间
    /// 计，故增减一小时的日照节约日仍沿用同一时钟边界。
    /// </summary>
    private bool IsSlotMuted(DateTimeOffset slot)
    {
        if (ActiveHours.Count == 0)
        {
            return false;
        }
        var local = TimeZoneInfo.ConvertTime(slot, DisplayZone);
        var time = TimeOnly.FromDateTime(local.DateTime);
        foreach (var range in ActiveHours)
        {
            if (range.Day != local.DayOfWeek)
            {
                continue;
            }
            // End 为 MinValue 即延展到当日之末。
            if (time >= range.Start && (range.End == TimeOnly.MinValue || time < range.End))
            {
                return false;
            }
        }
        return true;
    }

    private string TimeLabel(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, DisplayZone).ToString("t", CultureInfo.CurrentCulture);
    private string OccurrenceLabel(SchedulerOccurrence item) => $"{item.Event.Title}, {TimeZoneInfo.ConvertTime(item.Start, DisplayZone):f} – {TimeZoneInfo.ConvertTime(item.End, DisplayZone):t}";

    private async Task NavigateAsync(int direction)
    {
        // 两种周视图都按整周推进，纵仅显示五日亦然；月视图则按
        // 日历月步进而非六周，故标题与其所称的月份同步。
        Date = IsMonth
            ? Date.AddMonths(direction)
            : Date.AddDays(direction * (View == SchedulerView.Day ? 1 : 7));
        RefreshSchedule();
        await DateChanged.InvokeAsync(Date);
    }

    private async Task NavigateToTodayAsync()
    {
        // 调度器所属的日可与服务器的本地日不同。
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out var zone))
        {
            return;
        }
        Date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        RefreshSchedule();
        await DateChanged.InvokeAsync(Date);
    }

    private async Task SetViewAsync(SchedulerView view)
    {
        View = view;
        RefreshSchedule();
        await ViewChanged.InvokeAsync(view);
    }

    private string WeekStartLabel(DayOfWeek day) => Localizer["Scheduler.WeekStartDay", CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day)];

    private async Task SetFirstDayOfWeekAsync(DayOfWeek day)
    {
        if (FirstDayOfWeek == day)
        {
            return;
        }
        FirstDayOfWeek = day;
        RefreshSchedule();
        await FirstDayOfWeekChanged.InvokeAsync(day);
    }

    // 悬停底色须置换而非叠加：:hover 提升了特指度，故朴素的
    // hover:bg-muted 会在光标停留时把已选槽重绘成未选态。
    private static string SlotClass(bool selected, bool muted) => ClassNames.cn(
        muted ? "atom:bg-muted/60" : null,
        "atom:absolute atom:left-0 atom:w-full atom:border-b atom:border-dashed atom:text-start atom:text-xs atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:z-10",
        selected
            ? "atom:bg-primary/15 atom:hover:bg-primary/25 atom:ring-1 atom:ring-inset atom:ring-primary atom:text-foreground"
            : "atom:text-muted-foreground atom:hover:bg-muted/50");

    /// <summary>播报该槽之名，仅当确有资源时才带上资源——「10:00 AM for .」听来就像缺陷。</summary>
    private string SlotLabel(DateTimeOffset start, SchedulerResource? resource, bool muted = false)
    {
        var time = TimeZoneInfo.ConvertTime(start, DisplayZone).ToString("f", CultureInfo.CurrentCulture);
        var label = string.IsNullOrWhiteSpace(resource?.Title)
            ? Localizer["Scheduler.SlotLabel", time]
            : Localizer["Scheduler.SlotLabelForResource", time, resource.Title];
        // 已置灰的槽在有视力者眼中观感不同，故直言其态而非倚赖底色。
        return muted ? $"{Localizer["Scheduler.OutsideActiveHours"]} {label}" : label;
    }

    private bool IsSlotSelected(int laneIndex, DateTimeOffset start) =>
        selectedSlot is { } slot && slot.LaneIndex == laneIndex && slot.Start == start;

    /// <summary>高亮单个时间槽。键除时刻外还含泳道，故两个资源泳道中的同一时刻仍彼此区分。</summary>
    private void SelectSlot(int laneIndex, DateTimeOffset start)
    {
        if (ReadOnly || saving)
        {
            return;
        }
        selectedOccurrence = null;
        selectedDay = null;
        selectedSlot = (laneIndex, start);
    }

    private void SlotKeyDown(KeyboardEventArgs args, DateTimeOffset start, string? resourceId)
    {
        if (IsSlotBlocked(start))
        {
            return;
        }
        // Enter 用于新建——本组件引入选中之前 Enter 即是此行为。
        // Space 不作处理：浏览器把它变成 click，而 click 负责选中。
        if (args.Key is "Enter")
        {
            CreateEvent(start, resourceId);
        }
    }

    /// <summary>
    /// 在指针处打开共用菜单。ReadOnly 且无覆写内容的调度器无所
    /// 可给，故宁可不开，也不开出一个空盒。
    /// </summary>
    private async Task ShowSlotMenuAsync(MouseEventArgs args, DateTimeOffset start, SchedulerResource? resource)
    {
        if (contextMenu == null || (ReadOnly && SlotContextMenuContent == null) || saving)
        {
            return;
        }
        eventMenuContext = null;
        slotMenuContext = new SchedulerSlotMenuContext(start, start.AddMinutes(SlotMinutes), resource, DefaultSlotMenuItems(start, resource?.Id));
        await contextMenu.OpenAt(args.ClientX, args.ClientY);
    }

    private async Task ShowEventMenuAsync(MouseEventArgs args, SchedulerOccurrence occurrence)
    {
        if (contextMenu == null || (ReadOnly && EventContextMenuContent == null) || saving)
        {
            return;
        }
        slotMenuContext = null;
        eventMenuContext = new SchedulerEventMenuContext(occurrence, DefaultEventMenuItems(occurrence));
        await contextMenu.OpenAt(args.ClientX, args.ClientY);
    }

    private async Task CloseContextMenuAsync()
    {
        if (contextMenu != null)
        {
            await contextMenu.Close();
        }
    }

    /// <summary>
    /// 就某次发生（occurrence）打开删除确认而不经由编辑器
    /// 使右键菜单得以一步删除。
    /// </summary>
    private void RequestDeleteFor(SchedulerOccurrence occurrence)
    {
        if (ReadOnly || saving)
        {
            return;
        }
        editingOccurrence = occurrence;
        editScope = SchedulerEditScope.Occurrence;
        draft = occurrence.Event.Clone();
        draft.Start = occurrence.Start;
        draft.End = occurrence.End;
        editorError = null;
        deleteConfirmationOpen = true;
    }

    /// <summary>
    /// 月视图（Month）没有 30 分钟槽可供选择，故一次点击选中整日。由某日新建需要一个
    /// 时刻：取 StartHour、时长一个 SlotMinutes，即编辑器打开时所用的同一缺省。
    /// </summary>
    private bool IsDaySelected(DateOnly date) => selectedDay == date;

    private void SelectDay(DateOnly date)
    {
        if (ReadOnly || saving)
        {
            return;
        }
        selectedOccurrence = null;
        selectedSlot = null;
        selectedDay = date;
    }

    /// <summary>
    /// 月视图没有时间轴，故唯有全无有效区间的日才可称为封闭。
    /// 仅部分时段有效的日仍然开放；其单元照常在 StartHour 处新建。
    /// </summary>
    private bool IsDayBlocked(DateOnly date) => BlockOutsideActiveHours && ActiveHours.Count > 0
        && !ActiveHours.Any(range => range.Day == date.DayOfWeek);

    private DateTimeOffset DayCreateInstant(DateOnly date) => DayBoundary(date, StartHour);

    private void CreateEventOnDay(DateOnly date)
    {
        if (IsDayBlocked(date))
        {
            return;
        }
        CreateEvent(DayCreateInstant(date));
    }

    private void DayKeyDown(KeyboardEventArgs args, DateOnly date)
    {
        if (args.Key is "Enter")
        {
            CreateEventOnDay(date);
        }
    }

    private SchedulerEditorContext BuildEditorContext(RenderFragment defaultContent) =>
        new(draft!, editingOccurrence is null, editScope, defaultContent);

    private SchedulerToolbarContext BuildToolbarContext(RenderFragment defaultContent) => new()
    {
        Date = Date,
        RangeStart = RangeDate,
        Heading = RangeHeading,
        View = View,
        FirstDayOfWeek = FirstDayOfWeek,
        TimeZoneId = TimeZoneId,
        Resources = Resources,
        VisibleResourceIds = VisibleResourceIds,
        DefaultContent = defaultContent,
        Navigate = EventCallback.Factory.Create<int>(this, NavigateAsync),
        GoToToday = EventCallback.Factory.Create(this, NavigateToTodayAsync),
        SetView = EventCallback.Factory.Create<SchedulerView>(this, SetViewAsync),
        SetVisibleResources = EventCallback.Factory.Create<IReadOnlyList<string>?>(this,
            values => SetVisibleResourcesAsync(values))
    };

    private static string MonthDayButtonClass(bool selected) => ClassNames.cn(
        "atom:mb-0.5 atom:flex atom:h-6 atom:w-6 atom:items-center atom:justify-center atom:rounded atom:text-xs atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        selected ? "atom:bg-primary atom:text-primary-foreground" : "atom:hover:bg-muted");

    /// <summary>就某一时刻打开编辑器以新建约定。</summary>
    public void CreateEvent(DateTimeOffset start, string? resourceId = null)
    {
        if (ReadOnly || saving)
        {
            return;
        }
        editingOccurrence = null;
        editScope = SchedulerEditScope.Series;
        // 工厂在创建过程中保留类型；Clone 已在编辑过程中保留它。
        draft = NewEventFactory?.Invoke() ?? new SchedulerEvent();
        draft.Start = start;
        draft.End = start.AddMinutes(SlotMinutes);
        draft.TimeZoneId = TimeZoneId;
        draft.ResourceIds = string.IsNullOrEmpty(resourceId) ? [] : [resourceId];
        OpenEditor();
    }

    /// <summary>
    /// 单击只高亮事件；须经双击、Enter 或右键菜单的
    /// 编辑项方打开编辑器。时间槽行为相同
    /// 且同一时刻仅高亮一项。
    /// </summary>
    private bool IsOccurrenceSelected(SchedulerOccurrence occurrence) =>
        selectedOccurrence is { } selected && selected.Id == occurrence.Event.Id && selected.Start == occurrence.Start;

    private void SelectOccurrence(SchedulerOccurrence occurrence)
    {
        if (saving)
        {
            return;
        }
        selectedSlot = null;
        selectedDay = null;
        selectedOccurrence = (occurrence.Event.Id, occurrence.Start);
    }

    private void OccurrenceKeyDown(KeyboardEventArgs args, SchedulerOccurrence occurrence)
    {
        if (args.Key is "Enter")
        {
            EditEvent(occurrence);
        }
    }

    private static string SelectedRing(bool selected) =>
        selected ? "atom:ring-2 atom:ring-ring atom:ring-offset-1" : string.Empty;

    /// <summary>为单次发生（occurrence）或其整个序列打开独立的编辑器副本。</summary>
    public void EditEvent(SchedulerOccurrence occurrence, SchedulerEditScope scope = SchedulerEditScope.Occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (ReadOnly || saving)
        {
            return;
        }
        editingOccurrence = occurrence;
        editScope = scope;
        SetDraftForScope();
        OpenEditor();
    }

    private void SetDraftForScope()
    {
        draft = editingOccurrence!.Event.Clone();
        if (editScope == SchedulerEditScope.Occurrence)
        {
            draft.Start = editingOccurrence.Start;
            draft.End = editingOccurrence.End;
        }
        UpdateLocalTimes();
        ReadRecurrence();
    }

    private void ScopeChanged(SchedulerEditScope value)
    {
        editScope = value;
        SetDraftForScope();
    }

    private void UpdateLocalTimes()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(EditorTimeZoneId);
        localStart = TimeZoneInfo.ConvertTime(draft!.Start, zone).DateTime;
        localEnd = TimeZoneInfo.ConvertTime(draft.End, zone).DateTime;
        if (draft.IsAllDay)
        {
            // 存储的结束端是排他的；编辑器索取的是事件所覆盖的最后一日。
            localStart = localStart.Value.Date;
            localEnd = TimeZoneInfo.ConvertTime(draft.End.AddTicks(-1), zone).Date;
        }
        startResolution = ResolutionFor(draft.Start, zone);
        endResolution = ResolutionFor(draft.End, zone);
    }

    /// <summary>
    /// 在草稿于计时块与全日带之间切换，其间把边界吸附到整日本地日
    /// 使编辑器绝不显示半日长的全天事件。
    /// </summary>
    private void AllDayChanged(bool value)
    {
        if (draft == null || saving)
        {
            return;
        }
        draft.IsAllDay = value;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(EditorTimeZoneId);
        if (value)
        {
            var startDate = TimeZoneInfo.ConvertTime(draft.Start, zone).Date;
            var lastDate = TimeZoneInfo.ConvertTime(draft.End.AddTicks(-1), zone).Date;
            if (lastDate < startDate)
            {
                lastDate = startDate;
            }
            draft.Start = SchedulerEngine.StartOfDay(startDate, EditorTimeZoneId);
            draft.End = SchedulerEngine.StartOfDay(lastDate.AddDays(1), EditorTimeZoneId);
        }
        else
        {
            // 回到计时事件时，整日已无意义。退回 StartHour 处的一个槽。
            var startDate = TimeZoneInfo.ConvertTime(draft.Start, zone).Date;
            draft.Start = SchedulerEngine.ToInstant(startDate.AddHours(StartHour), EditorTimeZoneId);
            draft.End = draft.Start.AddMinutes(SlotMinutes);
        }
        UpdateLocalTimes();
    }

    private string ResolutionLabel(SchedulerAmbiguousTimeResolution value) =>
        Localizer[value == SchedulerAmbiguousTimeResolution.Earlier ? "Scheduler.EarlierOffset" : "Scheduler.LaterOffset"];

    private bool IsAmbiguousTime(DateTime? local)
    {
        if (!local.HasValue || draft == null)
        {
            return false;
        }
        return TimeZoneInfo.FindSystemTimeZoneById(EditorTimeZoneId).IsAmbiguousTime(DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified));
    }

    private static SchedulerAmbiguousTimeResolution ResolutionFor(DateTimeOffset instant, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return zone.IsAmbiguousTime(local.DateTime) && local.Offset == zone.GetAmbiguousTimeOffsets(local.DateTime).Min()
            ? SchedulerAmbiguousTimeResolution.Later : SchedulerAmbiguousTimeResolution.Earlier;
    }

    private void ReadRecurrence()
    {
        recurrenceFrequency = string.IsNullOrWhiteSpace(draft!.RecurrenceRule) ? "NONE" : "EXISTING";
        recurrenceCount = 10;
        limitRecurrence = false;
        recurrenceDays.Clear();
        if (recurrenceFrequency == "NONE")
        {
            return;
        }

        // 只暴露本编辑器能够往返复现的规则。较复杂的
        // 使用者自带计划表在其被替换之前保持原样。
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in draft.RecurrenceRule!.Split(';'))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || !fields.TryAdd(pair[0].ToUpperInvariant(), pair[1].ToUpperInvariant()))
            {
                return;
            }
        }
        if (fields.Keys.Any(key => key is not ("FREQ" or "INTERVAL" or "COUNT" or "BYDAY"))
            || !fields.TryGetValue("FREQ", out var frequency)
            || !recurrenceFrequencies.Contains(frequency, StringComparer.Ordinal) || frequency == "NONE"
            || (fields.TryGetValue("INTERVAL", out var interval) && interval != "1"))
        {
            return;
        }
        if (fields.TryGetValue("COUNT", out var count))
        {
            if (!int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            {
                return;
            }
            recurrenceCount = parsed;
            limitRecurrence = true;
        }
        if (fields.TryGetValue("BYDAY", out var days))
        {
            if (frequency != "WEEKLY")
            {
                return;
            }
            foreach (var code in days.Split(','))
            {
                var day = Array.IndexOf(recurrenceDayCodes, code);
                if (day < 0)
                {
                    recurrenceDays.Clear();
                    return;
                }
                recurrenceDays.Add((DayOfWeek)day);
            }
        }
        if (frequency == "WEEKLY" && recurrenceDays.Count == 0)
        {
            recurrenceDays.Add(TimeZoneInfo.ConvertTime(draft.Start, TimeZoneInfo.FindSystemTimeZoneById(draft.TimeZoneId)).DayOfWeek);
        }
        recurrenceFrequency = frequency;
    }

    private void FrequencyChanged(string? value)
    {
        recurrenceFrequency = value ?? "NONE";
        if (recurrenceFrequency == "WEEKLY" && recurrenceDays.Count == 0)
        {
            recurrenceDays.Add((localStart ?? DateTime.Today).DayOfWeek);
        }
        UpdateRecurrence();
    }

    private IEnumerable<DayOfWeek> RecurrenceWeekDays => Enumerable.Range(0, 7)
        .Select(offset => (DayOfWeek)(((int)FirstDayOfWeek + offset) % 7));

    private void ToggleRecurrenceDay(DayOfWeek day, bool selected)
    {
        if (selected)
        {
            recurrenceDays.Add(day);
        }
        else
        {
            recurrenceDays.Remove(day);
        }
        UpdateRecurrence();
    }

    private void UpdateRecurrence()
    {
        if (recurrenceFrequency == "NONE")
        {
            draft!.RecurrenceRule = null;
        }
        else if (recurrenceFrequency != "EXISTING" && (recurrenceFrequency != "WEEKLY" || recurrenceDays.Count > 0))
        {
            draft!.RecurrenceRule = $"FREQ={recurrenceFrequency}"
                + (recurrenceFrequency == "WEEKLY" ? ";BYDAY=" + string.Join(',', recurrenceDays.Order().Select(day => recurrenceDayCodes[(int)day])) : "")
                + (limitRecurrence ? FormattableString.Invariant($";COUNT={recurrenceCount ?? 10}") : "");
        }
    }

    private void OpenEditor()
    {
        UpdateLocalTimes();
        ReadRecurrence();
        editorError = null;
        deleteConfirmationOpen = false;
        editorOpen = true;
        StateHasChanged();
    }

    private void ToggleResource(string resourceId, bool selected)
    {
        if (selected && !draft!.ResourceIds.Contains(resourceId))
        {
            draft.ResourceIds.Add(resourceId);
        }
        else if (!selected)
        {
            draft!.ResourceIds.Remove(resourceId);
        }
    }

    private async Task SaveAsync(bool delete)
    {
        if (draft == null || saving || ReadOnly)
        {
            return;
        }
        editorError = null;
        saving = true;
        StateHasChanged();
        try
        {
            if (!delete)
            {
                if (recurrenceFrequency == "WEEKLY" && recurrenceDays.Count == 0
                    && (editingOccurrence == null || editScope == SchedulerEditScope.Series
                        || string.IsNullOrWhiteSpace(editingOccurrence.Event.RecurrenceRule)))
                {
                    editorError = Localizer["Scheduler.ChooseRepeatDay"];
                    return;
                }
                if (!localStart.HasValue || !localEnd.HasValue)
                {
                    throw new ArgumentException("Start and end are required.");
                }
                if (draft.IsAllDay)
                {
                    // 编辑器收集的是所覆盖的最后一日；事件存储的则是排他结束端。
                    var lastDay = localEnd.Value.Date;
                    if (lastDay < localStart.Value.Date)
                    {
                        lastDay = localStart.Value.Date;
                    }
                    draft.Start = SchedulerEngine.StartOfDay(localStart.Value.Date, EditorTimeZoneId);
                    draft.End = SchedulerEngine.StartOfDay(lastDay.AddDays(1), EditorTimeZoneId);
                }
                else
                {
                    draft.Start = SchedulerEngine.ToInstant(localStart.Value, EditorTimeZoneId, startResolution);
                    draft.End = SchedulerEngine.ToInstant(localEnd.Value, EditorTimeZoneId, endResolution);
                }
                SchedulerEngine.Validate(draft);
            }
            var kind = delete ? SchedulerChangeKind.Delete : editingOccurrence == null ? SchedulerChangeKind.Create : SchedulerChangeKind.Update;
            var proposed = SchedulerEngine.ApplyChange(Events, draft, kind, editScope, editingOccurrence?.Start);
            _ = SchedulerEngine.Expand(proposed, DayBoundary(RangeDate, 0), DayBoundary(RangeDate.AddDays(DayCount), 0));
            var context = new SchedulerChangeContext { Kind = kind, Scope = editScope, Event = draft.Clone(),
                OccurrenceStart = editingOccurrence?.Start, Events = proposed };
            await OnEventChange.InvokeAsync(context);
            if (context.Cancel)
            {
                editorError = Localizer["Scheduler.SaveRejected"];
                return;
            }
            await EventsChanged.InvokeAsync(proposed);
            Events = proposed;
            editorOpen = false;
            deleteConfirmationOpen = false;
            RefreshSchedule();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or TimeZoneNotFoundException or InvalidTimeZoneException or Ical.Net.Evaluation.EvaluationException)
        {
            editorError = Localizer["Scheduler.InvalidEdit"];
        }
        catch (Exception)
        {
            editorError = Localizer["Scheduler.SaveFailed"];
        }
        finally
        {
            saving = false;
        }
    }

    private void SetEditorOpen(bool value)
    {
        if (!saving)
        {
            editorOpen = value;
            if (!value)
            {
                deleteConfirmationOpen = false;
            }
        }
    }

    private void RequestDelete()
    {
        if (editingOccurrence == null || saving || ReadOnly)
        {
            return;
        }
        editorError = null;
        deleteConfirmationOpen = true;
    }

    private void SetDeleteConfirmationOpen(bool value)
    {
        if (!saving)
        {
            restoreDeleteFocus = deleteConfirmationOpen && !value && editorOpen;
            deleteConfirmationOpen = value;
        }
    }

    private Task ConfirmDeleteAsync() => deleteConfirmationOpen ? SaveAsync(true) : Task.CompletedTask;

    /// <summary>核验可见的发生（occurrence）、泳道与吸附边界后，提交浏览器的拖拽/缩放。应用一般使用指针界面或 EditEvent。</summary>
    [JSInvokable]
    public async Task CommitInteractionAsync(int renderedRevision, string eventId, long occurrenceStart, int sourceLaneIndex,
        int targetLaneIndex, long startMilliseconds, long endMilliseconds, string action)
    {
        if (disposed || ReadOnly || saving || editorOpen || renderedRevision != revision
            || (action == "move" ? !AllowDrag : !AllowResize || action is not ("start" or "end"))
            || sourceLaneIndex < 0 || sourceLaneIndex >= lanes.Count || targetLaneIndex < 0 || targetLaneIndex >= lanes.Count)
        {
            return;
        }
        var source = lanes[sourceLaneIndex];
        var target = lanes[targetLaneIndex];
        var occurrence = source.Placements.Select(p => p.Occurrence).FirstOrDefault(o => o.Event.Id == eventId && o.Start.ToUnixTimeMilliseconds() == occurrenceStart);
        if (occurrence == null)
        {
            return;
        }
        var originalStart = occurrence.Start.ToUnixTimeMilliseconds();
        var originalEnd = occurrence.End.ToUnixTimeMilliseconds();
        var slot = SlotMinutes * 60_000L;
        var laneStart = target.Start.ToUnixTimeMilliseconds();
        var laneEnd = target.End.ToUnixTimeMilliseconds();
        if (startMilliseconds >= endMilliseconds)
        {
            return;
        }
        if (action == "move")
        {
            var earliest = laneStart - Math.Max(0, source.Start.ToUnixTimeMilliseconds() - originalStart);
            if (startMilliseconds < earliest || startMilliseconds >= laneEnd || endMilliseconds <= laneStart
                || endMilliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() || startMilliseconds < DateTimeOffset.MinValue.ToUnixTimeMilliseconds()
                || endMilliseconds - startMilliseconds != originalEnd - originalStart
                || (startMilliseconds - laneStart) % slot != 0)
            {
                return;
            }
        }
        else
        {
            if (sourceLaneIndex != targetLaneIndex || endMilliseconds - startMilliseconds < slot)
            {
                return;
            }
            var edge = action == "start" ? startMilliseconds : endMilliseconds;
            if (edge < laneStart || edge > laneEnd || (edge - laneStart) % slot != 0
                || (action == "start" ? endMilliseconds != originalEnd || originalStart < laneStart : startMilliseconds != originalStart || originalEnd > laneEnd))
            {
                return;
            }
        }
        if (originalStart == startMilliseconds && originalEnd == endMilliseconds && sourceLaneIndex == targetLaneIndex)
        {
            return;
        }
        // 封锁亦须顾及手势动作，否则事件可被径直拖入已封闭的槽。
        if (!IsRangeAllowed(DateTimeOffset.FromUnixTimeMilliseconds(startMilliseconds),
                DateTimeOffset.FromUnixTimeMilliseconds(endMilliseconds)))
        {
            return;
        }
        editingOccurrence = occurrence;
        editScope = SchedulerEditScope.Occurrence;
        draft = occurrence.Event.Clone();
        draft.Start = DateTimeOffset.FromUnixTimeMilliseconds(startMilliseconds);
        draft.End = DateTimeOffset.FromUnixTimeMilliseconds(endMilliseconds);
        if (action == "move" && source.Resource?.Id != target.Resource?.Id)
        {
            if (string.IsNullOrEmpty(target.Resource?.Id))
            {
                draft.ResourceIds.Clear();
            }
            else
            {
                if (source.Resource != null)
                {
                    draft.ResourceIds.Remove(source.Resource.Id);
                }
                if (!draft.ResourceIds.Contains(target.Resource.Id))
                {
                    draft.ResourceIds.Add(target.Resource.Id);
                }
            }
        }
        UpdateLocalTimes();
        ReadRecurrence();
        await SaveAsync(false);
        if (editorError != null)
        {
            editorOpen = true;
        }
        StateHasChanged();
    }

    /// <summary>全天事件在条带内的一段，已装入某条泳道（lane）。</summary>
    private sealed record BandBar(SchedulerOccurrence Occurrence, int StartColumn, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter);

    /// <summary>
    /// 全天条带的一行。两类及以上资源可见时每类各占一行，故跨日
    /// 横条得以覆盖相邻的日列；其下以日为主序的泳道排布无从做到。
    /// </summary>
    private sealed record BandRow(SchedulerResource? Resource, List<BandBar> Bars, int LaneCount, int[] Hidden);

    /// <summary>月视图（Month）网格中的一个日单元。</summary>
    private sealed record MonthCell(DateOnly Date, bool InMonth, List<SchedulerOccurrence> Chips, int HiddenCount);

    /// <summary>月视图（Month）网格的一个周行：其各单元，以及浮于其上的跨日横条。</summary>
    private sealed record MonthWeek(List<MonthCell> Cells, List<BandBar> Bars, int LaneCount);

    private sealed record Lane(DateOnly Date, SchedulerResource? Resource, DateTimeOffset Start, DateTimeOffset End, List<Placement> Placements);
    private sealed record Placement(SchedulerOccurrence Occurrence, int Column, int Columns);
}
