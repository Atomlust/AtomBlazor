using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// CheckboxGroup 中的单个复选框项。
/// </summary>
/// <typeparam name="TValue">与这个复选框项关联的值类型。</typeparam>
public partial class CheckboxGroupItem<TValue> : ComponentBase, IDisposable
{
    [CascadingParameter]
    internal CheckboxGroupContext<TValue>? Context { get; set; }

    /// <summary>
    /// 获取或设置该项代表的值。
    /// </summary>
    [Parameter, EditorRequired]
    public TValue Value { get; set; } = default!;

    /// <summary>
    /// 获取或设置在复选框旁显示的可选标签。
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// 获取或设置该项是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置应用到该项的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置替代 Label 参数渲染的自定义内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private readonly string itemId = $"cbg-{Guid.NewGuid():N}";

    private bool IsChecked => Context?.SelectedValues.Contains(Value) ?? false;

    private bool IsDisabled => Disabled || (Context?.IsDisabled ?? false);

    private string CssClass => ClassNames.cn("atom:flex atom:items-center atom:gap-2", Class);

    /// <summary>
    /// 获取标签元素的计算 CSS 类。
    /// </summary>
    /// <remarks>
    /// peer-disabled 变体必须经 <see cref="ClassNames"/> 输出，才能拿到裸 <c>peer</c> 孪生类，
    /// 让使用方自己的 Tailwind 构建也能解析该标记。
    /// </remarks>
    private static string LabelCssClass => ClassNames.cn(
        "atom:text-sm atom:font-medium atom:leading-none atom:peer-disabled:cursor-not-allowed atom:peer-disabled:opacity-70");

    /// <inheritdoc />
    protected override void OnInitialized() => Context?.RegisterItem(Value);

    private async Task HandleCheckedChanged(bool isChecked)
    {
        if (Context != null)
        {
            await Context.ToggleItem(Value, isChecked);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Context?.UnregisterItem(Value);
        GC.SuppressFinalize(this);
    }
}
