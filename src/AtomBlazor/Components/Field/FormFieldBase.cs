using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AtomBlazor;

/// <summary>
/// 表单字段包装控件的抽象基类：把具体控件与 <see cref="Field"/>、
/// <see cref="FieldLabel"/>、<see cref="FieldDescription"/>、<see cref="FieldError"/>
/// 组合成完整、无障碍的表单字段体验。
/// </summary>
/// <remarks>
/// 提供共享逻辑：ID 生成、EditContext 生命周期管理、ARIA 属性接线与错误状态计算。
/// 子类负责提供具体控件及其字段表达式。
/// </remarks>
public abstract class FormFieldBase : ComponentBase, IDisposable
{
    private FieldIdentifier? _fieldIdentifier;
    private LambdaExpression? _cachedExpression;
    private EditContext? _subscribedEditContext;

    private readonly string _controlId = $"formfield-{Guid.NewGuid():N}";

    /// <summary>
    /// 获取内部控件元素自动生成的 ID。
    /// </summary>
    protected string ControlId => _controlId;

    /// <summary>
    /// 获取描述元素自动生成的 ID。
    /// </summary>
    protected string DescriptionId => $"{ControlId}-description";

    /// <summary>
    /// 获取错误元素自动生成的 ID。
    /// </summary>
    protected string ErrorId => $"{ControlId}-error";

    // --- 共享参数 ---

    /// <summary>
    /// 获取或设置显示在控件上方或一侧的标签文本。
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// 获取或设置显示在控件下方的帮助文本。
    /// </summary>
    /// <remarks>
    /// 字段处于错误态时隐藏，由错误消息取而代之。
    /// </remarks>
    [Parameter]
    public string? HelperText { get; set; }

    /// <summary>
    /// 获取或设置要在字段上手动展示的错误文本。
    /// </summary>
    /// <remarks>
    /// 设置后字段进入无效态并把该文本作为错误消息展示，
    /// 用于从外部来源（如服务端校验或动态表单）注入校验错误。
    /// 当字段同时存在 EditContext 校验错误时，该文本优先于自动生成的校验消息。
    /// </remarks>
    [Parameter]
    public string? ErrorText { get; set; }

    /// <summary>
    /// 获取或设置字段布局的方向。
    /// </summary>
    [Parameter]
    public FieldOrientation Orientation { get; set; } = FieldOrientation.Vertical;

    /// <summary>
    /// 获取或设置应用到外层 Field 容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置控件的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    // --- 计算属性 ---

    /// <summary>
    /// 获取字段当前是否存在来自 EditContext 的错误。
    /// </summary>
    protected bool HasEditContextErrors => EditContextErrors.Any();

    /// <summary>
    /// 获取来自 EditContext 的校验错误消息。
    /// </summary>
    protected IEnumerable<string> EditContextErrors
    {
        get
        {
            if (CascadedEditContext is not null && _fieldIdentifier.HasValue)
            {
                return CascadedEditContext.GetValidationMessages(_fieldIdentifier.Value);
            }

            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// 获取字段是否处于无效态。子类可重写以追加错误来源（如解析错误）。
    /// </summary>
    protected virtual bool IsInvalid => HasEditContextErrors || !string.IsNullOrEmpty(ErrorText);

    /// <summary>
    /// 获取 <c>aria-describedby</c> 属性值：无效时指向错误元素，
    /// 存在帮助文本时指向描述元素。子类可重写以自定义逻辑。
    /// </summary>
    protected virtual string? DescribedById => IsInvalid
        ? ErrorId
        : !string.IsNullOrEmpty(HelperText) ? DescriptionId : null;

    // --- 抽象成员 ---

    /// <summary>
    /// 返回绑定字段的 lambda 表达式，用于创建与 EditContext 集成所需的
    /// <see cref="FieldIdentifier"/>。
    /// </summary>
    protected abstract LambdaExpression? GetFieldExpression();

    /// <summary>
    /// 通知级联的 <see cref="EditContext"/> 字段值已变化并触发校验。
    /// 应在更新绑定值之后调用。
    /// </summary>
    protected void NotifyFieldChanged()
    {
        if (CascadedEditContext is not null && _fieldIdentifier.HasValue)
        {
            CascadedEditContext.NotifyFieldChanged(_fieldIdentifier.Value);
        }
    }

    // --- 生命周期 ---

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (CascadedEditContext != _subscribedEditContext)
        {
            _subscribedEditContext?.OnValidationStateChanged -= OnValidationStateChanged;

            _subscribedEditContext = CascadedEditContext;

            _subscribedEditContext?.OnValidationStateChanged += OnValidationStateChanged;
        }

        var expression = GetFieldExpression();
        if (CascadedEditContext is not null && expression is not null)
        {
            // 典型写法 ValueExpression="@(() => Model.Name)" 每次渲染都会新建表达式树，
            // 按引用缓存命中不了。直接解析成员链取出模型与字段名，只有两者变化时才
            // 重建标识符——既避免逐帧 Expression.Compile()，也不会把校验错误留在旧模型上。
            if (TryGetFieldTarget(expression, out var model, out var fieldName))
            {
                if (!_fieldIdentifier.HasValue
                    || !ReferenceEquals(_fieldIdentifier.Value.Model, model)
                    || _fieldIdentifier.Value.FieldName != fieldName)
                {
                    _fieldIdentifier = new FieldIdentifier(model, fieldName);
                }

                _cachedExpression = expression;
            }
            else if (!ReferenceEquals(expression, _cachedExpression))
            {
                // 非成员访问表达式无法无编译求值，保留原有的引用缓存回退路径。
                _cachedExpression = expression;
                _fieldIdentifier = CreateFieldIdentifier(expression);
            }
        }
        else
        {
            _cachedExpression = null;
            _fieldIdentifier = null;
        }
    }

    /// <summary>
    /// 解析成员访问表达式，取出其所属模型实例与字段名。
    /// 对闭包链（如 <c>() =&gt; component.Model.Name</c>）只用反射读取，
    /// 不编译表达式树。
    /// </summary>
    private static bool TryGetFieldTarget(
        LambdaExpression expression,
        out object model,
        out string fieldName)
    {
        fieldName = string.Empty;
        model = null!;

        var body = expression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        if (body is not MemberExpression member
            || member.Expression is null
            || !TryEvaluateModel(member.Expression, out var value)
            || value is null)
        {
            return false;
        }

        fieldName = member.Member.Name;
        model = value;
        return true;
    }

    /// <summary>
    /// 在不编译表达式树的前提下求出模型表达式：仅支持常量与闭包字段/属性链。
    /// 任何带方法调用或索引器的写法都返回 false，交由编译回退处理。
    /// </summary>
    private static bool TryEvaluateModel(Expression? expression, out object? value)
    {
        if (expression is ConstantExpression constant)
        {
            value = constant.Value;
            return true;
        }

        if (expression is MemberExpression { Expression: not null } member
            && TryEvaluateModel(member.Expression, out var target)
            && target is not null)
        {
            switch (member.Member)
            {
                case FieldInfo field:
                    value = field.GetValue(target);
                    return true;
                case PropertyInfo property when property.GetIndexParameters().Length == 0:
                    value = property.GetValue(target);
                    return true;
            }
        }

        value = null;
        return false;
    }

    private static FieldIdentifier CreateFieldIdentifier(LambdaExpression expression)
    {
        // FieldIdentifier.Create<T> 要求类型化的 Expression<Func<T>>，而这里收到的是
        // LambdaExpression。解析成员表达式以提取模型实例与字段名。
        if (expression.Body is MemberExpression memberExpression)
        {
            var model = EvaluateExpression(memberExpression.Expression!);
            return new FieldIdentifier(model, memberExpression.Member.Name);
        }

        // 一元转换（如值类型装箱）的回退路径
        if (expression.Body is UnaryExpression { Operand: MemberExpression innerMember })
        {
            var model = EvaluateExpression(innerMember.Expression!);
            return new FieldIdentifier(model, innerMember.Member.Name);
        }

        throw new ArgumentException("所提供的表达式必须是成员访问表达式。", nameof(expression));
    }

    // 表达式求值需要运行时编译（与 Blazor InputBase/FieldIdentifier.Create 的
    // 校验管线同特性），其动态代码约束在本方法内抑制。
    [UnconditionalSuppressMessage("Trimming", "IL2026: 与反射相关的标注可能导致运行时缺失项",
        Justification = "字段表达式求值与框架 InputBase 校验管线同源，需运行时解释器。")]
    [UnconditionalSuppressMessage("AOT", "IL3050: 调用标注了 RequiresDynamicCode 的方法",
        Justification = "字段表达式求值与框架 InputBase 校验管线同源，需运行时解释器。")]
    private static object EvaluateExpression(Expression expression) =>
        Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object))).Compile()();

    private void OnValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) =>
        StateHasChanged();

    /// <inheritdoc />
    public void Dispose()
    {
        _subscribedEditContext?.OnValidationStateChanged -= OnValidationStateChanged;

        GC.SuppressFinalize(this);
    }
}
