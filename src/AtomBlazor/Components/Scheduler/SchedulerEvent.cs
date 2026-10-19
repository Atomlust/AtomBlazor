using Microsoft.AspNetCore.Components;
namespace AtomBlazor;

/// <summary>计时约定。Start 与 End 均为时刻；重复规则依 TimeZoneId 的墙上时间展开。</summary>
/// <remarks>
/// 若需携带自有字段——描述、议程、客户 ID——请从本类型。覆写
/// <see cref="Clone"/> 以返回你的类型并复制你的字段，并给调度器一枚
/// <c>NewEventFactory</c>，使新建事件是你的类型而非本基类。
/// <code>
/// public sealed class MyEvent : SchedulerEvent
/// {
/// public string? Description { get; set; }
///
/// public override SchedulerEvent Clone()
/// {
/// var copy = new MyEvent { Description = Description };
/// CopyTo(copy);
/// return copy;
/// }
/// }
/// </code>
/// </remarks>
public class SchedulerEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    /// <summary>IANA 时区，例如 America/New_York。缺省为 UTC。</summary>
    public string TimeZoneId { get; set; } = "UTC";
    /// <summary>
    /// 将事件绘制为全日带而非计时块。此时 Start 与 End 按 TimeZoneId 中的
    /// 日期读取，其时间分量一律忽略。<b>End 是排他的</b>：19 日单日的
    /// 全天事件自 19 日延续至 20 日。
    /// </summary>
    public bool IsAllDay { get; set; }
    public List<string> ResourceIds { get; set; } = [];
    /// <summary>不带 RRULE: 前缀的 RFC 5545 RRULE。null 表示无重复。</summary>
    public string? RecurrenceRule { get; set; }
    /// <summary>本序列中已省略的原始发生（occurrence）起始时刻。</summary>
    public HashSet<DateTimeOffset> ExcludedStarts { get; set; } = [];
    /// <summary>独立编辑之单次发生（occurrence）所属的序列 ID。</summary>
    public string? SeriesId { get; set; }
    /// <summary>独立编辑之单次发生（occurrence）的原始起始时刻。</summary>
    public DateTimeOffset? RecurrenceId { get; set; }

    /// <summary>
    /// 创建供编辑器使用的独立副本，连同资源与例外集合。
    /// </summary>
    /// <remarks>
    /// 类型须覆写本方法，否则每次编辑都会悄然丢弃你的自有字段。覆写中
    /// 请调用 <see cref="CopyTo"/>，如此便只需顾及自有字段。
    /// </remarks>
    public virtual SchedulerEvent Clone()
    {
        var copy = new SchedulerEvent();
        CopyTo(copy);
        return copy;
    }

    /// <summary>把本基类所持有的每个字段复制到 <paramref name="target"/> 上。</summary>
    protected void CopyTo(SchedulerEvent target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Id = Id;
        target.Title = Title;
        target.Start = Start;
        target.End = End;
        target.TimeZoneId = TimeZoneId;
        target.IsAllDay = IsAllDay;
        target.ResourceIds = [.. ResourceIds];
        target.RecurrenceRule = RecurrenceRule;
        target.ExcludedStarts = [.. ExcludedStarts];
        target.SeriesId = SeriesId;
        target.RecurrenceId = RecurrenceId;
    }
}

/// <summary>
/// 编辑器的草稿，用于在内置字段之旁渲染你的自有字段。
/// </summary>
/// <param name="Draft">
/// 正在编辑的事件——独立副本，故写在此处的内容在取消时即被丢弃。若你从
/// <see cref="SchedulerEvent"/> ，请把它转为你的类型。
/// </param>
/// <param name="IsNew">编辑器为新建而非编辑而打开时为 true。</param>
/// <param name="Scope">本次编辑作用于单次发生（occurrence）还是整个序列。</param>
/// <param name="DefaultContent">
/// 内置字段。渲染它即保留它们并在其周围增补自有项；不渲染
/// 便将其全部取代。
/// </param>
public sealed record SchedulerEditorContext(
    SchedulerEvent Draft,
    bool IsNew,
    SchedulerEditScope Scope,
    RenderFragment DefaultContent);

/// <summary>
/// 调度器在某个工作日所突出的时段。落于该日所有区间之外的槽
/// 会被置灰。
/// </summary>
/// <remarks>
/// 置灰仅为视觉呈现——槽仍可点击，仍可在其上创建事件。
/// 同一日记两段区间即可置灰其间空档，例如午休。全无
/// 区间的日整日置灰。
/// </remarks>
/// <param name="Day">该区间所适用的工作日。</param>
/// <param name="Start">首个被突出的墙上时间，取调度器的显示时区。</param>
/// <param name="End">
/// 被突出区间的末端，排他。日终午夜请用 <see cref="TimeOnly.MinValue"/>
/// ——<c>TimeOnly</c> 无法表示 24:00。
/// </param>
public sealed record SchedulerDayHours(DayOfWeek Day, TimeOnly Start, TimeOnly End);

/// <summary>
/// 编辑器提供的一个时区，名称由你自定。
/// </summary>
/// <param name="Id">
/// 形如 <c>Australia/Sydney</c> 的 IANA 标识符。经校验——你无法靠改名
/// 凭空造出一个不存在的时区。
/// </param>
/// <param name="Title">呈现给使用者的标签。取 "Sydney" 而非 "Australia/Sydney"。</param>
public sealed record SchedulerTimeZone(string Id, string Title);

/// <summary>以自身独立调度泳道呈现的资源。</summary>
public sealed record SchedulerResource(string Id, string Title);

/// <summary>已实体化的一次发生（occurrence）。Event 指向其源序列或独立事件。</summary>
public sealed record SchedulerOccurrence(SchedulerEvent Event, DateTimeOffset Start, DateTimeOffset End);

public enum SchedulerView { Day, Week, WorkWeek, Month }
public enum SchedulerEditScope { Occurrence, Series }
public enum SchedulerChangeKind { Create, Update, Delete }
public enum SchedulerAmbiguousTimeResolution { Earlier, Later }

/// <summary>拟议的事件改动。原子地持久化 Events，或置 Cancel 以留住编辑器。</summary>
public sealed class SchedulerChangeContext
{
    public required SchedulerChangeKind Kind { get; init; }
    public required SchedulerEditScope Scope { get; init; }
    public required SchedulerEvent Event { get; init; }
    public DateTimeOffset? OccurrenceStart { get; init; }
    public required IReadOnlyList<SchedulerEvent> Events { get; init; }
    public bool Cancel { get; set; }
}

/// <summary>
/// 槽的右键菜单开于何物之上，以及调度器本会显示的菜单项。
/// </summary>
/// <param name="Start">该槽起点时刻。</param>
/// <param name="End">该槽终点时刻——其后一个 <c>SlotMinutes</c>。</param>
/// <param name="Resource">该槽所属的资源泳道；调度器无资源时为 null。</param>
/// <param name="DefaultItems">
/// 内置菜单项。渲染它即保留它们并在其周围增补自有项；不渲染
/// 便整个取代该菜单。
/// </param>
public sealed record SchedulerSlotMenuContext(
    DateTimeOffset Start,
    DateTimeOffset End,
    SchedulerResource? Resource,
    RenderFragment DefaultItems);

/// <summary>
/// 事件的右键菜单开于何物之上，以及调度器本会显示的菜单项。
/// </summary>
/// <param name="Occurrence">被右键点击的那次发生（occurrence）。</param>
/// <param name="DefaultItems">
/// 内置菜单项。渲染它即保留它们并在其周围增补自有项；不渲染
/// 便整个取代该菜单。
/// </param>
public sealed record SchedulerEventMenuContext(
    SchedulerOccurrence Occurrence,
    RenderFragment DefaultItems);

/// <summary>
/// 取代调度器工具栏所需的状况与指令。
/// </summary>
/// <remarks>
/// 有意保持精简。<c>Date</c>、<c>View</c>、<c>FirstDayOfWeek</c> 与
/// <c>VisibleResourceIds</c> 本就都是可双向绑定的参数，故自定义工具栏能以
/// 自身状态经 <c>@bind-</c> 驱动它们。此处只收无法由外部表达的
/// 指令：<see cref="Navigate"/> 知晓当前视图的步长（一日、七日、
/// 一月），<see cref="GoToToday"/> 则按调度器所在时区而非
/// 服务器时区来判定“今天”。
/// </remarks>
public sealed class SchedulerToolbarContext
{
    /// <summary>所绑定的日期。</summary>
    public required DateOnly Date { get; init; }
    /// <summary>当前视图所渲染的首日——周或月网格之起点。</summary>
    public required DateOnly RangeStart { get; init; }
    /// <summary>内置工具栏所显示的标题：月视图（Month）为月份名，其余为完整日期。</summary>
    public required string Heading { get; init; }
    public required SchedulerView View { get; init; }
    public required DayOfWeek FirstDayOfWeek { get; init; }
    public required string TimeZoneId { get; init; }
    public required IReadOnlyList<SchedulerResource> Resources { get; init; }
    /// <summary>null 显示全部资源；空列表不显示任何资源。</summary>
    public required IReadOnlyList<string>? VisibleResourceIds { get; init; }
    /// <summary>内置工具栏。渲染它即保留它并在其周围增补；不渲染便整个取代它。</summary>
    public required RenderFragment DefaultContent { get; init; }
    /// <summary>将视图后退或前进。传 -1 或 1；步长随视图而定。</summary>
    public required EventCallback<int> Navigate { get; init; }
    /// <summary>前往调度器所在时区的今天，并保持当前视图。</summary>
    public required EventCallback GoToToday { get; init; }
    public required EventCallback<SchedulerView> SetView { get; init; }
    public required EventCallback<IReadOnlyList<string>?> SetVisibleResources { get; init; }
}
