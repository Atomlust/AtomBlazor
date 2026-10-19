using System.Linq.Expressions;
using AtomBlazor.Primitives;

using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

/// <summary>
/// 封装所有输入组件共用的 EditContext 校验逻辑。
/// 由于输入组件直接继承自 ComponentBase，这里以组合方式复用。
/// </summary>
internal sealed class InputValidationBehavior
{
    private FieldIdentifier fieldIdentifier;
    private EditContext? editContext;
    private string? formattedName;

    /// <summary>
    /// 获取字段在当前 EditContext 中是否存在校验错误。
    /// </summary>
    public bool IsInvalid
    {
        get
        {
            if (editContext != null && fieldIdentifier.FieldName != null)
            {
                return editContext.GetValidationMessages(fieldIdentifier).Any();
            }
            return false;
        }
    }

    /// <summary>
    /// 获取有效的 name 属性值：位于 EditForm 内时回退为值表达式的完整成员路径
    /// （例如 "Input.Username"——与 InputBase 行为一致，使 [SupplyParameterFromForm]
    /// 能在 SSR 表单回传时正确绑定），再回退到 FieldIdentifier 的末端字段名。
    /// </summary>
    public string? GetEffectiveName(string? name) =>
        name ?? (editContext != null && fieldIdentifier.FieldName != null
            ? formattedName ?? fieldIdentifier.FieldName
            : null);

    /// <summary>
    /// 获取合并了手动 AriaInvalid、父级字段状态与 EditContext 校验结果的有效
    /// aria-invalid 值。
    /// </summary>
    public string? GetEffectiveAriaInvalid(bool? ariaInvalid, bool? fieldIsInvalid)
    {
        if (ariaInvalid.HasValue)
        {
            return ariaInvalid.Value ? "true" : "false";
        }
        if (fieldIsInvalid == true || IsInvalid)
        {
            return "true";
        }
        return null;
    }

    /// <summary>
    /// 通知 EditContext 字段值已变化并触发校验。
    /// </summary>
    public void NotifyFieldChanged()
    {
        if (editContext != null && fieldIdentifier.FieldName != null)
        {
            editContext.NotifyFieldChanged(fieldIdentifier);
        }
    }

    /// <summary>
    /// 依据级联参数更新 EditContext 与 FieldIdentifier。
    /// 应在 <see cref="Microsoft.AspNetCore.Components.ComponentBase.OnParametersSet"/> 中调用。
    /// </summary>
    public void Update<T>(EditContext? cascadedEditContext, Expression<Func<T>>? valueExpression)
    {
        if (cascadedEditContext != null && valueExpression != null)
        {
            editContext = cascadedEditContext;
            fieldIdentifier = FieldIdentifier.Create(valueExpression);
            formattedName = ExpressionPathFormatter.FormatLambda(valueExpression);
        }
    }
}
