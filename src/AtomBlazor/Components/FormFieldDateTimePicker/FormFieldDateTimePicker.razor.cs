using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="DateTimePicker"/> 提供的表单字段封装组件，
/// 自动呈现标签、帮助文本与错误消息。
/// </summary>
/// <remarks>
/// <para>
/// FormFieldDateTimePicker 在 <see cref="DateTimePicker"/> 之上提供更高层次的抽象，
/// 自动处理标签展示、帮助文本与错误消息渲染。
/// </para>
/// <para>
/// 若需要完全掌控布局与错误展示，请直接使用 <see cref="DateTimePicker"/>
/// 配合 <see cref="Field"/> 组件体系。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FormFieldDateTimePicker @bind-Value="appointment"
///                            Label="Appointment"
///                            HelperText="Select a date and time"
///                            MinDate="DateTime.Today" /&gt;
/// </code>
/// </example>
public partial class FormFieldDateTimePicker : FormFieldBase
{
    // --- DateTimePicker 透传参数 ---

    /// <summary>
    /// 获取或设置所选日期与时间值。
    /// </summary>
    [Parameter]
    public DateTime? Value { get; set; }

    /// <summary>
    /// 获取或设置选中日期时间变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<DateTime?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// 由 <c>@bind-Value</c> 自动提供。
    /// </summary>
    [Parameter]
    public Expression<Func<DateTime?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置展示的日期时间格式。
    /// 为 null 时，<see cref="ShowSeconds"/> 为 true 取 "G"（随文化区域含秒的日期时间），
    /// 否则取 "g"（随文化区域的日期时间）。
    /// </summary>
    [Parameter]
    public string? DateTimeFormat { get; set; }

    /// <summary>
    /// 获取或设置未选中日期时间时展示的占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置可选日期的最小值。
    /// </summary>
    [Parameter]
    public DateTime? MinDate { get; set; }

    /// <summary>
    /// 获取或设置可选日期的最大值。
    /// </summary>
    [Parameter]
    public DateTime? MaxDate { get; set; }

    /// <summary>
    /// 获取或设置判定某日期是否禁选的方法。
    /// </summary>
    [Parameter]
    public Func<DateTime, bool>? DisabledDates { get; set; }

    /// <summary>
    /// 获取或设置每周的第一天。
    /// 缺省为当前文化区域定义的首日。
    /// </summary>
    [Parameter]
    public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>
    /// 获取或设置时间选择器的时间格式（12 小时制带 AM/PM 或 24 小时制）。
    /// </summary>
    [Parameter]
    public TimeFormat TimeFormat { get; set; } = TimeFormat.Hour12;

    /// <summary>
    /// 获取或设置是否展示秒选择器。
    /// </summary>
    [Parameter]
    public bool ShowSeconds { get; set; }

    /// <summary>
    /// 获取或设置分钟的步进间隔。
    /// </summary>
    [Parameter]
    public int MinuteStep { get; set; } = 1;

    /// <summary>
    /// 获取或设置日期时间选择器是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置日期时间选择器是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 DateTimePicker 触发按钮的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(DateTime? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
