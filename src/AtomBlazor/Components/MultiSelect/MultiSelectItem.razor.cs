using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// MultiSelect 复合模式的子组件，渲染一个带富内容支持的可选项复选框条目。
/// 必须用在 MultiSelect 组件之内。
/// </summary>
/// <typeparam name="TValue">项值的类型，与父 MultiSelect 一致。</typeparam>
public partial class MultiSelectItem<TValue> : ComponentBase, IDisposable
{
    [CascadingParameter]
    private MultiSelect<TValue>? Parent { get; set; }

    /// <summary>
    /// 获取或设置本项的值。
    /// </summary>
    [Parameter, EditorRequired]
    public TValue Value { get; set; } = default!;

    /// <summary>
    /// 获取或设置本项的展示文本。
    /// 用于搜索过滤与徽标展示文本查找。
    /// </summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = default!;

    /// <summary>
    /// 获取或设置本项是否被禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置自定义条目渲染的子内容。
    /// 为 null 时把 Text 参数渲染为纯文本。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的其他 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsSelected => Parent?.IsValueSelected(Value) ?? false;

    // CSS 类访问器——规避 .razor 模板里命名空间/类名同名冲突
    private static string ItemCssClassValue => MultiSelect<TValue>.ItemCssClass;
    private static string CheckboxCssClassValue => MultiSelect<TValue>.CheckboxCssClass;

    private bool IsVisible
    {
        get
        {
            if (Parent is null)
            {
                return false;
            }

            // 外部过滤生效时，由使用者控制渲染哪些条目——
            // 全部显示，不再做内部文本过滤。
            if (Parent.IsExternalFiltering)
            {
                return true;
            }

            var query = Parent.SearchQuery;
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return Text.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }

    protected override void OnInitialized()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"{nameof(MultiSelectItem<TValue>)} 必须用在 {nameof(MultiSelect<TValue>)} 组件之内。");
        }

        Parent.RegisterItem(Value, Text);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        Parent?.RegisterItem(Value, Text);
    }

    private async Task HandleClick()
    {
        if (Parent is not null && !Disabled)
        {
            await Parent.ToggleItemValue(Value);
        }
    }

 // 此处不要从文本登记表注销。
 // 条目住在门户内，弹层关闭时会被卸载，但徽标展示文本在关闭期间仍需要这份登记表。
 // 条目会在下次打开时经 OnInitialized 重新登记。
    public void Dispose() => GC.SuppressFinalize(this);
}
