using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 日期范围选择器组件：双月历视图，可选快捷预设侧栏与应用/清除按钮。
/// </summary>
public partial class DateRangePicker : ComponentBase
{
    /// <summary>控件的可访问名称。</summary>
    [Parameter] public string? AriaLabel { get; set; }


    [Inject] private ILocalizer Localizer { get; set; } = default!;

    private bool _isOpen;
    private DateTime _displayMonth1;
    private DateTime _displayMonth2;
    private DateTime? _selectionStart;
    private DateTime? _selectionEnd;

    // OnParametersSet 守卫
    private DateRange? _previousValue;

    // 缓存字段
    private CultureInfo _resolvedCulture = CultureInfo.CurrentCulture;
    private DayOfWeek _cachedFirstDayOfWeek;
    private List<List<DateTime?>>? _cachedWeeksMonth1;
    private List<List<DateTime?>>? _cachedWeeksMonth2;

    // ShouldRender 跟踪字段
    private bool _parametersChanged;
    private bool _lastIsOpen;
    private DateTime _lastDisplayMonth1;
    private DateTime _lastDisplayMonth2;
    private DateTime? _lastSelectionStart;
    private DateTime? _lastSelectionEnd;
    private DateRange? _lastValue;
    private DateTime? _lastMinDate;
    private DateTime? _lastMaxDate;
    private bool _lastShowTwoMonths;
    private bool _lastShowPresets;
    private bool _lastShowButtons;
    private bool _lastAutoApply;
    private bool _lastDisabled;
    private IReadOnlyList<DateRangeQuickPick>? _lastPresets;

    /// <summary>
    /// 当前选中的日期范围。
    /// </summary>
    [Parameter]
    public DateRange? Value { get; set; }

    /// <summary>
    /// 日期范围变化时的回调。
    /// </summary>
    [Parameter]
    public EventCallback<DateRange?> ValueChanged { get; set; }

    /// <summary>
    /// 可选日期的下界。
    /// </summary>
    [Parameter]
    public DateTime? MinDate { get; set; }

    /// <summary>
    /// 可选日期的上界。
    /// </summary>
    [Parameter]
    public DateTime? MaxDate { get; set; }

    /// <summary>
    /// 判断某个日期是否被禁用的函数。
    /// </summary>
    [Parameter]
    public Func<DateTime, bool>? DisabledDates { get; set; }

    /// <summary>
    /// 为日历中的指定日按钮返回附加 CSS 类的函数
    /// 与内置日类合成。
    /// </summary>
    [Parameter]
    public Func<DateTime, string?>? DayClassFunc { get; set; }

    /// <summary>
    /// 可选模板，在每个日历日按钮内渲染自定义内容。
    /// 上下文经 <see cref="CalendarDayContext.IsInRange"/> 携带范围状态。
    /// 为 null 时渲染日号。
    /// </summary>
    [Parameter]
    public RenderFragment<CalendarDayContext>? DayTemplate { get; set; }

    /// <summary>
    /// 最少必须选中的天数。
    /// </summary>
    [Parameter]
    public int? MinDays { get; set; }

    /// <summary>
    /// 最多允许选中的天数。
    /// </summary>
    [Parameter]
    public int? MaxDays { get; set; }

    /// <summary>
    /// 是否并排显示两个月份。
    /// </summary>
    [Parameter]
    public bool ShowTwoMonths { get; set; } = true;

    /// <summary>
    /// 是否显示预设日期范围。
    /// </summary>
    [Parameter]
    public bool ShowPresets { get; set; } = true;

    /// <summary>
    /// 自定义快捷预设列表。为 null 时显示内置默认预设。
    /// 同时支持内置预设（经 <see cref="DateRangeQuickPick.FromPreset"/> 或自
    /// <see cref="DateRangePreset"/> 的隐式转换）与完全自定义条目
    /// （经 <see cref="DateRangeQuickPick.Custom"/>）。
    /// </summary>
    [Parameter]
    public IReadOnlyList<DateRangeQuickPick>? Presets { get; set; }

    private static readonly IReadOnlyList<DateRangeQuickPick> defaultPresets = new List<DateRangeQuickPick>
    {
        DateRangePreset.Today,
        DateRangePreset.Yesterday,
        DateRangePreset.Last7Days,
        DateRangePreset.Last30Days,
        DateRangePreset.ThisMonth,
        DateRangePreset.LastMonth
    };

    private IReadOnlyList<DateRangeQuickPick> EffectivePresets => Presets ?? defaultPresets;

    /// <summary>
    /// 每周的第一天。默认为当前文化的一周首日。
    /// </summary>
    [Parameter]
    public DayOfWeek? FirstDayOfWeek { get; set; }

    private DayOfWeek EffectiveFirstDayOfWeek => FirstDayOfWeek ?? _resolvedCulture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>
    /// 显示用的日期格式。默认为 "d"（随文化区的短日期）。
    /// </summary>
    [Parameter]
    public string DateFormat { get; set; } = "d";

    /// <summary>
    /// 未选择范围时的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    private string EffectivePlaceholder => Placeholder ?? Localizer["DateRangePicker.Placeholder"];

    /// <summary>
    /// 覆盖文化默认值的自定义月名数组。必须为 12 个元素。
    /// </summary>
    [Parameter]
    public string[]? MonthNames { get; set; }

    /// <summary>
    /// 覆盖文化默认值的自定义星期缩写数组。必须为 7 个元素、自周日起排。
    /// </summary>
    [Parameter]
    public string[]? CustomDayNames { get; set; }

    /// <summary>
    /// 选择器是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 触发按钮的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 是否在浮层底部显示清除与应用按钮。
    /// 为 <c>false</c> 时，选完的合法范围会自动应用（视同 <see cref="AutoApply"/>
    /// 为 <c>true</c>），因为没有应用按钮可点。默认为 <c>true</c>。
    /// </summary>
    [Parameter]
    public bool ShowButtons { get; set; } = true;

    /// <summary>
    /// 选出完整合法范围后是否自动应用，无需点击应用按钮。
    /// 自动应用时浮层会关闭。<see cref="ShowButtons"/> 为 <c>false</c> 时
    /// 自动应用恒开启。默认为 <c>false</c>。
    /// </summary>
    [Parameter]
    public bool AutoApply { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string[] DayNames => BuildDayNames();

    private string[] BuildDayNames()
    {
        if (CustomDayNames is { Length: 7 })
        {
            var start = (int)EffectiveFirstDayOfWeek;
            var result = new string[7];
            for (var i = 0; i < 7; i++)
            {
                result[i] = CustomDayNames[(start + i) % 7];
            }
            return result;
        }

        var cultureNames = _resolvedCulture.DateTimeFormat.AbbreviatedDayNames;
        var startIdx = (int)EffectiveFirstDayOfWeek;
        var cultureDays = new string[7];
        for (var i = 0; i < 7; i++)
        {
            cultureDays[i] = cultureNames[(startIdx + i) % 7];
        }
        return cultureDays;
    }

    // 预计算 CSS 类常量——每次渲染省去约 168 次 ClassNames.cn()/TailwindMerge 调用
    private const string cellEmpty = "atom:h-9 atom:w-9 atom:flex-1 atom:text-center atom:text-sm atom:p-0";
    private const string cellDefault = "atom:h-9 atom:w-9 atom:flex-1 atom:text-center atom:text-sm atom:p-0 atom:relative";
    private const string cellInRange = "atom:h-9 atom:w-9 atom:flex-1 atom:text-center atom:text-sm atom:p-0 atom:relative atom:bg-accent";
    private const string cellRangeStart = "atom:h-9 atom:w-9 atom:flex-1 atom:text-center atom:text-sm atom:p-0 atom:relative atom:rounded-s-md atom:bg-accent";
    private const string cellRangeEnd = "atom:h-9 atom:w-9 atom:flex-1 atom:text-center atom:text-sm atom:p-0 atom:relative atom:rounded-e-md atom:bg-accent";

    private const string dayBase = "atom:inline-flex atom:h-9 atom:w-full atom:items-center atom:justify-center atom:rounded-md atom:text-sm atom:font-normal atom:ring-offset-background atom:transition-colors atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2 atom:disabled:pointer-events-none atom:disabled:opacity-50";
    private const string dayDefault = dayBase + " atom:hover:bg-accent atom:hover:text-accent-foreground";
    private const string dayDisabled = dayBase + " atom:text-muted-foreground atom:opacity-50";
    private const string dayRangeEndpoint = dayBase + " atom:bg-primary atom:text-primary-foreground atom:hover:bg-primary atom:hover:text-primary-foreground";
    private const string dayInRange = dayBase + " atom:bg-accent atom:text-accent-foreground";
    private const string dayToday = dayBase + " atom:bg-accent atom:text-accent-foreground";

    private bool CanApply => _selectionStart.HasValue && _selectionEnd.HasValue;

    protected override void OnInitialized()
    {
        _resolvedCulture = CultureInfo.CurrentCulture;
        InitializeDisplayMonths();
        if (Value != null)
        {
            _selectionStart = Value.Start;
            _selectionEnd = Value.End;
        }
        _previousValue = Value;
    }

    protected override void OnParametersSet()
    {
        _parametersChanged = true;

        // 仅当 Value 真正来自外部变更时才同步
        if (Value != _previousValue)
        {
            _previousValue = Value;
            _selectionStart = Value?.Start;
            _selectionEnd = Value?.End;
        }

        // 在参数设置时点捕获文化，保证跨渲染周期稳定
        var currentCulture = CultureInfo.CurrentCulture;
        if (!ReferenceEquals(_resolvedCulture, currentCulture) && _resolvedCulture.Name != currentCulture.Name)
        {
            _resolvedCulture = currentCulture;
            _cachedWeeksMonth1 = null;
            _cachedWeeksMonth2 = null;
        }

        // FirstDayOfWeek 变化时失效缓存
        if (_cachedFirstDayOfWeek != EffectiveFirstDayOfWeek)
        {
            _cachedFirstDayOfWeek = EffectiveFirstDayOfWeek;
            _cachedWeeksMonth1 = null;
            _cachedWeeksMonth2 = null;
        }
    }

    private void InitializeDisplayMonths()
    {
        if (Value != null)
        {
            _displayMonth1 = new DateTime(Value.Start.Year, Value.Start.Month, 1);
        }
        else
        {
            _displayMonth1 = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }
        _displayMonth2 = _displayMonth1.AddMonths(1);
    }

    private string FormatRange(DateRange range) =>
        $"{range.Start.ToString(DateFormat, _resolvedCulture)} - {range.End.ToString(DateFormat, _resolvedCulture)}";

    private void PreviousMonth()
    {
        _displayMonth1 = _displayMonth1.AddMonths(-1);
        _displayMonth2 = _displayMonth2.AddMonths(-1);
        _cachedWeeksMonth1 = null;
        _cachedWeeksMonth2 = null;
    }

    private void NextMonth()
    {
        _displayMonth1 = _displayMonth1.AddMonths(1);
        _displayMonth2 = _displayMonth2.AddMonths(1);
        _cachedWeeksMonth1 = null;
        _cachedWeeksMonth2 = null;
    }

    private async Task SelectDate(DateTime date)
    {
        if (IsDateDisabled(date))
        {
            return;
        }

        if (!_selectionStart.HasValue || (_selectionStart.HasValue && _selectionEnd.HasValue))
        {
            // 开启新一轮选择
            _selectionStart = date;
            _selectionEnd = null;
        }
        else
        {
            // 完成选择
            _selectionEnd = date;

            // 保证起点早于终点
            if (_selectionEnd < _selectionStart)
            {
                (_selectionStart, _selectionEnd) = (_selectionEnd, _selectionStart);
            }

            // 校验范围约束
            var days = (_selectionEnd.Value - _selectionStart.Value).Days + 1;
            if (MinDays.HasValue && days < MinDays.Value)
            {
                // 重置选择
                _selectionStart = date;
                _selectionEnd = null;
                return;
            }
            if (MaxDays.HasValue && days > MaxDays.Value)
            {
                // 重置选择
                _selectionStart = date;
                _selectionEnd = null;
                return;
            }

            // 自动应用完成的选择（按钮隐藏时恒开启
            // 因为没有应用按钮可确认）
            if (AutoApply || !ShowButtons)
            {
                await Apply();
            }
        }
    }

    private async Task Apply()
    {
        if (_selectionStart.HasValue && _selectionEnd.HasValue)
        {
            var range = DateRange.Create(_selectionStart.Value, _selectionEnd.Value);
            Value = range;
            await ValueChanged.InvokeAsync(range);
            _isOpen = false;
        }
    }

    /// <summary>
    /// 清除已选日期。
    /// </summary>
    public async Task Clear()
    {
        _selectionStart = null;
        _selectionEnd = null;
        Value = null;
        await ValueChanged.InvokeAsync(null);
 // 此处的末尾 StateHasChanged 按 BL0012 先例移除（口径）：
        // 事件处理器触发的重渲染由框架自动完成。
    }

    // 日历草稿即事实来源，包括按下应用按钮之前。
    // 可空索引让原生 select 在无匹配时显示「自定义/占位」而不是选中第 0 项。
    private int? SelectedPresetIndex
    {
        get
        {
            var presets = EffectivePresets;
            for (var i = 0; i < presets.Count; i++)
            {
                if (IsSelectedPreset(presets[i]))
                {
                    return i;
                }
            }
            return null;
        }
    }

    private async Task OnPresetSelectChanged(int? index)
    {
        // 即使约束拒绝了所请求的预设，也要让原生 select 重新对齐。
        _parametersChanged = true;
        if (index is int selected && selected >= 0 && selected < EffectivePresets.Count)
        {
            await ApplyPreset(EffectivePresets[selected]);
        }
    }

    private async Task ApplyPreset(DateRangeQuickPick quickPick)
    {
        var range = ResolveRange(quickPick);
        if (range != null && CountSelectedDays(range.Start, range.End) > 0)
        {
            _selectionStart = range.Start;
            _selectionEnd = range.End;
            _displayMonth1 = new DateTime(range.Start.Year, range.Start.Month, 1);
            _displayMonth2 = _displayMonth1.AddMonths(1);
            _cachedWeeksMonth1 = null;
            _cachedWeeksMonth2 = null;

            // 自动应用预设范围（按钮隐藏时恒开启
            // 因为没有应用按钮可确认）
            if (AutoApply || !ShowButtons)
            {
                await Apply();
            }
        }
    }

    private static DateRange? ResolveRange(DateRangeQuickPick quickPick) =>
        quickPick.Preset.HasValue ? GetPresetRange(quickPick.Preset.Value) : quickPick.RangeFactory?.Invoke();

    private string ResolveLabel(DateRangeQuickPick quickPick) =>
        quickPick.Preset.HasValue ? GetPresetLabel(quickPick.Preset.Value) : quickPick.Label ?? string.Empty;

    private static DateRange? GetPresetRange(DateRangePreset preset)
    {
        var today = DateTime.Today;
        return preset switch
        {
            DateRangePreset.Today => new DateRange(today, today),
            DateRangePreset.Yesterday => new DateRange(today.AddDays(-1), today.AddDays(-1)),
            DateRangePreset.Last7Days => new DateRange(today.AddDays(-6), today),
            DateRangePreset.Last30Days => new DateRange(today.AddDays(-29), today),
            DateRangePreset.ThisMonth => new DateRange(new DateTime(today.Year, today.Month, 1),
                                                        new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1)),
            DateRangePreset.LastMonth => new DateRange(new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                                                        new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            DateRangePreset.ThisYear => new DateRange(new DateTime(today.Year, 1, 1),
                                                       new DateTime(today.Year, 12, 31)),
            _ => null
        };
    }

    private string GetPresetLabel(DateRangePreset preset) => preset switch
    {
        DateRangePreset.Today => Localizer["DateRangePicker.Today"],
        DateRangePreset.Yesterday => Localizer["DateRangePicker.Yesterday"],
        DateRangePreset.Last7Days => Localizer["DateRangePicker.Last7Days"],
        DateRangePreset.Last30Days => Localizer["DateRangePicker.Last30Days"],
        DateRangePreset.ThisMonth => Localizer["DateRangePicker.ThisMonth"],
        DateRangePreset.LastMonth => Localizer["DateRangePicker.LastMonth"],
        DateRangePreset.ThisYear => Localizer["DateRangePicker.ThisYear"],
        _ => Localizer["DateRangePicker.Custom"]
    };

    private bool IsDateDisabled(DateTime date)
    {
        if (MinDate.HasValue && date < MinDate.Value.Date)
        {
            return true;
        }

        if (MaxDate.HasValue && date > MaxDate.Value.Date)
        {
            return true;
        }

        if (DisabledDates != null && DisabledDates(date))
        {
            return true;
        }

        return false;
    }

    private int CountSelectedDays(DateTime start, DateTime end)
    {
        var s = start.Date <= end.Date ? start.Date : end.Date;
        var e = start.Date > end.Date ? start.Date : end.Date;

        // 快路径：无禁用日期回调——用 O(1) 算术
        if (DisabledDates == null && !MinDate.HasValue && !MaxDate.HasValue)
        {
            return (e - s).Days + 1;
        }

        var count = 0;
        var current = s;
        while (current <= e)
        {
            if (!IsDateDisabled(current))
            {
                count++;
            }
            current = current.AddDays(1);
        }
        return count;
    }

    private bool IsInRange(DateTime date)
    {
        if (!_selectionStart.HasValue)
        {
            return false;
        }

        if (_selectionEnd.HasValue)
        {
            var start = _selectionStart.Value < _selectionEnd.Value ? _selectionStart.Value : _selectionEnd.Value;
            var end = _selectionStart.Value > _selectionEnd.Value ? _selectionStart.Value : _selectionEnd.Value;
            return date.Date >= start.Date && date.Date <= end.Date;
        }

        return date.Date == _selectionStart.Value.Date;
    }

    private bool IsRangeStart(DateTime date)
    {
        if (!_selectionStart.HasValue)
        {
            return false;
        }

        if (!_selectionEnd.HasValue)
        {
            return date.Date == _selectionStart.Value.Date;
        }

        var start = _selectionStart.Value < _selectionEnd.Value ? _selectionStart.Value : _selectionEnd.Value;
        return date.Date == start.Date;
    }

    private bool IsRangeEnd(DateTime date)
    {
        if (!_selectionEnd.HasValue)
        {
            return false;
        }

        var end = _selectionStart!.Value > _selectionEnd.Value ? _selectionStart.Value : _selectionEnd.Value;
        return date.Date == end.Date;
    }

    private List<List<DateTime?>> GetWeeksInMonth1() =>
        _cachedWeeksMonth1 ??= BuildWeeksInMonth(_displayMonth1);

    private List<List<DateTime?>> GetWeeksInMonth2() =>
        _cachedWeeksMonth2 ??= BuildWeeksInMonth(_displayMonth2);

    private List<List<DateTime?>> BuildWeeksInMonth(DateTime monthDate)
    {
        var weeks = new List<List<DateTime?>>();
        var firstOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

        var startDate = firstOfMonth;
        while (startDate.DayOfWeek != EffectiveFirstDayOfWeek)
        {
            startDate = startDate.AddDays(-1);
        }

        var currentDate = startDate;
        while (currentDate <= lastOfMonth || weeks.Count < 6)
        {
            var week = new List<DateTime?>();
            for (var i = 0; i < 7; i++)
            {
                if (currentDate.Month == monthDate.Month)
                {
                    week.Add(currentDate);
                }
                else
                {
                    week.Add(null);
                }
                currentDate = currentDate.AddDays(1);
            }
            weeks.Add(week);

            if (currentDate.Month != monthDate.Month && weeks.Count >= 5)
            {
                break;
            }
        }

        return weeks;
    }

    private string GetMonthName(int month) => MonthNames is { Length: 12 }
        ? MonthNames[month - 1]
        : _resolvedCulture.DateTimeFormat.GetMonthName(month);

    private string ButtonCssClass => ClassNames.cn(
        ShowTwoMonths ? "atom:w-[300px]" : "atom:w-[240px]",
        "atom:justify-start atom:text-start atom:font-normal",
        Value == null ? "atom:text-muted-foreground" : null,
        Disabled ? "atom:opacity-50 atom:pointer-events-none" : null,
        Class
    );

    private bool IsSelectedPreset(DateRangeQuickPick quickPick)
    {
        if (!_selectionStart.HasValue || !_selectionEnd.HasValue)
        {
            return false;
        }
        var range = ResolveRange(quickPick);
        return range != null && range.Start.Date == _selectionStart.Value.Date &&
               range.End.Date == _selectionEnd.Value.Date;
    }

    private string GetPresetButtonClass(DateRangeQuickPick quickPick)
    {
        return ClassNames.cn(
            "atom:whitespace-nowrap atom:shrink-0 atom:justify-start",
            IsSelectedPreset(quickPick) ? "atom:bg-primary atom:text-primary-foreground atom:hover:bg-primary atom:hover:text-primary-foreground" : null
        );
    }

    private static string GetCellClass(DateTime? day, bool isInRange, bool isRangeStart, bool isRangeEnd)
    {
        if (!day.HasValue)
        {
            return cellEmpty;
        }

        if (isRangeStart)
        {
            return cellRangeStart;
        }

        if (isRangeEnd)
        {
            return cellRangeEnd;
        }

        if (isInRange)
        {
            return cellInRange;
        }

        return cellDefault;
    }

    private string GetDayClass(DateTime date, bool isDisabled, bool isInRange, bool isRangeStart, bool isRangeEnd, bool isToday)
    {
        string baseClass;
        if (isDisabled)
        {
            baseClass = dayDisabled;
        }
        else if (isRangeStart || isRangeEnd)
        {
            baseClass = dayRangeEndpoint;
        }
        else if (isInRange)
        {
            baseClass = dayInRange;
        }
        else if (isToday)
        {
            baseClass = dayToday;
        }
        else
        {
            baseClass = dayDefault;
        }

        var customClass = DayClassFunc?.Invoke(date);
        return string.IsNullOrEmpty(customClass) ? baseClass : ClassNames.cn(baseClass, customClass);
    }

    /// <summary>
    /// 依据跟踪的状态变更决定组件是否应重渲染。
    /// 该优化用于减少不必要的渲染周期。
    /// </summary>
    protected override bool ShouldRender()
    {
        if (_parametersChanged)
        {
            _parametersChanged = false;
            _lastIsOpen = _isOpen;
            _lastDisplayMonth1 = _displayMonth1;
            _lastDisplayMonth2 = _displayMonth2;
            _lastSelectionStart = _selectionStart;
            _lastSelectionEnd = _selectionEnd;
            _lastValue = Value;
            _lastMinDate = MinDate;
            _lastMaxDate = MaxDate;
            _lastShowTwoMonths = ShowTwoMonths;
            _lastShowPresets = ShowPresets;
            _lastShowButtons = ShowButtons;
            _lastAutoApply = AutoApply;
            _lastDisabled = Disabled;
            _lastPresets = Presets;
            return true;
        }

        var changed = _lastIsOpen != _isOpen
            || _lastDisplayMonth1 != _displayMonth1
            || _lastDisplayMonth2 != _displayMonth2
            || _lastSelectionStart != _selectionStart
            || _lastSelectionEnd != _selectionEnd
            || _lastValue != Value
            || _lastMinDate != MinDate
            || _lastMaxDate != MaxDate
            || _lastShowTwoMonths != ShowTwoMonths
            || _lastShowPresets != ShowPresets
            || _lastShowButtons != ShowButtons
            || _lastAutoApply != AutoApply
            || _lastDisabled != Disabled
            || !ReferenceEquals(_lastPresets, Presets);

        if (changed)
        {
            _lastIsOpen = _isOpen;
            _lastDisplayMonth1 = _displayMonth1;
            _lastDisplayMonth2 = _displayMonth2;
            _lastSelectionStart = _selectionStart;
            _lastSelectionEnd = _selectionEnd;
            _lastValue = Value;
            _lastMinDate = MinDate;
            _lastMaxDate = MaxDate;
            _lastShowTwoMonths = ShowTwoMonths;
            _lastShowPresets = ShowPresets;
            _lastShowButtons = ShowButtons;
            _lastAutoApply = AutoApply;
            _lastDisabled = Disabled;
            _lastPresets = Presets;
            return true;
        }

        return false;
    }
}
