using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="TimePicker"/> 提供的表单字段封装组件，
/// 自动展示标签、帮助文本与错误消息。
/// </summary>
/// <remarks>
/// <para>
/// FormFieldTimePicker 在 <see cref="TimePicker"/> 之上提供更高层的抽象，
/// 自动处理标签展示、帮助文本与错误消息渲染。
/// </para>
/// <para>
/// 若需完全掌控布局与错误展示，请直接使用 <see cref="TimePicker"/>
/// 配合 <see cref="Field"/> 组件体系。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;FormFieldTimePicker @bind-Value="meetingTime"
///                        Label="Meeting Time"
///                        HelperText="Business hours only"
///                        Format="TimeFormat.Hour24" /&gt;
/// </code>
/// </example>
public partial class FormFieldTimePicker : FormFieldBase
{
    // --- TimePicker 透传参数 ---

    /// <summary>
    /// 获取或设置所选时间值。
    /// </summary>
    [Parameter]
    public TimeSpan? Value { get; set; }

    /// <summary>
    /// 获取或设置选中时间变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TimeSpan?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// 由 <c>@bind-Value</c> 自动提供。
    /// </summary>
    [Parameter]
    public Expression<Func<TimeSpan?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置时间格式（12 小时制或 24 小时制）。
    /// </summary>
    [Parameter]
    public TimeFormat Format { get; set; } = TimeFormat.Hour12;

    /// <summary>
    /// 获取或设置是否显示秒选择列。
    /// </summary>
    [Parameter]
    public bool ShowSeconds { get; set; }

    /// <summary>
    /// 获取或设置分钟步进间隔。
    /// </summary>
    [Parameter]
    public int MinuteStep { get; set; } = 1;

    /// <summary>
    /// 获取或设置允许的最小时间。
    /// </summary>
    [Parameter]
    public TimeSpan? MinTime { get; set; }

    /// <summary>
    /// 获取或设置允许的最大时间。
    /// </summary>
    [Parameter]
    public TimeSpan? MaxTime { get; set; }

    /// <summary>
    /// 获取或设置未选择时间时显示的占位文本。
    /// </summary>
    [Parameter]
    public string Placeholder { get; set; } = "Select time";

    /// <summary>
    /// 获取或设置时间选择器是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置时间选择器是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 TimePicker 触发按钮的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(TimeSpan? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
