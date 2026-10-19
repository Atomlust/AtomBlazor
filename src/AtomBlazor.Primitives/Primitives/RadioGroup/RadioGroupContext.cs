namespace AtomBlazor.Primitives;

/// <summary>
/// 用于在 RadioGroup 与 RadioGroupItem 组件之间共享状态的上下文类。
/// </summary>
/// <typeparam name="TValue">与单选组各项关联的值类型。</typeparam>
/// <remarks>
/// 该上下文由 RadioGroup 通过 Blazor 的 <c>CascadingValue</c> 机制下发给子级 RadioGroupItem。
/// </remarks>
public class RadioGroupContext<TValue>
{
    /// <summary>
    /// 获取或设置单选组当前选中的值。
    /// </summary>
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置单选组是否禁用。
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置某个单选项被选中时调用的回调。
    /// </summary>
    public Func<TValue, Task>? SelectValue { get; set; }

    /// <summary>
    /// 获取或设置已注册的单选项列表。
    /// </summary>
    public List<RadioGroupItem<TValue>> Items { get; set; } = new();

    /// <summary>
    /// 获取或设置应用到组内所有各项的默认 CSS 类。
    /// 由上级 RadioGroup 的 ItemClass 参数级联而来。
    /// </summary>
    public string? ItemClass { get; set; }
}
