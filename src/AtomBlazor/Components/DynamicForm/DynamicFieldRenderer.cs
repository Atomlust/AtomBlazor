using System.Globalization;
using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AtomBlazor;

/// <summary>
/// 根据 <see cref="FormFieldDefinition"/> 渲染对应组件的静态辅助类。
/// 由 <see cref="DynamicForm"/> 内部使用，将 <see cref="FieldType"/> 映射到具体组件。
/// </summary>
internal static class DynamicFieldRenderer
{
    /// <summary>
    /// 将单个字段渲染进渲染树。
    /// </summary>
    public static void RenderField(
        RenderTreeBuilder builder,
        int seq,
        FormFieldDefinition field,
        object? value,
        Func<object?, Task> onValueChanged,
        string? errorText,
        bool disabled,
        bool readOnly,
        IComponent owner,
        RenderFragment<DynamicFieldRenderContext>? customRenderer,
        FormLayout layout = FormLayout.Vertical)
    {
        var baseDisabled = disabled || field.Disabled;
        var isReadOnly = readOnly || field.ReadOnly;
        // 对于没有原生 ReadOnly 支持的字段类型，只读一律降级为禁用
        var isDisabled = baseDisabled || isReadOnly;

        switch (field.Type)
        {
            case FieldType.Text:
            case FieldType.Email:
            case FieldType.Password:
            case FieldType.Url:
            case FieldType.Phone:
                RenderTextInput(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Number:
                RenderNumericInput(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Currency:
                RenderCurrencyInput(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Textarea:
                RenderTextarea(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.RichText:
                RenderRichText(builder, seq, field, value, onValueChanged, errorText, baseDisabled, isReadOnly, owner, layout);
                break;

            case FieldType.Select:
                RenderSelect(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Combobox:
                RenderCombobox(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.MultiSelect:
                RenderMultiSelect(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.NativeSelect:
                RenderNativeSelect(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Checkbox:
                RenderCheckbox(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Switch:
                RenderSwitch(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.CheckboxGroup:
                RenderCheckboxGroup(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.RadioGroup:
                RenderRadioGroup(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Date:
                RenderDatePicker(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.DateTime:
                RenderDateTimePicker(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.DateRange:
                RenderDateRangePicker(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Time:
                RenderTimePicker(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Color:
                RenderColorPicker(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.OTP:
                RenderInputOTP(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Slider:
                RenderSlider(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.RangeSlider:
                RenderRangeSlider(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Tags:
                RenderTagInput(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.File:
                RenderFileUpload(builder, seq, field, value, onValueChanged, errorText, isDisabled, owner, layout);
                break;

            case FieldType.Custom:
                RenderCustomField(builder, seq, field, value, onValueChanged, errorText, baseDisabled, isReadOnly, customRenderer, layout);
                break;
        }
    }

    // ── 文本输入（Text、Email、Password、Url、Phone） ─────────

    private static void RenderTextInput(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        var inputType = field.Type switch
        {
            FieldType.Email => InputType.Email,
            FieldType.Password => InputType.Password,
            FieldType.Url => InputType.Url,
            FieldType.Phone => InputType.Tel,
            _ => InputType.Text
        };

        builder.OpenComponent<FormFieldInput<string>>(seq);
        builder.AddAttribute(seq + 1, "Value", value as string);
        builder.AddAttribute(seq + 2, "ValueChanged", EventCallback.Factory.Create<string?>(owner, v => onValueChanged(v)));
        builder.AddAttribute(seq + 3, "Type", inputType);
        AddCommonFormFieldAttributes(builder, seq + 4, field, errorText, disabled, layout);
        builder.AddAttribute(seq + 9, "Required", field.Required);
        if (field.Placeholder is not null)
        {
            builder.AddAttribute(seq + 10, "Placeholder", field.Placeholder);
        }

        builder.CloseComponent();
    }

    // ── 数字输入 ──────────────────────────────────────────────

    private static void RenderNumericInput(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, controlId, describedById) =>
        {
            controlBuilder.OpenComponent<NumericInput<double>>(0);
            controlBuilder.AddAttribute(1, "Value", ConvertToDouble(value));
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<double>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            if (field.Placeholder is not null)
            {
                controlBuilder.AddAttribute(4, "Placeholder", field.Placeholder);
            }

            AddMetadataDouble(controlBuilder, 5, field, "min", "Min");
            AddMetadataDouble(controlBuilder, 6, field, "max", "Max");
            AddMetadataDouble(controlBuilder, 7, field, "step", "Step");
            controlBuilder.AddAttribute(8, "Id", controlId);
            controlBuilder.AddAttribute(9, "Required", field.Required);
            if (describedById is not null)
            {
                controlBuilder.AddAttribute(10, "AriaDescribedBy", describedById);
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 货币输入 ──────────────────────────────────────────────

    private static void RenderCurrencyInput(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, controlId, describedById) =>
        {
            controlBuilder.OpenComponent<CurrencyInput>(0);
            controlBuilder.AddAttribute(1, "Value", ConvertToDecimal(value));
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            if (field.Placeholder is not null)
            {
                controlBuilder.AddAttribute(4, "Placeholder", field.Placeholder);
            }

            AddMetadataString(controlBuilder, 5, field, "currencyCode", "CurrencyCode");
            controlBuilder.AddAttribute(6, "Id", controlId);
            controlBuilder.AddAttribute(7, "Required", field.Required);
            if (describedById is not null)
            {
                controlBuilder.AddAttribute(8, "AriaDescribedBy", describedById);
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 多行文本域 ────────────────────────────────────────────

    private static void RenderTextarea(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, controlId, describedById) =>
        {
            controlBuilder.OpenComponent<Textarea>(0);
            controlBuilder.AddAttribute(1, "Value", value as string);
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string?>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            if (field.Placeholder is not null)
            {
                controlBuilder.AddAttribute(4, "Placeholder", field.Placeholder);
            }

            controlBuilder.AddAttribute(5, "Id", controlId);
            controlBuilder.AddAttribute(6, "Required", field.Required);
            if (describedById is not null)
            {
                controlBuilder.AddAttribute(7, "AriaDescribedBy", describedById);
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 富文本编辑器 ──────────────────────────────────────────

    /// <summary>
 /// 说明：此处渲染 （Quill 富文本编辑器， 排期交付）。
 /// 时该组件尚未，按 Select→Sheet 先例以 Textarea 兜底渲染——
    /// 保留取值/设值/禁用/占位符与 ARIA 接线，但丢失富文本编辑能力
    /// 且只读态暂降级为禁用（Textarea 无 ReadOnly 参数）。
 /// 待 RichTextEditor 交付时替换为本组件并恢复 ReadOnly 直传。
    /// </summary>
    private static void RenderRichText(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, bool readOnly, IComponent owner, FormLayout layout)
    {
        // readOnly 在当前兜底下并入 disabled（见方法注释的说明）
        var effectiveDisabled = disabled || readOnly;

        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, controlId, describedById) =>
        {
            controlBuilder.OpenComponent<Textarea>(0);
            controlBuilder.AddAttribute(1, "Value", value as string);
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string?>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", effectiveDisabled);
            if (field.Placeholder is not null)
            {
                controlBuilder.AddAttribute(4, "Placeholder", field.Placeholder);
            }

            controlBuilder.AddAttribute(5, "Id", controlId);
            controlBuilder.AddAttribute(6, "Required", field.Required);
            if (describedById is not null)
            {
                controlBuilder.AddAttribute(7, "AriaDescribedBy", describedById);
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 下拉选择 ──────────────────────────────────────────────

    private static void RenderSelect(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        builder.OpenComponent<FormFieldSelect<string>>(seq);
        builder.AddAttribute(seq + 1, "Value", value as string);
        builder.AddAttribute(seq + 2, "ValueChanged", EventCallback.Factory.Create<string?>(owner, v => onValueChanged(v)));
        if (field.Options is not null)
        {
            builder.AddAttribute(seq + 3, "Options", (IEnumerable<SelectOption<string>>)field.Options);
        }

        AddCommonFormFieldAttributes(builder, seq + 4, field, errorText, disabled, layout);
        if (field.Placeholder is not null)
        {
            builder.AddAttribute(seq + 9, "Placeholder", field.Placeholder);
        }

        builder.CloseComponent();
    }

    // ── 组合框 ────────────────────────────────────────────────

    private static void RenderCombobox(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        builder.OpenComponent<FormFieldCombobox<string>>(seq);
        builder.AddAttribute(seq + 1, "Value", value as string);
        builder.AddAttribute(seq + 2, "ValueChanged", EventCallback.Factory.Create<string?>(owner, v => onValueChanged(v)));
        if (field.Options is not null)
        {
            builder.AddAttribute(seq + 3, "Options", (IEnumerable<SelectOption<string>>)field.Options);
        }

        AddCommonFormFieldAttributes(builder, seq + 4, field, errorText, disabled, layout);
        if (field.Placeholder is not null)
        {
            builder.AddAttribute(seq + 9, "Placeholder", field.Placeholder);
        }

        builder.CloseComponent();
    }

    // ── 多选 ──────────────────────────────────────────────────

    private static void RenderMultiSelect(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        builder.OpenComponent<FormFieldMultiSelect<string>>(seq);
        builder.AddAttribute(seq + 1, "Values", value as IEnumerable<string>);
        builder.AddAttribute(seq + 2, "ValuesChanged", EventCallback.Factory.Create<IEnumerable<string>?>(owner, v => onValueChanged(v)));
        if (field.Options is not null)
        {
            builder.AddAttribute(seq + 3, "Options", (IEnumerable<SelectOption<string>>)field.Options);
        }

        AddCommonFormFieldAttributes(builder, seq + 4, field, errorText, disabled, layout);
        if (field.Placeholder is not null)
        {
            builder.AddAttribute(seq + 9, "Placeholder", field.Placeholder);
        }

        builder.CloseComponent();
    }

    // ── 原生下拉 ──────────────────────────────────────────────

    private static void RenderNativeSelect(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<NativeSelect<string>>(0);
            controlBuilder.AddAttribute(1, "Value", value as string);
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string?>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            if (field.Placeholder is not null)
            {
                controlBuilder.AddAttribute(4, "Placeholder", field.Placeholder);
            }

            if (field.Options is not null)
            {
                controlBuilder.AddAttribute(5, "ChildContent", (RenderFragment)(optionBuilder =>
                {
                    var optSeq = 0;
                    foreach (var option in field.Options)
                    {
                        optionBuilder.OpenElement(optSeq, "option");
                        optionBuilder.AddAttribute(optSeq + 1, "value", option.Value);
                        optionBuilder.AddContent(optSeq + 2, option.Text);
                        optionBuilder.CloseElement();
                        optSeq += 10;
                    }
                }));
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 复选框 ────────────────────────────────────────────────

    private static void RenderCheckbox(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        builder.OpenComponent<FormFieldCheckbox>(seq);
        builder.AddAttribute(seq + 1, "Checked", ConvertToBool(value));
        builder.AddAttribute(seq + 2, "CheckedChanged", EventCallback.Factory.Create<bool>(owner, v => onValueChanged(v)));
        AddCommonFormFieldAttributes(builder, seq + 3, field, errorText, disabled, layout);
        builder.CloseComponent();
    }

    // ── 开关 ──────────────────────────────────────────────────

    private static void RenderSwitch(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        builder.OpenComponent<FormFieldSwitch>(seq);
        builder.AddAttribute(seq + 1, "Checked", ConvertToBool(value));
        builder.AddAttribute(seq + 2, "CheckedChanged", EventCallback.Factory.Create<bool>(owner, v => onValueChanged(v)));
        AddCommonFormFieldAttributes(builder, seq + 3, field, errorText, disabled, layout);
        builder.CloseComponent();
    }

    // ── 复选框组 ──────────────────────────────────────────────

    private static void RenderCheckboxGroup(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<CheckboxGroup<string>>(0);
            var selectedValues = value as IReadOnlyCollection<string>
                ?? (value as IEnumerable<string>)?.ToArray()
                ?? Array.Empty<string>();
            controlBuilder.AddAttribute(1, "Values", selectedValues);
            controlBuilder.AddAttribute(2, "ValuesChanged", EventCallback.Factory.Create<IReadOnlyCollection<string>>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);

            if (field.Options is not null)
            {
                controlBuilder.AddAttribute(4, "ChildContent", (RenderFragment)(itemBuilder =>
                {
                    var itemSeq = 0;
                    foreach (var option in field.Options)
                    {
                        itemBuilder.OpenComponent<CheckboxGroupItem<string>>(itemSeq);
                        itemBuilder.AddAttribute(itemSeq + 1, "Value", option.Value);
                        itemBuilder.AddAttribute(itemSeq + 2, "Label", option.Text);
                        itemBuilder.CloseComponent();
                        itemSeq += 10;
                    }
                }));
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 单选组 ────────────────────────────────────────────────

    private static void RenderRadioGroup(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        builder.OpenComponent<FormFieldRadioGroup<string>>(seq);
        builder.AddAttribute(seq + 1, "Value", value as string ?? "");
        builder.AddAttribute(seq + 2, "ValueChanged", EventCallback.Factory.Create<string>(owner, v => onValueChanged(v)));
        AddCommonFormFieldAttributes(builder, seq + 3, field, errorText, disabled, layout);

        if (field.Options is not null)
        {
            builder.AddAttribute(seq + 10, "ChildContent", (RenderFragment)(itemBuilder =>
            {
                var itemSeq = 0;
                foreach (var option in field.Options)
                {
                    itemBuilder.OpenComponent<RadioGroupItem<string>>(itemSeq);
                    itemBuilder.AddAttribute(itemSeq + 1, "Value", option.Value);
                    itemBuilder.AddAttribute(itemSeq + 2, "Label", option.Text);
                    itemBuilder.CloseComponent();
                    itemSeq += 10;
                }
            }));
        }

        builder.CloseComponent();
    }

    // ── 日期选择器 ────────────────────────────────────────────

    private static void RenderDatePicker(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<DatePicker>(0);
            controlBuilder.AddAttribute(1, "Value", value as DateTime?);
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<DateTime?>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            if (field.Placeholder is not null)
            {
                controlBuilder.AddAttribute(4, "Placeholder", field.Placeholder);
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 日期时间选择器 ────────────────────────────────────────

    private static void RenderDateTimePicker(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        var dateTime = value as DateTime?;

        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenElement(0, "div");
            controlBuilder.AddAttribute(1, "class", "atom:flex atom:items-start atom:gap-2");

            // 日期选择器
            controlBuilder.OpenComponent<DatePicker>(2);
            controlBuilder.AddAttribute(3, "Value", dateTime?.Date);
            controlBuilder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<DateTime?>(owner, newDate =>
            {
                var currentTime = dateTime?.TimeOfDay ?? TimeSpan.Zero;
                var combined = newDate.HasValue ? newDate.Value.Date + currentTime : (DateTime?)null;
                return onValueChanged(combined);
            }));
            controlBuilder.AddAttribute(5, "Disabled", disabled);
            controlBuilder.CloseComponent();

            // 时间选择器
            controlBuilder.OpenComponent<TimePicker>(10);
            controlBuilder.AddAttribute(11, "Value", dateTime?.TimeOfDay);
            controlBuilder.AddAttribute(12, "ValueChanged", EventCallback.Factory.Create<TimeSpan?>(owner, newTime =>
            {
                if (!dateTime.HasValue)
                {
                    return onValueChanged(null);
                }

                var combined = dateTime.Value.Date + (newTime ?? TimeSpan.Zero);
                return onValueChanged(combined);
            }));
            controlBuilder.AddAttribute(13, "Disabled", disabled);
            controlBuilder.CloseComponent();

            controlBuilder.CloseElement();
        });
    }

    // ── 日期区间选择器 ────────────────────────────────────────

    private static void RenderDateRangePicker(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<DateRangePicker>(0);
            controlBuilder.AddAttribute(1, "Value", value as DateRange);
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<DateRange?>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            if (field.Placeholder is not null)
            {
                controlBuilder.AddAttribute(4, "Placeholder", field.Placeholder);
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 时间选择器 ────────────────────────────────────────────

    private static void RenderTimePicker(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<TimePicker>(0);
            controlBuilder.AddAttribute(1, "Value", value as TimeSpan?);
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<TimeSpan?>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            if (field.Placeholder is not null)
            {
                controlBuilder.AddAttribute(4, "Placeholder", field.Placeholder);
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 颜色选择器 ────────────────────────────────────────────

    private static void RenderColorPicker(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<ColorPicker>(0);
            controlBuilder.AddAttribute(1, "Value", value as string ?? "#000000");
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            controlBuilder.CloseComponent();
        });
    }

    // ── 一次性密码输入 ────────────────────────────────────────

    private static void RenderInputOTP(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<InputOTP>(0);
            controlBuilder.AddAttribute(1, "Value", value as string);
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            AddMetadataInt(controlBuilder, 4, field, "length", "Length");
            controlBuilder.CloseComponent();
        });
    }

    // ── 滑块 ──────────────────────────────────────────────────

    private static void RenderSlider(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<Slider>(0);
            controlBuilder.AddAttribute(1, "Value", ConvertToDouble(value));
            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<double>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            AddMetadataDouble(controlBuilder, 4, field, "min", "Min");
            AddMetadataDouble(controlBuilder, 5, field, "max", "Max");
            AddMetadataDouble(controlBuilder, 6, field, "step", "Step");
            controlBuilder.CloseComponent();
        });
    }

    // ── 区间滑块 ──────────────────────────────────────────────

    private static void RenderRangeSlider(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<RangeSlider>(0);
            if (value is ValueTuple<double, double> range)
            {
                controlBuilder.AddAttribute(1, "Value", range);
            }

            controlBuilder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<(double Start, double End)>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            AddMetadataDouble(controlBuilder, 4, field, "min", "Min");
            AddMetadataDouble(controlBuilder, 5, field, "max", "Max");
            AddMetadataDouble(controlBuilder, 6, field, "step", "Step");
            controlBuilder.CloseComponent();
        });
    }

    // ── 标签输入 ──────────────────────────────────────────────

    private static void RenderTagInput(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<TagInput>(0);
            controlBuilder.AddAttribute(1, "Tags", value as IReadOnlyList<string>);
            controlBuilder.AddAttribute(2, "TagsChanged", EventCallback.Factory.Create<IReadOnlyList<string>?>(owner, v => onValueChanged(v)));
            controlBuilder.AddAttribute(3, "Disabled", disabled);
            if (field.Placeholder is not null)
            {
                controlBuilder.AddAttribute(4, "Placeholder", field.Placeholder);
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 文件上传 ──────────────────────────────────────────────

    private static void RenderFileUpload(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, IComponent owner, FormLayout layout)
    {
        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
        {
            controlBuilder.OpenComponent<FileUpload>(0);
            controlBuilder.AddAttribute(1, "Disabled", disabled);
            AddMetadataString(controlBuilder, 2, field, "accept", "Accept");
            AddMetadataBool(controlBuilder, 3, field, "multiple", "Multiple");
            controlBuilder.AddAttribute(4, "FilesChanged",
                EventCallback.Factory.Create<IReadOnlyList<FileUploadItem>>(owner, files => onValueChanged(files)));
            if (value is IReadOnlyList<FileUploadItem> existingFiles)
            {
                controlBuilder.AddAttribute(5, "Files", existingFiles);
            }

            controlBuilder.CloseComponent();
        });
    }

    // ── 自定义字段 ────────────────────────────────────────────

    private static void RenderCustomField(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field,
        object? value, Func<object?, Task> onValueChanged, string? errorText,
        bool disabled, bool readOnly,
        RenderFragment<DynamicFieldRenderContext>? customRenderer, FormLayout layout)
    {
        if (customRenderer is null)
        {
            return;
        }

        var context = new DynamicFieldRenderContext(field, value, onValueChanged, disabled, readOnly);

        RenderWrappedField(builder, seq, field, errorText, layout, (controlBuilder, _, _) =>
            controlBuilder.AddContent(0, customRenderer(context)));
    }

    // ── 共享辅助方法 ──────────────────────────────────────────

    /// <summary>
    /// 添加全部 FormFieldBase 封装组件（FormFieldInput、FormFieldSelect 等）
    /// 共享的通用参数。
    /// 注意：此处不包含 Placeholder，因为并非所有封装都支持它
    /// （例如 FormFieldCheckbox、FormFieldSwitch、FormFieldRadioGroup）。
    /// </summary>
    private static void AddCommonFormFieldAttributes(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field, string? errorText, bool disabled,
        FormLayout layout)
    {
        if (field.Label is not null)
        {
            builder.AddAttribute(seq, "Label", field.Label);
        }

        if (field.Description is not null)
        {
            builder.AddAttribute(seq + 1, "HelperText", field.Description);
        }

        builder.AddAttribute(seq + 2, "ErrorText", errorText);
        builder.AddAttribute(seq + 3, "Disabled", disabled);
        builder.AddAttribute(seq + 4, "Orientation", GetFieldOrientation(layout));
    }

    private static FieldOrientation GetFieldOrientation(FormLayout layout) => layout switch
    {
        FormLayout.Horizontal => FieldOrientation.Horizontal,
        _ => FieldOrientation.Vertical
    };

    private static void RenderWrappedField(
        RenderTreeBuilder builder, int seq, FormFieldDefinition field, string? errorText,
        FormLayout layout, Action<RenderTreeBuilder, string, string?> renderControl)
    {
        var controlId = $"atom-df-{field.Name}";
        var descriptionId = $"{controlId}-desc";
        var errorId = $"{controlId}-error";
        var describedById = errorText is not null ? errorId
            : field.Description is not null ? descriptionId
            : null;

        builder.OpenComponent<Field>(seq);
        builder.AddAttribute(seq + 1, "IsInvalid", errorText is not null);
        builder.AddAttribute(seq + 2, "Orientation", GetFieldOrientation(layout));
        builder.AddAttribute(seq + 3, "ChildContent", (RenderFragment)(innerBuilder =>
        {
            // 标签
            if (field.Label is not null)
            {
                innerBuilder.OpenComponent<FieldLabel>(0);
                innerBuilder.AddAttribute(1, "For", controlId);
                innerBuilder.AddAttribute(2, "ChildContent", (RenderFragment)(lb =>
                    lb.AddContent(0, field.Label)));
                innerBuilder.CloseComponent();
            }

            // 将控件与描述/错误一并包进 FieldContent，保证水平布局下
            // 的一致性（提供 min-w-0 与垂直堆叠）
            innerBuilder.OpenComponent<FieldContent>(10);
            innerBuilder.AddAttribute(11, "ChildContent", (RenderFragment)(contentBuilder =>
            {
                // 控件——传入 controlId 与 describedById 完成 ARIA 接线
                renderControl(contentBuilder, controlId, describedById);

                // 描述或错误
                if (errorText is not null)
                {
                    contentBuilder.OpenComponent<FieldError>(50);
                    contentBuilder.AddAttribute(51, "Id", errorId);
                    contentBuilder.AddAttribute(52, "ChildContent", (RenderFragment)(eb =>
                        eb.AddContent(0, errorText)));
                    contentBuilder.CloseComponent();
                }
                else if (field.Description is not null)
                {
                    contentBuilder.OpenComponent<FieldDescription>(50);
                    contentBuilder.AddAttribute(51, "Id", descriptionId);
                    contentBuilder.AddAttribute(52, "ChildContent", (RenderFragment)(db =>
                        db.AddContent(0, field.Description)));
                    contentBuilder.CloseComponent();
                }
            }));
            innerBuilder.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private static void AddMetadataDouble(RenderTreeBuilder builder, int seq, FormFieldDefinition field, string key, string paramName)
    {
        if (field.Metadata?.TryGetValue(key, out var val) == true)
        {
            builder.AddAttribute(seq, paramName, Convert.ToDouble(val, CultureInfo.InvariantCulture));
        }
    }

    private static void AddMetadataInt(RenderTreeBuilder builder, int seq, FormFieldDefinition field, string key, string paramName)
    {
        if (field.Metadata?.TryGetValue(key, out var val) == true)
        {
            builder.AddAttribute(seq, paramName, Convert.ToInt32(val, CultureInfo.InvariantCulture));
        }
    }

    private static void AddMetadataString(RenderTreeBuilder builder, int seq, FormFieldDefinition field, string key, string paramName)
    {
        if (field.Metadata?.TryGetValue(key, out var val) == true)
        {
            builder.AddAttribute(seq, paramName, val.ToString());
        }
    }

    private static void AddMetadataBool(RenderTreeBuilder builder, int seq, FormFieldDefinition field, string key, string paramName)
    {
        if (field.Metadata?.TryGetValue(key, out var val) == true)
        {
            builder.AddAttribute(seq, paramName, Convert.ToBoolean(val, CultureInfo.InvariantCulture));
        }
    }

    private static double ConvertToDouble(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private static decimal ConvertToDecimal(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static bool ConvertToBool(object? value)
    {
        return value switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var result) && result,
            _ => false
        };
    }
}
