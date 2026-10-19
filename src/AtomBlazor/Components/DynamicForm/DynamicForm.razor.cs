using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AtomBlazor;

/// <summary>
/// 根据 <see cref="FormSchema"/> 定义渲染完整表单，并为每个字段自动挑选
/// 合适的输入组件。
/// </summary>
public partial class DynamicForm : ComponentBase
{
    private Dictionary<string, string?> fieldErrors = new();
    private bool isSubmitting;

    /// <summary>
    /// 获取或设置表单的 schema 定义。
    /// </summary>
    [Parameter]
    public FormSchema? Schema { get; set; }

    /// <summary>
    /// 获取或设置当前表单值字典。使用 <c>@bind-Values</c> 可进行双向绑定。
    /// </summary>
    [Parameter]
    public Dictionary<string, object?> Values { get; set; } = new();

    /// <summary>
    /// 获取或设置值字典变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<Dictionary<string, object?>> ValuesChanged { get; set; }

    /// <summary>
    /// 获取或设置表单提交时调用的回调（无论校验是否通过）。
    /// </summary>
    [Parameter]
    public EventCallback<Dictionary<string, object?>> OnSubmit { get; set; }

    /// <summary>
    /// 获取或设置全部校验通过提交表单时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<Dictionary<string, object?>> OnValidSubmit { get; set; }

    /// <summary>
    /// 获取或设置任意字段值变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<FormFieldChangedEventArgs> OnFieldChanged { get; set; }

    /// <summary>
    /// 获取或设置表单布局模式。
    /// </summary>
    [Parameter]
    public FormLayout Layout { get; set; } = FormLayout.Vertical;

    /// <summary>
    /// 获取或设置使用 <see cref="FormLayout.Horizontal"/> 布局时字段标签的固定宽度。
    /// 保证所有标签等宽，使输入框对齐一致。
    /// 接受任意合法 CSS 宽度值（如 "100px"、"8rem"）。
    /// 为 null 时标签随内容收缩。
    /// </summary>
    [Parameter]
    public string? LabelWidth { get; set; }

    /// <summary>
    /// 获取或设置字段网格布局的列数。
    /// </summary>
    [Parameter]
    public int Columns { get; set; } = 1;

    /// <summary>
    /// 获取或设置是否将所有字段渲染为只读。
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>
    /// 获取或设置是否禁用所有字段。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置内置提交按钮的文本。
    /// </summary>
    [Parameter]
    public string SubmitText { get; set; } = "Submit";

    /// <summary>
    /// 获取或设置是否显示内置提交按钮。
    /// </summary>
    [Parameter]
    public bool ShowSubmitButton { get; set; } = true;

    /// <summary>
    /// 获取或设置是否在表单顶部显示校验错误摘要。
    /// 为 false（默认）时错误仅在每个字段旁内联展示。
    /// </summary>
    [Parameter]
    public bool ShowValidationSummary { get; set; }

    /// <summary>
    /// 获取或设置应用到表单元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置按字段名索引的自定义字段渲染器。
    /// 用于为 <see cref="FieldType.Custom"/> 字段提供自定义渲染。
    /// </summary>
    [Parameter]
    public Dictionary<string, RenderFragment<DynamicFieldRenderContext>>? FieldRenderers { get; set; }

    /// <summary>
    /// 获取或设置渲染在表单内部的附加内容（如额外按钮）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        ApplyDefaultValues();
        ClearHiddenFieldValues();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ApplyDefaultValues();
        ClearHiddenFieldValues();
    }

    private void ApplyDefaultValues()
    {
        if (Schema is null)
        {
            return;
        }

        foreach (var field in GetAllFields())
        {
            if (!Values.ContainsKey(field.Name))
            {
                if (field.DefaultValue is not null)
                {
                    Values[field.Name] = field.DefaultValue;
                }
                else
                {
                    // 为非可空字段类型预置默认值，使校验与渲染出的默认态一致
                    var implicitDefault = GetImplicitDefault(field.Type);
                    if (implicitDefault is not null)
                    {
                        Values[field.Name] = implicitDefault;
                    }
                }
            }
        }
    }

    private static object? GetImplicitDefault(FieldType type)
    {
        return type switch
        {
            FieldType.Number => 0d,
            FieldType.Currency => 0m,
            FieldType.Slider => 0d,
            FieldType.RangeSlider => (0d, 0d),
            FieldType.Checkbox => false,
            FieldType.Switch => false,
            _ => null
        };
    }

    private IEnumerable<FormFieldDefinition> GetAllFields()
    {
        if (Schema is null)
        {
            return [];
        }

        if (Schema.Sections.Count > 0)
        {
            return Schema.Sections.SelectMany(s => s.Fields);
        }

        return Schema.Fields;
    }

    private static List<FormFieldDefinition> GetOrderedFields(List<FormFieldDefinition> fields)
    {
        return fields
            .Select((f, i) => (field: f, index: i))
            .OrderBy(x => x.field.Order ?? x.index)
            .Select(x => x.field)
            .ToList();
    }

    private bool IsFieldVisible(FormFieldDefinition field)
    {
        if (string.IsNullOrEmpty(field.VisibleWhen))
        {
            return true;
        }

        try
        {
            return VisibilityExpression.Evaluate(field.VisibleWhen, Values);
        }
        catch
        {
            return true;
        }
    }

    private bool IsSectionVisible(FormSectionDefinition section)
    {
        if (string.IsNullOrEmpty(section.VisibleWhen))
        {
            return true;
        }

        try
        {
            return VisibilityExpression.Evaluate(section.VisibleWhen, Values);
        }
        catch
        {
            return true;
        }
    }

    private int GetEffectiveColumns(int? sectionOverride) =>
        sectionOverride ?? Schema?.Columns ?? Columns;

    private static string GetGridClass(int columns)
    {
        return columns switch
        {
            1 => "atom:grid atom:grid-cols-1 atom:gap-4",
            2 => "atom:grid atom:grid-cols-1 atom:md:grid-cols-2 atom:gap-4",
            3 => "atom:grid atom:grid-cols-1 atom:md:grid-cols-2 atom:lg:grid-cols-3 atom:gap-4",
            // 钳制到 4 列响应式预设，保证移动端布局不破版
            _ => "atom:grid atom:grid-cols-1 atom:md:grid-cols-2 atom:lg:grid-cols-4 atom:gap-4"
        };
    }

    private static string? GetGridStyle(int columns) => null;

    private static string? GetColSpanStyle(FormFieldDefinition field, int maxColumns)
    {
        if (field.ColSpan > 1 && maxColumns > 1)
        {
            var span = Math.Min(field.ColSpan, maxColumns);
            return $"grid-column: span {span} / span {span}";
        }

        return null;
    }

    // ── 值变化处理 ────────────────────────────────────────────

    private async Task OnFieldValueChanged(string fieldName, object? newValue)
    {
        var oldValue = Values.TryGetValue(fieldName, out var v) ? v : null;
        Values[fieldName] = newValue;

        // 字段变化时清除该字段错误
        fieldErrors.Remove(fieldName);

        // 清除刚刚变为隐藏状态的字段的值与错误
        ClearHiddenFieldValues();

        // 为可能重新可见的字段重新应用默认值
        ApplyDefaultValues();

        await ValuesChanged.InvokeAsync(Values);

        if (OnFieldChanged.HasDelegate)
        {
            await OnFieldChanged.InvokeAsync(new FormFieldChangedEventArgs(fieldName, oldValue, newValue));
        }

        StateHasChanged();
    }

    private void ClearHiddenFieldValues()
    {
        if (Schema is null)
        {
            return;
        }

        if (Schema.Sections.Count > 0)
        {
            foreach (var section in Schema.Sections)
            {
                var sectionHidden = !IsSectionVisible(section);
                foreach (var field in section.Fields)
                {
                    if ((sectionHidden || !IsFieldVisible(field)) && Values.ContainsKey(field.Name))
                    {
                        Values.Remove(field.Name);
                        fieldErrors.Remove(field.Name);
                    }
                }
            }
        }
        else
        {
            foreach (var field in Schema.Fields)
            {
                if (!IsFieldVisible(field) && Values.ContainsKey(field.Name))
                {
                    Values.Remove(field.Name);
                    fieldErrors.Remove(field.Name);
                }
            }
        }
    }

    private Func<object?, Task> CreateFieldCallback(string fieldName) =>
        newValue => OnFieldValueChanged(fieldName, newValue);

    // ── 校验 ──────────────────────────────────────────────────

    private bool ValidateForm()
    {
        fieldErrors.Clear();

        var fieldsToValidate = Schema is not null && Schema.Sections.Count > 0
            ? Schema.Sections
                .Where(s => IsSectionVisible(s))
                .SelectMany(s => s.Fields)
            : GetAllFields();

        foreach (var field in fieldsToValidate)
        {
            if (!IsFieldVisible(field))
            {
                continue;
            }

            Values.TryGetValue(field.Name, out var value);
            var error = ValidateField(field, value);
            if (error is not null)
            {
                fieldErrors[field.Name] = error;
            }
        }

        return fieldErrors.Count == 0;
    }

    private static string? ValidateField(FormFieldDefinition field, object? value)
    {
        // 必填校验——对布尔字段，"必填"意味着值必须为 true
        if (field.Required)
        {
            if (field.Type is FieldType.Checkbox or FieldType.Switch)
            {
                if (!CoerceToBool(value))
                {
                    return $"{field.Label ?? field.Name} is required.";
                }
            }
            else if (IsEmpty(value))
            {
                return $"{field.Label ?? field.Name} is required.";
            }
        }

        // 非必填且为空时跳过后续校验
        if (IsEmpty(value))
        {
            return null;
        }

        if (field.Validations is null)
        {
            return null;
        }

        foreach (var validation in field.Validations)
        {
            var error = EvaluateValidation(field, validation, value);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? EvaluateValidation(FormFieldDefinition field, FieldValidation validation, object? value)
    {
        var label = field.Label ?? field.Name;
        var str = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value?.ToString() ?? "";

        switch (validation.Type)
        {
            case ValidationType.Required:
                if (IsEmpty(value))
                {
                    return validation.Message ?? $"{label} is required.";
                }

                break;

            case ValidationType.MinLength:
                if (validation.Value is not null)
                {
                    var min = Convert.ToInt32(validation.Value, CultureInfo.InvariantCulture);
                    if (str.Length < min)
                    {
                        return validation.Message ?? $"{label} must be at least {min} characters.";
                    }
                }

                break;

            case ValidationType.MaxLength:
                if (validation.Value is not null)
                {
                    var max = Convert.ToInt32(validation.Value, CultureInfo.InvariantCulture);
                    if (str.Length > max)
                    {
                        return validation.Message ?? $"{label} must be at most {max} characters.";
                    }
                }

                break;

            case ValidationType.Min:
                if (validation.Value is not null && TryConvertToDouble(value, out var minNum))
                {
                    var minVal = Convert.ToDouble(validation.Value, CultureInfo.InvariantCulture);
                    if (minNum < minVal)
                    {
                        return validation.Message ?? $"{label} must be at least {minVal}.";
                    }
                }

                break;

            case ValidationType.Max:
                if (validation.Value is not null && TryConvertToDouble(value, out var maxNum))
                {
                    var maxVal = Convert.ToDouble(validation.Value, CultureInfo.InvariantCulture);
                    if (maxNum > maxVal)
                    {
                        return validation.Message ?? $"{label} must be at most {maxVal}.";
                    }
                }

                break;

            case ValidationType.Pattern:
                if (validation.Value is string pattern)
                {
                    try
                    {
                        if (!Regex.IsMatch(str, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
                        {
                            return validation.Message ?? $"{label} does not match the required format.";
                        }
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        return validation.Message ?? $"{label} does not match the required format.";
                    }
                }

                break;

            case ValidationType.Email:
                if (!Regex.IsMatch(str, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    return validation.Message ?? $"{label} must be a valid email address.";
                }

                break;

            case ValidationType.Url:
                if (!Uri.TryCreate(str, UriKind.Absolute, out _))
                {
                    return validation.Message ?? $"{label} must be a valid URL.";
                }

                break;

            case ValidationType.Phone:
                if (!Regex.IsMatch(str, @"^[\d\s\-\+\(\)]+$"))
                {
                    return validation.Message ?? $"{label} must be a valid phone number.";
                }

                break;

            case ValidationType.Custom:
                if (field.Validations is not null)
                {
                    var customValidator = field.Metadata?.TryGetValue("customValidator", out var validatorObj) == true
                        ? validatorObj as Func<object?, string?>
                        : null;
                    if (customValidator is not null)
                    {
                        var customError = customValidator(value);
                        if (customError is not null)
                        {
                            return validation.Message ?? customError;
                        }
                    }
                }

                break;
        }

        return null;
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d:
                result = d;
                return true;
            case int i:
                result = i;
                return true;
            case decimal m:
                result = (double)m;
                return true;
            case float f:
                result = f;
                return true;
            case long l:
                result = l;
                return true;
            default:
                var str = value is IFormattable fmt
                    ? fmt.ToString(null, CultureInfo.InvariantCulture)
                    : value?.ToString() ?? "";
                return double.TryParse(str, CultureInfo.InvariantCulture, out result);
        }
    }

    private static bool IsEmpty(object? value)
    {
        return value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            System.Collections.IEnumerable enumerable => !enumerable.Cast<object?>().Any(),
            _ => false
        };
    }

    private static bool CoerceToBool(object? value)
    {
        return value switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var parsed) && parsed,
            _ => false
        };
    }

    // ── 表单提交 ──────────────────────────────────────────────

    private async Task HandleSubmit()
    {
        isSubmitting = true;
        // BL0012：HandleSubmit 由 @onsubmit 接线，事件处理器触发的重渲染由框架自动完成。

        try
        {
            if (OnSubmit.HasDelegate)
            {
                await OnSubmit.InvokeAsync(Values);
            }

            var isValid = ValidateForm();
            StateHasChanged();

            if (isValid && OnValidSubmit.HasDelegate)
            {
                await OnValidSubmit.InvokeAsync(Values);
            }
        }
        finally
        {
            isSubmitting = false;
            // BL0012：同上，表单提交事件的重渲染由框架自动完成。
        }
    }

    // ── 渲染 ──────────────────────────────────────────────────

    private void RenderFieldsGrid(RenderTreeBuilder builder, List<FormFieldDefinition> fields, int columns)
    {
        var orderedFields = GetOrderedFields(fields);
        var seq = 0;

        builder.OpenElement(seq++, "div");
        var containerClass = Layout == FormLayout.Inline
            ? "atom:flex atom:flex-wrap atom:items-end atom:gap-4"
            : GetGridClass(columns);
        builder.AddAttribute(seq++, "class", containerClass);

        var gridStyle = Layout == FormLayout.Inline ? null : GetGridStyle(columns);
        var labelWidthStyle = Layout == FormLayout.Horizontal && LabelWidth is not null
            ? $"--atom-label-width: {LabelWidth}"
            : null;
        var combinedStyle = string.Join("; ", new[] { gridStyle, labelWidthStyle }.Where(s => s is not null));
        if (combinedStyle.Length > 0)
        {
            builder.AddAttribute(seq++, "style", combinedStyle);
        }

        foreach (var field in orderedFields)
        {
            if (!IsFieldVisible(field))
            {
                continue;
            }

            var colSpanStyle = GetColSpanStyle(field, columns);
            builder.OpenElement(seq, "div");
            builder.SetKey(field.Name);
            if (Layout == FormLayout.Inline)
            {
                builder.AddAttribute(seq + 1, "class", "atom:flex-1 atom:min-w-32");
            }
            else if (colSpanStyle is not null)
            {
                builder.AddAttribute(seq + 1, "style", colSpanStyle);
            }

            fieldErrors.TryGetValue(field.Name, out var errorText);
            Values.TryGetValue(field.Name, out var value);

            RenderFragment<DynamicFieldRenderContext>? customRenderer = null;
            if (field.Type == FieldType.Custom)
            {
                FieldRenderers?.TryGetValue(field.Name, out customRenderer);
            }

            DynamicFieldRenderer.RenderField(
                builder,
                seq + 10,
                field,
                value,
                CreateFieldCallback(field.Name),
                errorText,
                Disabled,
                ReadOnly,
                this,
                customRenderer,
                Layout);

            builder.CloseElement();
            seq += 100;
        }

        builder.CloseElement();
    }

    private string FormCssClass => ClassNames.cn(
        Layout == FormLayout.Inline ? "atom:space-y-4" : "atom:space-y-6",
        Class
    );

    private string? GetFieldErrorText(string fieldName) =>
        fieldErrors.TryGetValue(fieldName, out var error) ? error : null;

    private bool HasErrors => fieldErrors.Count > 0;

    private IEnumerable<string> ErrorSummary => fieldErrors.Values.Where(e => e is not null).Cast<string>();
}
