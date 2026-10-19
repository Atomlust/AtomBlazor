using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="DateRangePicker"/> 提供的表单字段封装组件，
/// 自动呈现标签、帮助文本与错误消息。
/// </summary>
public partial class FormFieldDateRangePicker : FormFieldBase
{
    /// <summary>标识 EditForm 校验所绑定的字段。</summary>
    [Parameter] public Expression<Func<DateRange?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置所选日期范围。
    /// </summary>
    [Parameter]
    public DateRange? Value { get; set; }

    /// <summary>
    /// 获取或设置日期范围变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<DateRange?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置展示的日期格式。
    /// </summary>
    [Parameter]
    public string DateFormat { get; set; } = "d";

    /// <summary>
    /// 获取或设置未选中范围时展示的占位文本。
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
    /// 获取或设置必须选中的最少天数。
    /// </summary>
    [Parameter]
    public int? MinDays { get; set; }

    /// <summary>
    /// 获取或设置可以选中的最多天数。
    /// </summary>
    [Parameter]
    public int? MaxDays { get; set; }

    /// <summary>
    /// 获取或设置是否并排展示两个月份。
    /// </summary>
    [Parameter]
    public bool ShowTwoMonths { get; set; } = true;

    /// <summary>
    /// 获取或设置是否展示快捷预设范围。
    /// </summary>
    [Parameter]
    public bool ShowPresets { get; set; } = true;

    /// <summary>
    /// 获取或设置自定义快捷预设范围。
    /// </summary>
    [Parameter]
    public IReadOnlyList<DateRangeQuickPick>? Presets { get; set; }

    /// <summary>
    /// 获取或设置是否在浮层页脚展示 Clear 与 Apply 按钮。
    /// 为 <c>false</c> 时，已完成的选区会自动应用（视同 <see cref="AutoApply"/>
    /// 为 <c>true</c>），因为没有 Apply 按钮可用。缺省为 <c>true</c>。
    /// </summary>
    [Parameter]
    public bool ShowButtons { get; set; } = true;

    /// <summary>
    /// 获取或设置是否在选出完整且合法的范围后自动应用选区，
    /// 而无需点击 Apply 按钮。自动应用时浮层会关闭。
    /// 当 <see cref="ShowButtons"/> 为 <c>false</c> 时自动应用恒生效。缺省为 <c>false</c>。
    /// </summary>
    [Parameter]
    public bool AutoApply { get; set; }

    /// <summary>
    /// 获取或设置每周的第一天。
    /// </summary>
    [Parameter]
    public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>
    /// 获取或设置日期范围选择器是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 DateRangePicker 触发器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(DateRange? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
