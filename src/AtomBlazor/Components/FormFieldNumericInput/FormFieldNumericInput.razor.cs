using System.Linq.Expressions;
using System.Numerics;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为 <see cref="NumericInput{TValue}"/> 提供的表单字段封装组件，
/// 自动展示标签、帮助文本与错误消息。
/// </summary>
/// <remarks>
/// <para>
/// FormFieldNumericInput 在 <see cref="NumericInput{TValue}"/> 之上提供更高层的抽象，
/// 自动处理标签关联、帮助文本展示与错误消息渲染。
/// </para>
/// <para>
/// 若需完全掌控布局与错误展示，请直接使用 <see cref="NumericInput{TValue}"/>
/// 配合 <see cref="Field"/> 组件体系。
/// </para>
/// </remarks>
/// <typeparam name="TValue">数值类型（必须实现 <see cref="INumber{TSelf}"/>）。</typeparam>
/// <example>
/// <code>
/// &lt;FormFieldNumericInput TValue="int"
///                          @bind-Value="quantity"
///                          Label="Quantity"
///                          HelperText="Enter a value between 1 and 100"
///                          Min="1"
///                          Max="100"
///                          ShowButtons="true" /&gt;
/// </code>
/// </example>
public partial class FormFieldNumericInput<TValue> : FormFieldBase where TValue : struct, INumber<TValue>
{
    // --- NumericInput 透传参数 ---

    /// <summary>
    /// 获取或设置当前数值。
    /// </summary>
    [Parameter]
    public TValue Value { get; set; }

    /// <summary>
    /// 获取或设置值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式，用于 EditForm 集成。
    /// 由 <c>@bind-Value</c> 自动提供。
    /// </summary>
    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取或设置 HTML name 特性。透传给内部 NumericInput。
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置允许的最小值。
    /// </summary>
    [Parameter]
    public TValue? Min { get; set; }

    /// <summary>
    /// 获取或设置允许的最大值。
    /// </summary>
    [Parameter]
    public TValue? Max { get; set; }

    /// <summary>
    /// 获取或设置方向键/按钮变化的步进增量。
    /// </summary>
    [Parameter]
    public TValue? Step { get; set; }

    /// <summary>
    /// 获取或设置显示的小数位数。
    /// </summary>
    [Parameter]
    public int? DecimalPlaces { get; set; }

    /// <summary>
    /// 获取或设置是否允许负值。
    /// </summary>
    [Parameter]
    public bool AllowNegative { get; set; } = true;

    /// <summary>
    /// 获取或设置是否显示增减按钮。
    /// </summary>
    [Parameter]
    public bool ShowButtons { get; set; }

    /// <summary>
    /// 获取或设置占位文本。
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置输入是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置输入是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置值的显示格式串。
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// 获取或设置是否在键入过程中禁用防抖。
    /// </summary>
    [Parameter]
    public bool DisableDebounce { get; set; }

    /// <summary>
    /// 获取或设置防抖间隔（毫秒）。
    /// </summary>
    [Parameter]
    public int DebounceInterval { get; set; } = 500;

    /// <summary>
    /// 获取或设置是否在输入框获得焦点时用鼠标滚轮增减值。
    /// 缺省为 <c>false</c>；参见 <see cref="NumericInput{TValue}.EnableWheelStep"/>。
    /// </summary>
    [Parameter]
    public bool EnableWheelStep { get; set; }

    /// <summary>
    /// 获取或设置应用到内部 NumericInput 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(TValue value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
