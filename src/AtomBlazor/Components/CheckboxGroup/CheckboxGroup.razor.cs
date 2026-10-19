using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 管理一组复选框、并可选提供「全选」支持的分组组件。
/// </summary>
/// <typeparam name="TValue">与复选框组各项关联的值类型。</typeparam>
public partial class CheckboxGroup<TValue> : ComponentBase
{
    /// <summary>控件的可访问名称。</summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置当前选中的值集合。
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<TValue> Values { get; set; } = Array.Empty<TValue>();

    /// <summary>
    /// 获取或设置选中值集合变化时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyCollection<TValue>> ValuesChanged { get; set; }

    /// <summary>
    /// 获取或设置是否显示「全选」复选框。
    /// </summary>
    [Parameter]
    public bool ShowSelectAll { get; set; }

    /// <summary>
    /// 获取或设置全选复选框的标签文本。
    /// </summary>
    [Parameter]
    public string SelectAllLabel { get; set; } = "Select all";

    /// <summary>
    /// 获取或设置组内所有项是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置应用到分组容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置的子内容（CheckboxGroupItem 组件）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private CheckboxGroupContext<TValue> context = default!;
    private readonly HashSet<TValue> registeredItems = new();

    private string CssClass => ClassNames.cn("atom:grid atom:gap-3", Class);

    private bool IsAllSelected => registeredItems.Count > 0
        && Values.Count >= registeredItems.Count
        && registeredItems.All(v => Values.Contains(v));

    private bool IsIndeterminate => Values.Count > 0 && !IsAllSelected;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        context = new CheckboxGroupContext<TValue>
        {
            SelectedValues = Values,
            IsDisabled = Disabled,
            ToggleItem = ToggleItemAsync,
            RegisterItem = RegisterItem,
            UnregisterItem = UnregisterItem
        };
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // 项在自身初始化时注册，首帧之后全选态才算可靠。
        if (firstRender && ShowSelectAll && registeredItems.Count > 0)
        {
            StateHasChanged();
        }
    }

    private void RegisterItem(TValue value) => registeredItems.Add(value);

    private void UnregisterItem(TValue value) => registeredItems.Remove(value);

    private async Task ToggleItemAsync(TValue value, bool isChecked)
    {
        var newValues = new List<TValue>(Values);
        if (isChecked)
        {
            if (!newValues.Contains(value))
            {
                newValues.Add(value);
            }
        }
        else
        {
            newValues.Remove(value);
        }

        await ValuesChanged.InvokeAsync(newValues.AsReadOnly());
    }

    private async Task HandleSelectAllChanged(bool isChecked)
    {
        if (isChecked)
        {
            await ValuesChanged.InvokeAsync(registeredItems.ToList().AsReadOnly());
        }
        else
        {
            await ValuesChanged.InvokeAsync(Array.Empty<TValue>());
        }
    }
}
