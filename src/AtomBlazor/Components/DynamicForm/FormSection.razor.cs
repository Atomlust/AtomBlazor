using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 用标题、描述与可选的折叠行为对相关表单字段进行分组。
/// 既可独立使用，也可在 <see cref="DynamicForm"/> 内使用。
/// </summary>
public partial class FormSection : ComponentBase
{
    private bool isExpanded;

    /// <summary>
    /// 获取或设置分区的标题文本。
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// 获取或设置显示在标题下方的分区描述。
    /// </summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>
    /// 获取或设置分区的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置该分区是否可折叠。
    /// </summary>
    [Parameter]
    public bool Collapsible { get; set; }

    /// <summary>
    /// 获取或设置 <see cref="Collapsible"/> 为 true 时该分区初始是否展开。
    /// </summary>
    [Parameter]
    public bool DefaultExpanded { get; set; } = true;

    /// <summary>
    /// 获取或设置是否在分区上方显示分隔线。
    /// </summary>
    [Parameter]
    public bool ShowSeparator { get; set; } = true;

    /// <summary>
    /// 获取或设置应用到分区容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => isExpanded = DefaultExpanded;

    private string SectionCssClass => ClassNames.cn("atom:space-y-4", Class);
}
