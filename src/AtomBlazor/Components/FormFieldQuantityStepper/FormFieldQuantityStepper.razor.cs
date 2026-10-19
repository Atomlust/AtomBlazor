using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="QuantityStepper"/> 提供的表单字段封装组件，
/// 在数量控件外围自动展示标签、帮助文本与校验消息。
/// </summary>
public partial class FormFieldQuantityStepper : FormFieldBase
{
    /// <summary>
    /// 获取或设置当前数量。
    /// </summary>
    [Parameter]
    public int Value { get; set; }

    /// <summary>
    /// 获取或设置数量变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<int> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// </summary>
    [Parameter]
    public Expression<Func<int>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置允许的最小数量。
    /// </summary>
    [Parameter]
    public int Min { get; set; }

    /// <summary>
    /// 获取或设置允许的最大数量，<c>null</c> 表示不设上限。
    /// </summary>
    [Parameter]
    public int? Max { get; set; }

    /// <summary>
    /// 获取或设置每次按压施加的增减步长。
    /// </summary>
    [Parameter]
    public int Step { get; set; } = 1;

    /// <summary>
    /// 获取或设置数量是否只可读。
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>
    /// 获取或设置控件是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置在 <see cref="Min"/> 处按压减号按钮时调用的回调，
    /// 此时该按钮转为移除动作。
    /// </summary>
    [Parameter]
    public EventCallback OnRemove { get; set; }

    /// <summary>
    /// 获取或设置随值一起提交的表单字段名。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置应用到内部数量步进器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(int value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
