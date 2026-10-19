using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// Combobox 复合模式的子组件，渲染一个支持富内容的可选项。
/// 必须用在 Combobox 组件之内。
/// </summary>
/// <typeparam name="TValue">项值的类型，与父 Combobox 一致。</typeparam>
public partial class ComboboxItem<TValue> : ComponentBase, IDisposable
{
    [CascadingParameter]
    private Combobox<TValue>? Parent { get; set; }

    /// <summary>
    /// 为 true 时，本项属于父组件的隐藏注册通道：向父组件注册自己的显示文本，
    /// 但不渲染任何 DOM。父组件会把各项再渲染一遍——抢先地、不可见地——
    /// 让它们的标题在初始加载时（弹层、也即可交互的真实项，尚未挂载之前）就已知晓。
    /// </summary>
    [CascadingParameter(Name = ComboboxConstants.RegistrationScopeName)]
    private bool RegistrationOnly { get; set; }

    /// <summary>
    /// 获取或设置本项的值。
    /// </summary>
    [Parameter, EditorRequired]
    public TValue Value { get; set; } = default!;

    /// <summary>
    /// 获取或设置本项的显示文本。
    /// 用于搜索过滤与触发器显示文本查找。
    /// </summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = default!;

    /// <summary>
    /// 获取或设置本项是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置用于自定义项渲染的子内容。
    /// 为 null 时把 Text 参数渲染为纯文本。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsSelected => Parent?.IsValueSelected(Value) ?? false;

    protected override void OnInitialized()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"{nameof(ComboboxItem<TValue>)} 必须用在 {nameof(Combobox<TValue>)} 组件之内。");
        }

        Parent.RegisterItem(Value, Text);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        Parent?.RegisterItem(Value, Text);
    }

    private async Task HandleSelect() =>
        await Parent!.HandleItemSelected(Value, Text);

    // 不要在这里从文本注册表注销。
    // 项活在一个门户里，弹层关闭时门户会卸载，
    // 但注册表在弹层关闭期间仍是触发器显示文本所需。
    // 项会在下次打开时经由 OnInitialized 重新注册。
    public void Dispose() => GC.SuppressFinalize(this);
}
