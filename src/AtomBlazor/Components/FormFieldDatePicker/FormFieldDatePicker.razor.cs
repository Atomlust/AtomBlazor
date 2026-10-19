using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="DatePicker"/> 提供的表单字段封装组件，
/// 自动呈现标签、帮助文本与错误消息。
/// </summary>
/// <remarks>
/// <para>
/// FormFieldDatePicker 在 <see cref="DatePicker"/> 之上提供更高层次的抽象，
/// 自动处理标签展示、帮助文本与错误消息渲染。
/// </para>
/// <para>
/// 若需要完全掌控布局与错误展示，请直接使用 <see cref="DatePicker"/>
/// 配合 <see cref="Field"/> 组件体系。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FormFieldDatePicker @bind-Value="startDate"
///                        Label="Start Date"
///                        HelperText="Select a date in the future"
///                        MinDate="DateTime.Today" /&gt;
/// </code>
/// </example>
public partial class FormFieldDatePicker : FormFieldBase
{
    // --- DatePicker 透传参数 ---

    /// <summary>
    /// 获取或设置所选日期值。
    /// </summary>
    [Parameter]
    public DateTime? Value { get; set; }

    /// <summary>
    /// 获取或设置选中日期变化时调用的回调。
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
    /// 获取或设置展示的日期格式。
    /// 缺省为 "d"（随文化区域的短日期模式）。
    /// </summary>
    [Parameter]
    public string DateFormat { get; set; } = "d";

    /// <summary>
    /// 获取或设置为 true 时可直接在文本输入框键入日期，并附带日历切换按钮。
    /// 参见 <see cref="DatePicker.Editable"/>。
    /// </summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>
    /// 获取或设置 Editable 模式下键入时接受的日期格式；ISO（yyyy-MM-dd）
    /// 始终接受。参见 <see cref="DatePicker.InputFormats"/>。
    /// </summary>
    [Parameter]
    public string[]? InputFormats { get; set; }

    /// <summary>
    /// 获取或设置未选中日期时展示的占位文本。
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
    /// 获取或设置日期选择器是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置日期选择器是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 DatePicker 触发按钮的额外 CSS 类。
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
