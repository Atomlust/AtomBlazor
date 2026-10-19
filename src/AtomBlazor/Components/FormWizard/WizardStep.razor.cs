using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 表示 <see cref="FormWizard"/> 中的单个步骤。
/// 保存步骤元数据（标题、图标、校验），并在步骤活动时渲染其内容。
/// </summary>
public partial class WizardStep : ComponentBase, IDisposable
{
    private int index = -1;

    /// <summary>
    /// 获取通过级联参数传入的父级向导组件。
    /// </summary>
    [CascadingParameter]
    public FormWizard? Wizard { get; set; }

    /// <summary>
    /// 获取或设置在步骤指示器中显示的标题。
    /// </summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置在步骤指示器中标题下方显示的可选描述。
    /// </summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>
    /// 获取或设置在步骤指示器圆圈中显示的可选 Lucide 图标名。
    /// 未设置时显示步骤序号。
    /// </summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>
    /// 获取或设置该步骤是否可跳过。可选步骤在导航中显示“跳过”选项，
    /// 且前进时不需要通过校验。
    /// </summary>
    [Parameter]
    public bool IsOptional { get; set; }

    /// <summary>
    /// 获取或设置该步骤是否被禁用且无法导航进入。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置该步骤校验的模型属性名。
    /// 提供时，点击“下一步”仅针对这些字段对照级联的 <c>EditContext</c> 校验。
    /// 使用表单模型的顶层属性名。
    /// </summary>
    [Parameter]
    public string[]? FieldNames { get; set; }

    /// <summary>
    /// 获取或设置该步骤的自定义校验函数。提供时必须返回 <c>true</c>
    /// 才算该步骤通过校验。可独立于或与 <see cref="FieldNames"/> 组合使用。
    /// </summary>
    [Parameter]
    public Func<bool>? Validator { get; set; }

    /// <summary>
    /// 获取或设置该步骤活动时渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置步骤内容包裹层的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取该步骤在向导内的从零开始的索引。
    /// </summary>
    internal int Index => index;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Wizard is null)
        {
            throw new InvalidOperationException(
                "WizardStep 必须在 FormWizard 组件内使用。");
        }

        index = Wizard.RegisterStep(this, new WizardStepInfo
        {
            Title = Title,
            Description = Description,
            Icon = Icon,
            IsOptional = IsOptional,
            Disabled = Disabled,
            FieldNames = FieldNames,
            Validator = Validator
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Wizard?.UnregisterStep(this);
        GC.SuppressFinalize(this);
    }

    private string? CssClass => ClassNames.cn(Class);
}
