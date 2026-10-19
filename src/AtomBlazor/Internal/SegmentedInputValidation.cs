using System.Linq.Expressions;
using AtomBlazor.Primitives;

using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

/// <summary>
/// DateInput / TimeInput 共用的分段校验门面：绑定 EditContext 字段、写入/清除
/// 本地错误消息并在变化时回调（随带编译依赖，位于 Components/Internal）。
/// </summary>
internal sealed class SegmentedInputValidation(Action onValidationChanged) : IDisposable
{
    private EditContext? context;
    private FieldIdentifier fieldIdentifier;
    private ValidationMessageStore? messages;
    private string? error;
    private string? expressionName;

    /// <summary>本地错误或未清除的模型校验消息存在时为 true。</summary>
    internal bool IsInvalid => error != null || (context?.GetValidationMessages(fieldIdentifier).Any() ?? false);

    /// <summary>隐藏表单字段的 name：显式 Name 优先，否则取值表达式的成员路径。</summary>
    internal string? GetName(string? name) => name ?? expressionName;

    /// <summary>切换绑定的 EditContext 与字段；同一字段重复调用不产生副作用。</summary>
    internal void Update<T>(EditContext? editContext, Expression<Func<T>>? expression)
    {
        var nextField = expression == null ? default : FieldIdentifier.Create(expression);
        if (context == editContext && fieldIdentifier.Equals(nextField))
        {
            return;
        }
        Detach();
        fieldIdentifier = nextField;
        expressionName = expression == null ? null : ExpressionPathFormatter.FormatLambda(expression);
        context = expression == null ? null : editContext;
        if (context != null)
        {
            messages = new ValidationMessageStore(context);
            context.OnValidationStateChanged += HandleValidationChanged;
            if (error != null)
            {
                messages.Add(fieldIdentifier, error);
            }
        }
    }

    /// <summary>写入或清除本地错误消息，并通知校验状态变化。</summary>
    internal void SetError(string? value)
    {
        if (error == value) { return; }
        error = value;
        messages?.Clear();
        if (value != null)
        {
            messages?.Add(fieldIdentifier, value);
        }
        context?.NotifyValidationStateChanged();
    }

    /// <summary>通知宿主 EditContext 字段值已变化（触发模型侧重验）。</summary>
    internal void NotifyFieldChanged() => context?.NotifyFieldChanged(fieldIdentifier);
    private void HandleValidationChanged(object? sender, ValidationStateChangedEventArgs args) => onValidationChanged();

    private void Detach()
    {
        if (context != null)
        {
            context.OnValidationStateChanged -= HandleValidationChanged;
            messages?.Clear();
            context.NotifyValidationStateChanged();
        }
        messages = null;
        context = null;
    }

    public void Dispose() => Detach();
}
