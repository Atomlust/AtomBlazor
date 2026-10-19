using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using AtomBlazor.Primitives;

namespace AtomBlazor;

/// <summary>
/// 将 JSON 解析为 <see cref="FormSchema"/> 定义的解析器。
/// 支持两种格式：原生 FormSchema JSON 与 JSON Schema（子集）。
/// </summary>
public static class FormSchemaParser
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 将 JSON 字符串解析为 <see cref="FormSchema"/>。
    /// 自动探测输入是原生 FormSchema JSON 还是 JSON Schema。
    /// </summary>
    /// <param name="json">要解析的 JSON 字符串。</param>
    /// <returns>一个 <see cref="FormSchema"/> 实例。</returns>
    /// <exception cref="JsonException">JSON 无效或无法解析。</exception>
    public static FormSchema FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 探测 JSON Schema 格式：含有 "type": "object" 与 "properties"
        if (root.TryGetProperty("type", out var typeElement) &&
            typeElement.GetString() == "object" &&
            root.TryGetProperty("properties", out _))
        {
            return ParseJsonSchema(root);
        }

        // 原生 FormSchema 格式
        return ParseNativeSchema(json);
    }

    // ── 原生 FormSchema 解析 ──────────────────────────────────

    // NativeSchemaDto 为私有 DTO 图，System.Text.Json 反射序列化无法静态分析
    // 其修剪/动态代码约束在本方法内抑制（DTO 全部为公开可序列化形状，随程序集一同保留）。
    [UnconditionalSuppressMessage("Trimming", "IL2026: 与反射相关的标注可能导致运行时缺失项",
        Justification = "私有 DTO 图随程序集保留，运行时反射填充，无外部依赖。")]
    [UnconditionalSuppressMessage("AOT", "IL3050: 调用标注了 RequiresDynamicCode 的方法",
        Justification = "私有 DTO 图随程序集保留，运行时反射填充，无外部依赖。")]
    private static FormSchema ParseNativeSchema(string json)
    {
        // 使用 DTO 以兼容不同的大小写/格式可能
        var dto = JsonSerializer.Deserialize<NativeSchemaDto>(json, jsonOptions)
            ?? throw new JsonException("Failed to deserialize FormSchema from JSON.");

        var schema = new FormSchema
        {
            Title = dto.Title,
            Description = dto.Description,
            Columns = dto.Columns
        };

        if (dto.Sections is not null)
        {
            foreach (var sectionDto in dto.Sections)
            {
                schema.Sections.Add(ParseNativeSection(sectionDto));
            }
        }

        if (dto.Fields is not null)
        {
            foreach (var fieldDto in dto.Fields)
            {
                schema.Fields.Add(ParseNativeField(fieldDto));
            }
        }

        return schema;
    }

    private static FormSectionDefinition ParseNativeSection(NativeSectionDto dto)
    {
        var section = new FormSectionDefinition
        {
            Title = dto.Title,
            Description = dto.Description,
            Columns = dto.Columns,
            VisibleWhen = dto.VisibleWhen,
            Collapsible = dto.Collapsible ?? false,
            DefaultExpanded = dto.DefaultExpanded ?? true
        };

        if (dto.Fields is not null)
        {
            foreach (var fieldDto in dto.Fields)
            {
                section.Fields.Add(ParseNativeField(fieldDto));
            }
        }

        return section;
    }

    private static FormFieldDefinition ParseNativeField(NativeFieldDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new JsonException("Field 'name' is required and cannot be empty.");
        }

        var field = new FormFieldDefinition
        {
            Name = dto.Name,
            Label = dto.Label,
            Description = dto.Description,
            Placeholder = dto.Placeholder,
            Required = dto.Required ?? false,
            Disabled = dto.Disabled ?? false,
            ReadOnly = dto.ReadOnly ?? false,
            Order = dto.Order,
            ColSpan = dto.ColSpan ?? 1,
            VisibleWhen = dto.VisibleWhen
        };

        // 从字符串解析类型
        if (dto.Type is not null && Enum.TryParse<FieldType>(dto.Type, ignoreCase: true, out var fieldType))
        {
            field.Type = fieldType;
        }

        // 解析默认值
        if (dto.DefaultValue.HasValue)
        {
            field.DefaultValue = ParseJsonValue(dto.DefaultValue.Value);
        }

        // 解析选项
        if (dto.Options is not null)
        {
            field.Options = new List<SelectOption<string>>();
            foreach (var optionElement in dto.Options)
            {
                if (optionElement.ValueKind == JsonValueKind.Object)
                {
                    var optValue = optionElement.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                    var optText = optionElement.TryGetProperty("text", out var t) ? t.GetString() ?? optValue : optValue;
                    field.Options.Add(new SelectOption<string>(optValue, optText));
                }
                else if (optionElement.ValueKind == JsonValueKind.String)
                {
                    var str = optionElement.GetString() ?? "";
                    field.Options.Add(new SelectOption<string>(str, str));
                }
            }
        }

        // 解析校验规则
        if (dto.Validations is not null)
        {
            field.Validations = new List<FieldValidation>();
            foreach (var validationElement in dto.Validations)
            {
                // 跳过缺失或无效 type 的条目，避免默认落入 Required
                if (!validationElement.TryGetProperty("type", out var vType))
                {
                    continue;
                }

                var typeString = vType.GetString();
                if (string.IsNullOrWhiteSpace(typeString) ||
                    !Enum.TryParse<ValidationType>(typeString, ignoreCase: true, out var valType))
                {
                    continue;
                }

                var validation = new FieldValidation { Type = valType };

                if (validationElement.TryGetProperty("value", out var vVal))
                {
                    validation.Value = ParseJsonValue(vVal);
                }

                if (validationElement.TryGetProperty("message", out var vMsg))
                {
                    validation.Message = vMsg.GetString();
                }

                field.Validations.Add(validation);
            }
        }

        // 解析元数据
        if (dto.Metadata is not null)
        {
            field.Metadata = new Dictionary<string, object>();
            foreach (var prop in dto.Metadata.Value.EnumerateObject())
            {
                var val = ParseJsonValue(prop.Value);
                if (val is not null)
                {
                    field.Metadata[prop.Name] = val;
                }
            }
        }

        return field;
    }

    // ── JSON Schema 解析 ─────────────────────────────────────

    private static FormSchema ParseJsonSchema(JsonElement root)
    {
        var schema = new FormSchema();

        if (root.TryGetProperty("title", out var title))
        {
            schema.Title = title.GetString();
        }

        if (root.TryGetProperty("description", out var desc))
        {
            schema.Description = desc.GetString();
        }

        // 收集必填字段
        var requiredFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var req in required.EnumerateArray())
            {
                var name = req.GetString();
                if (name is not null)
                {
                    requiredFields.Add(name);
                }
            }
        }

        // 解析 properties
        if (root.TryGetProperty("properties", out var properties))
        {
            var order = 0;
            foreach (var prop in properties.EnumerateObject())
            {
                var field = ParseJsonSchemaProperty(prop.Name, prop.Value, requiredFields);
                field.Order = order++;
                schema.Fields.Add(field);
            }
        }

        return schema;
    }

    private static FormFieldDefinition ParseJsonSchemaProperty(string name, JsonElement property, HashSet<string> requiredFields)
    {
        var field = new FormFieldDefinition
        {
            Name = name,
            Required = requiredFields.Contains(name)
        };

        // title → Label
        if (property.TryGetProperty("title", out var title))
        {
            field.Label = title.GetString();
        }
        else
        {
            // 由属性名自动生成标签
            field.Label = PascalCaseToTitle(name);
        }

        // description
        if (property.TryGetProperty("description", out var desc))
        {
            field.Description = desc.GetString();
        }

        // 默认值
        if (property.TryGetProperty("default", out var defaultVal))
        {
            field.DefaultValue = ParseJsonValue(defaultVal);
        }

        // 判定字段类型
        var jsonType = property.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        var format = property.TryGetProperty("format", out var formatEl) ? formatEl.GetString() : null;
        var hasEnum = property.TryGetProperty("enum", out var enumEl);
        var hasOneOf = property.TryGetProperty("oneOf", out var oneOfEl) &&
                       oneOfEl.ValueKind == JsonValueKind.Array;

        // 检查 x-field-type UI 提示（例如 "combobox"、"multiselect"、"radio"）
        var xFieldType = property.TryGetProperty("x-field-type", out var xftEl) ? xftEl.GetString() : null;

        if (hasEnum)
        {
            field.Type = FieldType.Select;
            field.Options = new List<SelectOption<string>>();
            foreach (var item in enumEl.EnumerateArray())
            {
                var val = item.GetString() ?? item.ToString();
                field.Options.Add(new SelectOption<string>(val, val));
            }
        }
        else if (hasOneOf && TryParseOneOfOptions(oneOfEl, out var oneOfOptions))
        {
            // oneOf 的 const/title 模式 → 带文本/值配对的 Select
            field.Type = FieldType.Select;
            field.Options = oneOfOptions;
        }
        else if (jsonType == "array" && property.TryGetProperty("items", out var itemsEl))
        {
            // array 且带 items → 可推导选项时为 MultiSelect，否则为 Tags
            var itemOptions = ParseItemsOptions(itemsEl);
            if (itemOptions != null)
            {
                field.Type = FieldType.MultiSelect;
                field.Options = itemOptions;
            }
            else
            {
                field.Type = FieldType.Tags;
            }
        }
        else
        {
            field.Type = (jsonType, format) switch
            {
                ("string", "email") => FieldType.Email,
                ("string", "uri") or ("string", "url") => FieldType.Url,
                ("string", "date") => FieldType.Date,
                ("string", "date-time") => FieldType.DateTime,
                ("string", "time") => FieldType.Time,
                ("string", "password") => FieldType.Password,
                ("string", _) => FieldType.Text,
                ("integer", _) or ("number", _) => FieldType.Number,
                ("boolean", _) => FieldType.Checkbox,
                ("array", _) => FieldType.Tags,
                _ => FieldType.Text
            };
        }

        // 应用 x-field-type 覆盖（如提供）
        if (xFieldType is not null)
        {
            if (Enum.TryParse<FieldType>(xFieldType, ignoreCase: true, out var overrideType))
            {
                field.Type = overrideType;
            }
        }

        // 由 JSON Schema 关键字校验规则
        field.Validations = new List<FieldValidation>();

        if (property.TryGetProperty("minLength", out var minLen))
        {
            field.Validations.Add(new FieldValidation
            {
                Type = ValidationType.MinLength,
                Value = minLen.GetInt32()
            });
        }

        if (property.TryGetProperty("maxLength", out var maxLen))
        {
            field.Validations.Add(new FieldValidation
            {
                Type = ValidationType.MaxLength,
                Value = maxLen.GetInt32()
            });
        }

        if (property.TryGetProperty("minimum", out var min))
        {
            field.Validations.Add(new FieldValidation
            {
                Type = ValidationType.Min,
                Value = min.GetDouble()
            });
        }

        if (property.TryGetProperty("maximum", out var max))
        {
            field.Validations.Add(new FieldValidation
            {
                Type = ValidationType.Max,
                Value = max.GetDouble()
            });
        }

        if (property.TryGetProperty("pattern", out var pattern))
        {
            field.Validations.Add(new FieldValidation
            {
                Type = ValidationType.Pattern,
                Value = pattern.GetString()
            });
        }

        if (format == "email")
        {
            field.Validations.Add(new FieldValidation { Type = ValidationType.Email });
        }

        if (format is "uri" or "url")
        {
            field.Validations.Add(new FieldValidation { Type = ValidationType.Url });
        }

        // 移除空的校验规则列表
        if (field.Validations.Count == 0)
        {
            field.Validations = null;
        }

        return field;
    }

    // ── JSON Schema 选项辅助方法 ──────────────────────────────

    /// <summary>
    /// 尝试按 const/title 模式把 oneOf 数组解析为选择选项。
    /// 例如：[{"const": "apple", "title": "Apple"}, {"const": "banana", "title": "Banana"}]
    /// </summary>
    private static bool TryParseOneOfOptions(JsonElement oneOfEl, out List<SelectOption<string>> options)
    {
        options = new List<SelectOption<string>>();
        foreach (var item in oneOfEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("const", out var constEl))
            {
                // 不是 const/title 选项模式——直接放弃
                options = new List<SelectOption<string>>();
                return false;
            }

            var val = constEl.GetString() ?? constEl.ToString();
            var text = item.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? val : val;
            options.Add(new SelectOption<string>(val, text));
        }

        return options.Count > 0;
    }

    /// <summary>
    /// 从 JSON Schema 的 "items" 定义解析选项（用于数组类型）。
    /// 同时支持 "enum" 与带 const/title 的 "oneOf"。
    /// </summary>
    private static List<SelectOption<string>>? ParseItemsOptions(JsonElement itemsEl)
    {
        if (itemsEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // items 带 oneOf → 文本/值配对
        if (itemsEl.TryGetProperty("oneOf", out var oneOfEl) &&
            oneOfEl.ValueKind == JsonValueKind.Array &&
            TryParseOneOfOptions(oneOfEl, out var oneOfOptions))
        {
            return oneOfOptions;
        }

        // items 带 enum → 简单选项
        if (itemsEl.TryGetProperty("enum", out var enumEl) && enumEl.ValueKind == JsonValueKind.Array)
        {
            var options = new List<SelectOption<string>>();
            foreach (var item in enumEl.EnumerateArray())
            {
                var val = item.GetString() ?? item.ToString();
                options.Add(new SelectOption<string>(val, val));
            }

            return options.Count > 0 ? options : null;
        }

        return null;
    }

    // ── 辅助方法 ──────────────────────────────────────────────

    private static object? ParseJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private static string PascalCaseToTitle(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var chars = new List<char> { char.ToUpper(name[0], CultureInfo.InvariantCulture) };
        for (var i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0 && !char.IsUpper(name[i - 1]))
            {
                chars.Add(' ');
            }

            chars.Add(name[i]);
        }

        return new string(chars.ToArray());
    }

    // ── 原生 Schema DTO ───────────────────────────────────────

    private sealed class NativeSchemaDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? Columns { get; set; }
        public List<NativeSectionDto>? Sections { get; set; }
        public List<NativeFieldDto>? Fields { get; set; }
    }

    private sealed class NativeSectionDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? Columns { get; set; }
        public string? VisibleWhen { get; set; }
        public bool? Collapsible { get; set; }
        public bool? DefaultExpanded { get; set; }
        public List<NativeFieldDto>? Fields { get; set; }
    }

    private sealed class NativeFieldDto
    {
        public string? Name { get; set; }
        public string? Label { get; set; }
        public string? Description { get; set; }
        public string? Placeholder { get; set; }
        public string? Type { get; set; }
        public bool? Required { get; set; }
        public bool? Disabled { get; set; }
        public bool? ReadOnly { get; set; }
        public JsonElement? DefaultValue { get; set; }
        public int? Order { get; set; }
        public int? ColSpan { get; set; }
        public string? VisibleWhen { get; set; }
        public List<JsonElement>? Options { get; set; }
        public List<JsonElement>? Validations { get; set; }
        public JsonElement? Metadata { get; set; }
    }
}
