#pragma warning disable CA1716 // Step 与 Visual Basic 保留关键字重名：Bb 前缀剥离后领域术语必须落 Step，文件内抑制保语句逐行对齐（照 SelectOption.cs CA1716 与 TabsTrigger.razor.cs 文件级 pragma 先例，+1 行已登记偏差）
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// <see cref="Stepper"/> 中的一个步骤。提供指示器中显示的标签，以及
/// 可选的、在该步骤处于活动状态时所显示的内容。
/// </summary>
/// <remarks>
/// 没有 <see cref="ChildContent"/> 的步骤不渲染任何属于自己的东西；它只为指示器
/// 贡献自己的标签，而这正是一个纯进度型步进器所需要的全部。
/// </remarks>
public partial class Step : ComponentBase, IDisposable
{
    private int index = -1;
    internal string StepId { get; } = Guid.NewGuid().ToString("N");
    internal void SetIndex(int value)
    {
        index = value;
        StateHasChanged();
    }

    /// <summary>
    /// 获取或设置父步进器，通过级联参数接收。
    /// </summary>
    [CascadingParameter]
    public Stepper? Stepper { get; set; }

    /// <summary>
    /// 获取或设置在指示器中显示的标签。
    /// </summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置显示在标题下方的次级文本。
    /// </summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>
    /// 获取或设置显示在步骤标记中的 Lucide 图标名。不提供时，标记显示
    /// 步骤编号。
    /// </summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>
    /// 获取或设置显式状态，它覆盖由步骤位置推导出的状态。
    /// 用它把某一步标记为 <see cref="StepState.Error"/> 或 <see cref="StepState.Skipped"/>。
    /// </summary>
    [Parameter]
    public StepState? State { get; set; }

    /// <summary>
    /// 获取或设置该步骤是否禁用，禁用后点击无法激活它。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置该步骤是否为可选步骤，选后会在标题下方加一条提示。
    /// </summary>
    [Parameter]
    public bool Optional { get; set; }

    /// <summary>
    /// 获取或设置在本步骤处于活动状态时渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置步骤内容包裹元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到步骤内容包裹元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>获取本步骤在步进器中从零开始的索引。</summary>
    internal int Index => index;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Stepper is null)
        {
            throw new InvalidOperationException(
                "Step 必须在 Stepper 组件内使用。");
        }

        index = Stepper.RegisterStep(this, new StepInfo
        {
            Title = Title,
            Description = Description,
            Icon = Icon,
            State = State,
            Disabled = Disabled,
            Optional = Optional,
            HasContent = ChildContent is not null
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stepper?.UnregisterStep(this);
        GC.SuppressFinalize(this);
    }

    private string? CssClass => ClassNames.cn(Class);
}
