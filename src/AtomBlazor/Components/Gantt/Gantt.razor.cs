#pragma warning disable BL0016 // 与一致保留 ScrollToAsync 未守卫 interop：chart/ganttModule 双 null 门前置于调用，公开 API 由调用方页面自持断线语义；采文件级抑制以免逐点 pragma 打散与逐行对齐的行位（照 Dock／ResizablePanelGroup 先例）
using AtomBlazor.Primitives.Gantt;
using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>
/// 一张依时间轴绘制的计划：一侧是任务列表，每项对面各有一根横条
/// 箭头则标出何事须先行。
/// </summary>
/// <typeparam name="TItem">源项的类型。</typeparam>
/// <remarks>
/// <para>
/// 任务列表与时间轴是同一张表，而非两块须设法同步的滚动窗格。读甘特
/// 图须横贯整行——这个名称、那根横条——两块滚动窗格若要就某行位置
/// 达成一致唯有互相度量；共用同一行，便无从产生分歧。
/// </para>
/// <para>
/// 一切位置皆在 C# 中依列宽与槽宽算出，故图表在服务器、WebAssembly
/// 与打印样式表中皆绘出同貌。任何内容都不在浏览器
/// 中测量。
/// </para>
/// <para>
/// 有子项的任务即汇总项：其日期与进度取自其下各项工作，而非自带。
/// 无时长的任务即里程碑，以记号绘出。
/// </para>
/// </remarks>
public partial class Gantt<TItem> : ComponentBase, IAsyncDisposable
{
    private readonly List<GanttColumn<TItem>> columns = [];

    /// <summary>
    /// 被拖拽覆写过的列宽，按列键记录。
    /// </summary>
    /// <remarks>
    /// 存于此处而非列声明之上：子组件的参数归属写标记之人
    /// 从外面去设，下次父组件渲染时便会被改了回去。
    /// </remarks>
    private readonly Dictionary<string, int> widths = new(StringComparer.Ordinal);

    private readonly string chartId = $"atom-gantt-{Guid.NewGuid():N}";

    private HashSet<string> collapsed = new(StringComparer.Ordinal);
    private GanttChart<TItem>? chart;
    private ElementReference rootElement;
    private DotNetObjectReference<Gantt<TItem>>? selfRef;
    private IJSObjectReference? columnsModule;
    private IJSObjectReference? ganttModule;
    private bool jsReady;
    private bool seeded;
    private bool stale = true;
    private bool scrolled;

    // 重建改动了图表而 DOM 仍显示旧图时置位。JavaScript 是在其后一
    // 趟接线的，而非重建的当趟。
    private bool redrawPending;
    private string? buildError;
    private string? sortKey;
    private bool sortDescending;

    /// <summary>获取或设置任务，次序不限。</summary>
    [Parameter]
    public IEnumerable<TItem>? Data { get; set; }

    /// <summary>获取或设置任务标识符的读取器。须在全集内唯一。</summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, string> IdSelector { get; set; } = default!;

    /// <summary>获取或设置任务名称的读取器。</summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, string> TextSelector { get; set; } = default!;

    /// <summary>获取或设置任务开始时间的读取器。</summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, DateTimeOffset> StartSelector { get; set; } = default!;

    /// <summary>获取或设置任务结束时间的读取器。</summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, DateTimeOffset> EndSelector { get; set; } = default!;

    /// <summary>
    /// 获取或设置本任务所属上级任务标识符的读取器。平面
    /// 任务表请留空不置。
    /// </summary>
    [Parameter]
    public Func<TItem, string?>? ParentIdSelector { get; set; }

    /// <summary>获取或设置任务完成度的读取器，取 0 至 1。</summary>
    [Parameter]
    public Func<TItem, double>? ProgressSelector { get; set; }

    /// <summary>
    /// 获取或设置横条颜色的读取器，取任意 CSS 颜色。null 用主题之色。
    /// </summary>
    [Parameter]
    public Func<TItem, string?>? BarColorSelector { get; set; }

    /// <summary>
    /// 获取或设置任务列表各列。留空则图表显示一列
    /// 任务名称。
    /// </summary>
    [Parameter]
    public RenderFragment? Columns { get; set; }

    /// <summary>获取或设置绘制于任务之间的箭头。</summary>
    [Parameter]
    public IEnumerable<GanttDependency>? Dependencies { get; set; }

    /// <summary>获取或设置是否绘制依赖箭头。</summary>
    [Parameter]
    public bool ShowDependencies { get; set; } = true;

    /// <summary>获取或设置时间轴单槽所容纳的时长。</summary>
    [Parameter]
    public GanttZoom Zoom { get; set; } = GanttZoom.Day;

    /// <summary>获取或设置缩放变更时触发的回调。</summary>
    [Parameter]
    public EventCallback<GanttZoom> ZoomChanged { get; set; }

    /// <summary>获取或设置是否显示工具栏。</summary>
    [Parameter]
    public bool ShowToolbar { get; set; } = true;

    /// <summary>获取或设置工具栏是否提供各缩放档位。</summary>
    [Parameter]
    public bool ShowZoomControls { get; set; } = true;

    /// <summary>
    /// 获取或设置单槽绘制宽度（像素）。null 则依缩放档位取宽。
    /// </summary>
    [Parameter]
    public int? SlotWidth { get; set; }

    /// <summary>获取或设置时间轴的显式左边界。</summary>
    [Parameter]
    public DateTimeOffset? RangeStart { get; set; }

    /// <summary>获取或设置时间轴的显式右边界。</summary>
    [Parameter]
    public DateTimeOffset? RangeEnd { get; set; }

    /// <summary>获取或设置时间轴越过任务向外延展的幅度。</summary>
    [Parameter]
    public GanttRangeSnap RangeSnap { get; set; } = GanttRangeSnap.Major;

    /// <summary>获取或设置被置灰为非工作的星期。null 置灰周六与周日。</summary>
    [Parameter]
    public IReadOnlyCollection<DayOfWeek>? NonWorkingDays { get; set; }

    /// <summary>获取或设置是否置灰非工作日。</summary>
    [Parameter]
    public bool ShowNonWorkingDays { get; set; } = true;

    /// <summary>获取或设置是否以一条线标出当前时刻。</summary>
    [Parameter]
    public bool ShowToday { get; set; } = true;

    /// <summary>获取或设置图表首次绘制时是否滚动到今天。</summary>
    [Parameter]
    public bool ScrollToToday { get; set; } = true;

    /// <summary>获取或设置单行绘制高度（像素）。</summary>
    [Parameter]
    public int RowHeight { get; set; } = 36;

    /// <summary>获取或设置表头单层绘制高度（像素）。</summary>
    [Parameter]
    public int HeaderRowHeight { get; set; } = 30;

    /// <summary>
    /// 获取或设置其子项被隐藏的任务标识符。
    /// </summary>
    /// <remarks>
    /// 与 <c>DataGrid</c> 的 <c>ExpandedNodes</c> 相反，此处命名的是收拢集而非展开
    /// 集：计划本为敞开而读，空集正是读者最先想要的状态。
    /// </remarks>
    [Parameter]
    public HashSet<string>? CollapsedIds { get; set; }

    /// <summary>获取或设置分支开合时触发的回调。</summary>
    [Parameter]
    public EventCallback<HashSet<string>> CollapsedIdsChanged { get; set; }

    /// <summary>获取或设置是否所有分支初始敞开。置 false 则初始收拢。</summary>
    [Parameter]
    public bool DefaultExpandAll { get; set; } = true;

    /// <summary>获取或设置汇总项的日期与进度是否取自其子项。</summary>
    [Parameter]
    public bool RollUpSummaries { get; set; } = true;

    /// <summary>获取或设置横条是否可沿时间轴拖拽。</summary>
    [Parameter]
    public bool AllowDrag { get; set; }

    /// <summary>获取或设置是否可拖动横条两端以改变其长度。</summary>
    [Parameter]
    public bool AllowResize { get; set; }

    /// <summary>
    /// 获取或设置是否可拖动横条内的填充来设定任务的完成度。
    /// </summary>
    [Parameter]
    public bool AllowProgressDrag { get; set; }

    /// <summary>
    /// 获取或设置是否可由一根横条拖向另一根来绘制依赖。
    /// </summary>
    /// <remarks>
    /// 起拖的那一端与落点的那一端共同决定两者间的类型，故四种
    /// 类型皆出于同一手势，而非出于询问你本意为何的菜单。
    /// </remarks>
    [Parameter]
    public bool AllowLinking { get; set; }

    /// <summary>
    /// 获取或设置依赖被绘出时触发的回调。置 <c>Cancel</c> 可拒绝之。
    /// </summary>
    /// <remarks>
    /// 与任务拖拽相同，这是征询而非写入：新依赖进入图表所得的
    /// 集合之后，箭头才会现身。
    /// </remarks>
    [Parameter]
    public EventCallback<GanttDependencyContext<TItem>> OnDependencyCreate { get; set; }

    /// <summary>
    /// 获取或设置箭头被点击时触发的回调——删除箭头即由此处进行。
    /// </summary>
    /// <remarks>
    /// 仅在 <see cref="AllowLinking"/> 开启时可达，因为无人能改的箭头
    /// 本就不该让人误打误撞地点到。
    /// </remarks>
    [Parameter]
    public EventCallback<GanttDependency> OnDependencyClick { get; set; }

    /// <summary>
    /// 获取或设置拖拽是否落在完整的槽上。
    /// </summary>
    /// <remarks>
    /// 缺省开启。以天议定的计划，不该因为指针松手之处
    /// 便是那里，而带回一个 09:47 的开始时间。
    /// </remarks>
    [Parameter]
    public bool SnapToSlot { get; set; } = true;

    /// <summary>
    /// 获取或设置横条被拖拽时触发的回调。置 <c>Cancel</c> 可拒绝之。
    /// </summary>
    /// <remarks>
    /// 图表不写任务。请存储新日期并交回改动后的
    /// 集合，否则横条径直弹回原处。
    /// </remarks>
    [Parameter]
    public EventCallback<GanttChangeContext<TItem>> OnTaskChange { get; set; }

    /// <summary>获取或设置任务被点击（在列表中或横条上）时触发的回调。</summary>
    [Parameter]
    public EventCallback<TItem> OnTaskClick { get; set; }

    /// <summary>
    /// 获取或设置图表下方是否显示图形图例。
    /// </summary>
    /// <remarks>
    /// 缺省开启，且只列图表实际所绘：无里程碑则无
    /// 里程碑条目，无所置灰则无置灰条目。
    /// </remarks>
    [Parameter]
    public bool ShowLegend { get; set; } = true;

    /// <summary>
    /// 获取或设置悬停横条时是否显示载有其日期、时长与进度的卡片。
    /// </summary>
    /// <remarks>
    /// 卡片是悬停时由 CSS 显示的朴素标记，非浮动遮层。甘特图可容
    /// 数百条横条，而在 Blazor Server 上每次指针掠过都去问电路该显示
    /// 什么，卡片到手时指针早已移开。
    /// </remarks>
    [Parameter]
    public bool ShowTooltip { get; set; } = true;

    /// <summary>获取或设置悬停卡片的内容，取代默认各行为。</summary>
    [Parameter]
    public RenderFragment<GanttRow<TItem>>? TooltipTemplate { get; set; }

    /// <summary>
    /// 获取或设置行是否可在任务列表中上下拖拽。
    /// </summary>
    /// <remarks>
    /// 落于两行之间即重排序；落于某行中段即改换其父。两者皆以
    /// <see cref="OnTaskMove"/> 送达，因为只有调用方知道自己保持的次序是
    /// 列表次序、排序字段还是数据库中的某一列。
    /// </remarks>
    [Parameter]
    public bool AllowRowDrag { get; set; }

    /// <summary>
    /// 获取或设置行被落往他处时触发的回调。置 <c>Cancel</c> 可
    /// 拒绝之。
    /// </summary>
    [Parameter]
    public EventCallback<GanttMoveContext<TItem>> OnTaskMove { get; set; }

    /// <summary>获取或设置横条内部所绘内容，取代默认填充。</summary>
    [Parameter]
    public RenderFragment<GanttRow<TItem>>? BarTemplate { get; set; }

    /// <summary>
    /// 获取或设置任务名称是否书写在其横条之旁。
    /// </summary>
    /// <remarks>
    /// 缺省关闭。任务列表固定于一侧，名称已在其行上，再来
    /// 第二份便是头一个碍着横条的东西。
    /// </remarks>
    [Parameter]
    public bool ShowTaskLabels { get; set; }

    /// <summary>获取或设置是否可拖动列缘以改变列宽。</summary>
    [Parameter]
    public bool Resizable { get; set; } = true;

    /// <summary>获取或设置被拖列的最小宽度（像素）。</summary>
    [Parameter]
    public int MinColumnWidth { get; set; } = 60;

    /// <summary>获取或设置滚动区域高度，取 CSS 长度值。null 则任其伸展。</summary>
    [Parameter]
    public string? Height { get; set; }

    /// <summary>获取或设置是否以加载指示取代图表。</summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>获取或设置无可绘制内容时所显示的消息。</summary>
    [Parameter]
    public string? EmptyMessage { get; set; }

    /// <summary>获取或设置屏幕阅读器为本图表播报的标签。</summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>获取或设置根元素的附加类。</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置图表构建完成后触发的回调。
    /// </summary>
    /// <remarks>
    /// 在父级标记里经 <c>@ref</c> 读 <see cref="Chart"/> 恒慢一拍
    /// 因为父级标记写就之时本组件尚未运行。请用此回调。
    /// </remarks>
    [Parameter]
    public EventCallback<GanttChart<TItem>?> OnBuilt { get; set; }

    /// <summary>获取或设置摊派（splat）到根元素的特性。</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    /// <summary>
    /// 获取图表最近一次构建之所得；无可构建之物时为 null。
    /// </summary>
    public GanttChart<TItem>? Chart => chart;

    /// <summary>获取任务列表各列，按声明次序。</summary>
    internal IReadOnlyList<GanttColumn<TItem>> DeclaredColumns => columns;

    /// <summary>获取承载展开器与缩进的列。</summary>
    internal GanttColumn<TItem>? TreeColumn =>
        columns.FirstOrDefault(c => c.IsTree) ?? columns.FirstOrDefault();

    /// <summary>获取任务列表的宽度（像素）。</summary>
    internal int TreeWidth => columns.Sum(c => c.EffectiveWidth);

    /// <summary>获取单槽绘制宽度（像素）。</summary>
    internal int EffectiveSlotWidth => SlotWidth ?? Zoom switch
    {
        GanttZoom.Hour => 36,
        GanttZoom.Day => 34,
        GanttZoom.Week => 58,
        GanttZoom.Month => 74,
        GanttZoom.Quarter => 82,
        _ => 88,
    };

    /// <summary>获取横条上是否开启了任何手势。</summary>
    private bool Interactive => AllowDrag || AllowResize || AllowProgressDrag || AllowLinking || AllowRowDrag;

    /// <summary>获取列的绘制宽度；拖其列缘可覆写。</summary>
    /// <param name="column">该列。</param>
    /// <returns>宽度（像素）。</returns>
    internal int WidthOf(GanttColumn<TItem> column) =>
        widths.TryGetValue(column.EffectiveKey, out var width) ? width : column.Width;

    /// <summary>登记本图 <c>Columns</c> 中声明的一列。</summary>
    /// <param name="column">该列。</param>
    internal void Register(GanttColumn<TItem> column)
    {
        columns.Add(column);
        Invalidate();
    }

    /// <summary>将一列撤出。</summary>
    /// <param name="column">该列。</param>
    internal void Unregister(GanttColumn<TItem> column)
    {
        columns.Remove(column);
        Invalidate();
    }

    /// <summary>
    /// 标记图表需要重新构建。
    /// </summary>
    /// <remarks>
    /// 构建并非无偿，故它在下次渲染之前只做一次，而非每有子组件报来
    /// 一个变动的参数便重来一遍。
    /// </remarks>
    internal void Invalidate()
    {
        stale = true;
        StateHasChanged();
    }

    /// <summary>开启或收拢一个分支。</summary>
    /// <param name="id">要开合之任务的标识符。</param>
    internal async Task ToggleAsync(string id)
    {
        if (!collapsed.Remove(id))
        {
            collapsed.Add(id);
        }

        await NotifyCollapsedAsync();
    }

    /// <summary>展开所有分支。</summary>
    public async Task ExpandAllAsync()
    {
        collapsed.Clear();
        await NotifyCollapsedAsync();
    }

    /// <summary>收拢所有分支。</summary>
    public async Task CollapseAllAsync()
    {
        collapsed = Summaries();
        await NotifyCollapsedAsync();
    }

    /// <summary>更改单槽所容纳的时长。</summary>
    /// <param name="zoom">新的缩放档位。</param>
    public async Task SetZoomAsync(GanttZoom zoom)
    {
        if (Zoom == zoom)
        {
            return;
        }

        Zoom = zoom;
        scrolled = false;
        Invalidate();

        if (ZoomChanged.HasDelegate)
        {
            await ZoomChanged.InvokeAsync(zoom);
        }
    }

    /// <summary>滚动时间轴，使当前时刻进入视野。</summary>
    public async Task GoToTodayAsync() => await ScrollToAsync(DateTimeOffset.Now);

    /// <summary>滚动时间轴，使某一时刻进入视野。</summary>
    /// <param name="value">要滚动至的时刻。</param>
    public async Task ScrollToAsync(DateTimeOffset value)
    {
        if (chart is null || ganttModule is null)
        {
            return;
        }

        var x = TreeWidth + (chart.Axis.Position(value) * EffectiveSlotWidth);
        await ganttModule.InvokeVoidAsync("scrollTo", rootElement, x, TreeWidth);
    }

    /// <summary>
    /// 记录拖拽落定的列宽。由 JavaScript 调用。
    /// </summary>
    /// <param name="columnId">被拖列的键。</param>
    /// <param name="dragged">拖拽结束时各受管列的宽度。</param>
    [JSInvokable]
    public void OnResizeCompleted(string columnId, Dictionary<string, double> dragged)
    {
        ArgumentNullException.ThrowIfNull(dragged);

        foreach (var (key, width) in dragged)
        {
            widths[key] = (int)Math.Round(width);
        }

        // 表须依新的任务列表宽度重新度量，否则横条仍留在
        // 旧宽度所置之处。
        Invalidate();
    }

    /// <summary>
    /// 上报一次横条拖拽的完成。由 JavaScript 调用。
    /// </summary>
    /// <param name="id">被拖任务的标识符。</param>
    /// <param name="action">握住了横条的哪个部位。</param>
    /// <param name="slots">移动的距离，以槽计。</param>
    [JSInvokable]
    public async Task OnBarDragged(string id, string action, double slots)
    {
        if (chart is null || slots == 0)
        {
            return;
        }

        var row = chart.Rows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        if (row is null)
        {
            return;
        }

        var kind = action switch
        {
            "start" => GanttChangeKind.ResizeStart,
            "end" => GanttChangeKind.ResizeEnd,
            _ => GanttChangeKind.Move,
        };

        var from = row.OffsetSlots;
        var to = row.OffsetSlots + row.LengthSlots;

        switch (kind)
        {
            case GanttChangeKind.Move:
                from += slots;
                to += slots;
                break;
            case GanttChangeKind.ResizeStart:
                from = Math.Min(from + slots, to);
                break;
            default:
                to = Math.Max(to + slots, from);
                break;
        }

        var context = new GanttChangeContext<TItem>
        {
            Kind = kind,
            Item = row.Item,
            Row = row,
            Start = chart.Axis.At(from),
            End = chart.Axis.At(to),
            Progress = row.Progress,
        };

        if (OnTaskChange.HasDelegate)
        {
            await OnTaskChange.InvokeAsync(context);
        }

        // 两种结局都要重绘：获准则调用方的新日期已入 Data；被拒则
        // 横条须回到拖拽起始之处。
        Invalidate();
    }

    /// <summary>
    /// 上报一次横条填充拖拽的完成。由 JavaScript 调用。
    /// </summary>
    /// <param name="id">被拖任务的标识符。</param>
    /// <param name="fraction">拖拽要求任务达到的完成度，取 0 至 1。</param>
    [JSInvokable]
    public async Task OnProgressDragged(string id, double fraction)
    {
        var row = chart?.Rows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        if (row is null)
        {
            return;
        }

        if (OnTaskChange.HasDelegate)
        {
            await OnTaskChange.InvokeAsync(new GanttChangeContext<TItem>
            {
                Kind = GanttChangeKind.Progress,
                Item = row.Item,
                Row = row,
                Start = row.Start,
                End = row.End,
                Progress = Math.Clamp(fraction, 0, 1),
            });
        }

        Invalidate();
    }

    /// <summary>
    /// 上报在两横条之间绘出的一条依赖。由 JavaScript 调用。
    /// </summary>
    /// <param name="fromId">拖拽起点所在任务的标识符。</param>
    /// <param name="fromAnchor">拖拽自其哪一端起手。</param>
    /// <param name="toId">拖拽落点所在任务的标识符。</param>
    /// <param name="toAnchor">拖拽终于其哪一端。</param>
    [JSInvokable]
    public async Task OnLinkDrawn(string fromId, string fromAnchor, string toId, string toAnchor)
    {
        var from = chart?.Rows.FirstOrDefault(r => string.Equals(r.Id, fromId, StringComparison.Ordinal));
        var to = chart?.Rows.FirstOrDefault(r => string.Equals(r.Id, toId, StringComparison.Ordinal));

        if (from is null || to is null || ReferenceEquals(from, to))
        {
            return;
        }

        var leaving = string.Equals(fromAnchor, "start", StringComparison.Ordinal);
        var arriving = string.Equals(toAnchor, "start", StringComparison.Ordinal);

        // 手势所触及的两端，正是其名由以构成的两端。
        var type = (leaving, arriving) switch
        {
            (true, true) => GanttDependencyType.StartToStart,
            (true, false) => GanttDependencyType.StartToFinish,
            (false, false) => GanttDependencyType.FinishToFinish,
            _ => GanttDependencyType.FinishToStart,
        };

        if (OnDependencyCreate.HasDelegate)
        {
            await OnDependencyCreate.InvokeAsync(new GanttDependencyContext<TItem>
            {
                Dependency = new GanttDependency(from.Id, to.Id, type),
                From = from,
                To = to,
            });
        }

        Invalidate();
    }

    /// <summary>
    /// 上报一行被落往任务列表中的他处。由 JavaScript 调用。
    /// </summary>
    /// <param name="id">被拖任务的标识符。</param>
    /// <param name="targetId">其落于之任务的标识符。</param>
    /// <param name="position">落于目标之前、之后还是之内。</param>
    [JSInvokable]
    public async Task OnRowDropped(string id, string targetId, string position)
    {
        var row = chart?.Rows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        var target = chart?.Rows.FirstOrDefault(r => string.Equals(r.Id, targetId, StringComparison.Ordinal));

        // 任务不可落入自身的分支：那会使其成为自己的祖先，而其由以
        // 绘制的树也就不再是树。
        if (row is null || target is null || ReferenceEquals(row, target) || Descends(target, row))
        {
            Invalidate();
            return;
        }

        var where = position switch
        {
            "before" => GanttDropPosition.Before,
            "inside" => GanttDropPosition.Inside,
            _ => GanttDropPosition.After,
        };

        if (OnTaskMove.HasDelegate)
        {
            await OnTaskMove.InvokeAsync(new GanttMoveContext<TItem>
            {
                Item = row.Item,
                Row = row,
                Target = target,
                Position = where,
                NewParentId = where == GanttDropPosition.Inside ? target.Id : target.ParentId,
            });
        }

        Invalidate();
    }

    /// <summary>判断某行是否位于另一行的下方某处。</summary>
    /// <param name="row">可能居于下方的行。</param>
    /// <param name="ancestor">可能居于其上的行。</param>
    /// <returns>确在其下时为 <see langword="true"/>。</returns>
    private bool Descends(GanttRow<TItem> row, GanttRow<TItem> ancestor)
    {
        var parent = row.ParentId;
        while (parent is { Length: > 0 })
        {
            if (string.Equals(parent, ancestor.Id, StringComparison.Ordinal))
            {
                return true;
            }

            parent = chart?.Rows.FirstOrDefault(r => string.Equals(r.Id, parent, StringComparison.Ordinal))?.ParentId;
        }

        return false;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (CollapsedIds is not null && !ReferenceEquals(CollapsedIds, collapsed))
        {
            collapsed = new HashSet<string>(CollapsedIds, StringComparer.Ordinal);
        }

        stale = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (stale)
        {
            var previous = chart;
            var previousError = buildError;
            Rebuild();

            // 按值比较而非按引用。重建总是产出新图表，故引用测试
            // 恒为"已变"——而 OnBuilt 会对处理它的任何人调用 StateHasChanged
            // 其重渲染又再设本组件的参数，于是又标记为过期，于是又重建。
            // 那个循环无物可止，它会把 Server 上的电路与 WebAssembly
            // 上的标签页一并挂死。
            var chartChanged = !SameChart(previous, chart);
            if (chartChanged || !string.Equals(previousError, buildError, StringComparison.Ordinal))
            {
                if (chartChanged && OnBuilt.HasDelegate)
                {
                    await OnBuilt.InvokeAsync(chart);
                }

                // 屏幕上仍是本次重建之前的图表——首次渲染时那是空消息
                // 其内根本没有图表元素。此时接线 JavaScript 便是把
                // 一个并非元素的东西交给它。请再请求一次渲染，并在其后
                // 一趟、DOM 已与构建相符时接线。
                redrawPending = true;
                StateHasChanged();
                return;
            }
        }

        // 首次挂载需要同重建一样的完整接线，故搭载同一枚标志而非另
        // 立一枚。firstRender 仅在恰一趟为 true 且无法向后
        // 传递；此标志可以。
        if (firstRender)
        {
            redrawPending = true;
        }

        // 只有装配真正用过它之后才清除。调用之前便消耗它正是那个
        // 缺陷：中途退出的一趟丢掉了下一趟所需的那个信号，于是列
        // 缩放手柄画了出来却从未接线。它只在声明了列的图表上
        // 现身，因为列在图表建成之后失效重建正是产生
        // 中途退出那一趟的缘由。
        if (await SetUpJsAsync(redrawPending))
        {
            redrawPending = false;
        }
    }

    /// <summary>
    /// 为图表接线 JavaScript。
    /// </summary>
    /// <param name="redrawn">自上次接线以来图表是否又重画过。</param>
    /// <returns>
    /// 接线执行时为 <see langword="true"/>。中途退出则为 <see langword="false"/>
    /// 以便调用方把重画信号留给能用上它的那一趟。
    /// </returns>
    private async Task<bool> SetUpJsAsync(bool redrawn)
    {
        // 此处每一种都在绘制图表之外的东西，JavaScript 所需的元素
        // 并不在文档中。只检查图表已建成，正是把红横幅放上演示页
        // 的缘由：图表可以在 C# 里已存在，而标记仍在显示
        // 转圈、空消息或错误。
        if (chart is null || chart.IsEmpty || Loading || buildError is not null)
        {
            return false;
        }

        // 然后是直接检验，因为上面一切只推断元素存在。列在图表已
        // 建成之后注册会调用 Invalidate，其后一趟可在图表分支绘制
        // 之前抵达此处——这是状态检查看不见的交错。未被任何渲染指派的
        // ElementReference 其 Id 为 null，把那样的东西交给 JavaScript
        // 正是"addEventListener is not a function"的成因。
        // 过问引用本身则无从被竞态所乘。
        if (string.IsNullOrEmpty(rootElement.Id))
        {
            return false;
        }

        try
        {
            if (!jsReady)
            {
                selfRef = DotNetObjectReference.Create(this);
                ganttModule = await JsModules.GetAsync(Js, "./_content/AtomBlazor/js/gantt.js");
                jsReady = true;
            }

            if (Interactive)
            {
                await ganttModule!.InvokeVoidAsync("initialize", rootElement, selfRef);
            }

            // 仅在重画之后：在 Blazor Server 上这里每次调用都是电路往返
            // 而一趟渲染未曾替换的手柄早已接好了线。
            if (redrawn && Resizable && columns.Count > 0)
            {
                columnsModule ??= await JsModules.GetAsync(Js, "./_content/AtomBlazor/js/table-columns.js");
                await columnsModule.InvokeVoidAsync("initColumnResize", rootElement, selfRef, chartId, MinColumnWidth);
                await columnsModule.InvokeVoidAsync("setupResizeHandles", chartId);
            }

            if (ScrollToToday && !scrolled)
            {
                scrolled = true;
                await GoToTodayAsync();
            }

            return true;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            // 电路在渲染中途离去；已无物可接线。
        }
        catch (JSException)
        {
            // 模块加载失败，或调用被浏览器拒绝。捕获它才能让接线问题
            // 不至于变成高悬于一张绘制完好图表之上的红横幅：
            // 手势打了折扣，计划依然可读。
        }

        // 按"未执行"上报，好让后来的趟次再试一次，而不至于让图表
        // 留着一堆无人接线的手柄。
        return false;
    }

    /// <summary>
    /// 判断两次构建所绘是否为同一张图表。
    /// </summary>
    /// <param name="left">上次构建的图表，可为 null。</param>
    /// <param name="right">刚构建的图表，可为 null。</param>
    /// <returns>读者或处理方无从察觉任何差异时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// <para>
    /// 本方法遍历各行，与产出这些行的重建已经做过的工作同一量级
    /// 故其花费是对缓存中已有之物多走一趟，而非第二次构建。
    /// 一见差异即止。
    /// </para>
    /// <para>
    /// 逐一精确比较而非经由哈希：哈希花费相仿却有把两张不同的图表
    /// 说成相同的小概率，而漏报一次 <c>OnBuilt</c> 是沉默的错误
    /// 多报一次不过是白费功夫。
    /// </para>
    /// <para>
    /// 有意不比较源项。改动图表不绘制的字段并非图表
    /// 之变，而各列反正要从调用方自己的标记重渲染。
    /// </para>
    /// </remarks>
    private static bool SameChart(GanttChart<TItem>? left, GanttChart<TItem>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left.TaskCount != right.TaskCount
            || left.Rows.Count != right.Rows.Count
            || left.Links.Count != right.Links.Count)
        {
            return false;
        }

        if (left.Axis.Zoom != right.Axis.Zoom
            || left.Axis.Start != right.Axis.Start
            || left.Axis.End != right.Axis.End
            || !left.Axis.Minor.SequenceEqual(right.Axis.Minor)
            || !left.Axis.Major.SequenceEqual(right.Axis.Major))
        {
            return false;
        }

        for (var i = 0; i < left.Rows.Count; i++)
        {
            if (!SameRow(left.Rows[i], right.Rows[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < left.Links.Count; i++)
        {
            // GanttLink 是 record，故此比较其所载的每一端与每一位置。
            if (left.Links[i] != right.Links[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameRow(GanttRow<TItem> left, GanttRow<TItem> right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal)
        && string.Equals(left.Text, right.Text, StringComparison.Ordinal)
        && left.Start == right.Start
        && left.End == right.End
        && left.Progress.Equals(right.Progress)
        && left.Depth == right.Depth
        && left.ChildCount == right.ChildCount
        && left.IsSummary == right.IsSummary
        && left.IsMilestone == right.IsMilestone
        && left.IsExpanded == right.IsExpanded
        && left.OffsetSlots.Equals(right.OffsetSlots)
        && left.LengthSlots.Equals(right.LengthSlots);

    private void Rebuild()
    {
        stale = false;
        buildError = null;

        if (Data is null || IdSelector is null || TextSelector is null || StartSelector is null || EndSelector is null)
        {
            chart = null;
            return;
        }

        if (!seeded && !DefaultExpandAll && CollapsedIds is null)
        {
            // 由父标识符算出而非由已建图表算出，故首次渲染便已收拢
            // 而非先展开再猛地合上。
            collapsed = Summaries();
            seeded = true;
        }

        try
        {
            chart = GanttBuilder.Build(new GanttSource<TItem>
            {
                Items = Data,
                Id = IdSelector,
                ParentId = ParentIdSelector,
                Text = TextSelector,
                Start = StartSelector,
                End = EndSelector,
                Progress = ProgressSelector,
                Order = SortComparer(),
                Dependencies = ShowDependencies ? Dependencies : null,
                Zoom = Zoom,
                Snap = RangeSnap,
                RangeStart = RangeStart,
                RangeEnd = RangeEnd,
                NonWorkingDays = ShowNonWorkingDays ? NonWorkingDays : [],
                Labels = new GanttLabels
                {
                    WeekFormat = Localizer["Gantt.Week"],
                    QuarterFormat = Localizer["Gantt.Quarter"],
                },
                Collapsed = collapsed,
                RollUpSummaries = RollUpSummaries,
            });
        }
        catch (ArgumentException error)
        {
            // 无法绘制的计划是声明问题而非缺陷。请在标记中陈明
            // 而非抛出异常——在 Blazor Server 上抛出会拖垮整个电路。
            chart = null;
            buildError = error.Message;
        }
    }

    private HashSet<string> Summaries()
    {
        var parents = new HashSet<string>(StringComparer.Ordinal);
        if (Data is null || ParentIdSelector is null)
        {
            return parents;
        }

        foreach (var item in Data)
        {
            if (ParentIdSelector(item) is { Length: > 0 } parent)
            {
                parents.Add(parent);
            }
        }

        return parents;
    }

    private Comparer<TItem>? SortComparer()
    {
        if (sortKey is null)
        {
            return null;
        }

        var column = columns.FirstOrDefault(c => string.Equals(c.EffectiveKey, sortKey, StringComparison.Ordinal));
        if (column is null)
        {
            return null;
        }

        var read = column.Value ?? (item => TextSelector(item));
        var inner = column.Comparer ?? Comparer<object?>.Default;
        var sign = sortDescending ? -1 : 1;

        return Comparer<TItem>.Create((a, b) => sign * inner.Compare(read(a), read(b)));
    }

    private async Task SortByAsync(GanttColumn<TItem> column)
    {
        if (string.Equals(sortKey, column.EffectiveKey, StringComparison.Ordinal))
        {
            // 升序、降序，然后回到任务到达时的次序。
            if (!sortDescending)
            {
                sortDescending = true;
            }
            else
            {
                sortKey = null;
                sortDescending = false;
            }
        }
        else
        {
            sortKey = column.EffectiveKey;
            sortDescending = false;
        }

        Invalidate();
        await Task.CompletedTask;
    }

    private async Task NotifyCollapsedAsync()
    {
        Invalidate();

        if (CollapsedIdsChanged.HasDelegate)
        {
            await CollapsedIdsChanged.InvokeAsync(new HashSet<string>(collapsed, StringComparer.Ordinal));
        }
    }

    private async Task ClickAsync(GanttRow<TItem> row)
    {
        if (OnTaskClick.HasDelegate)
        {
            await OnTaskClick.InvokeAsync(row.Item);
        }
    }

    private async Task ClickLinkAsync(GanttLink link)
    {
        if (OnDependencyClick.HasDelegate)
        {
            await OnDependencyClick.InvokeAsync(link.Dependency);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>释放本图表所建立的 JavaScript 处理器。</summary>
    /// <returns>处理器清除完毕即完成的任务。</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        try
        {
            if (ganttModule is not null && jsReady)
            {
                await ganttModule.InvokeVoidAsync("dispose", rootElement);
            }
        }
        catch (JSDisconnectedException)
        {
            // 电路先走一步，处理器已随之而去。
        }
        catch (ObjectDisposedException)
        {
            // 还是如此，只是某个运行时的报法不同。
        }

        selfRef?.Dispose();
        selfRef = null;
    }
}
