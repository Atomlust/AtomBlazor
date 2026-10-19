using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

/// <summary>
/// 遵循 shadcn/ui 设计体系的复选框组件。
/// </summary>
/// <remarks>
/// <para>
/// Checkbox 组件提供可定制的复选框，支持双向数据绑定、表单校验、禁用态与不确定态。
/// 它自带 ARIA 属性与键盘支持，并接入 Blazor 的表单体系。
/// </para>
/// <para>
/// 特性：
/// - 通过 @bind-Checked 支持双向绑定
/// - 支持不确定（indeterminate）态，用于「全选」场景
/// - 表单校验集成（EditContext）
/// - 支持禁用态
/// - 键盘支持（Space 拨动）
/// - 自带 ARIA 属性
/// - 通过 CSS 变量兼容暗色模式
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Checkbox @bind-Checked="isAccepted" /&gt;
///
/// &lt;Checkbox Checked="@someChecked"
///           Indeterminate="@someIndeterminate"
///           CheckedChanged="@HandleCheckedChanged" /&gt;
/// </code>
/// </example>
public partial class Checkbox : ComponentBase
{
    private FieldIdentifier fieldIdentifier;
    private EditContext? editContext;
    private string? generatedId;
    private string? formattedName;

    /// <summary>
    /// 获取或设置从父级 EditForm 级联而来的 EditContext。
    /// </summary>
    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// 获取或设置复选框是否选中。
    /// </summary>
    /// <remarks>
    /// 通过 <c>@bind-Checked</c> 指令支持双向绑定；该属性变化时触发 <see cref="CheckedChanged"/> 回调。
    /// </remarks>
    [Parameter]
    public bool Checked { get; set; }

    /// <summary>
    /// 获取或设置选中状态变化时调用的回调。
    /// </summary>
    /// <remarks>
    /// 该事件回调支撑 <c>@bind-Checked</c> 双向绑定，用户拨动复选框状态时都会调用它，
    /// 同时通知 EditContext 以触发表单校验。
    /// </remarks>
    [Parameter]
    public EventCallback<bool> CheckedChanged { get; set; }

    /// <summary>
    /// 获取或设置复选框是否处于不确定（indeterminate）状态。
    /// </summary>
    /// <remarks>
    /// 不确定态通常用于「全选」复选框——子项只有部分被选中时。
    /// 为 true 时显示横线图标而非勾。
    /// </remarks>
    [Parameter]
    public bool Indeterminate { get; set; }

    /// <summary>
    /// 获取或设置不确定状态变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<bool> IndeterminateChanged { get; set; }

    /// <summary>
    /// 获取或设置复选框是否禁用。
    /// </summary>
    /// <remarks>
    /// 禁用后：
    /// - 无法点击或聚焦
    /// - 降低不透明度
    /// - 关闭指针事件
    /// - aria-disabled 属性置为 true
    /// </remarks>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置复选框在表单上下文中是否必填。
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置应用到复选框的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置复选框的 ARIA 标签。
    /// </summary>
    /// <remarks>
    /// 当复选框没有配套标签文本时，为屏幕阅读器提供可读名称。
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置复选框元素的 ID 特性。
    /// </summary>
    /// <remarks>
    /// 用于通过 label 的 for 属性把复选框与标签元素关联起来。
    /// </remarks>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置描述该复选框的元素 ID 集合，用于无障碍。
    /// </summary>
    [Parameter]
    public string? AriaDescribedBy { get; set; }

    /// <summary>
    /// 获取或设置标识绑定值的表达式。
    /// </summary>
    /// <remarks>
    /// 用于表单校验集成。提供后，复选框会注册到 EditContext 并参与表单校验。
    /// </remarks>
    [Parameter]
    public Expression<Func<bool>>? CheckedExpression { get; set; }

    /// <summary>
    /// 获取或设置表单提交使用的 HTML name 特性。
    /// </summary>
    /// <remarks>
    /// 位于 EditForm 内且未显式设置时，name 自动从 <see cref="CheckedExpression"/> 推导
    /// （例如 "Input.RememberMe"），使复选框能随 <c>[SupplyParameterFromForm]</c>
    /// 在 SSR/增强表单回传时提交。实际提交由一枚隐藏的原生 checkbox 镜像选中态完成。
    /// </remarks>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// 获取生效的 name 特性；位于 EditForm 内时回退到 CheckedExpression 的成员路径。
    /// </summary>
    private string? EffectiveName =>
        Name ?? (editContext != null && fieldIdentifier.FieldName != null
            ? formattedName ?? fieldIdentifier.FieldName
            : null);

    /// <summary>
    /// 获取复选框是否处于无效状态（用于校验）。
    /// </summary>
    private bool IsInvalid
    {
        get
        {
            if (editContext != null && CheckedExpression != null && fieldIdentifier.FieldName != null)
            {
                return editContext.GetValidationMessages(fieldIdentifier).Any();
            }

            return false;
        }
    }

    /// <summary>
    /// 获取生效的 id 特性；未显式提供时自动生成稳定值。
    /// </summary>
    private string EffectiveId => Id ?? (generatedId ??= $"checkbox-{Guid.NewGuid().ToString("N")[..8]}");

    /// <summary>
    /// 获取复选框元素的计算 CSS 类。
    /// </summary>
    /// <remarks>
    /// 组合：
    /// - 复选框基础样式（尺寸、边框、过渡）
    /// - 状态相关类（选中、不确定、禁用）
    /// - 键盘导航的焦点环
    /// - Class 参数带来的自定义类
    /// </remarks>
    private string CssClass => ClassNames.cn(
        "atom:peer atom:h-5 atom:w-5 atom:shrink-0 atom:rounded-sm atom:border atom:border-primary atom:flex atom:items-center atom:justify-center",
        "atom:ring-offset-background atom:focus-visible:outline-none atom:focus-visible:ring-2",
        "atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        "atom:disabled:cursor-not-allowed atom:disabled:opacity-50",
        (Checked || Indeterminate) ? "atom:bg-primary atom:text-primary-foreground" : "atom:bg-background",
        Class
    );

    /// <summary>
    /// 处理选中状态变化，并通知 EditContext 触发表单校验。
    /// </summary>
    private async Task HandleCheckedChanged(bool value)
    {
        Checked = value;
        await CheckedChanged.InvokeAsync(value);

        // 通知 EditContext 字段已变化以触发校验。
        if (editContext != null && CheckedExpression != null && fieldIdentifier.FieldName != null)
        {
            editContext.NotifyFieldChanged(fieldIdentifier);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // 存在级联 EditContext 时初始化校验集成。
        if (CascadedEditContext != null && CheckedExpression != null)
        {
            editContext = CascadedEditContext;
            fieldIdentifier = FieldIdentifier.Create(CheckedExpression);
            formattedName = ExpressionPathFormatter.FormatLambda(CheckedExpression);
        }
    }
}
