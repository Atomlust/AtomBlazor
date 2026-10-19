using AtomBlazor.Primitives;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

/// <summary>
/// 允许在一组互斥选项中进行单选的单选组组件。
/// </summary>
/// <typeparam name="TValue">与单选组各项关联的值类型。</typeparam>
/// <remarks>
/// <para>
/// RadioGroup 组件遵循 shadcn/ui 设计体系，为单选按钮提供容器。
/// 它支持双向数据绑定、表单校验与键盘交互。
/// </para>
/// <para>
/// 特性：
/// - 泛型 TValue 支持类型安全绑定
/// - 通过 @bind-Value 支持双向绑定
/// - 表单校验集成（EditContext）
/// - 支持禁用态
/// - 键盘支持（方向键、Space/Enter）
/// - 自带 ARIA 属性
/// - 通过 CSS 变量兼容暗色模式
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;RadioGroup @bind-Value="selectedOption"&gt;
///     &lt;div class="flex items-center gap-3"&gt;
///         &lt;RadioGroupItem Value="@("option1")" Id="r1" /&gt;
///         &lt;label for="r1"&gt;Option 1&lt;/label&gt;
///     &lt;/div&gt;
/// &lt;/RadioGroup&gt;
/// </code>
/// </example>
public partial class RadioGroup<TValue> : ComponentBase
{
    private FieldIdentifier fieldIdentifier;
    private EditContext? editContext;

    /// <summary>
    /// 获取或设置从父级 EditForm 级联而来的 EditContext。
    /// </summary>
    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// 获取或设置当前选中的值。
    /// </summary>
    /// <remarks>
    /// 通过 <c>@bind-Value</c> 指令支持双向绑定；该属性变化时触发 <see cref="ValueChanged"/> 回调。
    /// </remarks>
    [Parameter]
    public TValue Value { get; set; } = default!;

    /// <summary>
    /// 获取或设置选中值变化时调用的回调。
    /// </summary>
    /// <remarks>
    /// 该事件回调支撑 <c>@bind-Value</c> 双向绑定，选中某个单选按钮时都会调用它。
    /// </remarks>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>
    /// 获取或设置整个单选组是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用后组内所有单选按钮都不可再被选中。
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置单选组在表单上下文中是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置应用到单选组容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置单选组的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 为辅助技术描述该单选组的用途。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置描述该单选组的元素 ID 集合，用于无障碍。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置在单选组内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 应包含 RadioGroupItem 组件。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式。
    /// </summary>
    /// <remarks>
    /// 用于表单校验集成。提供后，单选组会注册到 EditContext 并参与表单校验。
    /// </remarks>
    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    /// <summary>
    /// 获取单选组是否处于无效状态（用于校验）。
    /// </summary>
    private bool IsInvalid
    {
        get
        {
            if (editContext != null && ValueExpression != null && fieldIdentifier.FieldName != null)
            {
                return editContext.GetValidationMessages(fieldIdentifier).Any();
            }

            return false;
        }
    }

    /// <summary>
    /// 获取单选组容器的计算 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn("atom:grid atom:gap-2", Class);

    /// <summary>
    /// 处理选中值变化，并通知 EditContext 触发表单校验。
    /// </summary>
    private async Task HandleValueChanged(TValue value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);

        // 通知 EditContext 字段已变化以触发校验。
        if (editContext != null && ValueExpression != null && fieldIdentifier.FieldName != null)
        {
            editContext.NotifyFieldChanged(fieldIdentifier);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // 存在级联 EditContext 时初始化校验集成。
        if (CascadedEditContext != null && ValueExpression != null)
        {
            editContext = CascadedEditContext;
            fieldIdentifier = FieldIdentifier.Create(ValueExpression);
        }
    }
}
