using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

/// <summary>日期与时间选择器组件：把浮层日历与小时/分钟（可选秒 / AM-PM）选择器组合为一体。</summary>
/// <remarks>
/// <para>
/// 选择日期时保留当前时间部分，调整时间时保留已选日期（尚未选日时默认今天）。
/// 浮层在选中日期后保持打开以便继续调时间；用户点击外部或按 Escape 时关闭。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;DateTimePicker @bind-Value="appointment" /&gt;
/// </code>
/// </example>
public partial class DateTimePicker : ComponentBase
{
    /// <summary>控件的无障碍名称。</summary>
    [Parameter] public string? AriaLabel { get; set; }


    private bool _isOpen;
    private Calendar? _calendar;
    private bool _focusDone;
    private int _internalHour;
    private int _internalMinute;
    private int _internalSecond;
    private FieldIdentifier _fieldIdentifier;
    private EditContext? _editContext;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// 选中的日期与时间值。
    /// </summary>
    [Parameter]
    public DateTime? Value { get; set; }

    /// <summary>
    /// 选中的日期与时间变化时的回调。
    /// </summary>
    [Parameter]
    public EventCallback<DateTime?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式。
    /// </summary>
    [Parameter]
    public Expression<Func<DateTime?>>? ValueExpression { get; set; }

    /// <summary>
    /// 触发按钮上显示的日期时间格式。
    /// 为 null 时：<see cref="ShowSeconds"/> 为 true 取 "G"（随文化区含秒日期时间），
    /// 否则取 "g"（随文化区日期时间）。
    /// </summary>
    [Parameter]
    public string? DateTimeFormat { get; set; }

    /// <summary>
    /// 未选择日期与时间时显示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

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
    /// 判定日期是否禁用的函数。
    /// </summary>
    [Parameter]
    public Func<DateTime, bool>? DisabledDates { get; set; }

    /// <summary>
    /// 为日历中某个日期按钮返回附加 CSS 类的函数，与内置日类组合。
    /// </summary>
    [Parameter]
    public Func<DateTime, string?>? DayClassFunc { get; set; }

    /// <summary>
    /// 在每个日历日按钮内渲染自定义内容的可选模板。为 null 时渲染日号。
    /// </summary>
    [Parameter]
    public RenderFragment<CalendarDayContext>? DayTemplate { get; set; }

    /// <summary>
    /// 每周第一天。缺省取当前文化区的一周首日。
    /// </summary>
    [Parameter]
    public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>
    /// 时间选择器的时间制式（12 小时带 AM/PM 或 24 小时）。
    /// </summary>
    [Parameter]
    public TimeFormat TimeFormat { get; set; } = TimeFormat.Hour12;

    /// <summary>
    /// 是否显示秒选择器。
    /// </summary>
    [Parameter]
    public bool ShowSeconds { get; set; }

    /// <summary>
    /// 分钟步进间隔，取值 1 到 59；其他值抛 ArgumentOutOfRangeException。
    /// </summary>
    [Parameter]
    public int MinuteStep { get; set; } = 1;

    /// <summary>
    /// 日期时间选择器是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置日期时间选择器是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 附加到触发按钮的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 附加到触发按钮的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string EffectivePlaceholder => Placeholder ?? Localizer["DateTimePicker.Placeholder"];

    private string EffectiveFormat => DateTimeFormat ?? (ShowSeconds ? "G" : "g");

    private int CurrentHour => Value?.Hour ?? _internalHour;

    private int CurrentMinute => Value?.Minute ?? _internalMinute;

    private int CurrentSecond => Value?.Second ?? _internalSecond;

    private bool IsAm => CurrentHour < 12;

    private int DisplayHour
    {
        get
        {
            if (TimeFormat == TimeFormat.Hour24)
            {
                return CurrentHour;
            }

            var hour = CurrentHour % 12;
            return hour == 0 ? 12 : hour;
        }
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (MinuteStep is < 1 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(MinuteStep), "MinuteStep must be between 1 and 59.");
        }

        if (Value.HasValue)
        {
            _internalHour = Value.Value.Hour;
            _internalMinute = Value.Value.Minute;
            _internalSecond = Value.Value.Second;
        }

        if (CascadedEditContext != null && ValueExpression != null)
        {
            _editContext = CascadedEditContext;
            _fieldIdentifier = FieldIdentifier.Create(ValueExpression);
        }
    }

    private async Task HandleDateSelected(DateTime? date)
    {
        if (date.HasValue)
        {
            // 日期变化时保留当前时间部分；浮层保持打开，用户可继续调时间。
            await SetValue(date.Value.Date.AddHours(_internalHour).AddMinutes(_internalMinute).AddSeconds(_internalSecond));
        }
        else
        {
            await SetValue(null);
        }
    }

    private void HandleOpenChanged(bool isOpen)
    {
        _isOpen = isOpen;

        // 每次打开都重新装载"打开即聚焦"闩，使活动日在下一次打开时重新聚焦，
        // 不论上一次如何关闭。
        if (isOpen)
        {
            _focusDone = false;
        }
    }

    private async Task HandleContentReady()
    {
        // 浮层定位就绪后把焦点移入日历，使活动日聚焦、方向键导航打开即用。加闩防止
        // 重新定位再触发时把焦点从用户导航到的位置拽回。
        if (_focusDone || _calendar is null)
        {
            return;
        }

        _focusDone = true;
        await _calendar.FocusActiveDayAsync();
    }

    private async Task SetValue(DateTime? value)
    {
        if (value is { } date && ((MinDate.HasValue && date.Date < MinDate.Value.Date)
            || (MaxDate.HasValue && date.Date > MaxDate.Value.Date)
            || DisabledDates?.Invoke(date.Date) == true))
        {
            return;
        }

        Value = value;

        if (value.HasValue)
        {
            _internalHour = value.Value.Hour;
            _internalMinute = value.Value.Minute;
            _internalSecond = value.Value.Second;
        }

        await ValueChanged.InvokeAsync(value);

        if (_editContext != null && ValueExpression != null && _fieldIdentifier.FieldName != null)
        {
            _editContext.NotifyFieldChanged(_fieldIdentifier);
        }
    }

    private async Task UpdateTime()
    {
        // 调整时间保留已选日期；尚未选日时默认今天。
        var date = Value?.Date ?? DateTime.Today;
        await SetValue(date.AddHours(_internalHour).AddMinutes(_internalMinute).AddSeconds(_internalSecond));
    }

    private async Task IncrementHour()
    {
        if (TimeFormat == TimeFormat.Hour24)
        {
            _internalHour = (_internalHour + 1) % 24;
        }
        else
        {
            // 在当前 AM/PM 时段内循环，保留时段。
            var isAm = _internalHour < 12;
            var hour12 = ((_internalHour % 12) + 1) % 12;
            _internalHour = isAm ? hour12 : hour12 + 12;
        }

        await UpdateTime();
    }

    private async Task DecrementHour()
    {
        if (TimeFormat == TimeFormat.Hour24)
        {
            _internalHour = (_internalHour - 1 + 24) % 24;
        }
        else
        {
            // 在当前 AM/PM 时段内循环，保留时段。
            var isAm = _internalHour < 12;
            var hour12 = ((_internalHour % 12) - 1 + 12) % 12;
            _internalHour = isAm ? hour12 : hour12 + 12;
        }

        await UpdateTime();
    }

    private async Task IncrementMinute()
    {
        _internalMinute = (_internalMinute + MinuteStep) % 60;
        await UpdateTime();
    }

    private async Task DecrementMinute()
    {
        _internalMinute = (_internalMinute - MinuteStep + 60) % 60;
        await UpdateTime();
    }

    private async Task IncrementSecond()
    {
        _internalSecond = (_internalSecond + 1) % 60;
        await UpdateTime();
    }

    private async Task DecrementSecond()
    {
        _internalSecond = (_internalSecond - 1 + 60) % 60;
        await UpdateTime();
    }

    private async Task SetAmPm(bool isAm)
    {
        if (isAm && _internalHour >= 12)
        {
            _internalHour -= 12;
        }
        else if (!isAm && _internalHour < 12)
        {
            _internalHour += 12;
        }

        await UpdateTime();
    }

    private async Task SetNow()
    {
        var now = DateTime.Now;
        _internalHour = now.Hour;
        _internalMinute = now.Minute / MinuteStep * MinuteStep;
        _internalSecond = ShowSeconds ? now.Second : 0;
        await SetValue(now.Date.AddHours(_internalHour).AddMinutes(_internalMinute).AddSeconds(_internalSecond));
    }

    private async Task Clear()
    {
        _internalHour = 0;
        _internalMinute = 0;
        _internalSecond = 0;
        await SetValue(null);
    }

    /// <summary>
    /// 获取触发按钮的完整 CSS 类。
    /// </summary>
    private string ButtonCssClass => ClassNames.cn(
        "atom:w-[280px] atom:justify-start atom:text-start atom:font-normal",
        // #459：否则浏览器自带轮廓会与上方字段的主题化焦点环并排出现。
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        !Value.HasValue ? "atom:text-muted-foreground" : null,
        Disabled ? "atom:opacity-50 atom:pointer-events-none" : null,
        Class
    );

    private static string ScrollButtonClass => ClassNames.cn(
        "atom:w-12 atom:h-8 atom:flex atom:items-center atom:justify-center",
        // #459：不加 offset——这些钮在时间列中直接层叠。
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:hover:bg-accent atom:hover:text-accent-foreground",
        "atom:transition-colors",
        "atom:disabled:opacity-50 atom:disabled:cursor-not-allowed"
    );

    private string GetAmPmButtonClass(bool isAm) => ClassNames.cn(
        "atom:w-12 atom:h-10 atom:text-sm atom:font-medium atom:transition-colors",
        isAm == IsAm ? "atom:bg-primary atom:text-primary-foreground" : "atom:hover:bg-accent atom:hover:text-accent-foreground",
        "atom:disabled:opacity-50 atom:disabled:cursor-not-allowed"
    );
}
