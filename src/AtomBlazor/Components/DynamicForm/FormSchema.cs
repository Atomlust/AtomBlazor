using AtomBlazor.Primitives;
using System.Text.Json.Serialization;

namespace AtomBlazor;

/// <summary>
/// 定义动态表单的模式，包括字段与可选的的分组。
/// </summary>
public class FormSchema
{
    /// <summary>
    /// 获取或设置在表单上方显示的标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 获取或设置在标题下方显示的描述。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 获取或设置表单的分区。用于带标题和描述的分组字段；
    /// 设置后 <see cref="Fields"/> 将被忽略。
    /// </summary>
    public List<FormSectionDefinition> Sections { get; set; } = [];

    /// <summary>
    /// 获取或设置扁平字段列表（无分区分组）。
    /// 当 <see cref="Sections"/> 非空时被忽略。
    /// </summary>
    public List<FormFieldDefinition> Fields { get; set; } = [];

    /// <summary>
    /// 获取或设置字段网格布局的默认列数。各分区可覆盖此值。
    /// 为 null 时使用 <see cref="DynamicForm.Columns"/> 参数。
    /// </summary>
    public int? Columns { get; set; }
}

/// <summary>
/// 定义动态表单中聚合相关字段的分区。
/// </summary>
public class FormSectionDefinition
{
    /// <summary>
    /// 获取或设置分区标题文本。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 获取或设置在标题下方显示的分区描述。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 获取或设置此分区内的字段。
    /// </summary>
    public List<FormFieldDefinition> Fields { get; set; } = [];

    /// <summary>
    /// 获取或设置此分区内的表单级列数覆盖值。
    /// 为 null 时使用表单级 <see cref="FormSchema.Columns"/>。
    /// </summary>
    public int? Columns { get; set; }

    /// <summary>
    /// 获取或设置控制此分区是否显示的可见性表达式。
    /// 针对当前表单值求值。示例：<c>"Country == 'US'"</c>。
    /// </summary>
    public string? VisibleWhen { get; set; }

    /// <summary>
    /// 获取或设置此分区是否可折叠。
    /// </summary>
    public bool Collapsible { get; set; }

    /// <summary>
    /// 获取或设置 <see cref="Collapsible"/> 为 true 时是否初始展开。
    /// </summary>
    public bool DefaultExpanded { get; set; } = true;
}

/// <summary>
/// 定义动态表单模式中的单个字段。
/// </summary>
public class FormFieldDefinition
{
    /// <summary>
    /// 获取或设置字段名称。用作值字典中的键。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 获取或设置在字段上方或旁边显示的标签文本。
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// 获取或设置在字段下方显示的描述/帮助文本。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 获取或设置字段为空时显示的占位符文本。
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// 获取或设置字段类型，决定渲染哪个组件。
    /// </summary>
    public FieldType Type { get; set; } = FieldType.Text;

    /// <summary>
    /// 获取或设置此字段是否为必填。
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置此字段是否被禁用。
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置此字段是否为只读。
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// 获取或设置此字段的默认值。当字段名不在值字典中时应用。
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 获取或设置显示顺序，值越小越靠前。
    /// 为 null 时按定义顺序显示。
    /// </summary>
    public int? Order { get; set; }

    /// <summary>
    /// 获取或设置此字段跨越的网格列数。
    /// </summary>
    public int ColSpan { get; set; } = 1;

    /// <summary>
    /// 获取或设置控制此字段是否显示的可见性表达式。
    /// 针对当前表单值求值。示例：<c>"Country == 'US'"</c>。
    /// </summary>
    public string? VisibleWhen { get; set; }

    /// <summary>
    /// 获取或设置 Select、Combobox、MultiSelect、RadioGroup 与 CheckboxGroup 字段的选项。
    /// </summary>
    public List<SelectOption<string>>? Options { get; set; }

    /// <summary>
    /// 获取或设置此字段的验证规则。
    /// </summary>
    public List<FieldValidation>? Validations { get; set; }

    /// <summary>
    /// 获取或设置自定义同步校验器：返回非 null 错误消息表示校验失败。
    /// 仅在 <see cref="Validations"/> 含 <see cref="ValidationType.Custom"/> 规则时执行，
    /// 且优先于 <see cref="Metadata"/> 中的 <c>customValidator</c>。
    /// </summary>
    [JsonIgnore]
    public Func<object?, string?>? CustomValidator { get; set; }

    /// <summary>
    /// 获取或设置自定义异步校验器：返回非 null 错误消息表示校验失败。
    /// 仅在 <see cref="Validations"/> 含 <see cref="ValidationType.Custom"/> 规则时执行。
    /// </summary>
    [JsonIgnore]
    public Func<object?, CancellationToken, ValueTask<string?>>? AsyncValidator { get; set; }

    /// <summary>
    /// 获取或设置字段类型专属配置的附加元数据。
    /// 例如：Textarea 的 <c>{ "rows", 5 }</c>，Slider 的 <c>{ "min", 0 }, { "max", 100 }</c>。
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
