using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="TimeInput"/> 提供的表单字段封装组件，在分段时刻输入之上
/// 附加标签、帮助文本与校验消息。
/// </summary>
public partial class FormFieldTimeInput : FormFieldBase
{
    /// <summary>
    /// 获取或设置所选当日时刻；分段不完整时为 <c>null</c>。
    /// </summary>
    [Parameter]
    public TimeSpan? Value { get; set; }

    /// <summary>
    /// 获取或设置时刻变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TimeSpan?> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<TimeSpan?>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置决定分隔符与 12/24 小时制缺省取向的文化区域信息。
    /// 缺省为当前文化。
    /// </summary>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <summary>
    /// 获取或设置显式指定的 12 或 24 小时制，覆盖文化区域自身的选择。
    /// </summary>
    [Parameter]
    public TimeFormat? Format { get; set; }

    /// <summary>
    /// 获取或设置可选时刻的闭下界。
    /// </summary>
    [Parameter]
    public TimeSpan? MinTime { get; set; }

    /// <summary>
    /// 获取或设置可选时刻的闭上界。
    /// </summary>
    [Parameter]
    public TimeSpan? MaxTime { get; set; }

    /// <summary>
    /// 获取或设置分钟段的步进增量。
    /// </summary>
    [Parameter]
    public int MinuteStep { get; set; } = 1;

    /// <summary>
    /// 获取或设置是否展示秒段。
    /// </summary>
    [Parameter]
    public bool ShowSeconds { get; set; }

    /// <summary>
    /// 获取或设置是否在分段旁显示时钟选择按钮。
    /// </summary>
    /// <remarks>紧凑表单行缺省不显示；置 true 可与独立输入保持一致。</remarks>
    [Parameter]
    public bool ShowPicker { get; set; }

    /// <summary>
    /// 获取或设置分段是否只读。
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>
    /// 获取或设置输入是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置字段是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置随值一起提交的表单字段名。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置应用到内部时刻输入的额外 CSS 类。
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
